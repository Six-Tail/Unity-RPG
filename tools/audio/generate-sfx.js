#!/usr/bin/env node
'use strict';
// 효과음 합성: 외부 음원 · 다운로드 없이 파형을 직접 계산해 WAV(44.1kHz · 16bit · 모노)로 저장합니다.
// 결과: Assets/_Project/Resources/Audio/SFX/{이름}.wav — 게임은 Sfx 열거형의 소문자 이름으로 찾음(SoundManager).
//
//   node tools/audio/generate-sfx.js            # 전부 다시 만들기
//   node tools/audio/generate-sfx.js click hit  # 일부만
//
// 소리를 바꾸려면 아래 SOUNDS 의 해당 함수를 고친 뒤 다시 실행하세요. 직접 만든 파일로 바꾸려면 같은 이름으로 덮어쓰면 됩니다.

const fs = require('fs');
const path = require('path');

const RATE = 44100;
const OUT = path.resolve(__dirname, '..', '..', 'Assets', '_Project', 'Resources', 'Audio', 'SFX');

// ---------------- 기본 도구 ----------------

const buf = (sec) => new Float32Array(Math.ceil(sec * RATE));
const note = (n) => 440 * Math.pow(2, (n - 69) / 12);            // MIDI 번호 → Hz
let seed = 12345;
const rand = () => { seed = (seed * 1664525 + 1013904223) >>> 0; return seed / 4294967296 * 2 - 1; };

/** 진동자를 더함. freq: 숫자 또는 (t) => Hz, amp: (t) => 0..1 */
function osc(out, start, dur, freq, amp, type = 'sine', gain = 1) {
  const s0 = Math.floor(start * RATE), n = Math.floor(dur * RATE);
  let ph = Math.random() * 0;
  for (let i = 0; i < n && s0 + i < out.length; i++) {
    const t = i / RATE;
    const f = typeof freq === 'function' ? freq(t) : freq;
    ph += f / RATE;
    const p = ph - Math.floor(ph);
    let v;
    switch (type) {
      case 'tri': v = 4 * Math.abs(p - 0.5) - 1; break;
      case 'square': v = p < 0.5 ? 0.8 : -0.8; break;
      case 'saw': v = 2 * p - 1; break;
      default: v = Math.sin(2 * Math.PI * p);
    }
    out[s0 + i] += v * amp(t) * gain;
  }
}

/** 잡음(필터 포함): lp/hp 는 0~1 계수(작을수록 강하게), cutoff 를 시간에 따라 바꾸려면 함수 */
function noise(out, start, dur, amp, { lp = 1, hp = 0, gain = 1 } = {}) {
  const s0 = Math.floor(start * RATE), n = Math.floor(dur * RATE);
  let l = 0, h = 0, prev = 0;
  for (let i = 0; i < n && s0 + i < out.length; i++) {
    const t = i / RATE;
    const a = typeof lp === 'function' ? lp(t) : lp;
    l += a * (rand() - l);
    let v = l;
    const b = typeof hp === 'function' ? hp(t) : hp;
    if (b > 0) { h = b * (h + v - prev); prev = v; v = h; }
    out[s0 + i] += v * amp(t) * gain;
  }
}

/** 종소리(비배음 성분) */
function bell(out, start, f, dur, gain = 1, bright = 1) {
  const partials = [[1, 1], [2.0, 0.5 * bright], [3.01, 0.28 * bright], [4.17, 0.18 * bright], [5.43, 0.1 * bright]];
  for (const [r, a] of partials)
    osc(out, start, dur, f * r, (t) => Math.min(1, t * 600) * Math.exp(-t * (3.5 + r * 2.2) / dur), 'sine', a * gain);
}

const exp = (tau) => (t) => Math.exp(-t / tau);
const attackDecay = (att, tau) => (t) => (t < att ? t / att : Math.exp(-(t - att) / tau));

/** 간단한 잔향(Schroeder: 콤 4 + 올패스 2) */
function reverb(x, mix = 0.25, size = 1) {
  const combs = [1557, 1617, 1491, 1422].map((d) => Math.floor(d * size));
  const out = new Float32Array(x.length);
  for (const d of combs) {
    const line = new Float32Array(d); let idx = 0, lp = 0;
    for (let i = 0; i < x.length; i++) {
      const y = line[idx];
      lp = y * 0.6 + lp * 0.4;
      line[idx] = x[i] + lp * 0.78;
      idx = (idx + 1) % d;
      out[i] += y * 0.25;
    }
  }
  for (const d of [225, 556]) {
    const line = new Float32Array(d); let idx = 0;
    for (let i = 0; i < out.length; i++) {
      const b = line[idx], v = out[i];
      const y = -v + b; line[idx] = v + b * 0.5; idx = (idx + 1) % d; out[i] = y;
    }
  }
  for (let i = 0; i < x.length; i++) x[i] = x[i] * (1 - mix * 0.5) + out[i] * mix;
  return x;
}

/** 저역 통과(한 극) */
function lowpass(x, a) { let l = 0; for (let i = 0; i < x.length; i++) { l += a * (x[i] - l); x[i] = l; } return x; }

function finish(x, peak = 0.89) {
  let m = 0;
  for (const v of x) m = Math.max(m, Math.abs(v));
  const g = m > 0 ? peak / m : 1;
  const fade = Math.min(x.length, Math.floor(0.01 * RATE));
  for (let i = 0; i < x.length; i++) {
    let v = x[i] * g;
    if (i > x.length - fade) v *= (x.length - i) / fade;
    x[i] = Math.tanh(v * 1.1) / Math.tanh(1.1);   // 부드럽게 눌러 찌그러짐 방지
  }
  return x;
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

// ---------------- 소리 ----------------

const SOUNDS = {
  // UI
  click() {
    const x = buf(0.07);
    osc(x, 0, 0.07, (t) => 2300 - 9000 * t, exp(0.012), 'sine', 0.9);
    noise(x, 0, 0.006, exp(0.002), { hp: 0.6, gain: 0.25 });
    return finish(x, 0.7);
  },
  confirm() {
    const x = buf(0.45);
    bell(x, 0, note(88), 0.3, 0.8); bell(x, 0.065, note(95), 0.35, 0.9);
    return finish(reverb(x, 0.2, 0.6), 0.8);
  },
  cancel() {
    const x = buf(0.35);
    osc(x, 0, 0.15, note(83), attackDecay(0.004, 0.05), 'tri', 0.7);
    osc(x, 0.07, 0.2, note(76), attackDecay(0.004, 0.07), 'tri', 0.7);
    return finish(reverb(x, 0.15, 0.5), 0.7);
  },
  reward() {
    const x = buf(1.3);
    [84, 88, 91, 96].forEach((n, i) => bell(x, i * 0.075, note(n), 0.7, 0.8));
    bell(x, 0.32, note(100), 0.9, 0.5);
    noise(x, 0.25, 0.8, (t) => 0.25 * Math.exp(-t * 4) * (0.6 + 0.4 * Math.sin(t * 90)), { hp: 0.85, gain: 0.5 });
    return finish(reverb(x, 0.3, 0.9), 0.85);
  },
  coin() {
    const x = buf(0.45);
    bell(x, 0, note(100), 0.25, 0.9, 1.4); bell(x, 0.07, note(105), 0.35, 0.8, 1.4);
    return finish(reverb(x, 0.15, 0.5), 0.75);
  },
  levelup() {
    const x = buf(1.6);
    [79, 83, 86, 91, 95, 98].forEach((n, i) => bell(x, i * 0.055, note(n), 0.5, 0.75));
    [91, 95, 98, 103].forEach((n) => osc(x, 0.34, 1.1, note(n), attackDecay(0.02, 0.35), 'sine', 0.22));
    noise(x, 0.3, 1.0, (t) => 0.2 * Math.exp(-t * 3), { hp: 0.9, gain: 0.5 });
    return finish(reverb(x, 0.32, 1), 0.85);
  },
  error() {
    const x = buf(0.3);
    osc(x, 0, 0.1, 220, attackDecay(0.003, 0.05), 'square', 0.5);
    osc(x, 0.12, 0.12, 196, attackDecay(0.003, 0.06), 'square', 0.5);
    return finish(lowpass(x, 0.25), 0.6);
  },

  // 가챠
  gachasummon() {
    const x = buf(2.0);
    noise(x, 0, 1.3, (t) => Math.pow(t / 1.3, 1.6) * Math.exp(-Math.max(0, t - 1.15) * 30), { lp: (t) => 0.02 + 0.35 * (t / 1.3), gain: 1.2 });
    osc(x, 0, 1.3, (t) => 180 + 700 * Math.pow(t / 1.3, 2), (t) => 0.3 * Math.pow(t / 1.3, 1.5), 'sine', 0.6);
    [84, 88, 91, 96, 100].forEach((n, i) => bell(x, 1.2 + i * 0.04, note(n), 0.7, 0.6));
    return finish(reverb(x, 0.35, 1.1), 0.85);
  },
  gacharare() {
    const x = buf(3.2);
    // 낮은 울림 + 밝은 장조 화음(천천히 퍼짐) + 내려오는 반짝임
    osc(x, 0, 1.4, (t) => 90 - 40 * t, exp(0.5), 'sine', 0.9);
    noise(x, 0, 0.8, exp(0.25), { lp: 0.05, gain: 0.6 });
    for (const n of [72, 76, 79, 84, 88, 91]) {
      osc(x, 0.05, 2.6, note(n) * 1.003, attackDecay(0.25, 0.9), 'saw', 0.07);
      osc(x, 0.05, 2.6, note(n) * 0.997, attackDecay(0.25, 0.9), 'saw', 0.07);
    }
    [108, 103, 100, 96, 91, 88, 84].forEach((n, i) => bell(x, 0.15 + i * 0.09, note(n), 0.8, 0.5));
    noise(x, 0.1, 2.0, (t) => 0.3 * Math.exp(-t * 1.5) * (0.5 + 0.5 * Math.sin(t * 70)), { hp: 0.9, gain: 0.5 });
    lowpass(x, 0.5);
    return finish(reverb(x, 0.4, 1.2), 0.9);
  },

  // 전투
  hit() {
    const x = buf(0.22);
    osc(x, 0, 0.18, (t) => 150 * Math.exp(-t * 18) + 55, exp(0.06), 'sine', 1);
    noise(x, 0, 0.12, exp(0.03), { lp: 0.25, gain: 0.9 });
    noise(x, 0, 0.02, exp(0.006), { hp: 0.5, gain: 0.4 });
    return finish(x, 0.85);
  },
  crit() {
    const x = buf(0.45);
    osc(x, 0, 0.22, (t) => 170 * Math.exp(-t * 16) + 50, exp(0.08), 'sine', 1);
    noise(x, 0, 0.16, exp(0.045), { lp: 0.35, gain: 1 });
    bell(x, 0.005, 1860, 0.35, 0.45, 1.6);
    noise(x, 0, 0.03, exp(0.008), { hp: 0.5, gain: 0.5 });
    return finish(reverb(x, 0.12, 0.6), 0.9);
  },
  heal() {
    const x = buf(1.0);
    for (const n of [84, 88, 91, 96])
      osc(x, 0, 0.8, (t) => note(n) * (1 + 0.02 * t), (t) => Math.min(1, t / 0.08) * Math.exp(-t * 3) * (0.8 + 0.2 * Math.sin(t * 40)), 'sine', 0.25);
    noise(x, 0, 0.7, (t) => 0.15 * Math.exp(-t * 4), { hp: 0.92, gain: 0.5 });
    return finish(reverb(x, 0.35, 0.9), 0.7);
  },
  skill() {
    const x = buf(0.55);
    noise(x, 0, 0.32, (t) => Math.sin(Math.PI * t / 0.32), { lp: (t) => 0.06 + 0.5 * (t / 0.32), gain: 0.9 });
    bell(x, 0.2, note(88), 0.3, 0.4);
    return finish(reverb(x, 0.2, 0.6), 0.75);
  },
  ultimate() {
    const x = buf(1.8);
    // 빨려 드는 소리 → 큰 폭발
    noise(x, 0, 0.35, (t) => Math.pow(t / 0.35, 2.2), { lp: (t) => 0.03 + 0.4 * (t / 0.35), gain: 0.8 });
    osc(x, 0.33, 1.2, (t) => 95 * Math.exp(-t * 3) + 32, exp(0.4), 'sine', 1);
    noise(x, 0.33, 1.0, exp(0.22), { lp: 0.12, gain: 1 });
    noise(x, 0.33, 0.05, exp(0.012), { hp: 0.4, gain: 0.5 });
    [72, 79, 84].forEach((n) => osc(x, 0.35, 1.0, note(n), attackDecay(0.01, 0.35), 'saw', 0.06));
    lowpass(x, 0.6);
    return finish(reverb(x, 0.3, 1.2), 0.92);
  },
  break() {
    const x = buf(0.6);
    for (let k = 0; k < 14; k++) {
      const f = 2500 + Math.abs(rand()) * 4500, st = Math.abs(rand()) * 0.08;
      osc(x, st, 0.25, f, exp(0.05 + Math.abs(rand()) * 0.06), 'sine', 0.15);
    }
    noise(x, 0, 0.35, exp(0.08), { hp: 0.7, gain: 0.8 });
    osc(x, 0, 0.15, (t) => 120 * Math.exp(-t * 15) + 50, exp(0.05), 'sine', 0.6);
    return finish(reverb(x, 0.2, 0.7), 0.85);
  },
  defeated() {
    const x = buf(0.6);
    osc(x, 0, 0.45, (t) => 320 * Math.exp(-t * 5) + 70, exp(0.15), 'tri', 0.6);
    noise(x, 0, 0.4, exp(0.12), { lp: 0.08, gain: 0.8 });
    return finish(reverb(x, 0.2, 0.7), 0.75);
  },
  victory() {
    const x = buf(2.6);
    // 금관 느낌(톱니파 + 저역 통과): 솔-도-미 짧게, 솔 길게 → 도 장조 화음
    const brass = (st, n, dur, g = 0.35) => {
      osc(x, st, dur, note(n), attackDecay(0.02, dur * 0.8), 'saw', g);
      osc(x, st, dur, note(n) * 1.004, attackDecay(0.02, dur * 0.8), 'saw', g * 0.7);
    };
    brass(0, 67, 0.16); brass(0.16, 72, 0.16); brass(0.32, 76, 0.16); brass(0.48, 79, 0.5);
    brass(1.0, 79, 0.12); brass(1.12, 81, 0.12);
    for (const n of [72, 76, 79, 84]) brass(1.24, n, 1.2, 0.22);
    osc(x, 1.24, 1.2, note(48), attackDecay(0.02, 0.6), 'tri', 0.5);
    [96, 100, 103].forEach((n, i) => bell(x, 1.24 + i * 0.06, note(n), 1.0, 0.35));
    lowpass(x, 0.18);
    return finish(reverb(x, 0.3, 1.1), 0.9);
  },
  defeat() {
    const x = buf(2.4);
    [[0, 76], [0.3, 74], [0.6, 72], [0.9, 69]].forEach(([st, n]) =>
      osc(x, st, 0.6, note(n), attackDecay(0.03, 0.3), 'tri', 0.5));
    for (const n of [57, 60, 64]) osc(x, 0.9, 1.4, note(n), attackDecay(0.2, 0.6), 'sine', 0.25);
    return finish(reverb(x, 0.35, 1.1), 0.75);
  },
};

// Sfx 열거형 이름(소문자)과 파일 이름을 맞춤
const FILES = {
  click: 'click', confirm: 'confirm', cancel: 'cancel', reward: 'reward', coin: 'coin', levelup: 'levelup', error: 'error',
  gachasummon: 'gachasummon', gacharare: 'gacharare',
  hit: 'hit', crit: 'crit', heal: 'heal', skill: 'skill', ultimate: 'ultimate', break: 'break', defeated: 'defeated',
  victory: 'victory', defeat: 'defeat',
};

fs.mkdirSync(OUT, { recursive: true });
const only = process.argv.slice(2);
for (const [key, fn] of Object.entries(SOUNDS)) {
  if (only.length && !only.includes(key)) continue;
  seed = 12345;
  const x = fn();
  const file = path.join(OUT, FILES[key] + '.wav');
  writeWav(file, x);
  console.log(`${FILES[key]}.wav  ${(x.length / RATE).toFixed(2)}s`);
}
console.log('→ ' + OUT);
