namespace SubcultureRPG.Meta
{
    /// <summary>육성 재료 종류.</summary>
    public enum MaterialType
    {
        ExpPotion,      // 경험치 물약 (레벨업)
        ExpCrystal,     // 경험치 결정 (레벨업, 대용량)
        AscensionStone, // 돌파석 (레벨 상한 돌파)
        SkillBook,      // 스킬 교본 (스킬 레벨업)
    }

    public static class Materials
    {
        public static string Name(MaterialType t) => t switch
        {
            MaterialType.ExpPotion => "경험치 물약",
            MaterialType.ExpCrystal => "경험치 결정",
            MaterialType.AscensionStone => "돌파석",
            MaterialType.SkillBook => "스킬 교본",
            _ => t.ToString(),
        };

        /// <summary>경험치 재료가 주는 경험치(그 외는 0).</summary>
        public static int ExpValue(MaterialType t) => t switch
        {
            MaterialType.ExpPotion => 500,
            MaterialType.ExpCrystal => 2500,
            _ => 0,
        };
    }
}
