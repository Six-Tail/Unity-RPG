# 사운드 (BGM · 효과음)

- 재생: [SoundManager.cs](../Assets/_Project/Scripts/Core/SoundManager.cs) — 씬에 없어도 시작할 때 스스로 생김(씬이 바뀌어도 유지)
- 설정 창: 홈 위쪽 아이콘 줄 **`설정`** — [SettingsWindow.cs](../Assets/_Project/Scripts/Meta/UI/SettingsWindow.cs)
- 임포트 설정: [AudioImportSettings.cs](../Assets/_Project/Scripts/Editor/AudioImportSettings.cs)

**파일만 넣으면 연결됩니다.** 없는 소리는 조용히 건너뜁니다.

## BGM — `Resources/Audio/BGM/{키}` (.ogg 권장 · .wav · .mp3)

| 키 | 화면 |
|---|---|
| `home` | 홈 |
| `stage` | 스테이지 · 특수 던전 선택 |
| `gacha` | 가챠 |
| `battle` | 전투(스테이지 · 던전 · 이벤트) |
| `boss` | 보스전 — 주간 보스 · 무한의 탑에서 들어간 전투 |
| `arena` | 결투장 전투 · 결투장 · 주간 보스 · 탑 화면 |
| `event` | 기간 한정 이벤트 |
| `menu` | 그 밖(육성 · 편성 · 가방 · 상점 · 임무 · 도감 · 친구 · 길드) |

- 없는 키는 `menu` → `home` 순으로 대체합니다. 그것도 없으면 조용히.
- 전투 곡은 어디서 들어왔는지로 고릅니다(`ScreenNavigator.BgmOf`): 주간 보스 · 무한의 탑 → `boss`, 결투장 → `arena`, 그 밖 → `battle`.
- 2026-11-25: 전투 곡이 안 어울린다는 의견으로 `battle`(교향악 + 록, 더 웅장하게) · `arena`(오케스트라 + 신스)를 다시 만들고 보스전 전용 `boss`(어두운 합창 · 파이프 오르간 · 타이코)를 더했습니다.
- 화면을 바꾸면 0.8초 동안 겹쳐 바뀌고(크로스페이드), 같은 곡이면 이어서 재생합니다.
- 승리 · 패배 효과음이 나올 때 BGM을 잠시 줄였다가 3.5초 뒤 되돌립니다.
- 반복 재생하므로 처음과 끝이 이어지는 곡이 좋습니다. 임포트하면 스트리밍 · Vorbis(품질 70%)로 설정됩니다.

### BGM 만들기 (AI 이미지 스튜디오 · ACE-Step 1.5 · 로컬 · 무료)

```bash
node tools/audio/generate-bgm.js --studio "<스튜디오 폴더>" --list                    # 곡 목록 · 분위기 설명
node tools/audio/generate-bgm.js --studio "<스튜디오 폴더>"                           # 아직 없는 곡 전부 (한 곡에 10초쯤)
node tools/audio/generate-bgm.js --studio "<스튜디오 폴더>" --key battle --force      # 한 곡만 다시
node tools/audio/generate-bgm.js --studio "<스튜디오 폴더>" --key battle --reloop     # 원본으로 다듬기만 다시
```

- 스튜디오 서버가 다른 포트에 있으면 `--server http://127.0.0.1:3001`처럼 지정합니다(오디오 탭이 있는 버전이어야 함).
- 곡마다 분위기 · 악기 · 빠르기는 스크립트의 `TRACKS`(영어 tags · bpm · 길이)에 있습니다. 결과는 매번 달라지므로 마음에 안 들면 그 곡만 `--force`.
- **끊김 없는 반복**: 모델이 만든 곡은 끝이 잦아들고 무음이 붙습니다. 그래서 곡이 실제로 끝나는 지점을 음량으로 찾아 그 3초 앞에서, 끝 5초(페이드아웃)와 처음 5초(페이드인)를 겹쳐 붙입니다. 반복 지점이 곡의 5초 지점과 그대로 이어집니다.
- 음량은 곡끼리 비슷하게 맞춥니다(loudnorm −17 LUFS). mp3는 반복 지점에 짧은 공백이 생기므로 ogg로 저장합니다.
- 모델이 만든 원본은 `docs/art-preview/audio/`에 남습니다(커밋 안 함).

## 효과음 — `Resources/Audio/SFX/{이름}.wav`

| 이름 | 언제 |
|---|---|
| `click` | 모든 버튼(`UIFactory.AddButton` · 홈 타일 등 `ButtonFx`가 붙은 곳). 꺼진 버튼은 소리 없음 |
| `confirm` · `cancel` · `error` | 설정 창 미리 듣기 등(예비) |
| `reward` | 보상 알림 창(`Popups.Reward`) |
| `coin` | 상점 구매 |
| `levelup` | 육성(레벨업 · 돌파 · 스킬 레벨업) |
| `gachasummon` · `gacharare` | 뽑기 시작 · 6★가 나왔을 때(1.1초 뒤) |
| `hit` · `crit` · `heal` · `break` · `defeated` | 전투 타격 · 치명타 · 회복 · 브레이크 · 쓰러짐 (변형 `_2` · `_3`) |
| `skill` · `ultimate` | 스킬 · 궁극기 시전 (변형 `_2` · `_3`) |

**변형**: 같은 이름에 `_2` · `_3` · `_4`를 붙인 파일이 있으면 그중 하나를 골라 냅니다(직전과 다른 것). 전투 소리는 종류마다 2~3개씩 있어 매번 똑같이 들리지 않습니다.
| `victory` · `defeat` | 전투 승리 · 패배 |

- 전투 밖 효과음(버튼 · 보상 · 가챠 · 승리/패배 등 11개)은 **AI 이미지 스튜디오(Stable Audio Open 1.0 · 로컬 · 무료)로 만든 소리**입니다:
  `node tools/audio/generate-sfx-ai.js --studio "<스튜디오 폴더>" --force` (일부만: `--key click,hit --force`, 다듬기만 다시: `--retrim`). 소리 설명은 스크립트의 `SOUNDS`(영어 prompt).
  - 다듬기: 소리가 시작되는 곳을 찾아 앞의 무음을 자르고, 소리마다 정한 길이까지만 남기고, 끝을 부드럽게 줄이고, 음량을 맞춥니다. 타격음(`hit` · `crit` · `break`)은 가장 세지는 순간 바로 앞에서 시작하게 맞춰 누르는 즉시 소리가 납니다.
  - 결과는 매번 달라집니다. 마음에 안 드는 소리는 그것만 `--key <이름> --force`.
- **합성 버전**(외부 모델 없이 파형을 계산)으로 되돌리려면: `node tools/audio/generate-sfx.js` (일부만: `... click hit`). 같은 파일 이름으로 덮어씁니다.
- **전투 소리 16개**(`hit` · `crit` · `skill` · `ultimate` · `heal` · `break` · `defeated` 와 변형)는 AI가 아니라 **실제 녹음 소리를 겹쳐** 만듭니다 — [build-sfx-kenney.js](../tools/audio/build-sfx-kenney.js).
  - 재료: [Kenney](https://kenney.nl) 무료 팩 **Impact Sounds · RPG Audio · Sci-Fi Sounds** (CC0 — 상용 무료, 출처 표기 불필요). 팩은 저장소에 넣지 않고, 만든 결과(WAV)만 넣습니다.
  - 소리마다 재료 2~5개를 시작 시점 · 크기 · 높낮이를 정해 겹친 뒤(예: 치명타 = 칼 베기 + 금속 충돌 + 낮은 폭발음), 저음 정리 · 가벼운 압축 · 최대치 맞추기를 합니다. 조합은 스크립트의 `RECIPES`.
  - 다시 만들기: 세 팩을 한 폴더에 풀고 `node tools/audio/build-sfx-kenney.js --studio "<스튜디오 폴더>" --kenney "<팩을 푼 폴더>"` (일부만 `--key hit,crit`).
  - 2026-11-25: AI로 만든 전투 소리(예전 스킬 소리는 '피융' 하고 내려가는 레이저 소리)가 싸구려 같고 반복된다는 의견으로 바꿨습니다. 회복 소리는 팩에 없어 종소리를 높이고 유리 반짝임을 겹쳐 만들었습니다.
- **회복 · 스킬 소리는 효과음 라보 것을 우선** 씁니다 — `Resources/Audio/SFX/Lab/{이름}.wav`가 있으면 같은 이름의 기본 소리 대신 냅니다(`SoundManager.Pick`).
  - 출처: [효과음 라보](https://soundeffect-lab.info) 전투 페이지 — 회복 `magic-cure1` · `magic-cure4`, 스킬 `sword-gesture2`(칼 휘두르기) · `magic-charge1`(힘 모으기) · `magic-circle1`(마법진). 상용 무료 · 출처 표기 불필요.
  - **⚠ 재배포 금지(고친 파일 포함)이고 이 저장소는 공개라서 `SFX/Lab/`은 저장소에 넣지 않습니다**(.gitignore). 다른 PC에서는 한 번 실행:
    `node tools/audio/fetch-sfx-lab.js --studio "<스튜디오 폴더>"` — 공식 사이트에서 받아 같은 소리를 만듭니다([fetch-sfx-lab.js](../tools/audio/fetch-sfx-lab.js)). 안 하면 Kenney로 만든 기본 회복 · 스킬 소리가 나옵니다.
  - 빌드한 게임에 넣는 것은 이용 조건상 괜찮습니다(게임 효과음으로 쓰는 것은 재배포가 아님). 이용 조건: https://soundeffect-lab.info/agreement/
- 같은 소리는 0.05초 안에 겹쳐 내지 않습니다(다단 히트가 한꺼번에 울리지 않게). 타격음은 높낮이를 조금씩 바꿔 반복감을 줄입니다.
- 임포트하면 메모리에 풀어 두기(지연 없음) · ADPCM · 모노로 설정됩니다.

## 볼륨

설정 창의 **배경음 · 효과음 슬라이더**(0 ~ 100%)와 **음소거**. 기본값 배경음 60% · 효과음 80%. 이 기기의 PlayerPrefs(`sound.bgm` · `sound.sfx` · `sound.mute`)에 저장됩니다.

## 소리를 더 붙이려면

- 클라우드 세이브를 받아 씬을 다시 열면 소리 관리자가 전투 · 가챠에 다시 연결됩니다(예전에는 다시 열면 전투 효과음이 나지 않았음).
- 효과음 추가: `Sfx` 열거형에 이름을 더하고 같은 소문자 이름의 파일을 넣은 뒤 `SoundManager.Play(Sfx.이름)`.
- 버튼마다 다른 소리: 그 버튼의 `ButtonFx.clickSound`를 바꾸거나 `mute = true`.
- 화면별 BGM 키: `ScreenNavigator.BgmOf`.
