using UnityEngine;

namespace SubcultureRPG.Battle
{
    /// <summary>
    /// 전투 수치 튜닝. 메뉴: Create > SubcultureRPG > Battle > Battle Config.
    /// 컨트롤러에 비워 두면 기본값으로 동작합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "BattleConfig", menuName = "SubcultureRPG/Battle/Battle Config")]
    public class BattleConfig : ScriptableObject
    {
        [Header("행동 순서 (Action Value)")]
        [Tooltip("기준 행동치. 각 유닛의 다음 차례 간격 = base / SPD. 클수록 전체 템포가 느려짐.")]
        public float baseActionValue = 10000f;

        [Header("피해 공식")]
        [Tooltip("방어 경감 상수 K. 경감률 = DEF / (DEF + K).")]
        public float defConstant = 200f;

        [Header("에너지 / 필살기")]
        public int maxEnergy = 100;
        [Tooltip("피격 시 얻는 에너지.")]
        public int energyOnHit = 10;

        [Header("스킬 포인트 (팀 공유 — 기본 공격의 가치)")]
        [Tooltip("스킬 포인트 최대치.")]
        public int maxSkillPoints = 5;
        [Tooltip("전투 시작 시 스킬 포인트. 기본 공격은 +1, 스킬은 -1.")]
        public int startSkillPoints = 3;

        [Header("속성 / 브레이크")]
        [Tooltip("약점 적중 피해 배율.")]
        public float weaknessMultiplier = 1.5f;
        [Tooltip("약점 속성 공격이 브레이크 게이지를 깎는 비율(스킬 breakDamage 기준).")]
        public float breakRateWeak = 1f;
        [Tooltip("약점이 아닌 공격이 브레이크 게이지를 깎는 비율. 약점보다 훨씬 작게.")]
        public float breakRateNormal = 0.35f;
        [Tooltip("브레이크 상태 대상이 받는 추가 피해 배율(취약).")]
        public float breakVulnerability = 1.25f;
        [Tooltip("브레이크 순간 추가 피해 배율(그 타격 기준).")]
        public float breakBonus = 0.8f;
        [Tooltip("브레이크 시 대상 다음 행동 지연(행동치 증가).")]
        public float breakActionDelay = 4000f;

        /// <summary>컨트롤러에 Config를 연결하지 않았을 때 쓰는 런타임 기본 인스턴스.</summary>
        public static BattleConfig CreateDefault() => CreateInstance<BattleConfig>();
    }
}
