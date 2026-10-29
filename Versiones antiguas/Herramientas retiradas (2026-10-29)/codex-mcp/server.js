#!/usr/bin/env node
// codex-mcp: servidor MCP mínimo (sin dependencias) que permite a Claude
// encargar tareas a Codex CLI ("codex exec") y revisar su estado.
// Las tareas corren en segundo plano: encargar devuelve un id al momento
// y codex_estado consulta el progreso, así ninguna llamada se queda colgada.

'use strict';
const { spawn, execFile } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');
const crypto = require('crypto');

const VERSION = '1.0.0';
const DEFAULT_DIR = process.env.CODEX_MCP_DEFAULT_DIR ||
  'C:\\Users\\luarb\\dev\\unity\\ElSenderoDeLasEstrellas';
const JOBS_DIR = path.join(os.homedir(), '.codex-mcp', 'jobs');
fs.mkdirSync(JOBS_DIR, { recursive: true });

function findCodexJs() {
  const candidates = [];
  if (process.env.CODEX_JS) candidates.push(process.env.CODEX_JS);
  if (process.env.APPDATA) {
    candidates.push(path.join(process.env.APPDATA, 'npm', 'node_modules', '@openai', 'codex', 'bin', 'codex.js'));
  }
  for (const c of candidates) if (fs.existsSync(c)) return c;
  return null;
}

const PROMPT_FOOTER = '\n\n---\nCuando termines, responde en español con: (1) qué archivos has creado o modificado, ' +
  '(2) un resumen breve de cada cambio, y (3) cualquier duda, riesgo o cosa que no hayas podido hacer. ' +
  'No toques archivos fuera de lo que pide la tarea.';

const jobs = new Map(); // id -> { meta, child }

function jobDir(id) { return path.join(JOBS_DIR, id); }
function saveMeta(meta) {
  try { fs.writeFileSync(path.join(jobDir(meta.id), 'meta.json'), JSON.stringify(meta, null, 2)); } catch (_) {}
}
function loadMeta(id) {
  if (jobs.has(id)) return jobs.get(id).meta;
  try { return JSON.parse(fs.readFileSync(path.join(jobDir(id), 'meta.json'), 'utf8')); } catch (_) { return null; }
}

function startJob({ prompt, carpeta, modelo, resumeThread, parentId }) {
  const codexJs = findCodexJs();
  const id = new Date().toISOString().replace(/[-:T]/g, '').slice(0, 14) + '-' + crypto.randomBytes(2).toString('hex');
  const dir = jobDir(id);
  fs.mkdirSync(dir, { recursive: true });
  const finalFile = path.join(dir, 'final.md');
  const cwd = carpeta || DEFAULT_DIR;

  let args;
  if (resumeThread) {
    args = ['exec', 'resume', '--json', '--skip-git-repo-check', '-c', 'sandbox_mode="workspace-write"',
      '-o', finalFile];
    if (modelo) args.push('-m', modelo);
    args.push(resumeThread, '-');
  } else {
    args = ['exec', '--json', '--skip-git-repo-check', '--color', 'never', '-s', 'workspace-write',
      '-C', cwd, '-o', finalFile];
    if (modelo) args.push('-m', modelo);
    args.push('-');
  }

  const meta = {
    id, parentId: parentId || null, estado: 'en_curso', carpeta: cwd, modelo: modelo || null,
    tarea: prompt, threadId: resumeThread || null, inicio: new Date().toISOString(), fin: null,
    codigoSalida: null, error: null,
  };

  let child;
  try {
    if (codexJs) {
      child = spawn(process.execPath, [codexJs, ...args], { cwd, windowsHide: true, env: process.env });
    } else {
      child = spawn('codex', args, { cwd, windowsHide: true, env: process.env, shell: process.platform === 'win32' });
    }
  } catch (e) {
    meta.estado = 'error'; meta.error = 'No se pudo arrancar Codex: ' + e.message; meta.fin = new Date().toISOString();
    saveMeta(meta);
    return meta;
  }

  const events = fs.createWriteStream(path.join(dir, 'events.jsonl'));
  const stderr = fs.createWriteStream(path.join(dir, 'stderr.log'));
  let buf = '';
  child.stdout.on('data', (d) => {
    events.write(d);
    buf += d.toString('utf8');
    let nl;
    while ((nl = buf.indexOf('\n')) >= 0) {
      const line = buf.slice(0, nl); buf = buf.slice(nl + 1);
      try {
        const ev = JSON.parse(line);
        if (ev.type === 'thread.started' && ev.thread_id && !meta.threadId) { meta.threadId = ev.thread_id; saveMeta(meta); }
      } catch (_) {}
    }
  });
  child.stderr.on('data', (d) => stderr.write(d));
  child.on('error', (e) => {
    meta.estado = 'error'; meta.error = 'Error al ejecutar Codex: ' + e.message;
    meta.fin = new Date().toISOString(); saveMeta(meta);
  });
  child.on('close', (code) => {
    events.end(); stderr.end();
    if (meta.estado === 'en_curso') meta.estado = code === 0 ? 'terminado' : 'error';
    if (meta.estado === 'error' && !meta.error) meta.error = 'Codex terminó con código ' + code;
    meta.codigoSalida = code; meta.fin = new Date().toISOString();
    saveMeta(meta);
  });

  child.stdin.on('error', () => {});
  child.stdin.end(prompt + PROMPT_FOOTER);
  jobs.set(id, { meta, child });
  saveMeta(meta);
  return meta;
}

function readTail(file, maxChars) {
  try {
    const s = fs.readFileSync(file, 'utf8');
    return s.length > maxChars ? '…' + s.slice(-maxChars) : s;
  } catch (_) { return ''; }
}

function summarizeEvents(id, maxItems) {
  let lines = [];
  try { lines = fs.readFileSync(path.join(jobDir(id), 'events.jsonl'), 'utf8').split('\n').filter(Boolean); } catch (_) {}
  const out = [];
  for (const l of lines) {
    let ev; try { ev = JSON.parse(l); } catch (_) { continue; }
    if (ev.type === 'item.completed' && ev.item) {
      const it = ev.item;
      if (it.type === 'agent_message') out.push('💬 ' + String(it.text || '').slice(0, 300));
      else if (it.type === 'command_execution') out.push('⚙️ ' + String(it.command || '').slice(0, 200) + (it.exit_code != null ? ' (salida ' + it.exit_code + ')' : ''));
      else if (it.type === 'file_change') out.push('📝 ' + (it.changes || []).map((c) => (c.kind || '') + ' ' + (c.path || '')).join(', '));
      else if (it.type === 'reasoning') continue;
      else out.push('• ' + it.type);
    } else if (ev.type === 'error') out.push('⚠️ ' + String(ev.message || '').slice(0, 300));
    else if (ev.type === 'turn.failed') out.push('❌ ' + JSON.stringify(ev.error || ev).slice(0, 300));
  }
  return out.slice(-maxItems);
}

function fileChanges(id) {
  const files = new Set();
  try {
    for (const l of fs.readFileSync(path.join(jobDir(id), 'events.jsonl'), 'utf8').split('\n')) {
      try {
        const ev = JSON.parse(l);
        if (ev.type === 'item.completed' && ev.item && ev.item.type === 'file_change') {
          for (const c of ev.item.changes || []) files.add((c.kind ? c.kind + ': ' : '') + c.path);
        }
      } catch (_) {}
    }
  } catch (_) {}
  return [...files];
}

function report(meta) {
  const parts = [];
  parts.push(`id: ${meta.id}`);
  parts.push(`estado: ${meta.estado}`);
  parts.push(`carpeta: ${meta.carpeta}`);
  if (meta.threadId) parts.push(`hilo de Codex: ${meta.threadId}`);
  if (meta.parentId) parts.push(`continúa de: ${meta.parentId}`);
  parts.push(`inicio: ${meta.inicio}` + (meta.fin ? `  fin: ${meta.fin}` : ''));
  if (meta.error) parts.push(`error: ${meta.error}`);
  const changes = fileChanges(meta.id);
  if (changes.length) parts.push('archivos tocados:\n  ' + changes.join('\n  '));
  if (meta.estado === 'en_curso') {
    const ev = summarizeEvents(meta.id, 12);
    parts.push('progreso reciente:\n  ' + (ev.length ? ev.join('\n  ') : '(aún sin eventos)'));
  } else {
    const final = readTail(path.join(jobDir(meta.id), 'final.md'), 12000).trim();
    parts.push('respuesta final de Codex:\n' + (final || '(vacía)'));
    if (meta.estado === 'error') {
      const ev = summarizeEvents(meta.id, 8);
      if (ev.length) parts.push('últimos eventos:\n  ' + ev.join('\n  '));
      const err = readTail(path.join(jobDir(meta.id), 'stderr.log'), 2000).trim();
      if (err) parts.push('stderr:\n' + err);
    }
  }
  return parts.join('\n');
}

function sleep(ms) { return new Promise((r) => setTimeout(r, ms)); }

const TOOLS = [
  {
    name: 'codex_encargar',
    description: 'Encarga una tarea a Codex (codex exec) en segundo plano. Devuelve un id al instante; usa codex_estado para seguirla. Codex puede leer y escribir dentro de la carpeta indicada (sandbox workspace-write).',
    inputSchema: {
      type: 'object',
      properties: {
        tarea: { type: 'string', description: 'Instrucciones completas para Codex: objetivo, archivos implicados, criterios de terminado y qué NO tocar.' },
        carpeta: { type: 'string', description: 'Carpeta de trabajo (ruta Windows). Por defecto, el proyecto Unity de El Sendero de las Estrellas.' },
        modelo: { type: 'string', description: 'Modelo opcional (si no, el de la config de Codex).' },
      },
      required: ['tarea'],
    },
  },
  {
    name: 'codex_estado',
    description: 'Consulta una tarea de Codex. Si sigue en curso muestra el progreso reciente; si ha terminado, su respuesta final y los archivos tocados. Puede esperar hasta 50 s a que termine.',
    inputSchema: {
      type: 'object',
      properties: {
        id: { type: 'string' },
        esperar_segundos: { type: 'number', description: 'Segundos a esperar si sigue en curso (0-50). Por defecto 0.' },
      },
      required: ['id'],
    },
  },
  {
    name: 'codex_continuar',
    description: 'Envía un mensaje de seguimiento (correcciones, aclaraciones) a una tarea terminada, en el mismo hilo de Codex. Devuelve un id nuevo.',
    inputSchema: {
      type: 'object',
      properties: {
        id: { type: 'string', description: 'id de la tarea anterior' },
        mensaje: { type: 'string' },
      },
      required: ['id', 'mensaje'],
    },
  },
  {
    name: 'codex_cancelar',
    description: 'Detiene una tarea de Codex en curso.',
    inputSchema: { type: 'object', properties: { id: { type: 'string' } }, required: ['id'] },
  },
  {
    name: 'codex_listar',
    description: 'Lista las tareas recientes de Codex con su estado.',
    inputSchema: { type: 'object', properties: { limite: { type: 'number' } } },
  },
];

async function callTool(name, a) {
  a = a || {};
  if (name === 'codex_encargar') {
    if (!a.tarea || !String(a.tarea).trim()) throw new Error('Falta "tarea".');
    const carpeta = a.carpeta || DEFAULT_DIR;
    if (!fs.existsSync(carpeta)) throw new Error('La carpeta no existe: ' + carpeta);
    const meta = startJob({ prompt: String(a.tarea), carpeta, modelo: a.modelo });
    await sleep(1500);
    return report(loadMeta(meta.id));
  }
  if (name === 'codex_estado') {
    let meta = loadMeta(a.id);
    if (!meta) throw new Error('No encuentro la tarea ' + a.id);
    const wait = Math.max(0, Math.min(50, Number(a.esperar_segundos) || 0));
    const until = Date.now() + wait * 1000;
    while (meta.estado === 'en_curso' && Date.now() < until) { await sleep(1000); meta = loadMeta(a.id); }
    if (meta.estado === 'en_curso' && !jobs.has(meta.id)) {
      meta.estado = 'perdida'; meta.error = 'El servidor se reinició mientras la tarea corría; revisa los archivos.';
    }
    return report(meta);
  }
  if (name === 'codex_continuar') {
    const prev = loadMeta(a.id);
    if (!prev) throw new Error('No encuentro la tarea ' + a.id);
    if (prev.estado === 'en_curso') throw new Error('Esa tarea sigue en curso; espera a que termine.');
    if (!prev.threadId) throw new Error('Esa tarea no tiene hilo de Codex para continuar.');
    const meta = startJob({ prompt: String(a.mensaje || ''), carpeta: prev.carpeta, modelo: prev.modelo, resumeThread: prev.threadId, parentId: prev.id });
    await sleep(1500);
    return report(loadMeta(meta.id));
  }
  if (name === 'codex_cancelar') {
    const j = jobs.get(a.id);
    if (!j || j.meta.estado !== 'en_curso') return 'La tarea no está en curso.';
    j.meta.estado = 'cancelada'; j.meta.error = 'Cancelada por Claude'; saveMeta(j.meta);
    if (process.platform === 'win32') execFile('taskkill', ['/pid', String(j.child.pid), '/T', '/F'], () => {});
    else j.child.kill('SIGTERM');
    return 'Cancelada: ' + a.id;
  }
  if (name === 'codex_listar') {
    const lim = Math.max(1, Math.min(50, Number(a.limite) || 15));
    let ids = [];
    try { ids = fs.readdirSync(JOBS_DIR).sort().reverse().slice(0, lim); } catch (_) {}
    if (!ids.length) return 'No hay tareas todavía.';
    return ids.map((id) => {
      const m = loadMeta(id); if (!m) return id + ' (sin datos)';
      const st = (m.estado === 'en_curso' && !jobs.has(id)) ? 'perdida' : m.estado;
      return `${id}  [${st}]  ${String(m.tarea).replace(/\s+/g, ' ').slice(0, 90)}`;
    }).join('\n');
  }
  throw new Error('Herramienta desconocida: ' + name);
}

// ---- JSON-RPC sobre stdio (una línea por mensaje) ----
function send(msg) { process.stdout.write(JSON.stringify(msg) + '\n'); }

async function handle(msg) {
  const { id, method, params } = msg;
  const isRequest = id !== undefined && id !== null;
  try {
    if (method === 'initialize') {
      return send({ jsonrpc: '2.0', id, result: {
        protocolVersion: (params && params.protocolVersion) || '2025-06-18',
        capabilities: { tools: {} },
        serverInfo: { name: 'codex-mcp', version: VERSION },
        instructions: 'Delegación de tareas a Codex CLI. Flujo: codex_encargar → codex_estado (con esperar_segundos) → revisar los archivos → codex_continuar si hay correcciones.',
      } });
    }
    if (method === 'ping') return isRequest && send({ jsonrpc: '2.0', id, result: {} });
    if (method === 'tools/list') return send({ jsonrpc: '2.0', id, result: { tools: TOOLS } });
    if (method === 'tools/call') {
      try {
        const text = await callTool(params.name, params.arguments);
        return send({ jsonrpc: '2.0', id, result: { content: [{ type: 'text', text }] } });
      } catch (e) {
        return send({ jsonrpc: '2.0', id, result: { content: [{ type: 'text', text: 'Error: ' + e.message }], isError: true } });
      }
    }
    if (!isRequest) return; // notificaciones (initialized, cancelled…)
    send({ jsonrpc: '2.0', id, error: { code: -32601, message: 'Método no soportado: ' + method } });
  } catch (e) {
    if (isRequest) send({ jsonrpc: '2.0', id, error: { code: -32603, message: e.message } });
  }
}

let inBuf = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', (chunk) => {
  inBuf += chunk;
  let nl;
  while ((nl = inBuf.indexOf('\n')) >= 0) {
    const line = inBuf.slice(0, nl).trim(); inBuf = inBuf.slice(nl + 1);
    if (!line) continue;
    let msg; try { msg = JSON.parse(line); } catch (_) { continue; }
    handle(msg);
  }
});
process.stdin.on('end', () => process.exit(0));
