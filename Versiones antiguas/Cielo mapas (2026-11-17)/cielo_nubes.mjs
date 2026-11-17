import fs from 'node:fs';
import zlib from 'node:zlib';
import { fileURLToPath } from 'node:url';

// Dos mapas de nubes del cielo, 2048x1024 en equirectangular (u = vuelta entera, v = de horizonte
// a cenit). No hay celdas ni cuadrícula: las nubes se colocan una a una y las que cruzan el borde
// se dibujan también al otro lado. Canales: R = luz, G = panza en sombra, B = filo de tinta, A = silueta.
//   MapaDeNubes.png  -> buen tiempo, nubes sueltas
//   MapaTormenta.png -> cielo cubierto, nubes grandes y solapadas
// Uso: node Tools/Cielo/cielo_nubes.mjs

const W = 2048, H = 1024, SS = 2;
const buf = Buffer.alloc(W * H * 4);

function rng(seed) {
  let a = seed >>> 0;
  return () => { a = (a + 0x6D2B79F5) | 0; let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
}

function nube(cx, baseY, w, h, seed) {
  const r = rng(seed);
  const baseH = h * 0.50;
  const bw = w * 0.94;
  const base = { x: cx - bw / 2, y: baseY - baseH, w: bw, h: baseH };
  const circles = [];
  for (let i = 0; i < 5; i++) {
    const t = (i + 0.5) / 5;
    const env = 0.70 + 0.30 * Math.sin(Math.PI * t);
    const centro = 1 + 0.34 * Math.exp(-Math.pow((t - 0.52) / 0.24, 2));
    const rad = h * (0.23 + 0.05 * r()) * env * centro;
    const x = cx - w * 0.36 + w * 0.72 * t + (r() - 0.5) * w * 0.03;
    const y = baseY - baseH * 0.55 - rad * 0.62 - r() * h * 0.05;
    circles.push({ x, y, rad });
  }
  return { circles, base };
}

function dentro(n, x, y) {
  for (const c of n.circles) {
    const dx = x - c.x, dy = y - c.y;
    if (dx * dx + dy * dy <= c.rad * c.rad) return true;
  }
  const b = n.base;
  if (x < b.x || x > b.x + b.w || y < b.y || y > b.y + b.h) return false;
  const rr = Math.min(b.h * 0.30, b.w * 0.18);
  const cx = Math.max(b.x + rr, Math.min(x, b.x + b.w - rr));
  const cy = Math.max(b.y + rr, Math.min(y, b.y + b.h - rr));
  const dx = x - cx, dy = y - cy;
  return dx * dx + dy * dy <= rr * rr;
}

function escribir(x, y, rr, gg, bb, aa) {
  const px = ((x % W) + W) % W;
  if (y < 0 || y >= H) return;
  const o = (y * W + px) * 4;
  if (aa > buf[o + 3]) {
    buf[o] = rr; buf[o + 1] = gg; buf[o + 2] = bb; buf[o + 3] = aa;
  }
}

function pintarNube(cx, baseY, w, h, seed) {
  const n = nube(cx, baseY, w, h, seed);
  let minX = n.base.x, maxX = n.base.x + n.base.w, minY = n.base.y;
  const maxY = baseY;
  for (const c of n.circles) {
    minX = Math.min(minX, c.x - c.rad); maxX = Math.max(maxX, c.x + c.rad);
    minY = Math.min(minY, c.y - c.rad);
  }
  const x0 = Math.floor(minX) - 3, x1 = Math.ceil(maxX) + 3;
  const y0 = Math.floor(minY) - 3, y1 = Math.ceil(maxY) + 3;
  const banda = baseY - h * 0.35;

  for (let y = y0; y <= y1; y++) {
    for (let x = x0; x <= x1; x++) {
      let inside = 0, ink = 0, shadow = 0;
      for (let sy = 0; sy < SS; sy++) for (let sx = 0; sx < SS; sx++) {
        const px = x + (sx + 0.5) / SS, py = y + (sy + 0.5) / SS;
        if (!dentro(n, px, py)) continue;
        inside++;
        const borde = !dentro(n, px + 2, py) || !dentro(n, px - 2, py) ||
                      !dentro(n, px, py + 2) || !dentro(n, px, py - 2);
        if (borde) ink++;
        else if (py > banda + Math.sin(x * 0.06) * 4 + Math.sin(x * 0.17 + 1.7) * 2) shadow++;
      }
      if (!inside) continue;
      const total = SS * SS;
      const k = inside / total;
      const b = Math.min(255, (ink / total) * 255 * 1.2);
      const g = Math.min(255 - b, (shadow / total) * 255 * 1.1);
      const r = Math.max(0, k * 255 - b - g);
      escribir(x, y, r, g, b, k * 255);
    }
  }
}

const crcTab = (() => { const t = new Uint32Array(256); for (let n = 0; n < 256; n++) {
  let c = n; for (let k = 0; k < 8; k++) c = (c & 1) ? 0xEDB88320 ^ (c >>> 1) : c >>> 1; t[n] = c >>> 0; } return t; })();
function crc32(b) { let c = 0xFFFFFFFF; for (const x of b) c = crcTab[(c ^ x) & 255] ^ (c >>> 8); return (c ^ 0xFFFFFFFF) >>> 0; }
function chunk(type, data) {
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
  const t = Buffer.from(type, 'ascii');
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(Buffer.concat([t, data])));
  return Buffer.concat([len, t, data, crc]);
}

function generar(archivo, cantidad, semilla, vMin, vMax, wMin, wMax, grandes) {
  buf.fill(0);
  const rand = rng(semilla);
  const nubes = [];
  for (let i = 0; i < cantidad; i++) {
    const u = rand();
    const v = vMin + rand() * (vMax - vMin);
    const w = wMin + rand() * (wMax - wMin);
    const h = w * (0.30 + rand() * 0.15);
    nubes.push({ u, v, w, h, seed: 100 + i * 7 });
  }
  for (const g of grandes) nubes.push(g);

  for (const c of nubes) pintarNube(c.u * W, (1 - c.v) * H, c.w, c.h, c.seed);

  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(W, 0); ihdr.writeUInt32BE(H, 4);
  ihdr[8] = 8; ihdr[9] = 6;
  const raw = Buffer.alloc((W * 4 + 1) * H);
  for (let y = 0; y < H; y++) {
    raw[y * (W * 4 + 1)] = 0;
    buf.copy(raw, y * (W * 4 + 1) + 1, y * W * 4, (y + 1) * W * 4);
  }
  const png = Buffer.concat([
    Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
    chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
  const destino = fileURLToPath(new URL('../../Assets/Art/Cielo/' + archivo, import.meta.url));
  fs.writeFileSync(destino, png);
  console.log(archivo, png.length, 'bytes');
}

generar('MapaDeNubes.png', 40, 20261117, 0.05, 0.60, 50, 200, [
  { u: 0.12, v: 0.15, w: 200, h: 78, seed: 900 },
  { u: 0.52, v: 0.21, w: 180, h: 70, seed: 901 },
  { u: 0.83, v: 0.11, w: 210, h: 82, seed: 902 },
]);

generar('MapaTormenta.png', 120, 424242, 0.02, 0.70, 80, 300, [
  { u: 0.08, v: 0.10, w: 330, h: 150, seed: 910 },
  { u: 0.30, v: 0.24, w: 300, h: 140, seed: 911 },
  { u: 0.55, v: 0.08, w: 360, h: 160, seed: 912 },
  { u: 0.74, v: 0.20, w: 300, h: 130, seed: 913 },
  { u: 0.92, v: 0.12, w: 320, h: 145, seed: 914 },
]);
