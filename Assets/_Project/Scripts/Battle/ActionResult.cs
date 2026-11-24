using System.Collections.Generic;

namespace SubcultureRPG.Battle
{
    /// <summary>한 번의 행동 결과(UI/로그용 스냅샷).</summary>
    public struct ActionResult
    {
        public BattleUnit actor;
        public BattleSkill skill;
        public List<TargetHit> hits;   // 대상별 합계
        public int energyAfter;        // 행동 후 시전자 에너지
        public int recast;             // 연쇄 시전 순번(0 = 일반 행동, 1 = 첫 재시전 …)
        public float recastMul;        // 연쇄 시전 피해 배율(1 = 감소 없음)

        /// <summary>대상 1명이 받은 결과.</summary>
        public struct TargetHit
        {
            public BattleUnit target;
            public int totalDamage;
            public bool anyCrit;
            public bool defeated;
            public int healed;              // 회복량(>0이면 힐)
            public bool weakness;           // 약점 적중
            public bool broke;              // 이 타격으로 브레이크
            public StatusType appliedEffect;// 부여된 효과(None이면 없음)
            public List<int> hitDamages;    // 타격별 피해(다단 히트 연출용, 브레이크 추가 피해 제외)
            public List<bool> hitCrits;     // 타격별 치명 여부
            public int breakBonusDamage;    // 브레이크 순간 추가 피해(0이면 없음)
            public bool extra;              // 본 행동이 아닌 부가 결과(추가 공격·반격·흡혈·패시브 회복)
            public string label;            // 부가 결과 머리말('추가 공격' · '반격' · '흡혈' …)
            public BattleUnit source;       // 부가 결과를 일으킨 유닛
        }
    }
}
