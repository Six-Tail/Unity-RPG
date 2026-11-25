#!/usr/bin/env node
'use strict';
// 화면별 BGM 제작: AI 이미지 스튜디오의 오디오 기능(음악 = ACE-Step 1.5 · 로컬 · 무료)으로 연주곡을 만들고,
// 끝과 처음이 이어지도록 다듬어(끝부분을 앞부분과 겹쳐 붙임) Assets/_Project/Resources/Audio/BGM/{키}.ogg 로 저장합니다.
// 게임은 이 키로 찾습니다(SoundManager · ScreenNavigator.BgmOf): home · menu · stage · gacha · battle · arena · event
//
//   1) 스튜디오 서버를 켭니다(오디오 탭이 있는 버전):  node server.js
//   2) 이 저장소 폴더에서:
//        node tools/audio/generate-bgm.js --studio "<스튜디오 폴더>" --list
//        node tools/audio/generate-bgm.js --studio "<스튜디오 폴더>"                 # 아직 없는 곡 전부
//        node tools/audio/generate-bgm.js --studio "<스튜디오 폴더>" --key battle --force   # 한 곡만 다시
//
// 옵션
//   --studio <폴더>    이미지 스튜디오 폴더(ffmpeg 을 여기서 불러옴). 환경변수 AI_IMAGE_STUDIO_DIR 로도 지정
//   --server <주소>    스튜디오 서버 주소 (기본 http://127.0.0.1:3000)
//   --key <키,...>     특정 곡만
//   --force            이미 있는 곡도 다시 생성
//   --reloop           모델을 다시 부르지 않고 저장해 둔 원본(docs/art-preview/audio/{키}_raw.mp3)으로 다듬기만 다시
//   --list             목록만 출력
//
// 곡 분위기를 바꾸려면 아래 TRACKS 의 tags(장르 · 분위기 · 악기, 영어)를 고친 뒤 --key <키> --force.
// 결과는 매번 달라지므로 마음에 안 들면 그 곡만 다시 만드세요.

const fs = require('fs');
const path = require('path');
const { execFileSync, spawnSync } = require('child_process');

const REPO = path.resolve(__dirname, '..', '..');
const OUT = path.join(REPO, 'Assets', '_Project', 'Resources', 'Audio', 'BGM');
const RAW = path.join(REPO, 'docs', 'art-preview', 'audio');   // 모델이 만든 원본 — 커밋하지 않음
const CROSS = 5;   // 반복 지점에서 겹치는 길이(초)

const COMMON = 'instrumental, no vocals, anime JRPG game soundtrack, clean mix, loopable background music';
const TRACKS = [
  { key: 'home', label: '홈(로비)', seconds: 110, bpm: 96, keyscale: 'C major',
    tags: 'warm cozy fantasy tavern lobby theme, acoustic guitar, flute, light strings, gentle piano, relaxed, cheerful, heartwarming' },
  { key: 'menu', label: '메뉴(육성 · 편성 · 상점 등)', seconds: 100, bpm: 88, keyscale: 'F major',
    tags: 'calm light fantasy menu music, soft piano, harp, music box, gentle pads, peaceful, relaxed, tidy' },
  { key: 'stage', label: '스테이지 선택', seconds: 110, bpm: 116, keyscale: 'D major',
    tags: 'adventurous world map theme, orchestral strings, french horns, light marching snare, hopeful, heroic journey, bright' },
  { key: 'gacha', label: '가챠', seconds: 100, bpm: 100, keyscale: 'A major',
    tags: 'magical sparkling summon theme, celesta, harp glissando, soft choir pads, shimmering bells, wonder, anticipation, mystical' },
  { key: 'battle', label: '전투(스테이지 · 던전 · 이벤트)', seconds: 110, bpm: 168, keyscale: 'E minor',
    tags: 'epic anime gacha RPG battle theme, soaring heroic string melody, powerful brass fanfare, fast symphonic rock drums and bass, ' +
      'electric guitar power chords, piano runs, determined, thrilling, cinematic orchestral' },
  { key: 'boss', label: '보스전(주간 보스 · 무한의 탑)', seconds: 120, bpm: 150, keyscale: 'C minor',
    tags: 'epic dark final boss battle theme, cosmic apocalyptic, massive symphonic orchestra with dark latin choir chants, ' +
      'pounding taiko and timpani, aggressive staccato strings, pipe organ, menacing low brass, intense, overwhelming, cinematic' },
  { key: 'arena', label: '결투장(전투 · 화면)', seconds: 110, bpm: 146, keyscale: 'D minor',
    tags: 'competitive anime PvP arena battle theme, hybrid orchestral electronic, energetic synth arpeggios, punchy drums, ' +
      'heroic brass, fast strings, confident, tense rivalry, stylish' },
  { key: 'event', label: '기간 한정 이벤트(축제)', seconds: 100, bpm: 126, keyscale: 'G major',
    tags: 'festive fantasy festival music, lively folk dance, fiddle, accordion, tambourine, hand claps, joyful celebration, playful' },
];

function fail(msg) { console.error('오류: ' + msg); process.exit(1); }

function parseArgs(argv) {
  const a = { key: null, force: false, reloop: false, list: false };
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i];
    const next = () => { if (i + 1 >= argv.length) fail(`${k} 뒤에 값이 필요합니다.`); return argv[++i]; };
    if (k === '--studio') a.studio = next();
    else if (k === '--server') a.server = next();
    else if (k === '--key') a.key = next().split(',').map((s) => s.trim()).filter(Boolean);
    else if (k === '--force') a.force = true;
    else if (k === '--reloop') a.reloop = true;
    else if (k === '--list') a.list = true;
    else fail(`알 수 없는 옵션: ${k}`);
  }
  a.studio = a.studio || process.env.AI_IMAGE_STUDIO_DIR || null;
  a.server = (a.server || process.env.AI_IMAGE_STUDIO_URL || 'http://127.0.0.1:3000').replace(/[/]+$/, '');
  return a;
}

const args = parseArgs(process.argv.slice(2));
const outOf = (t) => path.join(OUT, t.key + '.ogg');
const rawOf = (t) => path.join(RAW, t.key + '_raw.mp3');

async function requestMusic(t) {
  const res = await fetch(args.server + '/api/audio', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ kind: 'music', tags: `${t.tags}, ${COMMON}`, lyrics: '', seconds: t.seconds, bpm: t.bpm, language: 'en', keyscale: t.keyscale }),
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

function durationOf(ffmpeg, file) {
  const r = spawnSync(ffmpeg, ['-hide_banner', '-i', file], { encoding: 'utf8' });
  const m = (r.stderr || '').match(/Duration: (\d+):(\d+):([\d.]+)/);
  if (!m) throw new Error('길이를 읽지 못했습니다: ' + file);
  return (+m[1]) * 3600 + (+m[2]) * 60 + (+m[3]);
}

// 곡이 실제로 끝나는 지점(초): 1초 단위 음량이 곡 전체 중간값의 35% 아래로 내려가 다시 올라오지 않는 곳.
// 모델이 만든 곡은 끝에 잦아드는 마무리 + 무음이 붙는 경우가 많아, 그대로 반복하면 소리가 꺼졌다가 다시 시작함.
function musicEnd(ffmpeg, file) {
  const sr = 11025;
  const pcm = execFileSync(ffmpeg, ['-hide_banner', '-loglevel', 'error', '-i', file, '-f', 's16le', '-ac', '1', '-ar', String(sr), '-'], { maxBuffer: 1 << 30 });
  const n = Math.floor(pcm.length / 2), secs = Math.floor(n / sr);
  const rms = [];
  for (let t = 0; t < secs; t++) {
    let q = 0;
    for (let i = t * sr; i < (t + 1) * sr; i++) { const v = pcm.readInt16LE(i * 2) / 32768; q += v * v; }
    rms.push(Math.sqrt(q / sr));
  }
  const median = [...rms].sort((x, y) => x - y)[rms.length >> 1];
  let end = secs;
  while (end > 1 && rms[end - 1] < median * 0.35) end--;
  return end;
}

// 끝 → 처음이 이어지게: [X .. B] 뒤에 (B 부터 X초 페이드아웃 + 처음 X초 페이드인)을 겹쳐 붙임 → 반복 지점이 곡의 X초 지점과 같아짐.
// B 는 곡이 끝나는 지점보다 조금 앞(마무리로 잦아드는 부분을 피함).
function makeLoop(ffmpeg, raw, dst) {
  const X = CROSS;
  const L = Math.min(durationOf(ffmpeg, raw) - 0.25, musicEnd(ffmpeg, raw) - 3);   // 겹칠 구간의 끝(곡 끝 3초 전)
  if (L < X * 4) throw new Error('곡이 너무 짧습니다.');
  const f =
    `[0:a]atrim=start=${X}:end=${(L - X).toFixed(3)},asetpts=PTS-STARTPTS[body];` +
    `[0:a]atrim=start=${(L - X).toFixed(3)}:end=${L.toFixed(3)},asetpts=PTS-STARTPTS,afade=t=out:st=0:d=${X}:curve=qsin[tail];` +
    `[0:a]atrim=start=0:end=${X},asetpts=PTS-STARTPTS,afade=t=in:st=0:d=${X}:curve=qsin[head];` +
    `[tail][head]amix=inputs=2:normalize=0:duration=longest[x];` +
    `[body][x]concat=n=2:v=0:a=1,loudnorm=I=-17:TP=-1.5:LRA=11,aresample=44100[out]`;
  fs.mkdirSync(path.dirname(dst), { recursive: true });
  execFileSync(ffmpeg, ['-hide_banner', '-loglevel', 'error', '-y', '-i', raw, '-filter_complex', f, '-map', '[out]',
    '-c:a', 'libvorbis', '-q:a', '5', '-ac', '2', dst]);
  return durationOf(ffmpeg, dst);
}

(async () => {
  let list = TRACKS;
  if (args.key) list = list.filter((t) => args.key.includes(t.key));
  if (list.length === 0) fail('조건에 맞는 곡이 없습니다.');
  if (args.list) {
    for (const t of list) console.log(`  ${fs.existsSync(outOf(t)) ? '있음' : '없음'}  ${t.key.padEnd(8)} ${t.label}  (${t.seconds}초 · ${t.bpm}bpm · ${t.keyscale})\n           ${t.tags}`);
    console.log(`\n저장 위치: ${OUT}`);
    return;
  }
  if (!args.studio) fail('--studio "<이미지 스튜디오 폴더>" 를 지정하세요. (곡 다듬기에 그 폴더의 ffmpeg 을 씁니다)');
  let ffmpeg;
  try { ffmpeg = require(path.join(path.resolve(args.studio), 'node_modules', 'ffmpeg-static')); }
  catch { fail(`'${args.studio}' 에서 ffmpeg-static 을 불러오지 못했습니다.`); }

  const todo = list.filter((t) => args.force || args.reloop || !fs.existsSync(outOf(t)));
  if (todo.length === 0) { console.log('만들 곡이 없습니다 — 모두 이미 있습니다. (다시 만들려면 --force)'); return; }
  fs.mkdirSync(RAW, { recursive: true });
  const failed = [];
  for (let i = 0; i < todo.length; i++) {
    const t = todo[i];
    const t0 = Date.now();
    process.stdout.write(`[${i + 1}/${todo.length}] BGM/${t.key}.ogg  ${t.label} … `);
    try {
      if (!args.reloop || !fs.existsSync(rawOf(t))) fs.writeFileSync(rawOf(t), await requestMusic(t));
      const len = makeLoop(ffmpeg, rawOf(t), outOf(t));
      console.log(`완료 (${((Date.now() - t0) / 1000).toFixed(0)}초 · 반복 길이 ${len.toFixed(1)}초)`);
    } catch (err) {
      console.log(`실패: ${err.message}`);
      failed.push(t.key);
    }
  }
  console.log(`\n끝: 성공 ${todo.length - failed.length}곡 · 실패 ${failed.length}곡` + (failed.length ? ` (${failed.join(', ')})` : ''));
  if (failed.length) process.exitCode = 1;
})().catch((e) => fail(e.message));
