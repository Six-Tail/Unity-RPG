using UnityEngine;

namespace SubcultureRPG.Meta
{
    /// <summary>레벨/돌파/스킬 규칙(튜닝 지점).</summary>
    public static class Leveling
    {
        public const int MaxLevel = 80;
        public const int MaxAscension = 3;   // 돌파 0~3 → 레벨 상한 20/40/60/80
        public const int MaxSkillLevel = 10;

        /// <summary>돌파 단계별 레벨 상한.</summary>
        public static int LevelCap(int ascension) =>
            Mathf.Min(MaxLevel, 20 + 20 * Mathf.Clamp(ascension, 0, MaxAscension));

        /// <summary>level → level+1 에 필요한 경험치.</summary>
        public static int ExpForNext(int level) =>
            level >= MaxLevel ? int.MaxValue : 100 * Mathf.Max(1, level);

        // 돌파 비용(속성별 돌파 재료 + 금화)은 Economy 참고.

        /// <summary>
        /// 레벨업 금화: 경험치 1당 금화. 레벨이 높을수록 비쌈.
        /// Lv.1 0.68 · Lv.20 1.3 · Lv.40 1.95 · Lv.60 2.6 · Lv.79 3.22
        /// 누적: Lv.20까지 약 2만 · 40까지 +9.7만 · 60까지 +22.6만 · 80까지 +40.6만 = 총 약 75만 금화
        /// </summary>
        public static float GoldPerExp(int level) => GoldPerExpBase * (1f + Mathf.Max(1, level) / 20f);

        /// <summary>레벨업 금화 기본 단가(이 값 하나로 전체 레벨업 금화가 비례해서 바뀜).</summary>
        public const float GoldPerExpBase = 0.65f;

        /// <summary>경험치 투입 결과 미리보기.</summary>
        public struct LevelPreview
        {
            public int level, exp;       // 결과 레벨 / 남은 경험치
            public int usedExp;          // 실제로 들어간 경험치(상한 초과분 제외)
            public int wastedExp;        // 상한 초과로 버려지는 경험치
            public int gold;             // 필요한 금화
            public bool atCap;           // 결과가 레벨 상한
        }

        /// <summary>level/exp 상태에서 addExp를 넣으면? (상한 cap까지만, 금화는 들어간 경험치만큼 레벨별 단가로)</summary>
        public static LevelPreview Preview(int level, int exp, int cap, int addExp)
        {
            double gold = 0;
            int used = 0, add = Mathf.Max(0, addExp);
            while (add > 0 && level < cap)
            {
                int need = ExpForNext(level) - exp;
                int take = Mathf.Min(need, add);
                gold += take * (double)GoldPerExp(level);
                exp += take; add -= take; used += take;
                if (exp >= ExpForNext(level)) { exp = 0; level++; }
            }
            if (level >= cap) exp = 0;
            return new LevelPreview
            {
                level = level, exp = exp, usedExp = used, wastedExp = add,
                gold = (int)System.Math.Ceiling(gold - 1e-6), atCap = level >= cap,
            };
        }

        /// <summary>현재 상태에서 상한까지 필요한 경험치.</summary>
        public static int ExpToCap(int level, int exp, int cap)
        {
            long sum = 0;
            for (int l = level; l < cap; l++) sum += ExpForNext(l);
            return (int)Mathf.Max(0, Mathf.Min(int.MaxValue, sum - exp));
        }

        /// <summary>이번 스킬 레벨업에 필요한 스킬 교본 수.</summary>
        public static int SkillUpgradeCost(int skillLevel) => Mathf.Max(1, skillLevel);
    }
}
