# 임무 (일일 · 주간)

홈 → **임무**. 위쪽 탭 `일일 임무 · 주간 임무`, 임무마다 진행도 막대 · 보상 · `받기` 버튼, 아래에 `모두 받기`.
**뽑기권(픽업 · 상시)의 정식 획득처**입니다(그 밖에 [도감](codex.md) 수집 보상).

- 받을 수 있는 임무가 위로, 받은 임무는 흐리게 아래로 정렬됩니다.
- 홈의 임무 버튼과 탭에 지금 받을 수 있는 보상 수(`●3`)가 표시됩니다.
- 상단 안내 줄에 다음 초기화까지 남은 시간.

## 초기화

| 주기 | 시각 (로컬) |
|---|---|
| 일일 | 매일 **05:00** |
| 주간 | 매주 **월요일 05:00** |

상점과 같은 시각입니다(`ShopCatalog.ResetHour`). 초기화되면 진행도와 수령 기록이 함께 지워집니다.

## 임무 목록

### 일일

| 임무 | 보상 |
|---|---|
| 게임 접속 | 크리스탈 20 · 소탕권 ×5 |
| 전투 3회 승리 (스테이지 · 던전) | 금화 10,000 |
| 특수 던전 2회 클리어 | 경험치 물약 ×5 |
| 뽑기 1회 | 금화 5,000 |
| 캐릭터 육성 1회 (레벨업 · 돌파 · 스킬) | 초급 스킬 교본 ×3 |
| 상점에서 1회 구매 | 크리스탈 10 |
| **보너스** — 일일 임무 5개 완료 | **상시 뽑기권 ×1** · 크리스탈 30 |

### 주간

| 임무 | 보상 |
|---|---|
| 전투 20회 승리 | 크리스탈 100 · 소탕권 ×10 |
| 특수 던전 10회 클리어 | 상급 스킬 교본 ×3 |
| 뽑기 10회 | 금화 50,000 |
| 캐릭터 육성 10회 | 대형 경험치 물약 ×10 |
| **보너스** — 일일 임무 20개 완료 | **픽업 뽑기권 ×2** |
| **보너스** — 주간 임무 4개 완료 | **상시 뽑기권 ×3** · 크리스탈 100 |

- 한 주에 다 하면: 상시 뽑기권 10장(일일 7 + 주간 3) · 픽업 뽑기권 2장 · 크리스탈 약 550.
- 보너스 임무는 **다른 임무의 보상을 받은 횟수**로 달성됩니다(보너스 임무 자신은 세지 않음). 달성만 하고 받지 않으면 올라가지 않습니다.
- 뽑기는 횟수로 셉니다(10연 = 10회). 전투는 승리만 셉니다.

## 코드

| 파일 | 역할 |
|---|---|
| [MissionCatalog.cs](../Assets/_Project/Scripts/Meta/Missions/MissionCatalog.cs) | 임무 목록 · 보상(튜닝 지점), `MissionGoal` · `MissionEvents` |
| [PlayerMissions.cs](../Assets/_Project/Scripts/Meta/Missions/PlayerMissions.cs) | 진행도 · 수령 · 초기화 주기 · `PlayerPrefs("missions")` 저장 |
| [MissionScreen.cs](../Assets/_Project/Scripts/Meta/UI/MissionScreen.cs) | 임무 화면(코드로 UI 구성) |

### 진행도는 어디서 올라가나

| 목표 (`MissionGoal`) | 알리는 곳 |
|---|---|
| `BattleWin` · `DungeonClear` | `PlayerMissions`가 `StageRunner.OnStageFinished`(승리)를 구독 |
| `GachaPull` | `PlayerMissions`가 `GachaController.OnPulled`를 구독 |
| `Growth` | `GrowthScreen` — 레벨업 · 돌파 · 스킬 레벨업 직후 |
| `ShopBuy` | `PlayerShop.Buy` |
| `DailyClaim` · `WeeklyClaim` | `PlayerMissions.Claim` (보너스 임무용) |
| `Login` | 플레이 중이면 항상 달성 |

새 행동을 임무로 세고 싶으면 `MissionGoal`에 값을 **뒤에** 추가하고, 그 행동이 일어나는 곳에서 한 줄만 호출하면 됩니다:

```csharp
MissionEvents.Report(MissionGoal.ShopBuy);       // 1회
MissionEvents.Report(MissionGoal.GachaPull, 10); // 여러 회
```

### 참고

- 임무 `id`는 세이브 키입니다. 바꾸면 그 임무의 진행 기록이 사라집니다(삭제된 임무의 기록은 불러올 때 버림).
- 테스트: `Meta` 오브젝트의 `PlayerMissions` 우클릭 → `임무 전부 달성 처리 (테스트)` / `임무 기록 초기화`.
- 임무 도입 전에 만든 `Main.unity`도 그대로 동작합니다 — 씬에 `MissionScreen`이 없으면 `ScreenNavigator`가 실행 중에 만들고, `PlayerMissions`가 없으면 지갑 오브젝트에 붙입니다. 메인 씬을 다시 생성하면 둘 다 씬에 들어갑니다.
- 배경은 `Backgrounds/mission` → 없으면 `Backgrounds/party`(편성 화면 그림)를 씁니다.
