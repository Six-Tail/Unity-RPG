#!/usr/bin/env node
'use strict';
// 효과음 라보(soundeffect-lab.info — 상용 무료 · 출처 표기 불필요) 소리로 회복 · 스킬 효과음을 만듭니다.
//
// ⚠ 효과음 라보는 파일 재배포가 금지(고친 파일 포함)이고 이 저장소는 공개라서, 결과는 저장소에 넣지 않습니다(.gitignore).
//   결과: Assets/_Project/Resources/Audio/SFX/Lab/{이름}.wav — 있으면 게임이 같은 이름의 기본 소리(Kenney 로 만든 것) 대신 씀.
//   다른 PC 에서는 이 스크립트를 한 번 실행하면 공식 사이트에서 받아 같은 소리를 만듭니다. 안 하면 기본 소리로 나옵니다.
//
//   node tools/audio/fetch-sfx-lab.js --studio "<스튜디오 폴더>"
//   node tools/audio/fetch-sfx-lab.js --studio "<스튜디오 폴더>" --key heal     # 일부만(heal 이면 heal_2 까지)
//
// 받은 원본은 docs/art-preview/audio/lab/ 에 둡니다(커밋 안 함). 이용 조건: https://soundeffect-lab.info/agreement/

const fs = require('fs');
const path = require('path');
const { execFileSync, spawnSync } = require('child_process');

const REPO = path.resolve(__dirname, '..', '..');
const OUT = path.join(REPO, 'Assets', '_Project', 'Resources', 'Audio', 'SFX', 'Lab');
const CACHE = path.join(REPO, 'docs', 'art-preview', 'audio', 'lab');
const PAGE = 'https://soundeffect-lab.info/sound/battle/';
const RATE = 44100;

// file = 사이트의 파일 이름(PAGE + 'mp3/' + file + '.mp3'), keep = 남길 길이(초), peak = 최대 음량(0 ~ 1)
const SOUNDS = [
  { key: 'heal', file: 'magic-cure1', keep: 1.5, peak: 0.6 },        // 반짝이는 회복 차임
  { key: 'heal_2', file: 'magic-cure4', keep: 1.4, peak: 0.6 },      // 따뜻하게 차오르는 회복
  { key: 'skill', file: 'sword-gesture2', keep: 0.6, peak: 0.62 },   // 칼 휘두르기
  { key: 'skill_2', file: 'magic-charge1', keep: 0.9, peak: 0.6 },   // 힘 모으기
  { key: 'skill_3', file: 'magic-circle1', keep: 1.0, peak: 0.58 },   // 마법진이 펼쳐지며 올라가는 소리
];

function fail(msg) { console.error('오류: ' + msg); process.exit(1); }

function parseArgs(argv) {
  const a = { key: null };
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i];
    const next = () => { if (i + 1 >= argv.length) fail(`${k} 뒤에 값이 필요합니다.`); return argv[++i]; };
    if (k === '--studio') a.studio = next();
    else if (k === '--key') a.key = next().split(',').map((s) => s.trim()).filter(Boolean);
    else fail(`알 수 없는 옵션: ${k}`);
  }
  a.studio = a.studio || process.env.AI_IMAGE_STUDIO_DIR || null;
  return a;
}

async function download(file) {
  const dst = path.join(CACHE, file + '.mp3');
  if (fs.existsSync(dst)) return dst;
  // 사이트가 다른 곳에서 직접 연결하는 것을 막으므로, 소리 목록 페이지에서 받는 것처럼 Referer 를 붙임
  const res = await fetch(PAGE + 'mp3/' + file + '.mp3', { headers: { Referer: PAGE, 'User-Agent': 'Mozilla/5.0' } });
  if (!res.ok) throw new Error(`${file}.mp3 를 받지 못했습니다: HTTP ${res.status}`);
  fs.mkdirSync(CACHE, { recursive: true });
  fs.writeFileSync(dst, Buffer.from(await res.arrayBuffer()));
  return dst;
}

function trim(ffmpeg, src, s) {
  const out = path.join(OUT, s.key + '.wav');
  const tmp = out.replace(/\.wav$/, '.tmp.wav');
  const fade = Math.min(0.35, Math.max(0.05, s.keep * 0.3));
  // 앞 무음 자르기 → keep 초까지 → 끝 줄이기 → 모노
  const af = `silenceremove=start_periods=1:start_threshold=-45dB,atrim=0:${s.keep},afade=t=out:st=${(s.keep - fade).toFixed(3)}:d=${fade.toFixed(3)}`;
  execFileSync(ffmpeg, ['-hide_banner', '-loglevel', 'error', '-y', '-i', src, '-af', af, '-ac', '1', '-ar', String(RATE), '-c:a', 'pcm_f32le', tmp]);
  try {
    const info = spawnSync(ffmpeg, ['-hide_banner', '-i', tmp, '-af', 'volumedetect', '-f', 'null', '-'], { encoding: 'utf8' }).stderr;
    const max = parseFloat((info.match(/max_volume: (-?[\d.]+) dB/) || [])[1] || '0');
    const gain = 20 * Math.log10(s.peak) - max;
    execFileSync(ffmpeg, ['-hide_banner', '-loglevel', 'error', '-y', '-i', tmp, '-af', `volume=${gain.toFixed(2)}dB`, '-c:a', 'pcm_s16le', out]);
  } finally {
    fs.rmSync(tmp, { force: true });
  }
}

(async () => {
  const args = parseArgs(process.argv.slice(2));
  let list = SOUNDS;
  if (args.key) list = list.filter((s) => args.key.includes(s.key) || args.key.includes(s.key.replace(/_\d+$/, '')));
  if (list.length === 0) fail('조건에 맞는 소리가 없습니다.');
  if (!args.studio) fail('--studio "<이미지 스튜디오 폴더>" 를 지정하세요. (그 폴더의 ffmpeg 을 씁니다)');
  let ffmpeg;
  try { ffmpeg = require(path.join(path.resolve(args.studio), 'node_modules', 'ffmpeg-static')); }
  catch { fail(`'${args.studio}' 에서 ffmpeg-static 을 불러오지 못했습니다.`); }
  fs.mkdirSync(OUT, { recursive: true });
  for (const s of list) {
    const src = await download(s.file);
    trim(ffmpeg, src, s);
    console.log(`SFX/Lab/${s.key}.wav  ← 효과음 라보 ${s.file}.mp3`);
  }
  console.log('\n이 폴더(SFX/Lab)는 저장소에 넣지 않습니다(효과음 라보 재배포 금지).');
})().catch((e) => fail(e.message));
