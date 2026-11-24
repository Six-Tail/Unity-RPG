#!/usr/bin/env node
'use strict';
// AI 이미지 스튜디오(ai-image-studio)로 게임 이미지를 일괄 생성해
// Assets/_Project/Resources/Art/ 아래에 게임이 찾는 파일명·크기로 저장합니다.
//
//   1) 이미지 스튜디오를 켭니다:   (스튜디오 폴더에서)  npm start
//   2) 이 저장소 폴더에서:
//        node tools/art/generate-art.js --studio "<스튜디오 폴더>" --list            # 무엇을 만들지 보기(생성 안 함)
//        node tools/art/generate-art.js --studio "<스튜디오 폴더>" --only items     # 한 종류만
//        node tools/art/generate-art.js --studio "<스튜디오 폴더>"                  # 아직 없는 것 전부
//
// 옵션
//   --studio <폴더>     이미지 스튜디오 폴더(이미지 처리용 sharp 를 여기서 불러옴). 환경변수 AI_IMAGE_STUDIO_DIR 로도 지정
//   --server <주소>     스튜디오 서버 주소 (기본 http://127.0.0.1:3000)
//   --only <종류,...>   characters,enemies,items,equipment,status,gacha,title,backgrounds,banners 중 고르기
//   --key <키,...>      특정 항목만 (예: --key c_seraphine,home)
//   --force             이미 있는 파일도 다시 생성
//   --limit <N>         이번 실행에서 최대 N장
//   --quality <값>      low | medium | high | auto (기본 medium)
//   --model <모델>      실행하는 동안만 이 모델로 바꿈(끝나면 원래 모델로 복구). 예: local:flux1-schnell-fp8.safetensors
//   --yes               유료(OpenAI) 모델로 생성하는 것에 동의 — 로컬 GPU 모델은 필요 없음
//   --list              목록만 출력
//   --thumbs            이미 있는 캐릭터 일러스트에서 썸네일만 다시 자름(생성 안 함 · 서버 필요 없음)
//   --write-prompts     docs/art-prompts.md 를 다시 씀(서버 필요 없음)

const fs = require('fs');
const path = require('path');
const { entries, AVOID, GROUPS } = require('./art-manifest');

const REPO = path.resolve(__dirname, '..', '..');
const ART_ROOT = path.join(REPO, 'Assets', '_Project', 'Resources', 'Art');
const LOCAL_PREFIX = 'local:';

// ---------------------------------------------------------------- 인자

function parseArgs(argv) {
  const a = { only: null, key: null, force: false, limit: Infinity, quality: 'medium', yes: false, list: false, writePrompts: false, thumbs: false };
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i];
    const next = () => {
      if (i + 1 >= argv.length) fail(`${k} 뒤에 값이 필요합니다.`);
      return argv[++i];
    };
    if (k === '--studio') a.studio = next();
    else if (k === '--server') a.server = next();
    else if (k === '--only') a.only = next().split(',').map((s) => s.trim()).filter(Boolean);
    else if (k === '--key') a.key = next().split(',').map((s) => s.trim()).filter(Boolean);
    else if (k === '--force') a.force = true;
    else if (k === '--limit') a.limit = Math.max(1, parseInt(next(), 10) || 1);
    else if (k === '--quality') a.quality = next();
    else if (k === '--model') a.model = next();
    else if (k === '--yes') a.yes = true;
    else if (k === '--list') a.list = true;
    else if (k === '--thumbs') a.thumbs = true;
    else if (k === '--write-prompts') a.writePrompts = true;
    else if (k === '--help' || k === '-h') { printHelp(); process.exit(0); }
    else fail(`알 수 없는 옵션: ${k}  (--help)`);
  }
  if (a.only) for (const g of a.only) if (!GROUPS.includes(g)) fail(`--only 값이 잘못됐습니다: ${g}  (가능: ${GROUPS.join(', ')})`);
  if (!['low', 'medium', 'high', 'auto'].includes(a.quality)) fail('--quality 는 low | medium | high | auto 중 하나입니다.');
  a.studio = a.studio || process.env.AI_IMAGE_STUDIO_DIR || null;
  a.server = (a.server || process.env.AI_IMAGE_STUDIO_URL || 'http://127.0.0.1:3000').replace(/\/+$/, '');
  return a;
}

function printHelp() {
  const src = fs.readFileSync(__filename, 'utf8').split('\n');
  const end = src.findIndex((l) => l.startsWith('const fs'));
  console.log(src.slice(2, end).map((l) => l.replace(/^\/\/ ?/, '')).join('\n'));
}

function fail(msg) {
  console.error('오류: ' + msg);
  process.exit(1);
}

// ---------------------------------------------------------------- 목록

function select(args) {
  let list = entries;
  if (args.only) list = list.filter((e) => args.only.includes(e.group));
  if (args.key) list = list.filter((e) => args.key.includes(e.key));
  return list;
}

const outPath = (rel) => path.join(ART_ROOT, ...rel.split('/'));
const exists = (e) => fs.existsSync(outPath(e.out));

function printList(list) {
  let g = '';
  for (const e of list) {
    if (e.group !== g) { g = e.group; console.log(`\n[${g}]`); }
    console.log(`  ${exists(e) ? '있음' : '없음'}  ${e.out.padEnd(44)} ${e.width}x${e.height}  ${e.label}`);
  }
  const have = list.filter(exists).length;
  console.log(`\n전체 ${list.length}장 · 있음 ${have} · 없음 ${list.length - have}`);
  console.log(`저장 위치: ${ART_ROOT}`);
}

function writePrompts() {
  const L = [];
  L.push('# 이미지 프롬프트 목록', '');
  L.push('> 이 문서는 `tools/art/art-manifest.js`에서 자동으로 만듭니다. 직접 고치지 말고 그 파일을 고친 뒤');
  L.push('> `node tools/art/generate-art.js --write-prompts` 를 실행하세요. 사용법은 [art.md](art.md).', '');
  L.push('파일은 `Assets/_Project/Resources/Art/` 아래 **파일** 칸의 경로·이름으로 넣으면 게임에 자동으로 나옵니다.');
  L.push('직접 만들 때는 아래 프롬프트를 이미지 도구에 붙여 넣고, 표의 화면비·크기로 저장하세요.', '');
  L.push(`OpenAI(gpt-image) 모델에서는 프롬프트 끝에 \`${AVOID}\` 를 덧붙이면 글자·워터마크가 줄어듭니다.`);
  L.push('로컬 Flux 모델에서는 붙이지 마세요 — 그 말 때문에 오히려 글자가 그려집니다. (일괄 생성 스크립트는 모델에 맞춰 자동 처리)', '');
  const title = {
    characters: '캐릭터 일러스트 (3:4)', enemies: '적 일러스트 (1:1)', items: '재료 아이콘 (1:1)',
    equipment: '장비 부위 아이콘 (1:1)', status: '전투 상태 아이콘 (1:1)', gacha: '가챠 연출 소품', title: '타이틀 로고 · 키 아트', backgrounds: '화면 배경 (16:9)', banners: '가챠 배너 그림 (16:9)',
  };
  for (const g of GROUPS) {
    const list = entries.filter((e) => e.group === g);
    L.push(`## ${title[g]} — ${list.length}장`, '');
    if (g === 'characters') L.push('얼굴 썸네일(`Thumbs/{id}.png`, 256×256)은 일괄 생성 스크립트가 일러스트 윗부분을 잘라 자동으로 만듭니다.', '');
    if (g === 'banners') L.push('픽업 배너는 해당 캐릭터 일러스트를 **참고 이미지**로 넣어 같은 인물로 그립니다(일러스트를 먼저 만드세요).', '');
    for (const e of list) {
      L.push(`### ${e.label}`, '');
      L.push(`- 파일: \`${e.out}\` · ${e.width}×${e.height} · 화면비 ${e.aspect}${e.ref ? ` · 참고 이미지 \`${e.ref}\`` : ''}`);
      L.push('', '```', e.prompt, '```', '');
    }
  }
  const file = path.join(REPO, 'docs', 'art-prompts.md');
  fs.writeFileSync(file, L.join('\n'), 'utf8');
  console.log(`썼습니다: ${file}  (${entries.length}장)`);
}

// ---------------------------------------------------------------- 스튜디오 서버

async function api(server, method, route, body) {
  let res;
  try {
    const init = { method };
    if (body instanceof FormData) init.body = body;
    else if (body) { init.headers = { 'Content-Type': 'application/json' }; init.body = JSON.stringify(body); }
    res = await fetch(server + route, init);
  } catch (err) {
    throw new Error(`스튜디오 서버(${server})에 연결할 수 없습니다. 스튜디오 폴더에서 'npm start'로 먼저 켜 주세요. (${err.cause?.code || err.message})`);
  }
  const text = await res.text();
  let json = null;
  try { json = JSON.parse(text); } catch { /* JSON 이 아니면 아래에서 처리 */ }
  if (!res.ok) throw new Error((json && json.error) || `HTTP ${res.status} ${text.slice(0, 200)}`);
  return json;
}

async function download(server, url) {
  const res = await fetch(server + url);
  if (!res.ok) throw new Error(`생성된 이미지를 받지 못했습니다: HTTP ${res.status}`);
  return Buffer.from(await res.arrayBuffer());
}

async function generateOne(args, sharp, e, local, transparent) {
  // 아이콘·적: OpenAI 모델은 투명 배경으로 요청(transparent). 로컬 모델이나 모델이 거부한 재시도에서는 어두운 단색 배경.
  const clear = transparent && !!e.promptTransparent;
  const base = clear ? e.promptTransparent : e.prompt;
  const fields = {
    // "no text" 류는 OpenAI 모델에만 붙임(Flux 는 그 말 때문에 오히려 글자를 그림)
    prompt: local ? base : `${base}, ${AVOID}`, negative: e.negative || '', aspect: e.aspect, resolution: e.resolution,
    quality: args.quality, format: 'png', n: 1,
  };
  if (clear) fields.background = 'transparent';
  let body = fields;
  const refFile = e.ref ? outPath(e.ref) : null;
  if (refFile && fs.existsSync(refFile)) {
    // 참고 이미지 첨부(같은 캐릭터로 그리기) → multipart
    body = new FormData();
    for (const [k, v] of Object.entries(fields)) body.append(k, String(v));
    body.append('reference', new Blob([fs.readFileSync(refFile)], { type: 'image/png' }), path.basename(refFile));
  } else if (refFile) {
    console.log(`     (참고 이미지 ${e.ref} 가 아직 없어 글만으로 그립니다)`);
  }

  const result = await api(args.server, 'POST', '/api/generate', body);
  const img = result && result.images && result.images[0];
  if (!img || !img.url) throw new Error('서버가 이미지를 돌려주지 않았습니다.');
  const raw = await download(args.server, img.url);

  // 게임용 크기로 맞추기(가운데 기준으로 잘라 채움)
  const dst = outPath(e.out);
  fs.mkdirSync(path.dirname(dst), { recursive: true });
  let pipe = sharp(raw).resize(e.width, e.height, { fit: 'cover', position: 'centre' });
  pipe = e.format === 'jpg' ? pipe.jpeg({ quality: 88, mozjpeg: true }) : pipe.png({ compressionLevel: 9 });
  await pipe.toFile(dst);

  if (e.thumb) await makeThumb(sharp, e);
}

// 캐릭터 썸네일: 일러스트에서 얼굴 쪽(가운데 위)을 정사각형으로 잘라 256×256.
// 프롬프트가 "머리 위 여백 + 얼굴은 위쪽 1/3"이라 얼굴이 세로 15~40% 사이에 옵니다.
async function makeThumb(sharp, e) {
  const src = outPath(e.out);
  const side = Math.round(e.width * 0.56);
  const left = Math.round((e.width - side) / 2);
  const top = Math.round(e.height * 0.08);
  const t = outPath(e.thumb);
  fs.mkdirSync(path.dirname(t), { recursive: true });
  const buf = await sharp(src).extract({ left, top, width: side, height: side }).resize(256, 256).png({ compressionLevel: 9 }).toBuffer();
  fs.writeFileSync(t, buf);
}

// ---------------------------------------------------------------- 실행

function loadSharp(args) {
  if (!args.studio) fail('--studio "<이미지 스튜디오 폴더>" 를 지정하세요. (이미지 크기 조정에 그 폴더의 sharp 를 씁니다)');
  try {
    return require(path.join(path.resolve(args.studio), 'node_modules', 'sharp'));
  } catch (err) {
    return fail(`'${args.studio}' 에서 sharp 를 불러오지 못했습니다. 그 폴더에서 'npm install' 을 했는지 확인하세요. (${err.code || err.message})`);
  }
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  if (args.writePrompts) return writePrompts();

  const list = select(args);
  if (list.length === 0) fail('조건에 맞는 항목이 없습니다. (--list 로 확인)');
  if (args.list) return printList(list);

  if (args.thumbs) {
    const sharp = loadSharp(args);
    const chars = list.filter((e) => e.thumb && exists(e));
    for (const e of chars) await makeThumb(sharp, e);
    console.log(`썸네일 ${chars.length}장을 다시 만들었습니다.`);
    return;
  }

  let todo = args.force ? list : list.filter((e) => !exists(e));
  // 배너는 캐릭터 일러스트를 참고하므로 맨 뒤로
  todo = [...todo.filter((e) => !e.ref), ...todo.filter((e) => e.ref)].slice(0, args.limit);
  if (todo.length === 0) {
    console.log(`만들 것이 없습니다 — ${list.length}장 모두 이미 있습니다. (다시 만들려면 --force)`);
    return;
  }

  const sharp = loadSharp(args);

  const config = await api(args.server, 'GET', '/api/config');
  const original = config.model;
  let model = original;
  let switched = false;

  // 어떤 이유로 끝나든(오류·Ctrl+C) 바꿔 둔 모델은 원래대로
  const restore = async () => {
    if (!switched) return;
    switched = false;
    try {
      await api(args.server, 'POST', '/api/model', { model: original });
      console.log(`모델을 원래대로 되돌렸습니다: ${original}`);
    } catch (err) {
      console.error(`!! 모델을 원래대로 되돌리지 못했습니다. 스튜디오에서 직접 '${original}' 로 바꿔 주세요. (${err.message})`);
    }
  };
  process.on('SIGINT', async () => { console.log('\n중단합니다…'); await restore(); process.exit(130); });

  if (args.model && args.model !== original) {
    await api(args.server, 'POST', '/api/model', { model: args.model });
    model = args.model;
    switched = true;
  }

  const local = model.startsWith(LOCAL_PREFIX);
  console.log(`모델: ${model}  (${local ? '로컬 GPU · 무료' : 'OpenAI · 사용량만큼 과금'})`);
  console.log(`만들 이미지: ${todo.length}장  (품질 ${args.quality})  →  ${ART_ROOT}`);
  if (!local && !args.yes) {
    await restore();
    fail(`유료 모델입니다. ${todo.length}장을 생성하면 OpenAI 요금이 나옵니다. 계속하려면 --yes 를 붙이거나, ` +
         '로컬 GPU 모델을 쓰려면 --model "local:<체크포인트 파일명>" 을 지정하세요.');
  }

  const failed = [];
  const started = Date.now();
  try {
    for (let i = 0; i < todo.length; i++) {
      const e = todo[i];
      const t0 = Date.now();
      process.stdout.write(`[${i + 1}/${todo.length}] ${e.out}  ${e.label} … `);
      let ok = false, lastErr = null;
      for (let attempt = 1; attempt <= 2 && !ok; attempt++) {
        // 첫 시도는 투명 배경(지원 모델만), 실패하면 투명 없이 한 번 더
        try { await generateOne(args, sharp, e, local, !local && attempt === 1); ok = true; }
        catch (err) { lastErr = err; if (attempt === 1) process.stdout.write('재시도 … '); }
      }
      if (ok) console.log(`완료 (${((Date.now() - t0) / 1000).toFixed(1)}초)`);
      else { console.log(`실패: ${lastErr.message}`); failed.push({ e, error: lastErr.message }); }
    }
  } finally {
    await restore();
  }

  const mins = ((Date.now() - started) / 60000).toFixed(1);
  console.log(`\n끝: 성공 ${todo.length - failed.length}장 · 실패 ${failed.length}장 · ${mins}분`);
  if (failed.length) {
    for (const f of failed) console.log(`  실패  ${f.e.out}  — ${f.error}`);
    console.log('같은 명령을 다시 실행하면 실패한 것만 다시 만듭니다.');
    process.exitCode = 1;
  }
  console.log('Unity 로 돌아가면 새 이미지가 자동으로 임포트됩니다. (스튜디오 갤러리의 원본은 그대로 남아 있습니다)');
}

main().catch((err) => {
  console.error('오류: ' + err.message);
  process.exit(1);
});
