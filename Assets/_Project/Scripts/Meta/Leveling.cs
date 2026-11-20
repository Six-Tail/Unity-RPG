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

        /// <summary>이번 돌파에 필요한 돌파석 수.</summary>
        public static int AscensionCost(int ascension) =>
            2 * (Mathf.Clamp(ascension, 0, MaxAscension) + 1); // 2 / 4 / 6 / 8

        /// <summary>이번 스킬 레벨업에 필요한 스킬 교본 수.</summary>
        public static int SkillUpgradeCost(int skillLevel) => Mathf.Max(1, skillLevel);
    }
}
