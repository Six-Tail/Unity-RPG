#!/usr/bin/env node
'use strict';
// 전투 효과음 만들기(실제 녹음 소리 겹치기): Kenney 무료 효과음 팩(CC0 — 상용 무료 · 출처 표기 불필요)의 소리를
// 2~4개씩 겹치고(타이밍 · 크기 · 높낮이 조절) 다듬어 Assets/_Project/Resources/Audio/SFX/{이름}.wav 로 저장합니다.
// AI(Stable Audio Open)로 만든 전투 소리가 뭉개지고 싸구려 같다는 의견으로 바꿨습니다(2026-11-25).
//
// 준비: https://kenney.nl 에서 세 팩을 받아 한 폴더에 풀어 둡니다(각 팩 폴더 안에 Audio/*.ogg).
//   kenney.nl/assets/impact-sounds → impact-sounds/
//   kenney.nl/assets/rpg-audio     → rpg-audio/
//   kenney.nl/assets/sci-fi-sounds → sci-fi-sounds/
//
//   node tools/audio/build-sfx-kenney.js --studio "<스튜디오 폴더>" --kenney "<세 팩을 푼 폴더>"
//   node tools/audio/build-sfx-kenney.js --studio ... --kenney ... --key hit,crit     # 일부만(hit 이면 hit_2 · hit_3 까지)
//   node tools/audio/build-sfx-kenney.js --list
//
// 다듬기: 겹친 뒤 → 35Hz 아래 자르기 → 가볍게 압축(타격감) → 최대치 맞추기 → 끝 30ms 줄이기 → 모노 44.1kHz WAV.

const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const REPO = path.resolve(__dirname, '..', '..');
const OUT = path.join(REPO, 'Assets', '_Project', 'Resources', 'Audio', 'SFX');

// 소리 재료: '팩/이름' → <kenney>/<팩>/Audio/<이름>.ogg
// 겹: [재료, 시작(초), 크기(dB), 높낮이(배 — 1 = 그대로, 2 = 한 옥타브 위 · 빨라짐)]
// keep = 최대 길이(초), peak = 최대 음량(0 ~ 1), echo = 잔향(회복 · 궁극기)
const RECIPES = [
  // 타격 — 칼로 베기 · 주먹 · 내려찍기
  { key: 'hit', keep: 0.5, peak: 0.8, layers: [['rpg/knifeSlice', 0, 0], ['impact/impactPunch_medium_000', 0.03, -4]] },
  { key: 'hit_2', keep: 0.5, peak: 0.8, layers: [['rpg/knifeSlice2', 0, 0], ['impact/impactPlate_light_000', 0.02, -7]] },
  { key: 'hit_3', keep: 0.5, peak: 0.8, layers: [['impact/impactPunch_heavy_001', 0, 0], ['rpg/chop', 0, -3]] },
  // 치명타 — 베기 + 금속 충돌 + 낮은 쿵
  { key: 'crit', keep: 0.8, peak: 0.88, layers: [['rpg/knifeSlice', 0, 0], ['impact/impactMetal_heavy_000', 0.02, -2], ['scifi/lowFrequency_explosion_001', 0.02, -7]] },
  { key: 'crit_2', keep: 0.8, peak: 0.88, layers: [['impact/impactPunch_heavy_000', 0, 0], ['impact/impactPlate_heavy_000', 0.01, -3], ['scifi/explosionCrunch_000', 0.02, -9]] },
  // 스킬 시전 — 칼 뽑기 '스릉' · 휘두르는 바람
  { key: 'skill', keep: 0.55, peak: 0.62, layers: [['rpg/drawKnife1', 0, 0]] },
  { key: 'skill_2', keep: 0.6, peak: 0.62, layers: [['rpg/drawKnife3', 0, 0], ['rpg/cloth1', 0, -7]] },
  { key: 'skill_3', keep: 0.6, peak: 0.62, layers: [['rpg/cloth4', 0, -2], ['rpg/drawKnife2', 0.06, -3]] },
  // 궁극기 — 기 모으기(칼 · 종) → 큰 폭발 · 울림
  { key: 'ultimate', keep: 2.4, peak: 0.9, echo: true, layers: [['rpg/drawKnife2', 0, -2], ['impact/impactBell_heavy_000', 0.12, -8, 0.8], ['scifi/lowFrequency_explosion_000', 0.32, 0], ['scifi/explosionCrunch_001', 0.32, -8]] },
  { key: 'ultimate_2', keep: 2.4, peak: 0.9, echo: true, layers: [['rpg/cloth1', 0, -3], ['rpg/knifeSlice', 0.2, 0], ['impact/impactMetal_heavy_002', 0.22, -2], ['scifi/lowFrequency_explosion_000', 0.25, 0], ['scifi/explosionCrunch_002', 0.25, -4]] },
  // 회복 — 종소리를 높여 맑은 차임 + 유리 반짝임(팩에 회복 소리가 없어 만듦)
  { key: 'heal', keep: 1.1, peak: 0.55, echo: true, layers: [['impact/impactBell_heavy_003', 0, -2, 2.0], ['impact/impactGlass_light_000', 0.08, -9, 1.6], ['impact/impactGlass_light_002', 0.2, -11, 1.9]] },
  { key: 'heal_2', keep: 1.1, peak: 0.55, echo: true, layers: [['impact/impactBell_heavy_002', 0, -2, 2.5], ['impact/impactGlass_light_001', 0.1, -9, 1.8], ['impact/impactGlass_light_003', 0.24, -11, 2.0]] },
  // 브레이크 — 보호막 · 갑옷이 깨짐
  { key: 'break', keep: 0.9, peak: 0.82, layers: [['impact/impactGlass_heavy_000', 0, 0], ['impact/impactGlass_heavy_001', 0.04, -3], ['impact/impactPlate_heavy_001', 0, -4]] },
  { key: 'break_2', keep: 0.9, peak: 0.82, layers: [['impact/impactGlass_heavy_002', 0, 0], ['impact/impactMetal_heavy_001', 0, -3], ['impact/impactGlass_medium_000', 0.06, -6]] },
  // 쓰러짐 — 털썩 + 낮은 울림
  { key: 'defeated', keep: 0.9, peak: 0.7, layers: [['impact/impactSoft_heavy_000', 0, 0], ['scifi/lowFrequency_explosion_001', 0.02, -11]] },
  { key: 'defeated_2', keep: 0.9, peak: 0.7, layers: [['impact/impactSoft_heavy_001', 0, 0], ['impact/impactWood_heavy_000', 0, -8]] },
];

const PACK = { impact: 'impact-sounds', rpg: 'rpg-audio', scifi: 'sci-fi-sounds' };
const RATE = 44100;

function fail(msg) { console.error('오류: ' + msg); process.exit(1); }

function parseArgs(argv) {
  const a = { key: null, list: false };
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i];
    const next = () => { if (i + 1 >= argv.length) fail(`${k} 뒤에 값이 필요합니다.`); return argv[++i]; };
    if (k === '--studio') a.studio = next();
    else if (k === '--kenney') a.kenney = next();
    else if (k === '--key') a.key = next().split(',').map((s) => s.trim()).filter(Boolean);
    else if (k === '--list') a.list = true;
    else fail(`알 수 없는 옵션: ${k}`);
  }
  a.studio = a.studio || process.env.AI_IMAGE_STUDIO_DIR || null;
  return a;
}

const args = parseArgs(process.argv.slice(2));

function source(ref) {
  const [pack, name] = ref.split('/');
  return path.join(path.resolve(args.kenney), PACK[pack], 'Audio', name + '.ogg');
}

function build(ffmpeg, r) {
  const inputs = [];
  let graph = '';
  r.layers.forEach(([ref, at, db, pitch], i) => {
    const file = source(ref);
    if (!fs.existsSync(file)) throw new Error('재료가 없습니다: ' + file);
    inputs.push('-i', file);
    const ms = Math.round(at * 1000);
    const p = pitch && pitch !== 1 ? `asetrate=${Math.round(RATE * pitch)},aresample=${RATE},` : '';
    graph += `[${i}]aformat=sample_rates=${RATE}:channel_layouts=mono,${p}volume=${db}dB,adelay=${ms}:all=1[l${i}];`;
  });
  graph += r.layers.map((_, i) => `[l${i}]`).join('') + `amix=inputs=${r.layers.length}:normalize=0:duration=longest`;
  if (r.echo) graph += ',aecho=0.8:0.5:60|140:0.25|0.15';
  const fadeAt = Math.max(0, r.keep - 0.03).toFixed(3);
  graph += `,atrim=0:${r.keep},highpass=f=35,acompressor=threshold=-18dB:ratio=3:attack=2:release=80:makeup=2,` +
    `afade=t=out:st=${fadeAt}:d=0.03[out]`;
  const out = path.join(OUT, r.key + '.wav');
  const tmp = out.replace(/\.wav$/, '.tmp.wav');
  execFileSync(ffmpeg, ['-hide_banner', '-loglevel', 'error', '-y', ...inputs, '-filter_complex', graph, '-map', '[out]',
    '-ac', '1', '-ar', String(RATE), '-c:a', 'pcm_f32le', tmp]);
  try {
    // 최대치를 peak 에 맞춤
    const info = require('child_process').spawnSync(ffmpeg, ['-hide_banner', '-i', tmp, '-af', 'volumedetect', '-f', 'null', '-'], { encoding: 'utf8' }).stderr;
    const max = parseFloat((info.match(/max_volume: (-?[\d.]+) dB/) || [])[1] || '0');
    const gain = 20 * Math.log10(r.peak) - max;
    execFileSync(ffmpeg, ['-hide_banner', '-loglevel', 'error', '-y', '-i', tmp, '-af', `volume=${gain.toFixed(2)}dB`, '-c:a', 'pcm_s16le', out]);
  } finally {
    fs.rmSync(tmp, { force: true });
  }
  return out;
}

(function main() {
  let list = RECIPES;
  if (args.key) list = list.filter((r) => args.key.includes(r.key) || args.key.includes(r.key.replace(/_\d+$/, '')));
  if (list.length === 0) fail('조건에 맞는 소리가 없습니다.');
  if (args.list) {
    for (const r of list) console.log(`  ${r.key.padEnd(11)} ${r.layers.map((l) => l[0]).join(' + ')}`);
    return;
  }
  if (!args.studio) fail('--studio "<이미지 스튜디오 폴더>" 를 지정하세요. (그 폴더의 ffmpeg 을 씁니다)');
  if (!args.kenney) fail('--kenney "<Kenney 팩 세 개를 푼 폴더>" 를 지정하세요.');
  let ffmpeg;
  try { ffmpeg = require(path.join(path.resolve(args.studio), 'node_modules', 'ffmpeg-static')); }
  catch { fail(`'${args.studio}' 에서 ffmpeg-static 을 불러오지 못했습니다.`); }
  fs.mkdirSync(OUT, { recursive: true });
  for (const r of list) {
    build(ffmpeg, r);
    console.log(`SFX/${r.key}.wav  ← ${r.layers.map((l) => l[0].split('/')[1]).join(' + ')}`);
  }
})();
