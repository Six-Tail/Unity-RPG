using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 금화 비용 규칙(튜닝 지점). 금화는 레벨업·돌파·스킬·합성/초월(이후 장비 강화 등)에 반드시 드는 공용 재화.
    /// 획득: 메인 스테이지·돌파 던전 클리어마다.
    /// </summary>
    public static class Economy
    {
        /// <summary>경험치 재료 1개 사용 비용(경험치 5당 금화 1).</summary>
        public static int LevelUpGold(MaterialType expMaterial) => Materials.ExpValue(expMaterial) / 5;

        private static readonly int[] AscGold = { 5000, 15000, 30000 };       // 40 / 60 / 80 돌파
        private static readonly int[] AscCount = { 5, 8, 12 };                // 돌파 재료 개수

        /// <summary>이번 돌파(현재 ascension 0~2)의 금화.</summary>
        public static int AscensionGold(int ascension) => AscGold[Mathf.Clamp(ascension, 0, AscGold.Length - 1)];

        /// <summary>이번 돌파의 돌파 재료 개수.</summary>
        public static int AscensionMaterialCount(int ascension) => AscCount[Mathf.Clamp(ascension, 0, AscCount.Length - 1)];

        /// <summary>이번 돌파에 필요한 재료 종류(캐릭터 속성 × 등급).</summary>
        public static MaterialType AscensionMaterial(Element element, int ascension) =>
            Materials.Ascension(element, Materials.TierForAscension(ascension));

        /// <summary>스킬 레벨업(현재 레벨 → +1) 금화.</summary>
        public static int SkillUpGold(int skillLevel) => 1500 * Mathf.Max(1, skillLevel);

        /// <summary>합성(성급 +1) 또는 초월(+1) 금화. star = 현재 성급, transcend = 현재 초월.</summary>
        public static int MergeGold(int star, int transcend)
        {
            if (star < Transcendence.MaxStar)
                return star switch { 3 => 10000, 4 => 20000, _ => 40000 };   // → 4★ / 5★ / 6★
            return 30000 + 10000 * Mathf.Clamp(transcend, 0, Transcendence.MaxTranscend); // +1: 3만 … +6: 8만
        }
    }
}
