# 전투 UI (HUD)

위치: `Assets/_Project/Scripts/Battle/UI/` (+ 공용 `Assets/_Project/Scripts/UI/UIFactory.cs`)

`BattleController`의 이벤트에 붙어 전투 화면을 코드로 구성합니다. 프리팹/씬 배치 불필요 — 컴포넌트 하나가 Canvas·패널·바·버튼을 만듭니다.

## 구성

| 파일 | 역할 |
|---|---|
| `BattleHUD` (MonoBehaviour) | 전투 화면 전체 — 팀 배치·턴 순서 바·스킬/대상 입력·피해 숫자·승패 |
| `UnitPanel` (MonoBehaviour) | 유닛 1명 패널 — 이름·HP 바·에너지 바·현재 턴/대상 하이라이트·격파 |
| `UIFactory` (`SubcultureRPG.UI`) | 코드 uGUI 공용 헬퍼(가챠 UI와 별개의 공용 버전) |

## 세팅 (2단계)

1. 빈 GameObject에 **BattleHUD** 추가 → `controller`에 `BattleController` 연결(비우면 자동 탐색), `font`에 CJK TMP 폰트.
2. 플레이 → `BattleController`가 전투를 시작하면 HUD가 자동으로 그려집니다.

(가챠 UI와 동일하게 TMP Essentials 임포트 + 한글 폰트가 필요합니다. `docs/gacha-ui.md` 참고.)

## 조작

- **내 차례**: 하단에 스킬 버튼이 나타납니다. 스킬 클릭 →
  - 단일 대상: 적이 여럿이면 "대상을 선택하세요" → 적 패널 클릭. 적이 하나면 자동 발동.
  - 전체 대상(AllEnemies): 즉시 발동.
  - 필살기: 에너지가 가득 찼을 때만 활성화.
  - **스킬 포인트(SP)**: 스킬은 SP 1 소모, 기본 공격은 SP +1. SP가 0이면 스킬 버튼이 비활성화됩니다(화면 하단 'SP 표시' 참고). 그래서 기본 공격으로 SP를 적립하는 운영이 필요합니다.
- **적/자동 턴**: 스스로 진행.
- **자동 전투 / 배속** (우상단 버튼, 설정은 다음 전투에도 유지):
  - `자동 ON/OFF`: 내 턴도 AI가 진행. 내 차례에 켜면 그 턴부터 바로 자동.
  - `배속 ×1/×2`: 턴 간 대기 시간을 줄임. 허브에서는 **챕터 1 클리어 시 해금**(잠김 상태에서 누르면 해금 조건 안내). 독립 전투 씬은 처음부터 사용 가능.
  - 아군 AI(`BattleEngine.PickAutoAction`): HP 50% 미만 아군 회복 → 필살기 → 스킬(SP 2 이상, 또는 약점·버프처럼 확실히 이득일 때) → 기본 공격. 대상은 약점(미브레이크) > 마무리 가능 > 낮은 체력 순.
- **턴 순서 바**(상단): 다가올 행동 순서. 맨 앞이 다음 차례(밝게 표시).
- **승패**: 종료 시 결과 화면 + `다시` 버튼으로 재시작.

## 연출

- HP 바는 부드럽게 감소(색: 초록 → 노랑 → 빨강), 에너지 바는 가득 차면 금색.
- 피격 시 대상 위로 피해 숫자가 떠오릅니다(치명은 금색·크게·`!`).
- 현재 행동 유닛은 금색, 선택 가능한 대상은 붉은 테두리.
- 연출은 `Time.unscaledDeltaTime` 기반이라 일시정지 중에도 재생됩니다.

## 튜닝 (인스펙터)

| 필드 | 뜻 |
|---|---|
| `drawBackground` | 독립 테스트용 어두운 배경(실제 전투 배경이 있으면 끄기) |
| `turnPreviewCount` | 턴 순서 바에 표시할 개수 |

## 확장 포인트

- **포트레이트/배경**: `UnitPanel`은 `CharacterData.thumbnail`을 표시합니다. 전투 배경 아트는 `drawBackground`를 끄고 뒤에 배치.
- **스킬 설명/쿨다운/코스트 표시**: `BuildSkillButtons`에서 버튼에 추가.
- **대형 연출(필살기 컷인 등)**: `HandleResolved`에서 스킬별로 분기해 연결.
- **6성 Live2D 등장**: `CharacterViewFactory`로 모델을 띄워 전투 포트레이트를 대체 가능.
