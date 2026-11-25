#!/usr/bin/env node
'use strict';
// 움직이는 일러스트(셰이더 방식) 재료 만들기 — 한 장 그림에 '어디가 어떻게 움직일지' 지도를 붙여 셰이더가 흔듭니다.
//
//   ① 구역 지도(AI 편집): 같은 구도로 머리카락 = 빨강, 옷자락 · 망토 · 리본 = 파랑, 드러난 몸통 = 초록, 얼굴 = 노랑, 날개 · 빛 · 이펙트 = 흰색, 그 밖 = 검정
//   ② 눈 감은 그림(AI 편집): 눈만 감은 같은 그림
//   ③ 후처리: Resources/Art/Sway/{이름}_mask.png (R 흔들림 · G 호흡 · B 반짝임 · A 눈) + {이름}_blink.png (눈 감은 그림)
//      - 흔들림 세기는 얼굴에서 멀수록(머리카락 끝 · 옷자락 끝) 커짐, 경계는 흐리게
//      - 눈 영역 = 원본과 눈 감은 그림이 다른 곳 중 얼굴 근처만
//
//   node tools/art/generate-sway.js --studio "<스튜디오 폴더>" --name c_seraphine_bloom --yes          # AI 2장(유료) + 후처리
//   node tools/art/generate-sway.js --studio "<스튜디오 폴더>" --name c_seraphine_bloom --rebuild      # AI 없이 후처리만 다시
//   node tools/art/generate-sway.js --studio "<스튜디오 폴더>" --name c_seraphine_bloom --preview      # 셰이더와 같은 계산으로 미리보기 영상
//
// --name 은 Resources/Art/Characters/{이름}.png (기본 일러스트 c_x, 개화 c_x_bloom …)

const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');

const REPO = path.resolve(__dirname, '..', '..');
const ART = path.join(REPO, 'Assets', '_Project', 'Resources', 'Art');
const RAW = path.join(REPO, 'docs', 'art-preview', 'sway');
const W = 1024, H = 1536;   // 편집 캔버스(2:3) — 3:4 일러스트를 가운데에 두고 위아래는 검정

function fail(m) { console.error('오류: ' + m); process.exit(1); }
const args = (() => {
  const a = { model: 'gpt-image-2.6-sunburst', quality: 'high', server: 'http://127.0.0.1:3000' };
  const v = process.argv.slice(2);
  for (let i = 0; i < v.length; i++) {
    const k = v[i], next = () => v[++i];
    if (k === '--studio') a.studio = next();
    else if (k === '--name') a.name = next();
    else if (k === '--server') a.server = next().replace(/[/]+$/, '');
    else if (k === '--model') a.model = next();
    else if (k === '--yes') a.yes = true;
    else if (k === '--rebuild') a.rebuild = true;
    else if (k === '--preview') a.preview = true;
    else if (k === '--only') a.only = next();   // mask | blink
    else fail('알 수 없는 옵션: ' + k);
  }
  a.studio = a.studio || process.env.AI_IMAGE_STUDIO_DIR;
  if (!a.studio) fail('--studio 필요');
  if (!a.name) fail('--name 필요 (예: c_seraphine_bloom)');
  return a;
})();
const sharp = require(path.join(args.studio, 'node_modules', 'sharp'));
const ffmpeg = require(path.join(args.studio, 'node_modules', 'ffmpeg-static'));

const SRC = path.join(ART, 'Characters', args.name + '.png');
const OUT = path.join(ART, 'Sway');
const rawOf = (k) => path.join(RAW, `${args.name}_${k}_raw.png`);

const PROMPTS = {
  mask:
    'Turn this exact illustration into a flat-color segmentation map with exactly the same composition, pose, size and outlines. ' +
    'Paint every hair strand pure red (#FF0000). Paint loose cloth that can flutter (cape, coat tails, skirt, sleeves, ribbons, scarf) pure blue (#0000FF). ' +
    'Paint the visible chest and upper torso (including the clothing on it) pure green (#00FF00). Paint the face and neck skin pure yellow (#FFFF00). ' +
    'Paint wings, halos, glowing auras, flames, sparkles, petals and magical light effects pure white (#FFFFFF). ' +
    'Paint everything else (background, hands, arms, legs, weapons, rigid armor, belts) pure black (#000000). ' +
    'Flat solid colors only, no shading, no outlines, no gradients, no text.',
  blink:
    'Redraw this exact illustration completely unchanged except that the character gently closes both eyes in a relaxed blink ' +
    '(closed eyelids with eyelashes, same smile). Keep every other pixel identical: same pose, hair, outfit, background, colors, line art and framing. Do not add anything.',
};

async function api(method, route, body) {
  const init = { method };
  if (body instanceof FormData) init.body = body;
  else if (body) { init.headers = { 'Content-Type': 'application/json' }; init.body = JSON.stringify(body); }
  const r = await fetch(args.server + route, init);
  const t = await r.text(); let j = null; try { j = JSON.parse(t); } catch { }
  if (!r.ok) throw new Error((j && j.error) || `HTTP ${r.status} ${t.slice(0, 160)}`);
  return j;
}

// 원본(3:4)을 2:3 캔버스 가운데에 → 편집 → 같은 영역을 잘라 원본 크기로
async function edit(kind) {
  const meta = await sharp(SRC).metadata();
  const sh = Math.round(meta.height * (W / meta.width)), top = Math.round((H - sh) / 2);
  const input = await sharp({ create: { width: W, height: H, channels: 3, background: '#000' } })
    .composite([{ input: await sharp(SRC).resize(W, sh).removeAlpha().toBuffer(), left: 0, top }]).png().toBuffer();
  const form = new FormData();
  form.append('prompt', PROMPTS[kind]);
  form.append('size', `${W}x${H}`);
  form.append('quality', args.quality);
  form.append('n', '1');
  form.append('images', new Blob([input], { type: 'image/png' }), 'source.png');
  const res = await api('POST', '/api/edit', form);
  const raw = Buffer.from(await (await fetch(args.server + res.images[0].url)).arrayBuffer());
  const rm = await sharp(raw).metadata();
  const fy = rm.height / H;
  await sharp(raw).extract({ left: 0, top: Math.round(top * fy), width: rm.width, height: Math.round(sh * fy) })
    .resize(meta.width, meta.height, { fit: 'fill' }).png().toFile(rawOf(kind));
}

// ---------------- 후처리 ----------------

function boxBlur(src, w, h, r) {
  const tmp = new Float32Array(w * h), out = new Float32Array(w * h);
  for (let y = 0; y < h; y++) { let s = 0; for (let x = -r; x <= r; x++) s += src[y * w + Math.min(w - 1, Math.max(0, x))];
    for (let x = 0; x < w; x++) { tmp[y * w + x] = s / (2 * r + 1); s += src[y * w + Math.min(w - 1, x + r + 1)] - src[y * w + Math.max(0, x - r)]; } }
  for (let x = 0; x < w; x++) { let s = 0; for (let y = -r; y <= r; y++) s += tmp[Math.min(h - 1, Math.max(0, y)) * w + x];
    for (let y = 0; y < h; y++) { out[y * w + x] = s / (2 * r + 1); s += tmp[Math.min(h - 1, y + r + 1) * w + x] - tmp[Math.max(0, y - r) * w + x]; } }
  return out;
}

async function build() {
  const meta = await sharp(SRC).metadata();
  const w = meta.width, h = meta.height, n = w * h;
  const seg = await sharp(rawOf('mask')).resize(w, h, { fit: 'fill' }).removeAlpha().raw().toBuffer();
  const hair = new Float32Array(n), cloth = new Float32Array(n), torso = new Float32Array(n), face = new Float32Array(n), glow = new Float32Array(n);
  // 가장 가까운 기준색으로 분류
  const refs = [[255, 0, 0, hair], [0, 0, 255, cloth], [0, 255, 0, torso], [255, 255, 0, face], [255, 255, 255, glow], [0, 0, 0, null]];
  let fx = 0, fy = 0, fc = 0, fminY = h, fmaxY = 0, fminX = w, fmaxX = 0;
  for (let i = 0; i < n; i++) {
    const r = seg[i * 3], g = seg[i * 3 + 1], b = seg[i * 3 + 2];
    let best = 1e9, arr = null;
    for (const [rr, gg, bb, a] of refs) { const d = (r - rr) ** 2 + (g - gg) ** 2 + (b - bb) ** 2; if (d < best) { best = d; arr = a; } }
    if (arr) arr[i] = 1;
  }
  // 얼굴 = 그림 위쪽 45% 안의 노랑(허벅지 · 팔 같은 다른 피부가 노랑으로 칠해져도 섞이지 않게).
  // 위쪽 노랑의 세로 분포에서 가장 많이 몰린 줄을 기준으로 그 덩어리만 씀
  // 위쪽 45% 안의 노랑을 이어진 덩어리로 나눠 가장 큰 것 = 얼굴
  const topLimit = Math.round(h * 0.45);
  const comp = new Int32Array(w * topLimit).fill(-1);
  let bestY = Infinity, bestId = -1, id = 0;
  const minSize = w * h * 0.0015;   // 이보다 작은 노랑 조각(손가락 · 잡티)은 무시
  const stack = [];
  for (let s = 0; s < w * topLimit; s++) {
    if (!face[s] || comp[s] >= 0) continue;
    let size = 0, sy = 0;
    stack.push(s); comp[s] = id;
    while (stack.length) {
      const p = stack.pop(); size++;
      const x = p % w, y = (p / w) | 0;
      sy += y;
      for (const q of [x > 0 ? p - 1 : -1, x < w - 1 ? p + 1 : -1, y > 0 ? p - w : -1, y < topLimit - 1 ? p + w : -1])
        if (q >= 0 && face[q] && comp[q] < 0) { comp[q] = id; stack.push(q); }
    }
    // 충분히 큰 덩어리 중 가장 위에 있는 것 = 얼굴(그 아래 목 · 가슴 피부와 구분)
    if (size >= minSize && sy / size < bestY) { bestY = sy / size; bestId = id; }
    id++;
  }
  for (let p = 0; p < w * topLimit; p++) {
    if (comp[p] !== bestId) continue;
    const x = p % w, y = (p / w) | 0;
    fx += x; fy += y; fc++; fminY = Math.min(fminY, y); fmaxY = Math.max(fmaxY, y); fminX = Math.min(fminX, x); fmaxX = Math.max(fmaxX, x);
  }
  if (fc < 200) throw new Error('구역 지도에서 얼굴(노랑)을 찾지 못했습니다 — 다시 만들어 보세요(--only mask).');
  fx /= fc; fy /= fc;
  const faceR = Math.max(fmaxX - fminX, fmaxY - fminY) * 0.5;
  console.log(`얼굴 중심 (${fx.toFixed(0)}, ${fy.toFixed(0)}) 반지름 ${faceR.toFixed(0)}`);

  // R 흔들림: 머리카락 + 옷자락, 얼굴에서 멀수록 세게(뿌리는 고정, 끝은 크게)
  const swayRaw = new Float32Array(n);
  const reach = Math.max(w, h) * 0.55;
  for (let i = 0; i < n; i++) {
    if (!hair[i] && !cloth[i]) continue;
    const x = i % w, y = (i / w) | 0;
    const d = Math.hypot(x - fx, (y - fy) * 0.9);
    const k = Math.min(1, Math.max(0, (d - faceR * 0.8) / reach));
    swayRaw[i] = (hair[i] ? 1 : 0.75) * Math.pow(k, 0.8);
  }
  const sway = boxBlur(swayRaw, w, h, 6);
  const breath = boxBlur(torso, w, h, 14);
  const glowB = boxBlur(glow, w, h, 4);

  // A 눈: 원본과 눈 감은 그림의 차이 중 얼굴 상자 안쪽만
  const eye = new Float32Array(n);
  if (fs.existsSync(rawOf('blink'))) {
    const a = await sharp(SRC).removeAlpha().raw().toBuffer();
    const bimg = await sharp(rawOf('blink')).resize(w, h, { fit: 'fill' }).removeAlpha().raw().toBuffer();
    // 눈은 얼굴 덩어리의 위쪽 15% ~ 70% 높이(앞머리 아래 · 입 위), 좌우는 얼굴 폭보다 조금 넓게
    const fh = fmaxY - fminY, fw = fmaxX - fminX;
    const x0 = fminX - fw * 0.1, x1 = fmaxX + fw * 0.1, y0 = fminY + fh * 0.15, y1 = fminY + fh * 0.7;
    for (let y = Math.max(0, y0 | 0); y < Math.min(h, y1); y++)
      for (let x = Math.max(0, x0 | 0); x < Math.min(w, x1); x++) {
        const i = y * w + x;
        const d = Math.abs(a[i * 3] - bimg[i * 3]) + Math.abs(a[i * 3 + 1] - bimg[i * 3 + 1]) + Math.abs(a[i * 3 + 2] - bimg[i * 3 + 2]);
        eye[i] = d > 90 ? 1 : 0;
      }
    // 잡티 제거: 흐린 뒤 다시 문턱 → 눈 덩어리만 남기고 부드러운 가장자리
    const e1 = boxBlur(eye, w, h, 3);
    for (let i = 0; i < n; i++) eye[i] = e1[i] > 0.35 ? 1 : 0;
    const e2 = boxBlur(eye, w, h, 5);
    for (let i = 0; i < n; i++) eye[i] = Math.min(1, e2[i] * 1.6);
    // 눈 감은 그림: 알파 = 눈 영역(그 밖은 투명) — 셰이더가 알파 × 깜빡임 정도로 섞음
    fs.mkdirSync(OUT, { recursive: true });
    const bl = Buffer.alloc(n * 4);
    for (let i = 0; i < n; i++) { bl[i * 4] = bimg[i * 3]; bl[i * 4 + 1] = bimg[i * 3 + 1]; bl[i * 4 + 2] = bimg[i * 3 + 2]; bl[i * 4 + 3] = Math.round(eye[i] * 255); }
    await sharp(bl, { raw: { width: w, height: h, channels: 4 } }).png().toFile(path.join(OUT, args.name + '_blink.png'));
    console.log(`눈 영역 ${(100 * eye.reduce((a, v) => a + (v > 0.5 ? 1 : 0), 0) / n).toFixed(2)}%`);
  }

  // 지도: 불투명 RGB(R 흔들림 · G 호흡 · B 반짝임). 알파를 쓰면 투명한 곳의 색이 지워지므로 쓰지 않음
  const px = Buffer.alloc(n * 3);
  for (let i = 0; i < n; i++) {
    px[i * 3] = Math.round(Math.min(1, sway[i]) * 255);
    px[i * 3 + 1] = Math.round(Math.min(1, breath[i]) * 255);
    px[i * 3 + 2] = Math.round(Math.min(1, glowB[i]) * 255);
  }
  fs.mkdirSync(OUT, { recursive: true });
  await sharp(px, { raw: { width: w, height: h, channels: 3 } }).png().toFile(path.join(OUT, args.name + '_mask.png'));
  await sharp(px, { raw: { width: w, height: h, channels: 3 } }).jpeg({ quality: 85 }).toFile(path.join(RAW, args.name + '_mask_vis.jpg'));
  console.log('→ ' + path.join(OUT, args.name + '_mask.png'));
}

// ---------------- 미리보기(셰이더와 같은 계산) ----------------
// 셰이더(SubcultureRPG/UI Sway)와 같은 식 — 바꾸면 둘 다 바꿀 것
const P = { swayAmp: 0.012, swaySpeed: 1.6, swayFreq: 9.0, breathAmp: 0.006, breathSpeed: 1.1, glowAmp: 0.22, glowSpeed: 2.4, blinkEvery: 4.0, blinkDur: 0.16 };

function sample(img, w, h, u, v, c) {
  // 쌍선형 샘플(가장자리 고정)
  const x = Math.min(w - 1.001, Math.max(0, u * w - 0.5)), y = Math.min(h - 1.001, Math.max(0, v * h - 0.5));
  const x0 = x | 0, y0 = y | 0, tx = x - x0, ty = y - y0;
  const i00 = (y0 * w + x0) * c, i10 = i00 + c, i01 = i00 + w * c, i11 = i01 + c;
  return (k) => (img[i00 + k] * (1 - tx) + img[i10 + k] * tx) * (1 - ty) + (img[i01 + k] * (1 - tx) + img[i11 + k] * tx) * ty;
}

function blinkAmount(t) {
  const ph = t % P.blinkEvery;
  return ph < P.blinkDur ? Math.sin(Math.PI * ph / P.blinkDur) : 0;
}

async function preview() {
  const meta = await sharp(SRC).metadata();
  const w = 384, h = Math.round(meta.height * 384 / meta.width);
  const base = await sharp(SRC).resize(w, h).removeAlpha().raw().toBuffer();
  const blinkPath = path.join(OUT, args.name + '_blink.png');
  const blink = fs.existsSync(blinkPath) ? await sharp(blinkPath).resize(w, h).ensureAlpha().raw().toBuffer() : null;
  const mask = await sharp(path.join(OUT, args.name + '_mask.png')).resize(w, h).removeAlpha().raw().toBuffer();
  const fps = 24, secs = 8, frames = fps * secs;
  const dst = path.join(RAW, args.name + '_preview.mp4');
  const enc = spawn(ffmpeg, ['-y', '-loglevel', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', `${w}x${h}`, '-r', String(fps), '-i', '-',
    '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '18', dst]);
  const done = new Promise((r, j) => { enc.on('close', (c) => (c === 0 ? r() : j(new Error('ffmpeg ' + c)))); });
  const frame = Buffer.alloc(w * h * 3);
  for (let f = 0; f < frames; f++) {
    const t = f / fps;
    const bl = blinkAmount(t);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const u = (x + 0.5) / w, v = (y + 0.5) / h;
      const m = sample(mask, w, h, u, v, 3);
      const sw = m(0) / 255, br = m(1) / 255, gl = m(2) / 255;
      const sb = blink ? sample(blink, w, h, u, v, 4) : null;
      const ey = sb ? sb(3) / 255 : 0;
      // 흔들림: 위아래로 흐르는 물결 + 숨쉬기(가슴 기준 세로로 살짝 부풂)
      const du = sw * P.swayAmp * Math.sin(t * P.swaySpeed * 2 * Math.PI / 2 + v * P.swayFreq + u * 3.0);
      const dv = sw * P.swayAmp * 0.35 * Math.cos(t * P.swaySpeed * Math.PI + v * P.swayFreq * 0.7)
        - br * P.breathAmp * (0.5 + 0.5 * Math.sin(t * P.breathSpeed * 2 * Math.PI / 2));
      const s = sample(base, w, h, u - du, v - dv, 3);
      const shine = 1 + gl * P.glowAmp * (0.5 + 0.5 * Math.sin(t * P.glowSpeed + v * 6.0 - u * 4.0));
      const o = (y * w + x) * 3, k = ey * bl;
      for (let ch = 0; ch < 3; ch++) frame[o + ch] = Math.min(255, Math.round((s(ch) * (1 - k) + (sb ? sb(ch) : 0) * k) * shine));
    }
    if (!enc.stdin.write(frame)) await new Promise((r) => enc.stdin.once('drain', r));
  }
  enc.stdin.end();
  await done;
  console.log('→ ' + dst);
}

(async () => {
  if (!fs.existsSync(SRC)) fail('일러스트가 없습니다: ' + SRC);
  fs.mkdirSync(RAW, { recursive: true });
  if (args.preview) return preview();
  if (!args.rebuild) {
    if (!args.yes) fail(`${args.model} 은 유료 모델입니다(2장). 진행하려면 --yes`);
    const cfg = await api('GET', '/api/config');
    const original = cfg.model;
    if (original !== args.model) await api('POST', '/api/model', { model: args.model });
    try {
      for (const k of ['mask', 'blink']) {
        if (args.only && args.only !== k) continue;
        process.stdout.write(`${k} … `);
        const t0 = Date.now();
        await edit(k);
        console.log(`완료 (${((Date.now() - t0) / 1000).toFixed(0)}초)`);
      }
    } finally {
      if (original !== args.model) await api('POST', '/api/model', { model: original });
    }
  }
  await build();
})().catch((e) => fail(e.message));
