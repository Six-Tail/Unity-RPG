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

1. **막 내림**(0.7초): 위아래 검은 띠가 내려오고 성운 배경(`Backgrounds/bloom`)이 밝아지며 지금 일러스트가 나타남
2. **각성**(2.6초): 캐릭터 뒤 · 발밑(납작하게 눕힌)에 **개화 마법진**이 펼쳐져 빨라지며 돌고, 빛줄기 · 속성 색 빛 · 빨려 드는 입자 · 반짝이,
   캐릭터가 떠오르며 점점 세게 떨림. 아래 자막에 **깨어나는 대사**가 한 글자씩
3. **섬광 → 개화**: 섬광이 걷히며 **개화 일러스트**로 바뀌고, 빛 폭발 · 충격파 고리 · **빛의 꽃**이 피며 꽃잎 · 반짝이가 터져 나감(화면 흔들림)
4. **개화**: 위에 `숨겨진 특성 개화`(무지개) · ◆ 특성 이름 ◆ · 효과, 아래 대화창(이름표)에 **개화 대사**가 한 글자씩. 꽃잎이 계속 내리고 꽃 · 마법진이 숨 쉬듯 돎

화면을 누르면: 각성 중 = 바로 개화로 · 대사가 나오는 중 = 다 보여 줌 · 다 나왔으면 닫음. 효과 그림은 모두 흰 빛이라 **속성 색을 곱해** 씀.

| 재료 | 파일 | 만들기 |
|---|---|---|
| 개화 마법진 · 빛의 꽃 · 빛 꽃잎 · 개화 섬광 | `Fx/bloom_circle` · `bloom_flower` · `bloom_petal` · `bloom_burst` | `node tools/art/generate-fx.js --studio … --model "local:flux1-schnell-fp8.safetensors" --key bloom_circle,bloom_flower,bloom_petal,bloom_burst` |
| 성운 배경 | `Backgrounds/bloom.jpg` | `node tools/art/generate-art.js --studio … --model "local:…" --key bloom` (모델이 위아래에 검은 띠를 그리면 잘라 냄) |
| 대사 | `CharacterLines.Bloom` — 캐릭터마다 (깨어나는 대사, 개화 대사), 없으면 역할별 기본 대사 | |

**디버그 미리보기**: 육성 화면 왼쪽 아래 `개화 연출 미리보기(테스트)` — 고른 캐릭터로 연출만 재생(레벨 · 의상은 그대로, 위에 '미리보기' 표시). 다른 테스트 버튼처럼 `DevTools`로 **출시 빌드에서는 없음**(Development Build · 에디터에서만).

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
