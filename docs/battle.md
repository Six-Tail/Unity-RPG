# 턴제 전투 시스템

위치: `Assets/_Project/Scripts/Battle/`

붕괴 스타레일 계열의 **속도 기반(Action Value) 턴제 전투**. 가챠처럼 순수 로직 엔진과 MonoBehaviour 컨트롤러를 분리해, 테스트가 쉽고 UI가 이벤트로 붙습니다.

## 구성

| 파일 | 역할 |
|---|---|
| `BattleEngine` | 순수 전투 엔진 — 행동 순서·행동 처리·AI·승패. RNG 주입 가능 |
| `BattleController` (MonoBehaviour) | 씬 진입점 — 팀 세팅, 턴 루프, 이벤트, 플레이어 입력, 자동 전투 |
| `BattleUnit` | 전투 중 유닛의 런타임 상태(HP/에너지/행동치) |
| `BattleUnitData` (SO) | 유닛 정의 = 가챠 캐릭터 + 스탯 + 스킬. *Create > SubcultureRPG > Battle > Unit* |
| `BattleStats` | 기본 스탯(HP/ATK/DEF/SPD/치명 + 레벨 성장) |
| `BattleSkill` (SO) | 스킬. *Create > SubcultureRPG > Battle > Skill* |
| `BattleConfig` (SO) | 수치 튜닝. *Create > SubcultureRPG > Battle > Battle Config* |
| `DamageCalculator` | 피해 공식(순수) |
| `ActionResult` | 행동 결과 스냅샷(UI/로그) |

## 핵심 규칙

- **행동 순서**: 각 유닛의 다음 차례 간격 = `baseActionValue / SPD`. SPD가 높을수록 자주 행동(HSR식). 가장 행동치가 낮은 유닛이 먼저 행동.
- **피해**: `유효ATK × power × (1 - 유효DEF/(유효DEF+K)) × (치명 시 1+critDmg)`, 최소 1. `K`는 `BattleConfig.defConstant`. 유효ATK/DEF는 버프/디버프 반영.
- **속성 / 약점 / 브레이크**: 스킬엔 속성, 적엔 약점 속성 + 내구도가 있습니다. 약점으로 때리면 추가 피해(`weaknessMultiplier`). **모든 공격이 브레이크 게이지(내구도)를 깎고**, 약점 속성이면 훨씬 많이 깎습니다(스킬 `breakDamage` × `breakRateWeak` 1.0 / 약점 아님 × `breakRateNormal` 0.35) → 0이 되면 **브레이크**(추가 피해 + 다음 행동 지연 + 브레이크 동안 받는 피해 증가 `breakVulnerability`).
- **버프 / 디버프 / 도트**: 스킬이 ATK/DEF 증감·지속 피해(도트)를 부여. 매 턴 시작에 도트가 들어오고 지속시간이 줄어듭니다.
- **힐**: 아군 대상 스킬의 `heal`(시전자 ATK 배율)로 회복.
- **에너지/필살기**: 기본·스킬 사용 시 에너지 획득, 피격 시에도 획득. 에너지가 가득 차면 필살기 사용 가능(사용 시 0으로 소모).
- **스킬 포인트(팀 공유)**: 기본 공격은 +1, 스킬은 -1. SP가 0이면 스킬을 못 쓰므로 기본 공격으로 적립하는 운영이 필요합니다(기본 ↔ 스킬 순환). 시작 3 / 최대 5 (`BattleConfig`). 필살기는 SP와 무관.
- **캐릭터별 스킬 · 패시브**: 다단 히트 · 확산 · 연타, 도발 · 기절 · 재생 · 속도/치명 버프, 발동형 패시브(추가 행동 · 추가 공격 · 반격 등) → [character-kits.md](character-kits.md).
- **승패**: 한 팀이 전멸하면 종료.

## 세팅

1. **스킬** 에셋 생성 — *Create > SubcultureRPG > Battle > Skill*. 기본 공격 1개는 꼭 만들기(`category = Basic`).
2. **유닛** 에셋 생성 — *Battle > Unit*. `character`에 가챠 `CharacterData` 연결(선택), `baseStats` 입력, `skills`에 스킬 연결(`[0]`=기본 공격 권장).
3. 빈 GameObject에 **BattleController** 추가 → `allyTeam` / `enemyTeam`에 유닛 넣기.
4. 플레이 → 자동 시작. 인스펙터에서 컴포넌트 우클릭 → **"자동 전투 테스트(로그)"** 로 콘솔에서 바로 확인.

## 코드 (플레이어 입력 연결)

```csharp
[SerializeField] BattleController battle;

void OnEnable()
{
    battle.OnPlayerInputNeeded += ShowSkillUI;   // 내 차례 → 스킬/대상 UI 표시
    battle.OnActionResolved   += PlayHitFx;      // 피해 연출
    battle.OnBattleFinishing  += PlayFinish;     // 승패 직후 마무리 연출(FinishDelay 실시간 초만큼 결과를 늦춤)
    battle.OnBattleEnd        += ShowResult;     // 승/패 화면
}

// 버튼에서:
void OnClickSkill(BattleSkill skill, BattleUnit target) => battle.SubmitAction(skill, target);
void OnClickBasic() => battle.SubmitBasicAttack();
```

- `AutoBattle`을 켜면 플레이어 턴도 아군 AI(`PickAutoAction`)가 진행. 적은 기존 `PickAction`.
- `SetSpeed(배속)`: 턴 간 대기 시간에 배속 적용(`Time.timeScale` 일시정지는 그대로 존중).
- `BattleEngine.PreviewOrder(n)` 으로 다가올 턴 순서(턴 바)를 미리 볼 수 있습니다.

## 테스트 팁

`BattleEngine`에 고정 시드 RNG를 주입하면 결정적 검증이 가능합니다.

```csharp
var seeded = new System.Random(12345);
var engine = new BattleEngine(allies, enemies, config, () => (float)seeded.NextDouble());
while (!engine.IsOver)
{
    var actor = engine.NextActor();
    var (skill, target) = engine.PickAction(actor);  // 양 팀 모두 AI로
    engine.ResolveAction(actor, skill, target);
}
Debug.Log($"승리: {engine.Winner}");
```

## 확장 포인트

- **아군 대상 / 힐 / 버프**: `TargetType`에 아군 대상을 추가하고 `ResolveAction`에 회복·상태이상 분기를 더하면 됩니다.
- **속성 / 약점**: HSR식 속성·약점 격파를 `DamageCalculator`와 `BattleUnit`에 추가.
- **가챠 연동**: 보유 캐릭터(`CharacterData`) → `BattleUnitData` 매핑으로 '뽑은 캐릭터가 출전'.
- **전투 UI**: 가챠 결과 UI처럼 HP/에너지 바·스킬 버튼·턴 순서 바를 위 이벤트에 붙이면 됩니다.
