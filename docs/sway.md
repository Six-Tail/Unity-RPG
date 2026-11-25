# 움직이는 일러스트 — 셰이더 방식

한 장 그림에 **움직임 지도**를 붙여 셰이더가 흔드는 방식입니다. 리깅 없이 자동으로 만들 수 있고, 영상보다 훨씬 가볍습니다.
고개 돌리기 같은 큰 동작은 안 되고 **머리카락 · 옷자락 흔들림, 호흡, 빛 반짝임, 눈 깜빡임** 정도입니다.

- 셰이더: [UI-Sway.shader](../Assets/_Project/Resources/Shaders/UI-Sway.shader) (`SubcultureRPG/UI Sway`)
- 컴포넌트: [SwayIllustration.cs](../Assets/_Project/Scripts/UI/SwayIllustration.cs)
- 재료 만들기: [generate-sway.js](../tools/art/generate-sway.js)
- 미리보기: 메뉴 **`SubcultureRPG > 움직이는 일러스트(셰이더) 미리보기`** — 플레이하지 않고 움직임을 보고, 흔들림 · 속도 · 호흡 · 반짝임 세기를 슬라이더로 바꿔 볼 수 있습니다(값은 미리보기에만 적용).

## 재료 (`Resources/Art/Sway/`)

| 파일 | 내용 |
|---|---|
| `{이름}_mask.png` | 움직임 지도(불투명 RGB) — **R 흔들림**(머리카락 · 옷자락, 얼굴에서 멀수록 셈) · **G 호흡**(가슴) · **B 반짝임**(날개 · 후광 · 꽃잎 · 이펙트) |
| `{이름}_blink.png` | 눈 감은 그림 — **알파 = 눈 영역**. 4초마다 0.16초 동안 섞어 깜빡임 |

`{이름}` = 일러스트 파일 이름: 기본 `c_seraphine`, 개화 `c_seraphine_bloom` (입고 있는 의상을 따라감 — [outfits.md](outfits.md)).

## 만들기

```bash
node tools/art/generate-sway.js --studio "<스튜디오 폴더>" --name c_seraphine_bloom --yes       # AI 2장(유료) + 지도 만들기
node tools/art/generate-sway.js --studio "<스튜디오 폴더>" --name c_seraphine_bloom --rebuild   # AI 없이 지도만 다시
node tools/art/generate-sway.js --studio "<스튜디오 폴더>" --name c_seraphine_bloom --preview   # 셰이더와 같은 계산으로 미리보기 영상
```

1. **구역 지도**(편집 모델): 같은 구도로 머리카락 = 빨강, 옷자락 = 파랑, 몸통 = 초록, 얼굴 = 노랑, 날개 · 빛 = 흰색, 나머지 = 검정으로 칠한 그림.
2. **눈 감은 그림**(편집 모델): 눈만 감은 같은 그림.
3. **후처리**: 얼굴 = 위쪽의 노란 덩어리 중 가장 위(목 · 허벅지 같은 다른 피부와 구분). 흔들림은 얼굴에서 멀수록 세게, 경계는 흐리게. 눈 영역 = 얼굴 윗부분에서 원본과 눈 감은 그림이 다른 곳.
4. 미리보기 영상: `docs/art-preview/sway/{이름}_preview.mp4` (커밋 안 함).

## 게임에서

일러스트 자리에서 고르는 순서: **Live2D 모델 → 반복 영상(Motion) → 셰이더 흔들림(Sway) → 정지 그림** (`Live2DUI.Show`).
반복 영상이 있는 그림(6★ · 6★ 개화)은 영상이 먼저 나오므로, 셰이더 방식은 **영상이 없는 캐릭터(5★ 이하)** 에 쓰는 것이 기본입니다.

## 조정

셰이더 기본값(`_SwayAmp` 0.012 · `_SwaySpeed` 1.6 · `_BreathAmp` 0.006 · `_GlowAmp` 0.22 …)과 미리보기 스크립트의 `P`가 같아야 합니다(둘 다 고칠 것). 깜빡임 간격은 `SwayIllustration.blinkEvery`.
