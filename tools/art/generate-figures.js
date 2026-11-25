#!/usr/bin/env node
'use strict';
// 전투 필드용 "전신 서 있는 그림"을 만듭니다 — 머리부터 발끝까지, 몸을 살짝 오른쪽(적 쪽)으로 돌린 전투 자세.
// 스튜디오의 편집 모델에게 캐릭터 일러스트를 주고 "같은 캐릭터를 전신 · 단색(크로마키) 배경으로" 다시 그리게 한 뒤, 단색을 투명하게 지워
// Assets/_Project/Resources/Art/Figures/{캐릭터 id}.png (683×1024, 투명 배경, 발이 아래쪽)로 저장합니다(전투 화면이 이 이름으로 찾음).
// 없는 캐릭터는 전투 화면이 Cutouts(허벅지까지 그림, 아래를 흐리게)로 대신 그립니다.
//
//   1) 스튜디오 폴더에서 서버를 켭니다:  node server.js
//   2) 이 저장소 폴더에서:
//        node tools/art/generate-figures.js --studio "<스튜디오 폴더>" --list
//        node tools/art/generate-figures.js --studio "<스튜디오 폴더>" --key c_seraphine --yes   # 한 명만
//        node tools/art/generate-figures.js --studio "<스튜디오 폴더>" --yes                     # 아직 없는 캐릭터 전부
//
// 옵션
//   --studio <폴더>    이미지 스튜디오 폴더(sharp 를 여기서 불러옴). 환경변수 AI_IMAGE_STUDIO_DIR 로도 지정
//   --server <주소>    스튜디오 서버 주소 (기본 http://127.0.0.1:3000)
//   --model <이름>     편집 모델 (기본 gpt-image-2.6-sunburst — 유료). 끝나면 원래 모델로 되돌립니다
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
const RAW_DIR = path.join(REPO, 'docs', 'art-preview', 'figures');   // 모델이 그린 원본(단색 배경) — 커밋하지 않음
const W = 1024, H = 1536;                                            // 편집 모델 캔버스(2:3)

function fail(msg) { console.error('오류: ' + msg); process.exit(1); }

function parseArgs(argv) {
  const a = { key: null, force: false, quality: 'medium', chroma: 'auto', list: false, yes: false, rekey: false, model: 'gpt-image-2.6-sunburst' };
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i];
    const next = () => { if (i + 1 >= argv.length) fail(`${k} 뒤에 값이 필요합니다.`); return argv[++i]; };
    if (k === '--studio') a.studio = next();
    else if (k === '--server') a.server = next();
    else if (k === '--model') a.model = next();
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
  return 'Draw this exact same character as a full-body standing battle sprite for a 2D turn-based RPG battle field. ' +
    'Keep the character design completely identical: same face and eyes, same hair style and color, same outfit with all details, same weapon or held item, ' +
    'same colors, same anime cel shading and line art quality. ' +
    (e && e.look ? `Character description: ${e.look}. ` : '') +
    'Weapons and held items must keep exactly the same type, shape, length, width and size relative to the body as in the reference image, with the same effects (flames, ice, lightning, glow): ' +
    'a greatsword stays a huge heavy two-handed greatsword with a very wide blade nearly as tall as the character, a large shield stays just as large, a long staff or spear stays just as long. ' +
    'Never shrink, thin out, simplify or replace any weapon. ' +
    'Show the whole body from the top of the head down to the feet and shoes, both feet fully visible standing on the ground, with a small empty margin above the head and below the feet. ' +
    'The pose does NOT need to match the reference: choose a fresh, dynamic battle-ready pose that suits the weapon and role of the character ' +
    '(e.g. sword raised or held low ready to strike, shield braced forward, staff or book raised casting, bow drawn), ' +
    'body turned three-quarters toward the right side of the image, face looking to the right toward the enemy. ' +
    'The character fills most of the frame height and is centered horizontally. ' +
    `The entire background is one perfectly flat, uniform, solid ${k.name} chroma key color: no scenery, no floor, no ground shadow, no glow, no light rays, no particles, no gradient. ` +
    `No ${k.other} tint, rim light or reflection on the character. Clean crisp outline, including between hair strands and between the arms and the body. Do not add text, borders or extra characters.`;
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
    // 참고 그림: 원본(3:4)을 2:3 캔버스 가운데에, 나머지는 키 색 → 결과는 캔버스 전체(전신)를 씀
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
  const region = { left: 0, top: 0, width: rm.width, height: rm.height };   // 결과 캔버스 전체(전신)
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
  const dst = path.join(ART_ROOT, 'Figures', `${id}.png`);
  fs.mkdirSync(path.dirname(dst), { recursive: true });
  await sharp(out, { raw: { width: info.width, height: info.height, channels: 4 } })
    .resize(683, 1024).png({ compressionLevel: 9 }).toFile(dst);
  return { dst, bg };
}

(async () => {
  let list = entries.filter((e) => e.group === 'characters' && /^Characters[/]/.test(e.out) && fs.existsSync(path.join(ART_ROOT, e.out)));
  if (args.key) list = list.filter((e) => args.key.includes(path.basename(e.out, path.extname(e.out))));
  const todo = list.filter((e) => args.force || args.rekey || !fs.existsSync(path.join(ART_ROOT, 'Figures', path.basename(e.out))));
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
  console.log(`완료 ${ok}/${todo.length} → ${path.join(ART_ROOT, 'Figures')}`);
})().catch((e) => fail(e.message));
