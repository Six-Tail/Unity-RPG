using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 육성 재료 종류. 세이브는 정수값으로 저장되므로 기존 값은 바꾸지 말고 뒤에만 추가하세요.
    /// 돌파 재료 = 100 + 속성×10 + 등급(1~3). 등급 1 = 40 돌파, 2 = 60 돌파, 3 = 80 돌파.
    /// </summary>
    public enum MaterialType
    {
        ExpPotion = 0,      // 경험치 물약 (레벨업)
        ExpCrystal = 1,     // 경험치 결정 (레벨업, 대용량)
        AscensionStone = 2, // (구) 공용 돌파석 — 더 이상 쓰이지 않음. 기존 세이브 호환용
        SkillBook = 3,      // 스킬 교본 (스킬 레벨업)

        PhysicalAsc1 = 101, PhysicalAsc2 = 102, PhysicalAsc3 = 103,
        FireAsc1 = 111, FireAsc2 = 112, FireAsc3 = 113,
        IceAsc1 = 121, IceAsc2 = 122, IceAsc3 = 123,
        LightningAsc1 = 131, LightningAsc2 = 132, LightningAsc3 = 133,
        WindAsc1 = 141, WindAsc2 = 142, WindAsc3 = 143,
    }

    public static class Materials
    {
        public const int AscensionTiers = 3;

        // [속성, 등급-1] — 등급마다 이름이 다른 별도 재료
        private static readonly string[,] AscNames =
        {
            { "단련의 파편", "단련의 결정", "단련의 핵" },   // 물리
            { "불씨 조각", "화염 결정", "홍염의 핵" },       // 화염
            { "서리 조각", "빙결 결정", "영구동토의 핵" },   // 얼음
            { "전하 조각", "뇌광 결정", "천둥의 핵" },       // 번개
            { "바람 조각", "질풍 결정", "폭풍의 핵" },       // 바람
        };

        public static string Name(MaterialType t)
        {
            if (IsAscension(t)) return AscNames[(int)ElementOf(t), TierOf(t) - 1];
            return t switch
            {
                MaterialType.ExpPotion => "경험치 물약",
                MaterialType.ExpCrystal => "경험치 결정",
                MaterialType.AscensionStone => "구 돌파석",
                MaterialType.SkillBook => "스킬 교본",
                _ => t.ToString(),
            };
        }

        /// <summary>경험치 재료가 주는 경험치(그 외는 0).</summary>
        public static int ExpValue(MaterialType t) => t switch
        {
            MaterialType.ExpPotion => 500,
            MaterialType.ExpCrystal => 2500,
            _ => 0,
        };

        // ---------------- 돌파 재료 ----------------

        /// <summary>속성·등급(1~3)의 돌파 재료.</summary>
        public static MaterialType Ascension(Element e, int tier) =>
            (MaterialType)(100 + (int)e * 10 + UnityEngine.Mathf.Clamp(tier, 1, AscensionTiers));

        public static bool IsAscension(MaterialType t)
        {
            int v = (int)t;
            return v > 100 && v % 10 >= 1 && v % 10 <= AscensionTiers && (v - 100) / 10 <= (int)Element.Wind;
        }

        public static Element ElementOf(MaterialType t) => (Element)(((int)t - 100) / 10);
        public static int TierOf(MaterialType t) => (int)t % 10;

        /// <summary>돌파 단계(현재 ascension 0~2) → 필요한 재료 등급(1~3).</summary>
        public static int TierForAscension(int ascension) => UnityEngine.Mathf.Clamp(ascension + 1, 1, AscensionTiers);

        /// <summary>등급 표시(초급/중급/상급).</summary>
        public static string TierName(int tier) => tier switch { 1 => "초급", 2 => "중급", 3 => "상급", _ => "?" };

        /// <summary>등급이 쓰이는 돌파(40/60/80).</summary>
        public static int TierTargetLevel(int tier) => 20 + 20 * UnityEngine.Mathf.Clamp(tier, 1, AscensionTiers);

        /// <summary>속성 색이 들어간 재료 이름(리치 텍스트).</summary>
        public static string RichName(MaterialType t) =>
            IsAscension(t) ? $"<color={Traits.Hex(ElementOf(t))}>{Name(t)}</color>" : Name(t);
    }
}
