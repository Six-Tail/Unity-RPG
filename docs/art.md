# 이미지 에셋 (일러스트 · 아이콘 · 배경 · 배너)

이미지는 **정해진 폴더에 정해진 파일명으로 넣기만 하면** 게임에 자동으로 나옵니다. 연결 작업이 따로 없습니다.
이미지가 없는 곳은 지금까지처럼 글자·색으로 표시되므로, 일부만 넣어도 됩니다.

- 넣는 곳: `Assets/_Project/Resources/Art/`
- 찾는 코드: [GameArt.cs](../Assets/_Project/Scripts/Core/GameArt.cs)
- 프롬프트 목록(112장): [art-prompts.md](art-prompts.md)

## 파일 이름 규칙

| 폴더 | 파일명 | 크기 | 어디에 나오나 |
|---|---|---|---|
| `Characters/` | `{캐릭터 id}.png` (예: `c_seraphine.png`) | 768×1024 (3:4) | 홈·가챠 픽업 미리보기, 가챠 결과·6성 연출 |
| `Thumbs/` | `{캐릭터 id}.png` | 256×256 | 육성·편성 목록, 전투 유닛 칸, 가챠 결과 카드 (없으면 일러스트 사용) |
| `Enemies/` | `{적 이름}.png` (예: `그림자병.png`) | 512×512 | 전투 유닛 칸 |
| `Items/` | `{MaterialType 이름}.png` (예: `ExpSmall.png`, `FireAsc2.png`) · 금화는 `Gold.png` | 256×256 | 가방 칸, 상점 상품 칸 |
| `Equipment/` | `slot_{부위}.png` — `head` `hands` `body` `feet` `neck` `ring` | 256×256 | 장비 칸·장비 목록(글자 뒤에 옅게) |
| `Backgrounds/` | `{키}.jpg` — 아래 표 | 1920×1080 (16:9) | 각 화면 배경 |
| `Banners/` | `{픽업 캐릭터 id}.jpg` · `standard.jpg` | 1920×1080 | 가챠 화면 배경(배너별) |
| `Motion/` | `{캐릭터 id}.mp4` | 832×1088 · 6초 반복 | 6성 일러스트 자리의 움직이는 일러스트 — [live2d.md](live2d.md) |
| `Status/` | `AtkUp` `AtkDown` `DefUp` `DefDown` `Dot` `Break` `SpdUp` `SpdDown` `CritUp` `Regen` `Taunt` `Stun` `.png` | 128×128 | 전투 버프·디버프 아이콘 — 기호만 그리고 배경은 투명(게임이 어두운 칸 + 효과 색 테두리를 그림). 없으면 색 + 글자 아이콘 |
| `Elements/` | `Physical` `Fire` `Ice` `Lightning` `Wind` `.png` | 128×128 | 전투 유닛 패널의 속성 아이콘 · 약점 표시 — `--only status`에 함께 포함. 없으면 속성 색 원 + 글자 |
| `Gacha/` | `scroll`(1280×720) `circle`(768) `quill` `seal`(256) `.png` | — | 가챠 소환 연출 소품(계약 스크롤 · 마법진 · 깃펜 · 인장), 투명 배경 — `--only gacha` |

- 캐릭터 id는 `CharacterData.id`입니다([characters.md](characters.md)). 에셋 인스펙터에서 `portrait`·`thumbnail`을 직접 연결하면 그쪽이 우선합니다.
- 확장자는 png·jpg 아무거나 됩니다(이름만 맞으면 됨). 배경은 용량 때문에 jpg를 권장합니다.
- 화면비가 달라도 됩니다 — 배경은 비율을 유지한 채 화면을 가득 채우고, 나머지는 칸 안에 비율대로 들어갑니다.

### 배경 키

| 키 | 화면 |
|---|---|
| `home` `gacha` `stage` `growth` `party` `bag` `shop` | 홈 · 가챠 · 스테이지 선택 · 육성 · 편성 · 가방 · 상점 |
| `mission` `codex` | 임무(없으면 `party`) · 도감(없으면 `growth`) |
| `battle` | 전투 공용(아래가 없을 때) |
| `battle_ch1` … `battle_ch15` | 메인 스토리 챕터별 전투 |
| `battle_dungeon_ascension` `_gold` `_book` `_equipment` `_exp` | 특수 던전별 전투 |

가챠 화면은 `Banners/{픽업 캐릭터 id}`(상시는 `Banners/standard`) → 없으면 `Backgrounds/gacha` 순으로 찾습니다. 배너 그림이 있으면 가운데 미리보기 일러스트는 숨깁니다.
배경 위에는 글자가 읽히도록 어두운 막을 덮습니다(화면별 `BackgroundArt.Create(root, 어둡기)` 값으로 조절).

## 일괄 생성 (AI 이미지 스튜디오)

별도 프로그램 **ai-image-studio**(텍스트→이미지 웹앱)의 API를 불러, 목록의 이미지를 만들어 위 폴더에 게임용 크기·이름으로 저장하는 스크립트가 있습니다.

1. 스튜디오 폴더에서 서버를 켭니다: `npm start`
2. 이 저장소 폴더에서:

```bash
# 무엇을 만들지 보기(생성 안 함)
node tools/art/generate-art.js --list

# 로컬 GPU 모델로 아직 없는 것 전부 생성 (무료)
node tools/art/generate-art.js --studio "<스튜디오 폴더>" --model "local:flux1-schnell-fp8.safetensors"

# 한 종류만 / 특정 항목만 다시
node tools/art/generate-art.js --studio "<스튜디오 폴더>" --model "local:..." --only items
node tools/art/generate-art.js --studio "<스튜디오 폴더>" --model "local:..." --key c_seraphine --force
```

| 옵션 | 뜻 |
|---|---|
| `--studio <폴더>` | 스튜디오 폴더(이미지 크기 조정에 그 폴더의 `sharp`를 씀). 환경변수 `AI_IMAGE_STUDIO_DIR`로도 지정 |
| `--model <모델>` | 실행하는 동안만 그 모델로 바꾸고 **끝나면 원래 모델로 되돌림**. 생략하면 스튜디오에 설정된 모델 |
| `--only <종류,…>` | `characters` `enemies` `items` `equipment` `status` `gacha` `backgrounds` `banners` |
| `--key <키,…>` | 특정 항목만 |
| `--force` | 이미 있는 파일도 다시 생성 (기본은 없는 것만) |
| `--limit <N>` · `--quality <low\|medium\|high>` | 장수 제한 · 품질 |
| `--yes` | **유료(OpenAI) 모델로 생성하는 데 동의** — 로컬 GPU 모델은 필요 없음 |
| `--thumbs` | 이미 있는 캐릭터 일러스트에서 썸네일만 다시 자름(생성 안 함 · 서버 필요 없음) |

- **요금**: 로컬 GPU(`local:…`) 모델은 무료입니다. OpenAI 모델은 장수만큼 과금되므로 `--yes`를 붙여야만 실행됩니다.
- **소요 시간**(RTX 5080 · Flux schnell 기준): 1장 6~12초, 픽업 배너는 참고 이미지 때문에 약 30초. 전체 112장 ≈ 15~20분.
- 캐릭터 썸네일은 일러스트 윗부분을 잘라 자동으로 만듭니다. 픽업 배너는 그 캐릭터 일러스트를 참고 이미지로 넣어 같은 인물로 그립니다(그래서 배너는 맨 마지막에 생성).
- 아이콘·장비·적은 OpenAI 모델일 때 **투명 배경**으로 요청합니다(칸 색과 어긋나는 네모가 안 생김). 모델이 투명 배경을 거부하면 어두운 단색 배경으로 한 번 더 시도하고, 로컬 Flux는 처음부터 어두운 단색 배경으로 그립니다.
- 마음에 안 드는 그림은 `--key <키> --force`로 그것만 다시 뽑으면 됩니다. 스튜디오 갤러리(`outputs/`)에는 원본이 그대로 남습니다.
- 실패한 항목은 같은 명령을 다시 실행하면 그것만 다시 만듭니다.

### 프롬프트 고치기

[tools/art/art-manifest.js](../tools/art/art-manifest.js) 한 곳에 캐릭터 외형·적·아이템·배경 묘사와 공통 화풍이 있습니다. 고친 뒤:

```bash
node tools/art/generate-art.js --write-prompts
```

→ [art-prompts.md](art-prompts.md)가 다시 만들어집니다. 캐릭터·적·재료를 게임에 추가했으면 이 목록에도 추가하세요.

> Flux 계열(로컬)은 프롬프트에 "no text" 같은 말을 넣으면 오히려 글자를 그립니다. 그래서 그런 문구는 OpenAI 모델일 때만 자동으로 붙이고, 묘사에서도 간판·깃발처럼 글자를 부르는 단어는 피했습니다.

## 직접 만든 그림 넣기

1. [art-prompts.md](art-prompts.md)에서 프롬프트를 복사해 아무 이미지 도구로 생성(또는 직접 그림).
2. 표의 파일명으로 `Assets/_Project/Resources/Art/{폴더}/`에 저장.
3. Unity로 돌아오면 자동 임포트됩니다.

## Unity 임포트 설정

[ArtImportSettings.cs](../Assets/_Project/Scripts/Editor/ArtImportSettings.cs)가 `Resources/Art/` 아래 이미지를 **처음 임포트할 때** Sprite로 바꾸고 종류별 최대 크기(배경·배너 2048 · 캐릭터 1024 · 적 512 · 그 외 256)를 맞춥니다.
프로젝트 기본 모드가 3D라 이 설정이 없으면 일반 텍스처로 들어와 게임이 찾지 못합니다.

- 이미 임포트된 이미지의 설정은 건드리지 않습니다(인스펙터에서 바꾼 값 유지).
- 이 스크립트가 생기기 전에 넣은 이미지가 안 나오면: 메뉴 **`SubcultureRPG > 아트 다시 임포트 (Resources/Art)`**.
- 이미지(png·jpg)는 Git LFS로 올라갑니다. Unity가 만든 `.meta`도 함께 커밋하세요.

## 아직 이미지 자리가 없는 곳

- 육성 화면의 재료 칸, 스테이지 보상 목록(글자 표시 그대로)
- 재화(크리스탈·뽑기권) 아이콘
- 6성 Live2D는 따로 [live2d.md](live2d.md) — 모델 폴더를 `Assets/_Project/Art/Characters/Live2D/{id}/`에 두면 연결됩니다(Cubism SDK 필요)
