#!/usr/bin/env node
'use strict';
// 효과음 제작(AI): 이미지 스튜디오의 오디오 기능(효과음 = Stable Audio Open 1.0 · 로컬 · 무료)으로 소리를 만들고,
// 게임에 쓰기 좋게 다듬어 Assets/_Project/Resources/Audio/SFX/{이름}.wav 로 저장합니다(Sfx 열거형의 소문자 이름).
//
// 다듬기: 모델이 만든 소리는 앞에 무음이 붙거나 길게 늘어지므로
//   ① 소리가 시작되는 곳을 찾아 앞을 자르고 ② 정해 둔 길이까지만 남기고(끝이 일찍 조용해지면 거기까지) ③ 끝을 부드럽게 줄이고 ④ 음량을 맞춥니다.
//
//   node tools/audio/generate-sfx-ai.js --studio "<스튜디오 폴더>" --list
//   node tools/audio/generate-sfx-ai.js --studio "<스튜디오 폴더>" --force           # 전부 다시 제작
//   node tools/audio/generate-sfx-ai.js --studio "<스튜디오 폴더>" --key click,hit --force
//   node tools/audio/generate-sfx-ai.js --studio "<스튜디오 폴더>" --key hit --retrim  # 모델을 다시 부르지 않고 다듬기만
//
// 옵션
//   --studio <폴더>   이미지 스튜디오 폴더(ffmpeg 을 여기서 불러옴). 환경변수 AI_IMAGE_STUDIO_DIR 로도 지정
//   --server <주소>   스튜디오 서버 주소 (기본 http://127.0.0.1:3000)
//   --key <이름,...>  특정 소리만
//   --force           이미 있는 파일도 다시 제작(기본은 없는 것만)
//   --retrim          저장해 둔 원본(docs/art-preview/audio/sfx/{이름}_raw.mp3)으로 다듬기만 다시
//   --quality <값>    low | medium | high (기본 medium — 소리마다 quality 를 정해 두면 그것을 씀)
//   --list            목록만 출력
//
// 결과는 매번 달라집니다. 마음에 안 드는 소리는 --key <이름> --force 로 그것만 다시 만드세요.
// 합성 버전(외부 모델 없이 파형 계산)으로 되돌리려면: node tools/audio/generate-sfx.js [이름...]

const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const REPO = path.resolve(__dirname, '..', '..');
const OUT = path.join(REPO, 'Assets', '_Project', 'Resources', 'Audio', 'SFX');
const RAW = path.join(REPO, 'docs', 'art-preview', 'audio', 'sfx');
const RATE = 44100;

// gen = 모델에 요청할 길이(초, 최소 1), keep = 남길 최대 길이(초), peak = 최대 음량(0 ~ 1)
// attack = (타격음용) 소리가 가장 세지는 순간의 이만큼 앞에서 시작 — 앞의 긴 휘두르는 소리를 줄여 누르는 즉시 '탁' 하고 나게
const SOUNDS = [
  { key: 'click', gen: 1, keep: 0.12, peak: 0.6, prompt: 'single short soft UI button click, clean tick, mobile game interface sound effect' },
  { key: 'confirm', gen: 2, keep: 0.6, peak: 0.75, prompt: 'positive UI confirmation chime, two bright ascending bell notes, game interface sound effect' },
  { key: 'cancel', gen: 2, keep: 0.45, peak: 0.65, prompt: 'soft UI cancel sound, two short descending muted notes, game interface sound effect' },
  { key: 'reward', gen: 3, keep: 1.6, peak: 0.85, prompt: 'magical reward jingle, sparkling ascending chimes with glitter shimmer, mobile game item obtained sound effect' },
  { key: 'coin', gen: 2, keep: 0.7, peak: 0.75, prompt: 'gold coins clinking, bright metallic coin purchase sound, game sound effect' },
  { key: 'levelup', gen: 3, keep: 2.0, peak: 0.85, prompt: 'level up fanfare, bright ascending arpeggio with sparkling shimmer, RPG game sound effect' },
  { key: 'error', gen: 1, keep: 0.4, peak: 0.6, prompt: 'short low error buzz, negative UI beep, game interface sound effect' },
  { key: 'gachasummon', gen: 3, keep: 2.2, peak: 0.85, prompt: 'magical summoning whoosh, rising shimmering energy build up, fantasy game sound effect' },
  { key: 'gacharare', gen: 4, keep: 3.4, peak: 0.9, prompt: 'epic legendary reveal, bright heavenly choir chord with sparkling chimes and a deep impact, fantasy game sound effect' },
  // 전투 소리(hit · crit · heal · skill · ultimate · break · defeated 와 변형 _2 · _3)는 AI로 만들면 뭉개지고 싸구려 같아서
  // 실제 녹음(Kenney CC0 팩)을 겹쳐 만듭니다 → tools/audio/build-sfx-kenney.js (2026-11-25)
  { key: 'victory', gen: 4, keep: 3.6, peak: 0.9, prompt: 'short triumphant victory fanfare, bright brass and orchestra, RPG battle win jingle' },
  { key: 'defeat', gen: 4, keep: 3.2, peak: 0.75, prompt: 'short sad defeat jingle, slow descending minor melody, game over sound' },
];

function fail(msg) { console.error('오류: ' + msg); process.exit(1); }

function parseArgs(argv) {
  const a = { key: null, force: false, retrim: false, list: false, quality: 'medium' };
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i];
    const next = () => { if (i + 1 >= argv.length) fail(`${k} 뒤에 값이 필요합니다.`); return argv[++i]; };
    if (k === '--studio') a.studio = next();
    else if (k === '--server') a.server = next();
    else if (k === '--key') a.key = next().split(',').map((s) => s.trim()).filter(Boolean);
    else if (k === '--force') a.force = true;
    else if (k === '--retrim') a.retrim = true;
    else if (k === '--quality') a.quality = next();
    else if (k === '--list') a.list = true;
    else fail(`알 수 없는 옵션: ${k}`);
  }
  a.studio = a.studio || process.env.AI_IMAGE_STUDIO_DIR || null;
  a.server = (a.server || process.env.AI_IMAGE_STUDIO_URL || 'http://127.0.0.1:3000').replace(/[/]+$/, '');
  return a;
}

const args = parseArgs(process.argv.slice(2));
const outOf = (s) => path.join(OUT, s.key + '.wav');
const rawOf = (s) => path.join(RAW, s.key + '_raw.mp3');

async function requestSfx(s) {
  const res = await fetch(args.server + '/api/audio', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ kind: 'sfx', prompt: s.prompt, seconds: s.gen, quality: s.quality || args.quality }),
  });
  const text = await res.text();
  let json = null; try { json = JSON.parse(text); } catch { /* 본문이 JSON 이 아님 */ }
  if (!res.ok) throw new Error((json && json.error) || `HTTP ${res.status} — 스튜디오 서버에 오디오 기능이 없으면 서버를 다시 켜세요. ${text.slice(0, 120)}`);
  const url = json && json.audio && json.audio.url;
  if (!url) throw new Error('서버가 오디오를 돌려주지 않았습니다.');
  const file = await fetch(args.server + url);
  if (!file.ok) throw new Error(`생성된 오디오를 받지 못했습니다: HTTP ${file.status}`);
  return Buffer.from(await file.arrayBuffer());
}

function writeWav(file, x) {
  const data = Buffer.alloc(x.length * 2);
  for (let i = 0; i < x.length; i++) data.writeInt16LE(Math.max(-32767, Math.min(32767, Math.round(x[i] * 32767))), i * 2);
  const h = Buffer.alloc(44);
  h.write('RIFF', 0); h.writeUInt32LE(36 + data.length, 4); h.write('WAVE', 8);
  h.write('fmt ', 12); h.writeUInt32LE(16, 16); h.writeUInt16LE(1, 20); h.writeUInt16LE(1, 22);
  h.writeUInt32LE(RATE, 24); h.writeUInt32LE(RATE * 2, 28); h.writeUInt16LE(2, 32); h.writeUInt16LE(16, 34);
  h.write('data', 36); h.writeUInt32LE(data.length, 40);
  fs.writeFileSync(file, Buffer.concat([h, data]));
}

// 원본 → 다듬은 WAV. 결과 정보(길이 · 자른 앞부분)를 돌려줌
function trim(ffmpeg, s) {
  const pcm = execFileSync(ffmpeg, ['-hide_banner', '-loglevel', 'error', '-i', rawOf(s), '-f', 's16le', '-ac', '1', '-ar', String(RATE), '-'], { maxBuffer: 1 << 28 });
  const n = Math.floor(pcm.length / 2);
  const x = new Float32Array(n);
  let pk = 0;
  for (let i = 0; i < n; i++) { x[i] = pcm.readInt16LE(i * 2) / 32768; pk = Math.max(pk, Math.abs(x[i])); }
  if (pk < 0.01) throw new Error('소리가 거의 없는 결과입니다(다시 만들어 보세요).');

  // ① 시작점: 5ms 창의 음량이 처음으로 최대치의 8%를 넘는 곳(조금 앞에서 시작)
  const win = Math.floor(RATE * 0.005);
  const env = (i) => { let m = 0; for (let k = i; k < Math.min(n, i + win); k++) m = Math.max(m, Math.abs(x[k])); return m; };
  let start = 0;
  for (let i = 0; i + win < n; i += win) if (env(i) > pk * 0.08) { start = Math.max(0, i - Math.floor(RATE * 0.004)); break; }
  if (s.attack) {
    // 타격음: 음량이 최대치의 70%에 처음 닿는 순간의 attack 초 앞에서 시작
    for (let i = start; i + win < n; i += win)
      if (env(i) > pk * 0.7) { start = Math.max(start, i - Math.floor(RATE * s.attack)); break; }
  }

  // ② 길이: keep 초까지. 그 안에서 소리가 일찍 조용해지면(최대치의 3% 아래가 60ms 이어지면) 거기까지
  let len = Math.min(n - start, Math.floor(s.keep * RATE));
  const minLen = Math.floor(Math.min(s.keep, 0.08) * RATE), hold = Math.floor(RATE * 0.06);
  let quiet = 0;
  for (let i = start + minLen; i < start + len; i += win) {
    if (env(i) < pk * 0.03) { quiet += win; if (quiet >= hold) { len = i - start; break; } } else quiet = 0;
  }
  const y = x.slice(start, start + len);

  // ③ 끝을 부드럽게(전체의 25%, 최소 15ms · 최대 0.35초) + 맨 앞 2ms 올림(딱 소리 방지)
  const fade = Math.min(y.length, Math.max(Math.floor(RATE * 0.015), Math.min(Math.floor(RATE * 0.35), Math.floor(y.length * 0.25))));
  for (let i = 0; i < fade; i++) { const k = i / fade; y[y.length - 1 - i] *= k * k; }
  const rise = Math.min(y.length, Math.floor(RATE * 0.002));
  for (let i = 0; i < rise; i++) y[i] *= i / rise;

  // ④ 음량: 최대치를 peak 에 맞춤
  let p2 = 0;
  for (const v of y) p2 = Math.max(p2, Math.abs(v));
  const g = p2 > 0 ? s.peak / p2 : 1;
  for (let i = 0; i < y.length; i++) y[i] *= g;

  fs.mkdirSync(OUT, { recursive: true });
  writeWav(outOf(s), y);
  return { seconds: y.length / RATE, cut: start / RATE };
}

(async () => {
  let list = SOUNDS;
  if (args.key) list = list.filter((s) => args.key.includes(s.key) || args.key.includes(s.key.replace(/_\d+$/, '')));
  if (list.length === 0) fail('조건에 맞는 소리가 없습니다.');
  if (args.list) {
    for (const s of list) console.log(`  ${fs.existsSync(rawOf(s)) ? 'AI 원본 있음' : 'AI 원본 없음'}  ${s.key.padEnd(12)} 최대 ${s.keep}초  ${s.prompt}`);
    return;
  }
  if (!args.studio) fail('--studio "<이미지 스튜디오 폴더>" 를 지정하세요. (소리 다듬기에 그 폴더의 ffmpeg 을 씁니다)');
  let ffmpeg;
  try { ffmpeg = require(path.join(path.resolve(args.studio), 'node_modules', 'ffmpeg-static')); }
  catch { fail(`'${args.studio}' 에서 ffmpeg-static 을 불러오지 못했습니다.`); }

  // 기본: AI 원본이 아직 없는 소리만. --force 면 전부 새로, --retrim 이면 원본으로 다듬기만
  const todo = list.filter((s) => args.force || args.retrim || !fs.existsSync(rawOf(s)));
  if (todo.length === 0) { console.log('만들 소리가 없습니다 — 모두 이미 있습니다. (다시 만들려면 --force)'); return; }
  fs.mkdirSync(RAW, { recursive: true });
  const failed = [];
  for (let i = 0; i < todo.length; i++) {
    const s = todo[i];
    const t0 = Date.now();
    process.stdout.write(`[${i + 1}/${todo.length}] SFX/${s.key}.wav … `);
    try {
      if (!args.retrim || !fs.existsSync(rawOf(s))) fs.writeFileSync(rawOf(s), await requestSfx(s));
      const r = trim(ffmpeg, s);
      console.log(`완료 (${((Date.now() - t0) / 1000).toFixed(0)}초 · 길이 ${r.seconds.toFixed(2)}초 · 앞 ${r.cut.toFixed(2)}초 잘라냄)`);
    } catch (err) {
      console.log(`실패: ${err.message}`);
      failed.push(s.key);
    }
  }
  console.log(`\n끝: 성공 ${todo.length - failed.length}개 · 실패 ${failed.length}개` + (failed.length ? ` (${failed.join(', ')})` : ''));
  if (failed.length) process.exitCode = 1;
})().catch((e) => fail(e.message));
