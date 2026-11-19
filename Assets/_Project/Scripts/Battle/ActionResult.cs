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

        /// <summary>대상 1명이 받은 결과.</summary>
        public struct TargetHit
        {
            public BattleUnit target;
            public int totalDamage;
            public bool anyCrit;
            public bool defeated;
        }
    }
}
