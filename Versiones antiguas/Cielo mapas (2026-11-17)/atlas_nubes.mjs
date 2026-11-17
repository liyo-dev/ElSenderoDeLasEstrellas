import fs from 'node:fs';
import zlib from 'node:zlib';
import { fileURLToPath } from 'node:url';

// Atlas de nubes de dibujo: 3x2 celdas de 512. En cada celda, m??scaras en RGB:
//   R = zona iluminada, G = panza en sombra, B = filo de tinta. A = silueta.
// El shader compone los tres con los colores de la hora; sin degradados ni alfa suave.
// Uso: node Tools/Cielo/atlas_nubes.mjs

const CELDA_W = 512, CELDA_H = 256, COLS = 3, ROWS = 2, SS = 2;
const ATLAS_W = CELDA_W * COLS, ATLAS_H = CELDA_H * ROWS;

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
  const n = 5;
  for (let i = 0; i < n; i++) {
    const t = (i + 0.5) / n;
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
  // Base con las esquinas de abajo redondeadas: sin ellas la nube parece un cartel.
  const rr = Math.min(b.h * 0.30, b.w * 0.18);
  const cx = Math.max(b.x + rr, Math.min(x, b.x + b.w - rr));
  const cy = Math.max(b.y + rr, Math.min(y, b.y + b.h - rr));
  const dx = x - cx, dy = y - cy;
  return dx * dx + dy * dy <= rr * rr;
}

const defs = [
  { cx: 256, baseY: 246, w: 470, h: 214, seed: 11 },
  { cx: 256, baseY: 248, w: 400, h: 236, seed: 29 },
  { cx: 256, baseY: 244, w: 360, h: 190, seed: 43 },
  { cx: 256, baseY: 248, w: 490, h: 170, seed: 57 },
  { cx: 256, baseY: 246, w: 440, h: 205, seed: 71 },
  { cx: 256, baseY: 248, w: 390, h: 225, seed: 89 },
].map((d) => ({ ...d, n: nube(d.cx, d.baseY, d.w, d.h, d.seed) }));

const out = Buffer.alloc(ATLAS_W * ATLAS_H * 4);

for (let y = 0; y < ATLAS_H; y++) {
  for (let x = 0; x < ATLAS_W; x++) {
    const col = Math.floor(x / CELDA_W), row = Math.floor(y / CELDA_H);
    const def = defs[row * COLS + col];
    const lx = x - col * CELDA_W, ly = y - row * CELDA_H;
    const n = def.n, banda = def.baseY - def.h * 0.35;

    let inside = 0, shadow = 0, ink = 0;
    for (let sy = 0; sy < SS; sy++) for (let sx = 0; sx < SS; sx++) {
      const px = lx + (sx + 0.5) / SS, py = ly + (sy + 0.5) / SS;
      if (!dentro(n, px, py)) continue;
      inside++;
      const borde = !dentro(n, px + 2, py) || !dentro(n, px - 2, py) ||
                    !dentro(n, px, py + 2) || !dentro(n, px, py - 2);
      if (borde) ink++;
      else if (py > banda + Math.sin(lx * 0.05) * 5 + Math.sin(lx * 0.13 + 1.7) * 2.5) shadow++;
    }
    if (!inside) continue;

    const total = SS * SS;
    const k = inside / total;
    const b = Math.min(1, (ink / total) * 1.2);
    const g = Math.min(1 - b, (shadow / total) * 1.1);
    const rr = Math.max(0, k - b - g);
    const o = (y * ATLAS_W + x) * 4;
    out[o] = Math.round(255 * rr);
    out[o + 1] = Math.round(255 * g);
    out[o + 2] = Math.round(255 * b);
    out[o + 3] = Math.round(255 * k);
  }
}

const crcTab = (() => { const t = new Uint32Array(256); for (let n = 0; n < 256; n++) {
  let c = n; for (let k = 0; k < 8; k++) c = (c & 1) ? 0xEDB88320 ^ (c >>> 1) : c >>> 1; t[n] = c >>> 0; } return t; })();
function crc32(buf) { let c = 0xFFFFFFFF; for (const b of buf) c = crcTab[(c ^ b) & 255] ^ (c >>> 8); return (c ^ 0xFFFFFFFF) >>> 0; }
function chunk(type, data) {
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
  const t = Buffer.from(type, 'ascii');
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(Buffer.concat([t, data])));
  return Buffer.concat([len, t, data, crc]);
}
const ihdr = Buffer.alloc(13);
ihdr.writeUInt32BE(ATLAS_W, 0); ihdr.writeUInt32BE(ATLAS_H, 4);
ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
const raw = Buffer.alloc((ATLAS_W * 4 + 1) * ATLAS_H);
for (let y = 0; y < ATLAS_H; y++) {
  raw[y * (ATLAS_W * 4 + 1)] = 0;
  out.copy(raw, y * (ATLAS_W * 4 + 1) + 1, y * ATLAS_W * 4, (y + 1) * ATLAS_W * 4);
}
const png = Buffer.concat([
  Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
  chunk('IHDR', ihdr),
  chunk('IDAT', zlib.deflateSync(raw, { level: 9 })),
  chunk('IEND', Buffer.alloc(0)),
]);
const destino = fileURLToPath(new URL('../../Assets/Art/Cielo/NubesDeDibujo.png', import.meta.url));
fs.writeFileSync(destino, png);
console.log('atlas', png.length, 'bytes ->', destino);

