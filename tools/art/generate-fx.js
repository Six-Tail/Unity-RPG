#!/usr/bin/env node
'use strict';
// 전투 스킬 이펙트 재료(텍스처)를 만듭니다 — 베기 궤적 · 불꽃 · 얼음 파편 · 번개 · 마법진 등.
// 검은 배경 위의 흰색(단색) 빛으로 그리게 한 뒤, 밝기를 투명도로 바꿔(검정 = 투명) 흰 그림 + 알파로 저장합니다.
// 게임(SkillFx)은 이 흰 그림에 속성 색을 곱해 쓰므로, 한 장으로 모든 속성에 씁니다.
// 저장: Assets/_Project/Resources/Art/Fx/{키}.png (512×512, 투명 배경)
//
//   node tools/art/generate-fx.js --studio "<스튜디오 폴더>" --list
//   node tools/art/generate-fx.js --studio "<스튜디오 폴더>" --key slash_arc,flame --yes
//   node tools/art/generate-fx.js --studio "<스튜디오 폴더>" --yes            # 아직 없는 것 전부
//   node tools/art/generate-fx.js --studio "<스튜디오 폴더>" --rekey          # 저장한 원본으로 투명 처리만 다시
//
// 옵션: --server <주소> · --model <이름> · --quality low|medium|high · --force · --yes · --list · --rekey

const fs = require('fs');
const path = require('path');

const REPO = path.resolve(__dirname, '..', '..');
const OUT_DIR = path.join(REPO, 'Assets', '_Project', 'Resources', 'Art', 'Fx');
const RAW_DIR = path.join(REPO, 'docs', 'art-preview', 'fx');   // 모델이 그린 원본(검은 배경) — 커밋하지 않음
const SIZE = 512;

// [키, 이름, 그림 설명]  — 모두 가운데 하나만, 세로로 긴 것은 위아래 방향으로
const FX = [
  ['slash_arc', '베기 궤적(초승달)', 'a single crescent-shaped sword slash arc trail, thick bright core tapering to razor-thin pointed ends, curving from lower left to upper right, motion-blurred energy streak with fine speed lines along the edge'],
  ['slash_line', '베기 일직선', 'a single straight vertical razor-sharp sword slash streak, very bright thin core, tapering to needle points at both top and bottom ends, faint motion streaks beside it'],
  ['impact_hit', '타격 섬광', 'a single explosive hit-impact spark burst, sharp irregular star-shaped spikes radiating from a bright center, like an anime punch impact flash'],
  ['spark_star', '반짝이', 'a single four-pointed twinkling star sparkle with long thin cross rays and a small bright round core'],
  ['glow_orb', '빛 구슬', 'a single soft round glowing energy orb with an intensely bright core fading smoothly outward, subtle swirling wisps inside'],
  ['shockwave', '충격파 고리', 'a single circular shockwave ring seen from the front, a thin bright ring edge with ragged energy wisps and faint inner distortion, the center is empty and dark'],
  ['magic_circle', '마법진', 'a single ornate magic circle seen flat from the front, concentric rings with mystical runes, geometric star pattern and small glyph symbols, thin glowing lines, perfectly round and symmetric'],
  ['light_pillar', '빛기둥', 'a single tall vertical pillar of holy light beam, brightest in the center line, soft edges, small sparkles rising along it, fading at the top and bottom'],
  ['flame', '불꽃', 'a single stylized rising flame tongue, licking upward in a teardrop shape with curling wisps at the tip, anime fire effect, bright hot core'],
  ['fire_burst', '화염 폭발', 'a single round fiery explosion burst with billowing flame tongues radiating outward and small embers, anime explosion effect'],
  ['ice_shard', '얼음 파편', 'a single sharp elongated ice crystal shard pointing straight up, faceted crystalline surfaces with bright edge highlights, vertical'],
  ['ice_burst', '서리 폭발', 'a single burst of jagged ice crystals spiking outward in all directions from the center, frosty mist and tiny snowflake sparkles'],
  ['lightning_bolt', '번개 줄기', 'a single jagged vertical lightning bolt from the top edge to the bottom edge, bright white-hot core with small forked branches and electric glow'],
  ['lightning_arc', '전기 스파크', 'a single crackling ball of electric arcs, many thin jagged lightning tendrils branching outward from the center'],
  ['wind_swirl', '바람 소용돌이', 'a single swirling wind vortex seen from the front, several curved crescent air-blade streaks spiraling around the center, light leaf-shaped wisps'],
  ['wind_blade', '바람 칼날', 'a single thin curved crescent air blade, sharp crisp edge, with two fainter parallel wind streaks trailing behind it'],
  ['smoke_puff', '연기', 'a single soft round puff of smoke or dust cloud, fluffy billowing edges, semi-transparent wisps'],
  ['heal_cross', '회복 빛', 'a single soft glowing plus-shaped healing cross symbol with gentle radiant halo and tiny floating sparkles around it'],
  ['rune_glyph', '룬 문자', 'a single glowing mystical rune glyph symbol made of thin elegant lines inside a small circle, fantasy magic sigil'],
  ['feather_streak', '빛 줄기', 'a single soft elongated vertical streak of light like a comet tail, bright rounded head at the top fading into a long thin tail at the bottom'],
  ['moon', '보름달(궁극기 장면)', 'a single large full moon disc seen straight on, soft crater and mare details, a bright glowing rim and a faint halo around it'],
  // 6★ 궁극기 장면 전용 소품(세로로 긴 것은 위아래 방향)
  ['scene_greatsword', '불꽃 대검(세라핀 장면)', 'a single huge ornate greatsword pointing straight down, wide heavy blade, flames streaming upward from the blade edges, seen from the front, vertical'],
  ['scene_aurora', '오로라 커튼(오로라 장면)', 'a single long horizontal flowing aurora borealis curtain ribbon with soft vertical light streaks hanging down from a wavy top edge, spanning the whole width'],
  ['scene_crystal', '거대한 얼음 결정(시엘 장면)', 'a single giant cluster of sharp faceted ice crystals erupting upward from the bottom, tall jagged spikes, bright edge highlights'],
  ['scene_dagger', '곡도(카엘 장면)', 'a single slim curved dagger blade pointing straight up with a small guard and wrapped grip, a faint wind trail along the curve, vertical'],
  ['scene_shield', '거대한 방패(아크 장면)', 'a single massive heraldic tower shield seen from the front with a lightning bolt emblem in the center, thick rim, sparks crackling on the edges'],
  ['scene_rays', '빛줄기(에스텔 장면)', 'soft holy god rays of light radiating downward from a bright point at the top center, wide fan of light beams with floating dust motes'],
  // 6★ 궁극기 전용 문양(SkillFx.Ultimate가 캐릭터 id로 찾음: Fx/ult_{id}) — 무기 · 상징을 넣은 큰 문양
  ['ult_c_seraphine', '세라핀 궁극기 문양', 'a huge flaming greatsword thrust straight down through the center of a blazing phoenix-wing emblem, burning feathers fanning out to both sides'],
  ['ult_c_ciel', '시엘 궁극기 문양', 'two slender rapiers crossed in an X in front of a giant six-pointed snowflake crystal emblem with sharp icy facets'],
  ['ult_c_ark', '아크 궁극기 문양', 'a massive heraldic tower shield emblem at the center crackling with branching lightning bolts radiating outward in all directions'],
  ['ult_c_aurora', '오로라 궁극기 문양', 'a tall crystal staff in the center surrounded by flowing aurora ribbons forming a round halo, with tiny twinkling stars'],
  ['ult_c_kael', '카엘 궁극기 문양', 'twin curved daggers crossed in the center of a spiraling wind vortex made of crescent air blades and leaf-shaped gusts'],
  ['ult_c_violet', '바이올렛 궁극기 문양', 'a single razor-sharp katana slash streak cutting diagonally across a thin outlined crescent moon ring (only the outline of the moon, empty inside), with a few falling petals and thin speed lines along the cut'],
  ['ult_c_estelle', '에스텔 궁극기 문양', 'a hanging lantern with a gentle flame in the center, surrounded by a radiant ring of rising feathers and soft embers like a sacred blessing'],
];

const PROMPT = (desc) =>
  `Game VFX texture sprite for a 2D anime RPG battle effect: ${desc}. ` +
  'Rendered as pure white and light grey glowing energy only, completely monochrome, absolutely no color hue. ' +
  'The entire background is perfectly pure solid black (#000000) with no texture, no vignette, no noise. ' +
  'One single effect, centered, fully inside the frame with a black margin on every side, nothing touching the edges. ' +
  'Soft bloom glow, high contrast, crisp clean shapes, no characters, no objects, no text, no frame.';

function fail(msg) { console.error('오류: ' + msg); process.exit(1); }

function parseArgs(argv) {
  const a = { key: null, force: false, quality: 'medium', list: false, yes: false, rekey: false, model: null };
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i];
    const next = () => { if (i + 1 >= argv.length) fail(`${k} 뒤에 값이 필요합니다.`); return argv[++i]; };
    if (k === '--studio') a.studio = next();
    else if (k === '--server') a.server = next();
    else if (k === '--model') a.model = next();
    else if (k === '--key') a.key = next().split(',').map((s) => s.trim()).filter(Boolean);
    else if (k === '--force') a.force = true;
    else if (k === '--quality') a.quality = next();
    else if (k === '--rekey') a.rekey = true;
    else if (k === '--yes') a.yes = true;
    else if (k === '--list') a.list = true;
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

// 검은 배경 → 투명: 알파 = 가장 밝은 채널(살짝 곡선), 색은 흰색. 가장자리 8%는 서서히 0으로(모서리 잔티 제거).
async function toAlpha(rawPath, dst) {
  const { data, info } = await sharp(rawPath).resize(SIZE, SIZE, { fit: 'cover' }).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  const out = Buffer.alloc(SIZE * SIZE * 4);
  const edge = SIZE * 0.08;
  for (let y = 0; y < SIZE; y++)
    for (let x = 0; x < SIZE; x++) {
      const i = (y * SIZE + x) * 3, o = (y * SIZE + x) * 4;
      let v = Math.max(data[i], data[i + 1], data[i + 2]) / 255;
      v = Math.max(0, (v - 0.04) / 0.96);                 // 검정에 가까운 잡티 제거
      const d = Math.min(x, y, SIZE - 1 - x, SIZE - 1 - y);
      if (d < edge) v *= d / edge;
      out[o] = 255; out[o + 1] = 255; out[o + 2] = 255;
      out[o + 3] = Math.round(Math.pow(v, 0.9) * 255);
    }
  await sharp(out, { raw: { width: info.width, height: info.height, channels: 4 } }).png({ compressionLevel: 9 }).toFile(dst);
}

async function makeOne([key, , desc]) {
  const rawPath = path.join(RAW_DIR, `${key}_raw.png`);
  if (!args.rekey || !fs.existsSync(rawPath)) {
    const res = await api('POST', '/api/generate', {
      prompt: PROMPT(desc), negative: 'color, colored, background scenery, text, watermark, border, frame',
      aspect: '1:1', resolution: '1K', quality: args.quality, format: 'png', n: 1,
    });
    const img = res && res.images && res.images[0];
    if (!img || !img.url) throw new Error('서버가 이미지를 돌려주지 않았습니다.');
    fs.writeFileSync(rawPath, Buffer.from(await (await fetch(args.server + img.url)).arrayBuffer()));
  }
  const dst = path.join(OUT_DIR, `${key}.png`);
  await toAlpha(rawPath, dst);
  return dst;
}

(async () => {
  fs.mkdirSync(RAW_DIR, { recursive: true });
  fs.mkdirSync(OUT_DIR, { recursive: true });
  let list = FX;
  if (args.key) list = list.filter(([k]) => args.key.includes(k));
  const todo = list.filter(([k]) => args.force || args.rekey || !fs.existsSync(path.join(OUT_DIR, `${k}.png`)));
  if (args.list) { for (const f of list) console.log(`${todo.includes(f) ? '만들 것' : '있음   '}  ${f[0]}  ${f[1]}`); return; }
  if (todo.length === 0) { console.log('만들 것이 없습니다. (--force 로 다시 생성)'); return; }

  let original = null;
  if (!args.rekey) {
    const cfg = await api('GET', '/api/config');
    original = cfg.model;
    const model = args.model || original;
    if (!/^local:/.test(model) && !args.yes) fail(`${model} 은 유료 모델입니다(${todo.length}장). 진행하려면 --yes 를 붙이세요.`);
    if (model !== original) await api('POST', '/api/model', { model });
    else original = null;
  }
  let ok = 0;
  try {
    for (const f of todo) {
      const t0 = Date.now();
      try { await makeOne(f); ok++; console.log(`[${ok}/${todo.length}] ${f[0]} (${f[1]})  ${((Date.now() - t0) / 1000).toFixed(0)}s`); }
      catch (err) { console.error(`실패 ${f[0]}: ${err.message}`); }
    }
  } finally {
    if (original) await api('POST', '/api/model', { model: original });
  }
  console.log(`완료 ${ok}/${todo.length} → ${OUT_DIR}`);
})().catch((e) => fail(e.message));

module.exports = { FX };
