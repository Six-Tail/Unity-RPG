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
| `arena` | 결투장 · 주간 보스 화면 |
| `event` | 기간 한정 이벤트 |
| `menu` | 그 밖(육성 · 편성 · 가방 · 상점 · 임무 · 도감 · 친구 · 길드) |

- 없는 키는 `menu` → `home` 순으로 대체합니다. 그것도 없으면 조용히.
- 화면을 바꾸면 0.8초 동안 겹쳐 바뀌고(크로스페이드), 같은 곡이면 이어서 재생합니다.
- 승리 · 패배 효과음이 나올 때 BGM을 잠시 줄였다가 3.5초 뒤 되돌립니다.
- 반복 재생하므로 처음과 끝이 이어지는 곡이 좋습니다. 임포트하면 스트리밍 · Vorbis(품질 70%)로 설정됩니다.

## 효과음 — `Resources/Audio/SFX/{이름}.wav`

| 이름 | 언제 |
|---|---|
| `click` | 모든 버튼(`UIFactory.AddButton` · 홈 타일 등 `ButtonFx`가 붙은 곳). 꺼진 버튼은 소리 없음 |
| `confirm` · `cancel` · `error` | 설정 창 미리 듣기 등(예비) |
| `reward` | 보상 알림 창(`Popups.Reward`) |
| `coin` | 상점 구매 |
| `levelup` | 육성(레벨업 · 돌파 · 스킬 레벨업) |
| `gachasummon` · `gacharare` | 뽑기 시작 · 6★가 나왔을 때(1.1초 뒤) |
| `hit` · `crit` · `heal` · `break` · `defeated` | 전투 타격 · 치명타 · 회복 · 브레이크 · 쓰러짐 |
| `skill` · `ultimate` | 스킬 · 궁극기 시전 |
| `victory` · `defeat` | 전투 승리 · 패배 |

- 지금 들어 있는 효과음 18개는 **합성한 소리**입니다(외부 음원 없음): `node tools/audio/generate-sfx.js` (일부만: `... click hit`). 소리를 바꾸려면 스크립트의 해당 함수를 고치거나, 같은 이름의 파일로 덮어쓰세요.
- 같은 소리는 0.05초 안에 겹쳐 내지 않습니다(다단 히트가 한꺼번에 울리지 않게). 타격음은 높낮이를 조금씩 바꿔 반복감을 줄입니다.
- 임포트하면 메모리에 풀어 두기(지연 없음) · ADPCM · 모노로 설정됩니다.

## 볼륨

설정 창의 **배경음 · 효과음 슬라이더**(0 ~ 100%)와 **음소거**. 기본값 배경음 60% · 효과음 80%. 이 기기의 PlayerPrefs(`sound.bgm` · `sound.sfx` · `sound.mute`)에 저장됩니다.

## 소리를 더 붙이려면

- 효과음 추가: `Sfx` 열거형에 이름을 더하고 같은 소문자 이름의 파일을 넣은 뒤 `SoundManager.Play(Sfx.이름)`.
- 버튼마다 다른 소리: 그 버튼의 `ButtonFx.clickSound`를 바꾸거나 `mute = true`.
- 화면별 BGM 키: `ScreenNavigator.BgmOf`.
