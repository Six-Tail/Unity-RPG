#!/usr/bin/env node
'use strict';
// 캐릭터 일러스트로 "움직이는 일러스트"(짧은 반복 영상)를 만듭니다.
// AI 이미지 스튜디오의 로컬 영상 모델(Wan 2.2 · 무료)로 일러스트 → 영상(3초)을 만들고,
// 앞으로 재생한 뒤 되감는 식으로 이어 붙여 끊김 없이 반복되게 한 다음
// Assets/_Project/Resources/Art/Motion/{캐릭터 id}.mp4 로 저장합니다(게임이 이 이름으로 찾음).
//
//   1) 스튜디오 폴더에서 서버를 켭니다:  node server.js   (ComfyUI + Wan 2.2 모델 필요)
//   2) 이 저장소 폴더에서:
//        node tools/art/generate-motion.js --studio "<스튜디오 폴더>" --list           # 무엇을 만들지 보기
//        node tools/art/generate-motion.js --studio "<스튜디오 폴더>" --key c_ciel     # 한 명만
//        node tools/art/generate-motion.js --studio "<스튜디오 폴더>"                  # 아직 없는 6성 전부
//
// 옵션
//   --studio <폴더>    이미지 스튜디오 폴더(ffmpeg 을 여기서 불러옴). 환경변수 AI_IMAGE_STUDIO_DIR 로도 지정
//   --server <주소>    스튜디오 서버 주소 (기본 http://127.0.0.1:3000)
//   --key <id,...>     특정 캐릭터만 (6성이 아니어도 됨)
//   --all              6성뿐 아니라 일러스트가 있는 모든 캐릭터
//   --bloom            개화 일러스트(Characters/{id}_bloom.png)로 개화 의상용 영상(Motion/{id}_bloom.mp4)을 만듦
//   --force            이미 있는 영상도 다시 생성
//   --seconds <1~5>    원본 영상 길이(기본 3 → 반복 영상은 그 두 배)
//   --quality <값>     low | medium | high (기본 high — 손·얼굴이 덜 뭉개짐)
//   --motion "<문장>"  움직임 프롬프트를 직접 지정
//   --engine <값>      live2d (기본 — 스튜디오의 'Live2D 대기 동작' 모드: 호흡 · 깜빡임 · 머리카락 흔들림, 카메라 고정)
//                      | wan (예전 방식 — 일반 영상 생성 후 이 스크립트가 되감기로 이어 붙임)
//   --list             목록만 출력

const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');
const { entries } = require('./art-manifest');

const REPO = path.resolve(__dirname, '..', '..');
const ART_ROOT = path.join(REPO, 'Assets', '_Project', 'Resources', 'Art');

// 손이 뭉개지거나 눈 색이 바뀌는 문제가 있어, 손·얼굴은 고정하고 머리카락·옷·이펙트만 움직이게 지시합니다.
// live2d 모드: 스튜디오가 대기 동작(호흡 · 깜빡임 · 흔들림)과 카메라 고정을 이미 지시하므로, 덧붙일 말만.
// (처음 시험에서 '모양 유지'만 덧붙였더니 거의 움직이지 않아서, 흔들릴 부분을 크게 · 계속 움직이라고 지시)
const LIVE2D_MOTION =
  'the long hair flows and waves continuously in a steady magical wind, loose strands and ribbons flutter, ' +
  'the cape, sleeves and skirt hem ripple and flutter, glowing particles, sparks and petals swirl and drift around the character, ' +
  'wings and halo shimmer and pulse with light, the chest rises and falls with each breath, the character blinks once';

const DEFAULT_MOTION =
  'very subtle idle animation, only the hair and the hem of the clothes sway softly in a gentle breeze, ' +
  'small magical effects and particles flicker and drift, the face stays the same with eyes open and the same eye color, ' +
  'no blinking, the arms hands and fingers stay completely still and keep their exact shape, ' +
  'the camera is completely static, the character stays in the same pose and position';

function parseArgs(argv) {
  const a = { key: null, all: false, force: false, seconds: 3, quality: 'high', list: false, motion: null, engine: 'live2d' };
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i];
    const next = () => { if (i + 1 >= argv.length) fail(`${k} 뒤에 값이 필요합니다.`); return argv[++i]; };
    if (k === '--studio') a.studio = next();
    else if (k === '--server') a.server = next();
    else if (k === '--key') a.key = next().split(',').map((s) => s.trim()).filter(Boolean);
    else if (k === '--all') a.all = true;
    else if (k === '--bloom') a.bloom = true;
    else if (k === '--force') a.force = true;
    else if (k === '--seconds') a.seconds = Math.min(5, Math.max(1, parseInt(next(), 10) || 3));
    else if (k === '--quality') a.quality = next();
    else if (k === '--motion') a.motion = next();
    else if (k === '--engine') a.engine = next();
    else if (k === '--list') a.list = true;
    else if (k === '--help' || k === '-h') { printHelp(); process.exit(0); }
    else fail(`알 수 없는 옵션: ${k}  (--help)`);
  }
  if (!['low', 'medium', 'high'].includes(a.quality)) fail('--quality 는 low | medium | high 중 하나입니다.');
  if (!['live2d', 'wan'].includes(a.engine)) fail('--engine 은 live2d | wan 중 하나입니다.');
  if (a.motion == null) a.motion = a.engine === 'live2d' ? LIVE2D_MOTION : DEFAULT_MOTION;
  a.studio = a.studio || process.env.AI_IMAGE_STUDIO_DIR || null;
  a.server = (a.server || process.env.AI_IMAGE_STUDIO_URL || 'http://127.0.0.1:3000').replace(/\/+$/, '');
  return a;
}

function printHelp() {
  const src = fs.readFileSync(__filename, 'utf8').split('\n');
  const end = src.findIndex((l) => l.startsWith('const fs'));
  console.log(src.slice(2, end).map((l) => l.replace(/^\/\/ ?/, '')).join('\n'));
}

function fail(msg) { console.error('오류: ' + msg); process.exit(1); }

const portraitOf = (e) => path.join(ART_ROOT, ...e.out.split('/'));
// 영상 이름 = 일러스트 파일 이름(개화 일러스트 c_x_bloom.png → Motion/c_x_bloom.mp4 — CharacterOutfits 의 접미사와 같음)
const motionOf = (e) => path.join(ART_ROOT, 'Motion', path.basename(e.out, path.extname(e.out)) + '.mp4');

function select(args) {
  let list = entries.filter((e) => e.group === (args.bloom ? 'bloom' : 'characters'));
  if (args.key) list = list.filter((e) => args.key.includes(e.key) || args.key.includes(e.charId));
  else if (!args.all) list = list.filter((e) => e.star === 6);
  return list;
}

async function api(server, method, route, body) {
  let res;
  try {
    res = await fetch(server + route, { method, body });
  } catch (err) {
    throw new Error(`스튜디오 서버(${server})에 연결할 수 없습니다. 스튜디오 폴더에서 'node server.js'로 먼저 켜 주세요. (${err.cause?.code || err.message})`);
  }
  const text = await res.text();
  let json = null;
  try { json = JSON.parse(text); } catch { /* 아래에서 처리 */ }
  if (!res.ok) throw new Error((json && json.error) || `HTTP ${res.status} ${text.slice(0, 200)}`);
  return json;
}

async function generateOne(args, ffmpeg, e) {
  const live2d = args.engine === 'live2d';
  const form = new FormData();
  form.append('engine', args.engine);
  // live2d: 스튜디오 화면과 같이 장면 설명 없이 움직임만(장면 설명이 앞에 오면 움직임 지시가 약해짐)
  if (!live2d) form.append('scenePrompt', `2D anime illustration, ${e.look}, cel shaded, clean line art, same art style as the start image`);
  form.append('motionPrompt', args.motion);
  form.append('aspect', '3:4');
  form.append('resolution', '1K');
  form.append('seconds', String(args.seconds));
  form.append('quality', args.quality);
  form.append('start', new Blob([fs.readFileSync(portraitOf(e))], { type: 'image/png' }), e.key + '.png');

  const out = await api(args.server, 'POST', '/api/video', form);
  if (!out || !out.image || !out.image.url) throw new Error('서버가 영상을 돌려주지 않았습니다.');
  const res = await fetch(args.server + out.image.url);
  if (!res.ok) throw new Error(`생성된 영상을 받지 못했습니다: HTTP ${res.status}`);

  const dst = motionOf(e);
  fs.mkdirSync(path.dirname(dst), { recursive: true });
  const raw = dst.replace(/\.mp4$/, '.raw.tmp.mp4');
  fs.writeFileSync(raw, Buffer.from(await res.arrayBuffer()));
  try {
    // wan: 앞으로 재생 + 되감기(첫 프레임 중복 제외) → 끝과 처음이 이어지는 반복 영상
    // live2d: 스튜디오가 이미 왕복(pingpong)으로 이어 붙여 주므로 다시 인코딩만
    const filter = live2d
      ? '[0:v]format=yuv420p[v]'
      : '[0:v]split[a][b];[b]reverse,trim=start_frame=1[r];[a][r]concat=n=2:v=1:a=0,format=yuv420p[v]';
    execFileSync(ffmpeg, [
      '-hide_banner', '-loglevel', 'error', '-y', '-i', raw,
      '-filter_complex', filter,
      // -bf 0: B프레임 없음 → 첫 프레임 시각이 0 이고 타임스탬프가 순서대로(Unity 임포트 경고 방지)
      // 색 정보(bt709)를 명시 → Windows Media Foundation 의 'Color primaries unknown' 경고 방지
      '-map', '[v]', '-c:v', 'libx264', '-crf', '20', '-preset', 'slow', '-bf', '0',
      '-color_primaries', 'bt709', '-color_trc', 'bt709', '-colorspace', 'bt709',
      '-movflags', '+faststart', '-an', dst,
    ]);
  } finally {
    fs.rmSync(raw, { force: true });
  }
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const list = select(args);
  if (list.length === 0) fail('조건에 맞는 캐릭터가 없습니다. (--list 로 확인)');

  if (args.list) {
    for (const e of list)
      console.log(`  영상 ${fs.existsSync(motionOf(e)) ? '있음' : '없음'} · 일러스트 ${fs.existsSync(portraitOf(e)) ? '있음' : '없음'}  ${e.key.padEnd(14)} ${e.label}`);
    console.log(`\n저장 위치: ${path.join(ART_ROOT, 'Motion')}`);
    return;
  }

  const noArt = list.filter((e) => !fs.existsSync(portraitOf(e)));
  for (const e of noArt) console.log(`건너뜀(일러스트 없음): ${e.key}`);
  const todo = list.filter((e) => fs.existsSync(portraitOf(e)) && (args.force || !fs.existsSync(motionOf(e))));
  if (todo.length === 0) { console.log('만들 것이 없습니다 — 모두 이미 있습니다. (다시 만들려면 --force)'); return; }

  if (!args.studio) fail('--studio "<이미지 스튜디오 폴더>" 를 지정하세요. (영상 합치기에 그 폴더의 ffmpeg 을 씁니다)');
  let ffmpeg;
  try { ffmpeg = require(path.join(path.resolve(args.studio), 'node_modules', 'ffmpeg-static')); }
  catch (err) { fail(`'${args.studio}' 에서 ffmpeg-static 을 불러오지 못했습니다. 그 폴더에서 'npm install' 을 했는지 확인하세요.`); }
  if (!ffmpeg || !fs.existsSync(ffmpeg)) fail('ffmpeg 실행 파일을 찾지 못했습니다.');

  const config = await api(args.server, 'GET', '/api/config');
  if (config.localVideo !== 'wan') fail('스튜디오에 로컬 영상 모델(Wan 2.2)이 없습니다. ComfyUI 와 모델 파일을 확인하세요.');

  const mode = args.engine === 'live2d' ? 'Live2D 대기 동작' : '일반 영상';
  console.log(`만들 영상: ${todo.length}개  (Wan 2.2 로컬 · 무료 · ${mode}, 원본 ${args.seconds}초 · 품질 ${args.quality}, 한 개에 3분쯤)`);
  const failed = [];
  for (let i = 0; i < todo.length; i++) {
    const e = todo[i];
    const t0 = Date.now();
    process.stdout.write(`[${i + 1}/${todo.length}] Motion/${path.basename(motionOf(e))}  ${e.label} … `);
    try {
      await generateOne(args, ffmpeg, e);
      console.log(`완료 (${((Date.now() - t0) / 1000).toFixed(0)}초)`);
    } catch (err) {
      console.log(`실패: ${err.message}`);
      failed.push(e.key);
    }
  }
  console.log(`\n끝: 성공 ${todo.length - failed.length}개 · 실패 ${failed.length}개` + (failed.length ? ` (${failed.join(', ')})` : ''));
  if (failed.length) process.exitCode = 1;
  console.log('Unity 로 돌아가면 새 영상이 자동으로 임포트됩니다. 손·얼굴이 어색하면 --key <id> --force 로 그 캐릭터만 다시 만드세요.');
}

main().catch((err) => { console.error('오류: ' + err.message); process.exit(1); });
