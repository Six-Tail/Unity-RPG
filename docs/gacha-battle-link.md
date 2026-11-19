# 가챠 ↔ 전투 연동

위치: `Assets/_Project/Scripts/Meta/` (+ `Battle/RuntimeUnits.cs`)

"뽑기 → 보유 → 파티 편성 → 전투"의 전체 루프를 잇습니다.

## 구성

| 파일 | 역할 |
|---|---|
| `PlayerRoster` (MonoBehaviour) | 보유 캐릭터. 가챠 결과(`OnPulled`)를 받아 자동 보유 + PlayerPrefs 저장 |
| `GameDatabase` (SO) | id→캐릭터, 캐릭터→전투유닛 조회. *Create > SubcultureRPG > Meta > Game Database* |
| `BattleLauncher` (MonoBehaviour) | 선택 파티 → 전투유닛 변환 → `BattleController` 시작 |
| `RuntimeUnits` (static) | 캐릭터→전투유닛. 저작 유닛 없으면 성급 기반 자동 생성(기본/스킬/필살기 포함) |
| `PartyScreen` (MonoBehaviour) | 보유 캐릭터 카드 목록 → 최대 N명 선택 → 전투 시작 |
| `BattleController.SetTeams` | 외부에서 팀 지정 후 전투 시작(연동 진입점) |

## 루프

1. **뽑기**: `GachaController.PullOnce/PullMulti` → `OnPulled` → `PlayerRoster`가 보유 목록에 추가(저장).
2. **보유**: 로스터는 캐릭터 id·중복 수·레벨을 PlayerPrefs(JSON)에 저장.
3. **편성**: `PartyScreen`이 보유 캐릭터를 카드로 표시 → 최대 N명 선택.
4. **전투**: `BattleLauncher.Launch(파티)` → 캐릭터를 `BattleUnitData`로 변환(DB 저작 우선, 없으면 성급 기반 생성) → `BattleController.SetTeams(아군, 적)` → 전투 시작.

## 뽑은 캐릭터가 바로 싸우는 이유

`RuntimeUnits.Generate`가 **성급으로 스탯을 스케일**(3성=1.0 … 6성≈2.05배)하고 기본/스킬/필살기를 붙여 전투 유닛을 즉석에서 만듭니다. 그래서 스탯을 일일이 저작하지 않아도 모든 캐릭터가 전투 가능합니다.
- 캐릭터별 고유 스탯/스킬을 쓰고 싶으면 `BattleUnitData`를 만들어 `GameDatabase.units`에 넣으면 그게 우선 적용됩니다.

## 빠른 체험

메뉴 **`SubcultureRPG > Demo > 가챠↔전투 연동 데모 생성`** → `LinkDemo` 씬 자동 생성 → ▶ Play.
- 테스트용으로 보유 캐릭터 6종이 지급되어 파티 편성 화면이 바로 뜹니다.
- 캐릭터를 골라 **전투 시작** → 그 캐릭터들이 적과 싸웁니다.

## 실제 연결 (수동 세팅)

1. `GameDatabase` 에셋 생성 → `characters`에 가챠 `CharacterData`들 등록.
2. 씬에 배치: `PlayerRoster`(database 연결, gacha 자동 탐색), `BattleController`(**startOnPlay 끄기**), `BattleLauncher`(controller/database/enemyTeam), `BattleHUD`, `PartyScreen`(roster/database/launcher).
3. 가챠로 뽑으면 로스터가 차고, `PartyScreen`에서 편성해 전투를 시작합니다.

## 확장 포인트

- **레벨/육성**: `OwnedCharacter.level`을 올려 `BattleUnitData.level` 성장(스탯 성장)과 연결.
- **중복 처리**: 중복 수(`count`)를 돌파/각성 재화로.
- **스테이지**: `BattleLauncher.enemyTeam`을 스테이지 데이터로 교체해 여러 전투 구성.
- **전투 종료 후**: 결과를 받아 파티 화면으로 복귀하거나 보상을 로스터/재화에 반영.
