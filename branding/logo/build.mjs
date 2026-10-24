// branding/logo/build.mjs — regenerates all raster logo assets from the SVG masters.
// Requires: node + Microsoft Edge (headless screenshot for transparent PNGs).
// Usage: node branding/logo/build.mjs
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = dirname(fileURLToPath(import.meta.url));
const EDGE = [
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
].find(existsSync);
if (!EDGE) {
  console.error('msedge.exe not found. Export PNGs manually: open the SVG in a browser at the target size.');
  process.exit(1);
}

const ICON_SIZES = [16, 20, 24, 32, 48, 64, 128, 256, 512];
const SIMPLE_SIZES = [16, 24, 32];
const ICO_SIZES = [16, 24, 32, 48, 64, 128, 256];
const LAUNCHER_SIZES = [48, 72, 96, 144, 192];   // mdpi..xxxhdpi
const NOTIF_SIZES = [24, 36, 48];                // mdpi..hdpi

const jobs = [];
for (const s of ICON_SIZES) jobs.push(['filemind-mark.svg', `png/icon-${s}.png`, s, s]);
for (const s of SIMPLE_SIZES) jobs.push(['filemind-mark-simple.svg', `png/icon-simple-${s}.png`, s, s]);
jobs.push(['splash-light.svg', 'png/splash-light.png', 1240, 600]);
jobs.push(['splash-dark.svg', 'png/splash-dark.png', 1240, 600]);
for (const s of LAUNCHER_SIZES) jobs.push(['android/ic_launcher_foreground.svg', `android/png/ic_launcher-${s}.png`, s, s]);
for (const s of NOTIF_SIZES) jobs.push(['android/ic_launcher_monochrome.svg', `android/png/notification-icon-${s}.png`, s, s]);

// Drop-in res/ tree (density buckets)
const LAUNCHER_DPI = { mdpi: 48, hdpi: 72, xhdpi: 96, xxhdpi: 144, xxxhdpi: 192 };
const FG_DPI = { mdpi: 108, hdpi: 162, xhdpi: 216, xxhdpi: 324, xxxhdpi: 432 };
const NOTIF_DPI = { mdpi: 24, hdpi: 36, xhdpi: 48, xxhdpi: 72, xxxhdpi: 96 };
for (const [d, s] of Object.entries(LAUNCHER_DPI)) {
  jobs.push(['android/ic_launcher_legacy.svg', `android/res/mipmap-${d}/ic_launcher.png`, s, s]);
  jobs.push(['android/ic_launcher_round.svg', `android/res/mipmap-${d}/ic_launcher_round.png`, s, s]);
}
for (const [d, s] of Object.entries(FG_DPI))
  jobs.push(['android/ic_launcher_foreground.svg', `android/res/mipmap-${d}/ic_launcher_foreground.png`, s, s]);
for (const [d, s] of Object.entries(NOTIF_DPI))
  jobs.push(['android/ic_launcher_monochrome.svg', `android/res/drawable-${d}/ic_notification.png`, s, s]);

const sleep = ms => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
const fileUrl = p => 'file:///' + p.replace(/\\/g, '/');
const wrapDir = join(ROOT, '.tmp-wrap');

for (const [svg, out, w, h] of jobs) {
  const outAbs = join(ROOT, out);
  mkdirSync(dirname(outAbs), { recursive: true });
  mkdirSync(wrapDir, { recursive: true });
  // Edge renders standalone file:// SVGs at unreliable intrinsic sizes;
  // a wrapper <img> with explicit pixel dims makes scaling deterministic.
  const wrap = join(wrapDir, `${out.replace(/[\\/]/g, '_')}.html`);
  writeFileSync(wrap, `<!doctype html><html><body style="margin:0"><img src="${fileUrl(join(ROOT, svg))}" style="width:${w}px;height:${h}px;display:block"></body></html>`);
  // Edge writes the screenshot after the main process exits — poll for it,
  // and retry the render if a cold/flaky Edge spawn never produces one.
  let buf;
  for (let attempt = 0; attempt < 3 && !buf; attempt++) {
    rmSync(outAbs, { force: true });
    try {
      execFileSync(EDGE, [
        '--headless', '--disable-gpu', '--hide-scrollbars',
        `--screenshot=${outAbs}`, `--window-size=${w},${h}`,
        '--default-background-color=00000000', fileUrl(wrap),
      ], { stdio: 'pipe', timeout: 60000 });
    } catch { /* hung Edge — retry */ }
    for (let i = 0; i < 80 && !buf; i++) {
      sleep(500);
      if (existsSync(outAbs)) {
        try { buf = readFileSync(outAbs); } catch { /* partial write */ }
      }
    }
  }
  if (!buf) throw new Error(`${out}: screenshot not produced`);
  const dims = pngSize(buf);
  if (dims.width !== w || dims.height !== h) {
    throw new Error(`${out}: expected ${w}x${h}, got ${dims.width}x${dims.height}`);
  }
  console.log(`ok ${out} ${w}x${h}`);
}
import { rmSync } from 'node:fs';
rmSync(wrapDir, { recursive: true, force: true });

function pngSize(buf) {
  if (buf.readUInt32BE(0) !== 0x89504e47) throw new Error('not a PNG');
  return { width: buf.readUInt32BE(16), height: buf.readUInt32BE(20) };
}

// Assemble filemind.ico from PNG frames (ICO embeds PNG data for Vista+).
const frames = ICO_SIZES.map(s => ({ size: s, data: readFileSync(join(ROOT, `png/icon-${s}.png`)) }));
const header = Buffer.alloc(6);
header.writeUInt16LE(0, 0);            // reserved
header.writeUInt16LE(1, 2);            // type: icon
header.writeUInt16LE(frames.length, 4);
let offset = 6 + 16 * frames.length;
const entries = frames.map(({ size, data }) => {
  const e = Buffer.alloc(16);
  e.writeUInt8(size >= 256 ? 0 : size, 0);
  e.writeUInt8(size >= 256 ? 0 : size, 1);
  e.writeUInt8(0, 2);                  // palette
  e.writeUInt8(0, 3);                  // reserved
  e.writeUInt16LE(1, 4);               // planes
  e.writeUInt16LE(32, 6);              // bpp
  e.writeUInt32LE(data.length, 8);
  e.writeUInt32LE(offset, 12);
  offset += data.length;
  return e;
});
writeFileSync(join(ROOT, 'filemind.ico'), Buffer.concat([header, ...entries, ...frames.map(f => f.data)]));
console.log(`ok filemind.ico (${ICO_SIZES.join('/')}px frames)`);
