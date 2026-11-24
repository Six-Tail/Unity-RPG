#!/usr/bin/env node
'use strict';
// 버튼 그림을 만듭니다 — 게임의 모든 버튼(UIFactory.AddButton)이 이 그림을 9-slice 로 늘려 씁니다.
// 스튜디오 모델로 "진주빛 판 + 금테" 버튼 한 장을 투명 배경으로 그린 뒤, 늘려도 모양이 안 깨지게 다듬어 두 장으로 나눕니다.
//
//   Assets/_Project/Resources/Art/UI/button_base.png    판(회색조) — 버튼마다 색을 입혀 씀(파랑 · 금색 · 초록 …)
//   Assets/_Project/Resources/Art/UI/button_frame.png   금테(색 그대로) — 판 위에 겹침
//
//   node tools/art/generate-buttons.js --studio "<스튜디오 폴더>" --yes        # 생성(유료 모델)
//   node tools/art/generate-buttons.js --studio "<스튜디오 폴더>" --rebuild    # 모델을 다시 부르지 않고 저장해 둔 원본으로 다시 다듬기
//
// 옵션
//   --studio <폴더>   이미지 스튜디오 폴더(sharp 를 여기서 불러옴). 환경변수 AI_IMAGE_STUDIO_DIR 로도 지정
//   --server <주소>   스튜디오 서버 주소 (기본 http://127.0.0.1:3000)
//   --model <이름>    생성 모델 (기본 gpt-image-2.6-sunburst — 유료). 끝나면 원래 모델로 되돌립니다
//   --quality <값>    low | medium | high (기본 high)
//   --raw <파일>      원본으로 쓸 그림을 직접 지정(투명 배경 PNG)
//   --rebuild         저장해 둔 원본(docs/art-preview/ui/button_raw.png)으로 다시 다듬기만
//   --yes             유료 모델 사용 확인
//
// 스프라이트 규격(바꾸면 Editor/ArtImportSettings.cs 의 ButtonBorder 도 같이 바꿀 것)
//   208×128 = 왼쪽 끝 96 + 가운데 16 + 오른쪽 끝 96(왼쪽을 뒤집은 것) · 9-slice 테두리 좌우 96, 위아래 44

const fs = require('fs');
const path = require('path');

const REPO = path.resolve(__dirname, '..', '..');
const OUT_DIR = path.join(REPO, 'Assets', '_Project', 'Resources', 'Art', 'UI');
const RAW_DIR = path.join(REPO, 'docs', 'art-preview', 'ui');
const CAP = 96, MID = 16, HEIGHT = 128;

const PROMPT =
  'A single wide horizontal fantasy game UI button, completely empty with no text and no icon, ' +
  'in the interface style of a modern anime mobile gacha RPG, viewed perfectly straight-on as a flat 2D front view. ' +
  'The button face is a smooth glossy pearl-white enamel plate with a soft vertical gradient, lighter at the top and light grey at the bottom, ' +
  'with a subtle glossy highlight band along the upper part. ' +
  'It has a thin elegant polished gold metal border. Small ornate gold filigree flourishes sit only at the four corners; ' +
  'the long straight top, bottom, left and right edges are plain thin gold lines with no decoration. Softly rounded corners. ' +
  'Perfectly symmetrical, crisp clean edges, clean line art with soft cel shading, high detail. ' +
  'The button is about three and a half times wider than tall and fills most of the image width. ' +
  'No text, no letters, no gem in the middle, no drop shadow, no glow, isolated on a fully transparent background.';

function fail(msg) { console.error('오류: ' + msg); process.exit(1); }

function parseArgs(argv) {
  const a = { quality: 'high', model: 'gpt-image-2.6-sunburst', yes: false, rebuild: false, raw: null };
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i];
    const next = () => { if (i + 1 >= argv.length) fail(`${k} 뒤에 값이 필요합니다.`); return argv[++i]; };
    if (k === '--studio') a.studio = next();
    else if (k === '--server') a.server = next();
    else if (k === '--model') a.model = next();
    else if (k === '--quality') a.quality = next();
    else if (k === '--raw') a.raw = next();
    else if (k === '--rebuild') a.rebuild = true;
    else if (k === '--yes') a.yes = true;
    else fail(`알 수 없는 옵션: ${k}`);
  }
  a.studio = a.studio || process.env.AI_IMAGE_STUDIO_DIR || null;
  a.server = (a.server || process.env.AI_IMAGE_STUDIO_URL || 'http://127.0.0.1:3000').replace(/[/]+$/, '');
  return a;
}

const args = parseArgs(process.argv.slice(2));
if (!args.studio) fail('--studio "<이미지 스튜디오 폴더>" 가 필요합니다.');
const sharp = require(path.join(args.studio, 'node_modules', 'sharp'));

async function api(method, route, body) {
  const init = { method };
  if (body) { init.headers = { 'Content-Type': 'application/json' }; init.body = JSON.stringify(body); }
  const res = await fetch(args.server + route, init);
  const text = await res.text();
  let json = null; try { json = JSON.parse(text); } catch { /* 본문이 JSON 이 아님 */ }
  if (!res.ok) throw new Error((json && json.error) || `HTTP ${res.status} ${text.slice(0, 200)}`);
  return json;
}

async function generateRaw(rawPath) {
  const cfg = await api('GET', '/api/config');
  const original = cfg.model;
  if (original !== args.model) await api('POST', '/api/model', { model: args.model });
  try {
    const res = await api('POST', '/api/generate', { prompt: PROMPT, size: '1536x1024', quality: args.quality, background: 'transparent', format: 'png', n: 1 });
    const raw = Buffer.from(await (await fetch(args.server + res.images[0].url)).arrayBuffer());
    fs.mkdirSync(path.dirname(rawPath), { recursive: true });
    fs.writeFileSync(rawPath, raw);
  } finally {
    if (original !== args.model) await api('POST', '/api/model', { model: original });
  }
}

const smooth = (lo, hi, v) => { const t = Math.min(1, Math.max(0, (v - lo) / (hi - lo))); return t * t * (3 - 2 * t); };

// 금색(따뜻한 색 + 채도 있음)인 정도 0~1 — 금테와 판을 가르는 기준
function goldness(r, g, b) {
  const mx = Math.max(r, g, b), mn = Math.min(r, g, b);
  if (mx < 24) return 0;
  const sat = (mx - mn) / mx;
  const warm = r >= g && g >= b ? 1 : 0;      // 빨강 ≥ 초록 ≥ 파랑 = 주황~노랑 계열
  return warm * smooth(0.16, 0.36, sat);
}

async function build(rawPath) {
  // 1) 투명 여백을 잘라내고 높이 128 로 맞춤
  const src = sharp(rawPath).ensureAlpha();
  const { data: a0, info: i0 } = await src.clone().raw().toBuffer({ resolveWithObject: true });
  let x0 = i0.width, y0 = i0.height, x1 = -1, y1 = -1;
  for (let y = 0; y < i0.height; y++) for (let x = 0; x < i0.width; x++) {
    if (a0[(y * i0.width + x) * 4 + 3] > 40) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
  }
  if (x1 < 0) fail('원본에 그림이 없습니다.');
  if (a0[3] > 200 && a0[(i0.width - 1) * 4 + 3] > 200) fail('원본 배경이 투명하지 않습니다(모델이 투명 배경을 지원하지 않음).');
  const bw = x1 - x0 + 1, bh = y1 - y0 + 1;
  const rw = Math.round(bw * (HEIGHT / bh));
  if (rw < CAP * 2 + MID) fail(`버튼이 너무 좁게 그려졌습니다 (${bw}x${bh}).`);
  const { data, info } = await sharp(rawPath).ensureAlpha().extract({ left: x0, top: y0, width: bw, height: bh })
    .resize(rw, HEIGHT, { fit: 'fill' }).raw().toBuffer({ resolveWithObject: true });

  // 2) 왼쪽 끝 + (끝 바로 안쪽 몇 줄의 평균을 늘린) 가운데 + 왼쪽 끝을 뒤집은 오른쪽 끝 → 좌우 대칭, 가로로 늘려도 테두리가 고름
  const W = CAP * 2 + MID;
  const px = Buffer.alloc(W * HEIGHT * 4);
  const at = (x, y) => (y * info.width + x) * 4;
  for (let y = 0; y < HEIGHT; y++) {
    const mid = [0, 0, 0, 0];
    for (let k = 0; k < 6; k++) for (let c = 0; c < 4; c++) mid[c] += data[at(CAP + k, y) + c] / 6;
    for (let x = 0; x < W; x++) {
      const o = (y * W + x) * 4;
      if (x < CAP) data.copy(px, o, at(x, y), at(x, y) + 4);
      else if (x < CAP + MID) for (let c = 0; c < 4; c++) px[o + c] = Math.round(mid[c]);
      else data.copy(px, o, at(W - 1 - x, y), at(W - 1 - x, y) + 4);
    }
  }

  // 3) 판(회색조 · 밝기 정규화)과 금테(금색 부분만)로 나눔
  const lum = (o) => (0.299 * px[o] + 0.587 * px[o + 1] + 0.114 * px[o + 2]) / 255;
  const plate = [];
  for (let o = 0; o < px.length; o += 4) if (px[o + 3] > 250 && goldness(px[o], px[o + 1], px[o + 2]) < 0.05) plate.push(lum(o));
  plate.sort((p, q) => p - q);
  // 판의 어두운 쪽(3%) ~ 밝은 쪽(97%)을 0.76 ~ 1.0 으로 펴서, 색을 입혀도 위아래 음영 · 광택이 보이게 함
  const pLo = plate.length ? plate[Math.floor(plate.length * 0.03)] : 0, pHi = plate.length ? plate[Math.floor(plate.length * 0.97)] : 1;
  const base = Buffer.alloc(px.length), frame = Buffer.alloc(px.length);
  for (let o = 0; o < px.length; o += 4) {
    const g = goldness(px[o], px[o + 1], px[o + 2]);
    // 판: 밝은 진주빛을 0.76~1.0 사이 회색으로(색을 입혀도 탁해지지 않게). 금테 아래는 중간 밝기 — 금테가 옅어질 때 은은한 테두리로 남음
    let v = 0.76 + 0.24 * Math.min(1, Math.max(0, (lum(o) - pLo) / Math.max(0.01, pHi - pLo)));
    v = v * (1 - g) + 0.6 * g;
    base[o] = base[o + 1] = base[o + 2] = Math.round(v * 255);
    base[o + 3] = px[o + 3];
    frame[o] = px[o]; frame[o + 1] = px[o + 1]; frame[o + 2] = px[o + 2];
    frame[o + 3] = Math.round(px[o + 3] * g);
  }
  fs.mkdirSync(OUT_DIR, { recursive: true });
  const raw = { raw: { width: W, height: HEIGHT, channels: 4 } };
  await sharp(px, raw).png().toFile(path.join(RAW_DIR, 'button_sliced.png'));
  await sharp(base, raw).png({ compressionLevel: 9 }).toFile(path.join(OUT_DIR, 'button_base.png'));
  await sharp(frame, raw).png({ compressionLevel: 9 }).toFile(path.join(OUT_DIR, 'button_frame.png'));
  console.log(`원본 버튼 ${bw}x${bh} → ${W}x${HEIGHT} (9-slice 좌우 ${CAP}, 위아래 44)  ${OUT_DIR}`);
}

(async () => {
  const rawPath = args.raw ? path.resolve(args.raw) : path.join(RAW_DIR, 'button_raw.png');
  fs.mkdirSync(RAW_DIR, { recursive: true });
  if (!args.rebuild && !args.raw) {
    if (!/^local:/.test(args.model) && !args.yes) fail(`${args.model} 은 유료 모델입니다(1장). 진행하려면 --yes 를 붙이세요.`);
    await generateRaw(rawPath);
  }
  if (!fs.existsSync(rawPath)) fail(`원본이 없습니다: ${rawPath}`);
  await build(rawPath);
})().catch((e) => fail(e.message));
