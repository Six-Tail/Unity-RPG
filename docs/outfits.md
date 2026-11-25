# 의상(옷장) · 개화 일러스트

캐릭터마다 **의상**을 골라 입힐 수 있습니다. 지금은 `기본`과 `개화` 두 벌이고, **추후 스킨은 여기에 추가**합니다.

- 의상 목록 · 저장: [CharacterOutfits.cs](../Assets/_Project/Scripts/Core/CharacterOutfits.cs) (저장 키 `outfits` — 계정 데이터)
- 옷장 창: [WardrobeWindow.cs](../Assets/_Project/Scripts/Meta/UI/WardrobeWindow.cs)
- 개화 연출: [BloomCeremony.cs](../Assets/_Project/Scripts/Meta/UI/BloomCeremony.cs)

## 의상

| 의상 | 해금 | 파일 접미사 |
|---|---|---|
| 기본 | 처음부터 | (없음) |
| **개화** | **Lv.100 달성**(숨겨진 특성 개화 — [character-kits.md](character-kits.md#숨겨진-특성-lv100-개화)) | `_bloom` |

고른 의상은 그 캐릭터가 나오는 **모든 그림**에 적용됩니다. 그 의상의 파일이 없으면 기본 의상 그림으로 대체합니다.

| 그림 | 경로 (`{id}` = 캐릭터 id, 개화면 `{id}_bloom`) | 쓰이는 곳 |
|---|---|---|
| 일러스트 | `Characters/{id}.png` | 홈 · 가챠 · 도감 · 육성 등 |
| 썸네일 | `Thumbs/{id}.png` | 목록 카드 · 전투 초상 · 프로필 |
| 배경 없는 그림 | `Cutouts/{id}.png` | 스토리 · 홈 대표 캐릭터(영상이 없는 캐릭터) |
| 전투 그림 | `Figures/{id}.png` | 전투 유닛 |
| 움직이는 영상 | `Motion/{id}.mp4` | 6★ 일러스트 자리 |

- 의상 일러스트는 있는데 그 의상의 영상이 없으면, 기본 의상 영상 대신 **정지 일러스트**를 보여 줍니다(옷이 다르게 보이지 않게).
- 코드: `GameArt.Portrait · Thumb · Cutout · Outfitted`, `MotionVideoStage.ClipOf`가 `CharacterOutfits.SuffixOf`를 봅니다.

## 옷장

육성 → 캐릭터 상세 오른쪽 위 **`옷장`**. 의상마다 카드(일러스트 · 이름 · 상태)가 나오고 `착용`으로 갈아입힙니다.

- 잠긴 의상은 어둡게 + 해금 조건, 일러스트를 아직 만들지 않은 의상은 `일러스트 준비 중`.
- 맨 오른쪽 `?` 칸은 추후 스킨 자리입니다.
- 의상을 바꾸면 홈 · 육성 · 도감이 바로 다시 그립니다(`CharacterOutfits.Changed`).

## 개화 연출

육성 화면에서 레벨업으로 **Lv.100에 닿는 순간** 전체 화면 연출이 나옵니다.

1. 화면이 어두워지고 지금 일러스트가 떠오름
2. 빛이 모여듦(빛줄기 회전 · 속성 색 빛 · 바깥에서 빨려 드는 입자 · 점점 세지는 떨림)
3. 섬광 → **개화 일러스트**로 바뀌며 꽃잎이 터져 나감
4. `숨겨진 특성 개화` · 특성 이름 · 효과 표시(무지개 글자). 화면을 누르면 닫힘

연출이 끝나면 의상이 **개화로 갈아입혀집니다**(개화 일러스트가 있을 때). 개화 전 일러스트로 되돌리려면 옷장에서 `기본`을 고르면 됩니다.

## 개화 일러스트 만들기

```bash
node tools/art/generate-art.js --studio "<스튜디오 폴더>" --only bloom --list                      # 목록
node tools/art/generate-art.js --studio "<스튜디오 폴더>" --model gpt-image-2.6-sunburst --only bloom --yes
node tools/art/generate-art.js --studio "<스튜디오 폴더>" --model gpt-image-2.6-sunburst --key bloom_c_ruru --force --yes   # 한 명만 다시
```

- 기본 일러스트(`Characters/{id}.png`)를 **참고 이미지**로 넣어 같은 인물 · 같은 구도로 그립니다(연출에서 "같은 자세로 변신"). 속성별 모티프(`BLOOM`) · 성급별 화려함(`BLOOM_RARITY`)은 [art-manifest.js](../tools/art/art-manifest.js).
- 썸네일(`Thumbs/{id}_bloom.png`)은 일러스트 윗부분을 잘라 자동으로 만듭니다.

### 6★ 개화 영상 (움직이는 일러스트)

```bash
node tools/art/generate-motion.js --studio "<스튜디오 폴더>" --bloom                    # 아직 없는 6★ 전부 (Wan 2.2 Live2D 대기 동작 모드 · 무료 · 한 명에 3분쯤)
node tools/art/generate-motion.js --studio "<스튜디오 폴더>" --bloom --key c_ciel --force
```

→ `Motion/{id}_bloom.mp4`. 개화 의상을 입은 6★은 일러스트 자리에서 이 영상이 재생됩니다.

## 새 스킨 추가

1. `CharacterOutfits.All`에 `new Outfit { id = "summer", name = "여름", suffix = "_summer", unlockText = "…" }` 추가.
2. `CharacterOutfits.Unlocked`에 해금 조건(보유 여부 등) 추가.
3. 그림을 `Characters/{id}_summer.png` · `Thumbs/{id}_summer.png` (필요하면 `Cutouts` · `Figures` · `Motion`)로 넣기.
