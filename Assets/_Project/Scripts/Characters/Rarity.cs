namespace SubcultureRPG.Characters
{
    /// <summary>캐릭터 성급(희귀도).</summary>
    public enum Rarity
    {
        R3 = 3,
        R4 = 4,
        R5 = 5,
        R6 = 6,
    }

    public static class RarityExtensions
    {
        /// <summary>
        /// 6성만 Live2D, 5성 이하는 정적 일러스트.
        /// 표시 방식 분기의 '단일 진실 공급원(single source of truth)'.
        /// 나중에 기준을 바꾸고 싶으면 여기 한 곳만 고치면 됩니다.
        /// </summary>
        public static bool UsesLive2D(this Rarity rarity) => rarity >= Rarity.R6;
    }
}
