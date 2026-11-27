#!/usr/bin/env node
'use strict';
// 캐릭터 일러스트의 "배경 없는 버전"(누끼)을 만듭니다 — 스토리 대화 · 홈 화면처럼 배경 위에 인물만 세우는 곳에서 씁니다.
// 스튜디오의 편집 모델에게 "인물은 그대로, 배경만 단색(크로마키)으로" 다시 그리게 한 뒤, 그 단색을 투명하게 지워
// Assets/_Project/Resources/Art/Cutouts/{캐릭터 id}.png (768×1024, 투명 배경)로 저장합니다(게임이 이 이름으로 찾음).
//
//   1) 스튜디오 폴더에서 서버를 켭니다:  node server.js
//   2) 이 저장소 폴더에서:
//        node tools/art/generate-cutouts.js --studio "<스튜디오 폴더>" --list
//        node tools/art/generate-cutouts.js --studio "<스튜디오 폴더>" --key c_seraphine --yes   # 한 명만
//        node tools/art/generate-cutouts.js --studio "<스튜디오 폴더>" --yes                     # 아직 없는 캐릭터 전부
//
// 옵션
//   --studio <폴더>    이미지 스튜디오 폴더(sharp 를 여기서 불러옴). 환경변수 AI_IMAGE_STUDIO_DIR 로도 지정
//   --server <주소>    스튜디오 서버 주소 (기본 http://127.0.0.1:3000)
//   --model <이름>     편집 모델 (기본 gpt-image-2.6-sunburst — 유료). 끝나면 원래 모델로 되돌립니다
//   --group <이름>     characters(기본) | bloom(개화 의상) | festival(축제 스킨) — 결과는 {원본 파일 이름}.png (예: c_ciel_bloom.png)
//   --key <id,...>     특정 캐릭터만
//   --force            이미 있는 것도 다시 생성
//   --quality <값>     low | medium | high (기본 medium)
//   --chroma <색>      green | magenta | auto (기본 auto — 일러스트에 덜 쓰인 색을 고름)
//   --rekey            모델을 다시 부르지 않고, 저장해 둔 원본(docs/art-preview/cutouts/*_raw.png)으로 투명 처리만 다시
//   --yes              유료 모델 사용 확인
//   --list             목록만 출력

const fs = require('fs');
const path = require('path');
const { entries } = require('./art-manifest');
const { KEYS, pickChroma, keyOut } = require('./chroma');

const REPO = path.resolve(__dirname, '..', '..');
const ART_ROOT = path.join(REPO, 'Assets', '_Project', 'Resources', 'Art');
const RAW_DIR = path.join(REPO, 'docs', 'art-preview', 'cutouts');   // 모델이 그린 원본(단색 배경) — 커밋하지 않음
const W = 1024, H = 1536;                                            // 편집 모델 캔버스(2:3)

function fail(msg) { console.error('오류: ' + msg); process.exit(1); }

function parseArgs(argv) {
  const a = { group: 'characters', key: null, force: false, quality: 'medium', chroma: 'auto', list: false, yes: false, rekey: false, model: 'gpt-image-2.6-sunburst' };
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i];
    const next = () => { if (i + 1 >= argv.length) fail(`${k} 뒤에 값이 필요합니다.`); return argv[++i]; };
    if (k === '--studio') a.studio = next();
    else if (k === '--server') a.server = next();
    else if (k === '--model') a.model = next();
    else if (k === '--group') a.group = next();
    else if (k === '--key') a.key = next().split(',').map((s) => s.trim()).filter(Boolean);
    else if (k === '--force') a.force = true;
    else if (k === '--quality') a.quality = next();
    else if (k === '--chroma') a.chroma = next();
    else if (k === '--rekey') a.rekey = true;
    else if (k === '--yes') a.yes = true;
    else if (k === '--list') a.list = true;
    else fail(`알 수 없는 옵션: ${k}`);
  }
  if (!['low', 'medium', 'high'].includes(a.quality)) fail('--quality 는 low | medium | high 중 하나입니다.');
  if (!['auto', 'green', 'magenta'].includes(a.chroma)) fail('--chroma 는 auto | green | magenta 중 하나입니다.');
  a.studio = a.studio || process.env.AI_IMAGE_STUDIO_DIR || null;
  a.server = (a.server || process.env.AI_IMAGE_STUDIO_URL || 'http://127.0.0.1:3000').replace(/[/]+$/, '');
  return a;
}

const args = parseArgs(process.argv.slice(2));
if (!args.studio) fail('--studio "<이미지 스튜디오 폴더>" 가 필요합니다.');
const sharp = require(path.join(args.studio, 'node_modules', 'sharp'));

async function api(method, route, body) {
  const init = { method };
  if (body instanceof FormData) init.body = body;
  else if (body) { init.headers = { 'Content-Type': 'application/json' }; init.body = JSON.stringify(body); }
  const res = await fetch(args.server + route, init);
  const text = await res.text();
  let json = null; try { json = JSON.parse(text); } catch { /* 본문이 JSON 이 아님 */ }
  if (!res.ok) throw new Error((json && json.error) || `HTTP ${res.status} ${text.slice(0, 200)}`);
  return json;
}

function promptFor(key, e) {
  const k = KEYS[key];
  return 'Redraw this exact illustration with only the background changed. ' +
    'Keep the character completely identical: same pose, same face and eyes, same hair, same outfit, same weapon and held items, ' +
    (e && e.weapon ? `Weapon details (follow exactly): ${e.weapon}. ` : '') +
    'same colors, same line art and anime cel shading, same size and same position in the frame. ' +
    `Remove the whole background, scenery, sky, floor, glow, light rays, sparkles and floating particles, and fill everything that is not the character with one perfectly flat, uniform, solid ${k.name} chroma key color. ` +
    `No gradient, no texture, no vignette, no shadow and no ground on the background. No ${k.other} tint, rim light or reflection on the character. ` +
    'Clean crisp outline around the character, including between hair strands and between the arms and the body. Do not add text, borders or new objects.';
}

async function makeOne(e, key) {
  const src = path.join(ART_ROOT, e.out);
  const id = path.basename(e.out, path.extname(e.out));
  const meta = await sharp(src).metadata();
  const sh = Math.round(meta.height * (W / meta.width));
  const top = Math.round((H - sh) / 2);
  const rawPath = path.join(RAW_DIR, `${id}_raw.png`);
  fs.mkdirSync(RAW_DIR, { recursive: true });

  if (args.rekey && fs.existsSync(rawPath + '.key')) key = fs.readFileSync(rawPath + '.key', 'utf8').trim();
  if (!args.rekey || !fs.existsSync(rawPath)) {
    // 원본(3:4)을 2:3 캔버스 가운데에 놓고 위아래는 키 색으로 채움 → 결과에서 같은 영역을 잘라 원본과 구도를 맞춤
    const input = await sharp({ create: { width: W, height: H, channels: 3, background: KEYS[key].rgb } })
      .composite([{ input: await sharp(src).resize(W, sh).removeAlpha().toBuffer(), left: 0, top }]).png().toBuffer();
    const form = new FormData();
    form.append('prompt', promptFor(key, e));
    form.append('size', `${W}x${H}`);
    form.append('quality', args.quality);
    form.append('n', '1');
    form.append('images', new Blob([input], { type: 'image/png' }), 'source.png');
    const res = await api('POST', '/api/edit', form);
    const raw = Buffer.from(await (await fetch(args.server + res.images[0].url)).arrayBuffer());
    fs.writeFileSync(rawPath, raw);
    fs.writeFileSync(rawPath + '.key', key);
  }

  const rm = await sharp(rawPath).metadata();
  const fy = rm.height / H;
  const region = { left: 0, top: Math.round(top * fy), width: rm.width, height: Math.round(sh * fy) };
  // 모델이 단색 대신 처음부터 투명 배경으로 돌려주는 경우가 있음 → 그 투명도를 그대로 씀
  const rgba = await sharp(rawPath).extract(region).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const info = rgba.info;
  let out, bg;
  if (rm.hasAlpha && rgba.data[3] < 16 && rgba.data[(info.width - 1) * 4 + 3] < 16) {
    out = rgba.data; bg = ['투명'];
  } else {
    const { data } = await sharp(rawPath).extract(region).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    ({ out, bg } = keyOut(data, info.width, info.height, key));
  }
  const dst = path.join(ART_ROOT, 'Cutouts', `${id}.png`);
  fs.mkdirSync(path.dirname(dst), { recursive: true });
  await sharp(out, { raw: { width: info.width, height: info.height, channels: 4 } })
    .resize(meta.width, meta.height).png({ compressionLevel: 9 }).toFile(dst);
  return { dst, bg };
}

(async () => {
  let list = entries.filter((e) => e.group === args.group && /^Characters[/]/.test(e.out) && fs.existsSync(path.join(ART_ROOT, e.out)));
  if (args.key) list = list.filter((e) => args.key.includes(path.basename(e.out, path.extname(e.out))));
  const todo = list.filter((e) => args.force || args.rekey || !fs.existsSync(path.join(ART_ROOT, 'Cutouts', path.basename(e.out))));
  for (const e of list) {
    e.chroma = args.chroma === 'auto' ? await pickChroma(sharp, path.join(ART_ROOT, e.out)) : args.chroma;
    if (args.list) console.log(`${todo.includes(e) ? '만들 것' : '있음   '}  ${path.basename(e.out)}  배경색 ${e.chroma}  ${e.label || ''}`);
  }
  if (args.list) return;
  if (todo.length === 0) { console.log('만들 것이 없습니다. (--force 로 다시 생성)'); return; }
  if (args.rekey) {
    for (const e of todo) { const r = await makeOne(e, e.chroma); console.log(`rekey ${path.basename(r.dst)}  bg=${r.bg.join(',')}`); }
    return;
  }

  const paid = !/^local:/.test(args.model);
  if (paid && !args.yes) fail(`${args.model} 은 유료 모델입니다(${todo.length}장). 진행하려면 --yes 를 붙이세요.`);
  const cfg = await api('GET', '/api/config');
  const original = cfg.model;
  if (original !== args.model) await api('POST', '/api/model', { model: args.model });
  let ok = 0;
  try {
    for (const e of todo) {
      const t0 = Date.now();
      try {
        const r = await makeOne(e, e.chroma);
        ok++;
        console.log(`[${ok}/${todo.length}] ${path.basename(r.dst)}  배경색 ${e.chroma} (${r.bg.join(',')})  ${((Date.now() - t0) / 1000).toFixed(0)}s`);
      } catch (err) { console.error(`실패 ${path.basename(e.out)}: ${err.message}`); }
    }
  } finally {
    if (original !== args.model) await api('POST', '/api/model', { model: original });
  }
  console.log(`완료 ${ok}/${todo.length} → ${path.join(ART_ROOT, 'Cutouts')}`);
})().catch((e) => fail(e.message));
