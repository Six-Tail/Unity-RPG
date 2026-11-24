'use strict';
// 크로마키(단색 배경 → 투명) 공용 처리 — generate-cutouts.js(배경 없는 일러스트) · generate-motion.js --cutout(배경 없는 반복 영상)

const KEYS = {
  green: { rgb: { r: 0, g: 255, b: 0 }, name: 'pure green (#00FF00)', other: 'green' },
  magenta: { rgb: { r: 255, g: 0, b: 255 }, name: 'pure magenta (#FF00FF)', other: 'magenta or pink' },
};

// 키 색이 얼마나 "튀는지": 초록 = g - max(r,b), 자홍 = min(r,b) - g
const dominance = (key, r, g, b) => (key === 'green' ? g - Math.max(r, b) : Math.min(r, b) - g);

// 일러스트에 초록 · 자홍 중 덜 쓰인 쪽을 배경색으로(초록 머리 캐릭터에 초록 배경을 쓰면 머리가 같이 지워짐)
async function pickChroma(sharp, src) {
  const { data, info } = await sharp(src).resize(192).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  const used = { green: 0, magenta: 0 };
  for (let i = 0; i < data.length; i += info.channels) {
    if (dominance('green', data[i], data[i + 1], data[i + 2]) > 28) used.green++;
    if (dominance('magenta', data[i], data[i + 1], data[i + 2]) > 28) used.magenta++;
  }
  return used.green <= used.magenta ? 'green' : 'magenta';
}

// 단색 배경 → 투명. 배경색에 가까울수록 투명하게, 가장자리는 배경색을 빼서 색 번짐을 없앰
function keyOut(data, w, h, key, fixedBg) {
  const out = Buffer.alloc(w * h * 4);
  // 실제 배경색은 모서리에서 잰다(모델이 #00FF00 을 정확히 칠하지는 않음)
  const sample = [];
  for (const [cx, cy] of [[4, 4], [w - 28, 4]])
    for (let y = cy; y < cy + 24; y++) for (let x = cx; x < cx + 24; x++) { const i = (y * w + x) * 3; sample.push([data[i], data[i + 1], data[i + 2]]); }
  const med = (c) => sample.map((s) => s[c]).sort((p, q) => p - q)[sample.length >> 1];
  const bg = fixedBg || [med(0), med(1), med(2)];   // 영상은 첫 프레임에서 잰 색을 계속 씀(프레임마다 흔들리지 않게)
  const bgD = dominance(key, bg[0], bg[1], bg[2]);
  if (bgD < 90) throw new Error(`배경이 ${key} 단색으로 그려지지 않았습니다 (위쪽 모서리 색 ${bg.join(',')}).`);
  const lo = bgD * 0.14, hi = bgD * 0.82;
  const cl = (v) => (v < 0 ? 0 : v > 255 ? 255 : Math.round(v));
  const n = w * h;
  // 1차: 배경색이 뚜렷한 곳만 투명(인물 안쪽의 연두 · 분홍 같은 비슷한 색은 지키려고 문턱 lo 를 둠)
  const alpha = new Float32Array(n), dom = new Float32Array(n);
  for (let p = 0, i = 0; p < n; p++, i += 3) {
    const d = dominance(key, data[i], data[i + 1], data[i + 2]);
    dom[p] = d;
    alpha[p] = d <= lo ? 1 : d >= hi ? 0 : 1 - (d - lo) / (hi - lo);
  }
  // 가장자리 띠(투명한 곳에서 R픽셀 이내): 여기서는 키 색 기운이 조금만 있어도 배경이 섞인 것으로 보고 더 과감하게 지움
  const R = 5;
  const tmp = new Float32Array(n), near = new Float32Array(n);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    let m = 1; for (let k = Math.max(0, x - R); k <= Math.min(w - 1, x + R); k++) m = Math.min(m, alpha[y * w + k]);
    tmp[y * w + x] = m;
  }
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    let m = 1; for (let k = Math.max(0, y - R); k <= Math.min(h - 1, y + R); k++) m = Math.min(m, tmp[k * w + x]);
    near[y * w + x] = m;
  }
  for (let p = 0, i = 0, o = 0; p < n; p++, i += 3, o += 4) {
    let r = data[i], g = data[i + 1], b = data[i + 2];
    const d = dom[p];
    let a = alpha[p];
    const edge = near[p] < 0.05;
    if (edge && d > 0) a = Math.min(a, Math.max(0, 1 - d / hi));
    if (a > 0 && a < 1) {            // 반투명: 섞여 든 배경색을 빼서 원래 색 복원
      r = (r - (1 - a) * bg[0]) / a; g = (g - (1 - a) * bg[1]) / a; b = (b - (1 - a) * bg[2]) / a;
    }
    if (a > 0 && d > 0) {            // 남은 번짐 억제: 키 색 성분이 다른 성분보다 튀지 않게(가장자리는 더 세게)
      if (key === 'green') g = Math.min(g, edge ? (r + b) / 2 : Math.max(r, b));
      else { const cut = Math.min(r, b) - g; if (cut > 0) { r -= cut; b -= cut; } }
    }
    out[o] = cl(r); out[o + 1] = cl(g); out[o + 2] = cl(b); out[o + 3] = cl(a * 255);
  }
  return { out, bg };
}

// 모서리(위쪽 두 곳)에서 잰 배경색
function measureBg(data, w) {
  const sample = [];
  for (const [cx, cy] of [[4, 4], [w - 28, 4]])
    for (let y = cy; y < cy + 24; y++) for (let x = cx; x < cx + 24; x++) { const i = (y * w + x) * 3; sample.push([data[i], data[i + 1], data[i + 2]]); }
  const med = (c) => sample.map((s) => s[c]).sort((p, q) => p - q)[sample.length >> 1];
  return [med(0), med(1), med(2)];
}


module.exports = { KEYS, dominance, pickChroma, keyOut, measureBg };
