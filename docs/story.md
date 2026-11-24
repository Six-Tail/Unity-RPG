# 스토리

메인 스테이지 전·후에 나오는 대화 장면입니다. 지금은 **제1장 '그림자 숲'(쉬움 1-1 ~ 1-6)** 만 들어 있습니다.

- 대본: [StoryData.cs](../Assets/_Project/Scripts/Meta/Story/StoryData.cs) (`StoryLibrary`)
- 화면: [StoryScreen.cs](../Assets/_Project/Scripts/Meta/UI/StoryScreen.cs)
- 연결: [StageRunner.cs](../Assets/_Project/Scripts/Meta/Stages/StageRunner.cs) — 출격·클리어 시점에 재생

## 언제 나오나

| 시점 | 내용 |
|---|---|
| 스테이지에 **처음 출격**할 때 | 편성 화면에서 `출격!` → 스토리 → 전투 시작. 행동력은 스토리가 끝난 뒤 전투가 시작될 때 차감 |
| **처음 클리어**했을 때(1-6만) | 결과 화면 위에서 재생. 끝나면 `다음 스테이지로` 버튼이 나옴(자동 진행은 하지 않음) |

- 한 번 본 스토리는 다시 자동으로 나오지 않습니다(`PlayerPrefs` `story.seen.{id}`).
- 스테이지 상세 패널 제목 오른쪽 **`스토리 다시 보기`** 로 본 스토리를 다시 볼 수 있습니다.
- 반복 입장 · 하드/극악 · 특수 던전에는 스토리가 없습니다.
- 테스트: Stage Runner 컴포넌트 ⋮ → `스토리 기록 초기화(다시 자동 재생)`.

## 화면 조작

- **화면 아무 곳이나 누르면** 다음 대사. 글자가 나오는 중에 누르면 한 번에 표시.
- 오른쪽 위 `자동 OFF/ON`: 대사가 다 나오면 잠시 뒤 자동으로 넘어감. `건너뛰기 >>`: 그 편 전체를 넘김.
- 말하는 인물은 밝게 · 살짝 크게, 나머지는 어둡게. 내레이션은 이름표 없이 가운데 기울임 글씨.
- 장 시작·끝에는 제목 카드(`제1장` / `그림자 숲`).

## 제1장 구성

| 편 | 배경 | 줄거리 |
|---|---|---|
| 1-1 숲의 입구 | `story_ch1_gate` | 제목 카드. 신임 지휘관과 견습 대원 루루 · 핀 · 리리가 그림자 숲에 도착, 첫 전투 |
| 1-2 갈림길 | `story_ch1_gate` | 시든 풀 자국을 따라 지도에 없는 길로, 매복 |
| 1-3 안개 낀 오솔길 | `story_ch1_forest` | 마력이 섞인 안개, 루루의 번개로 길을 밝힘 |
| 1-4 궁수의 둥지 | `story_ch1_forest` | 화살비 속에서 방패 대장 아크가 합류 |
| 1-5 그림자 야영지 | `story_ch1_camp` | 깃발 문양으로 봉인된 '어둠군주'의 부활을 알게 됨 |
| 1-6 어둠군주의 제단 | `story_ch1_altar` | 어둠군주와 대면, 결전 |
| 1-6 클리어 후 | `story_ch1_dawn` | 마을 사람들 구출, 서리가 맺힌 수정 조각 → 2장 '얼어붙은 성채'로 이어짐 |

등장인물 그림은 기존 캐릭터 일러스트(`Characters/c_ruru` 등)와 적 그림(`Enemies/어둠군주`)을 그대로 씁니다. 배경 5장은 이미지 스튜디오로 생성했습니다(`node tools/art/generate-art.js --only backgrounds`).

## 새 스토리 추가

`StoryLibrary.Build()`에 이어 씁니다. id는 `ch{챕터}_{번호}_before` / `_after`.

```csharp
Add(new Writer("ch2_1_before", "2-1 설원의 길")
    .Bg("story_ch2_snow")                     // Resources/Art/Backgrounds/story_ch2_snow
    .Title("제2장", "얼어붙은 성채")
    .N("내레이션 한 줄.")
    .L(Ruru, "대사 한 줄."));                 // 인물은 위쪽 Cast 목록에 추가
```

폰트에 `…`(말줄임표) 글자가 없어 대본에서는 `...`을 씁니다.
