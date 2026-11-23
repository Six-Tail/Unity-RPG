using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 육성 재료 종류. 세이브는 정수값으로 저장되므로 기존 값은 바꾸지 말고 뒤에만 추가하세요.
    /// 돌파 재료 = 100 + 속성×10 + 등급(1~3). 등급 1 = 40 돌파, 2 = 60 돌파, 3 = 80 돌파.
    /// </summary>
    public enum MaterialType
    {
        ExpPotion = 0,        // 경험치 물약 (+500)            희귀
        ExpCrystal = 1,       // 경험치 결정 (+5000)           전설
        // 2 = 삭제된 구 공용 돌파석. 값 재사용 금지(옛 세이브는 불러올 때 제거됨)
        SkillBook = 3,        // 초급 스킬 교본 (스킬 Lv.1~3)   고급
        ExpSmall = 4,         // 소형 경험치 물약 (+100)        고급
        ExpLarge = 5,         // 대형 경험치 물약 (+1000)       영웅
        EquipOre = 6,         // 장비 강화석                    희귀
        SkillBookBlue = 7,    // 중급 스킬 교본 (스킬 Lv.3~5)   희귀
        SkillBookPurple = 8,  // 상급 스킬 교본 (스킬 Lv.5~10)  영웅
        SkillLegend = 9,      // 전설의 비전서 (스킬 Lv.8~10)    전설 — 이벤트·상점 교환

        PhysicalAsc1 = 101, PhysicalAsc2 = 102, PhysicalAsc3 = 103,
        FireAsc1 = 111, FireAsc2 = 112, FireAsc3 = 113,
        IceAsc1 = 121, IceAsc2 = 122, IceAsc3 = 123,
        LightningAsc1 = 131, LightningAsc2 = 132, LightningAsc3 = 133,
        WindAsc1 = 141, WindAsc2 = 142, WindAsc3 = 143,
    }

    /// <summary>
    /// 아이템 등급(장비 성급과 공통): 3 고급(초록) · 4 희귀(파랑) · 5 영웅(보라) · 6 전설(금색).
    /// </summary>
    public static class Grades
    {
        public const int Min = 3, Max = 6;

        public static string Name(int grade) => grade switch
        {
            3 => "고급", 4 => "희귀", 5 => "영웅", _ => "전설",
        };

        public static Color ColorOf(int grade) => grade switch
        {
            3 => new Color(0.45f, 0.85f, 0.45f),   // 초록
            4 => new Color(0.40f, 0.65f, 1.00f),   // 파랑
            5 => new Color(0.74f, 0.47f, 1.00f),   // 보라
            _ => new Color(1.00f, 0.80f, 0.28f),   // 금색
        };

        public static string Hex(int grade) => "#" + ColorUtility.ToHtmlStringRGB(ColorOf(grade));

        /// <summary>'[전설]' 같은 등급 태그(등급 색).</summary>
        public static string Tag(int grade) => $"<color={Hex(grade)}>{Name(grade)}</color>";
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
                MaterialType.ExpSmall => "소형 경험치 물약",
                MaterialType.ExpPotion => "경험치 물약",
                MaterialType.ExpLarge => "대형 경험치 물약",
                MaterialType.ExpCrystal => "경험치 결정",
                MaterialType.SkillBook => "초급 스킬 교본",
                MaterialType.SkillBookBlue => "중급 스킬 교본",
                MaterialType.SkillBookPurple => "상급 스킬 교본",
                MaterialType.SkillLegend => "전설의 비전서",
                MaterialType.EquipOre => "장비 강화석",
                _ => t.ToString(),
            };
        }

        /// <summary>아이템 등급(3 고급 · 4 희귀 · 5 영웅 · 6 전설).</summary>
        public static int Grade(MaterialType t)
        {
            if (IsAscension(t)) return 3 + TierOf(t);   // 초급 희귀 · 중급 영웅 · 상급 전설
            return t switch
            {
                MaterialType.ExpSmall or MaterialType.SkillBook => 3,
                MaterialType.ExpPotion or MaterialType.SkillBookBlue or MaterialType.EquipOre => 4,
                MaterialType.ExpLarge or MaterialType.SkillBookPurple => 5,
                _ => 6,   // 경험치 결정 · 전설의 비전서
            };
        }

        /// <summary>가방 분류.</summary>
        public enum Category { Exp, Skill, Ascension, Equipment }

        public static Category CategoryOf(MaterialType t)
        {
            if (IsAscension(t)) return Category.Ascension;
            if (ExpValue(t) > 0) return Category.Exp;
            if (t == MaterialType.EquipOre) return Category.Equipment;
            return Category.Skill;
        }

        public static string CategoryName(Category c) => c switch
        {
            Category.Exp => "경험치 재료",
            Category.Skill => "스킬 재료",
            Category.Ascension => "돌파 재료",
            _ => "장비 재료",
        };

        /// <summary>가방에 보여 줄 모든 재료.</summary>
        public static List<MaterialType> All()
        {
            var list = new List<MaterialType>(ExpMaterials)
            {
                MaterialType.SkillBook, MaterialType.SkillBookBlue, MaterialType.SkillBookPurple, MaterialType.SkillLegend,
                MaterialType.EquipOre,
            };
            foreach (var e in Traits.AllElements)
                for (int tier = 1; tier <= AscensionTiers; tier++) list.Add(Ascension(e, tier));
            return list;
        }

        /// <summary>경험치 재료가 주는 경험치(그 외는 0).</summary>
        public static int ExpValue(MaterialType t) => t switch
        {
            MaterialType.ExpSmall => 100,
            MaterialType.ExpPotion => 500,
            MaterialType.ExpLarge => 1000,
            MaterialType.ExpCrystal => 5000,
            _ => 0,
        };

        /// <summary>경험치 재료(경험치 작은 순).</summary>
        public static readonly MaterialType[] ExpMaterials =
            { MaterialType.ExpSmall, MaterialType.ExpPotion, MaterialType.ExpLarge, MaterialType.ExpCrystal };

        /// <summary>스킬 교본(등급 낮은 순).</summary>
        public static readonly MaterialType[] SkillBooks =
            { MaterialType.SkillBook, MaterialType.SkillBookBlue, MaterialType.SkillBookPurple };

        public static bool IsSkillBook(MaterialType t) =>
            t == MaterialType.SkillBook || t == MaterialType.SkillBookBlue || t == MaterialType.SkillBookPurple;

        /// <summary>짧은 이름(슬롯 표시용).</summary>
        public static string ShortName(MaterialType t) => t switch
        {
            MaterialType.ExpSmall => "소형 물약",
            MaterialType.ExpPotion => "물약",
            MaterialType.ExpLarge => "대형 물약",
            MaterialType.ExpCrystal => "결정",
            MaterialType.SkillBook => "초급 교본",
            MaterialType.SkillBookBlue => "중급 교본",
            MaterialType.SkillBookPurple => "상급 교본",
            MaterialType.SkillLegend => "비전서",
            _ => Name(t),
        };

        /// <summary>가방 상세용 설명(용도 · 획득처).</summary>
        public static string Description(MaterialType t)
        {
            string head = $"{Grades.Tag(Grade(t))} 등급\n\n";
            if (IsAscension(t))
            {
                var e = ElementOf(t);
                int tier = TierOf(t);
                string el = $"<color={Traits.Hex(e)}>{Traits.Name(e)}</color>";
                return head + $"<color=#FFD36B>용도</color>\n{el} 속성 캐릭터의 <b>{TierTargetLevel(tier)} 돌파</b> " +
                       $"(Lv.{TierTargetLevel(tier) - 20} → {TierTargetLevel(tier)})\n\n" +
                       $"<color=#FFD36B>획득처</color>\n돌파 던전 · {el} {TierName(tier)}";
            }
            int exp = ExpValue(t);
            if (exp > 0)
                return head + $"<color=#FFD36B>용도</color>\n캐릭터 경험치 <b>+{exp:N0}</b>\n육성 → 레벨 탭에서 재료를 골라 사용 (금화 필요)\n\n" +
                       "<color=#FFD36B>획득처</color>\n메인 스테이지 · 특수 던전 · 경험치 던전";
            return head + t switch
            {
                MaterialType.SkillBook => "<color=#FFD36B>용도</color>\n스킬 Lv.1 → 3 (기본 공격·스킬·궁극기 각각)\n\n<color=#FFD36B>획득처</color>\n스킬 교본 던전 1~2단계",
                MaterialType.SkillBookBlue => "<color=#FFD36B>용도</color>\n스킬 Lv.3 → 5\n\n<color=#FFD36B>획득처</color>\n스킬 교본 던전 2단계 이상",
                MaterialType.SkillBookPurple => "<color=#FFD36B>용도</color>\n스킬 Lv.5 → 10\n\n<color=#FFD36B>획득처</color>\n스킬 교본 던전 3단계 이상",
                MaterialType.SkillLegend => "<color=#FFD36B>용도</color>\n스킬 Lv.7 → 8 · 9 · 10 (상급 교본과 함께)\n\n<color=#FFD36B>획득처</color>\n이벤트 · 상점 교환 (준비 중)",
                MaterialType.EquipOre => "<color=#FFD36B>용도</color>\n장비 강화 (금화 필요)\n\n<color=#FFD36B>획득처</color>\n특수 던전 · 장비 던전\n장비 분해",
                _ => "",
            };
        }

        // ---------------- 돌파 재료 ----------------

        /// <summary>속성·등급(1~3)의 돌파 재료.</summary>
        public static MaterialType Ascension(Element e, int tier) =>
            (MaterialType)(100 + (int)e * 10 + Mathf.Clamp(tier, 1, AscensionTiers));

        public static bool IsAscension(MaterialType t)
        {
            int v = (int)t;
            return v > 100 && v % 10 >= 1 && v % 10 <= AscensionTiers && (v - 100) / 10 <= (int)Element.Wind;
        }

        public static Element ElementOf(MaterialType t) => (Element)(((int)t - 100) / 10);
        public static int TierOf(MaterialType t) => (int)t % 10;

        /// <summary>돌파 단계(현재 ascension 0~2) → 필요한 재료 등급(1~3).</summary>
        public static int TierForAscension(int ascension) => Mathf.Clamp(ascension + 1, 1, AscensionTiers);

        /// <summary>등급 표시(초급/중급/상급).</summary>
        public static string TierName(int tier) => tier switch { 1 => "초급", 2 => "중급", 3 => "상급", _ => "?" };

        /// <summary>등급이 쓰이는 돌파(40/60/80).</summary>
        public static int TierTargetLevel(int tier) => 20 + 20 * Mathf.Clamp(tier, 1, AscensionTiers);

        /// <summary>등급 색 재료 이름(리치 텍스트).</summary>
        public static string RichName(MaterialType t) => $"<color={Grades.Hex(Grade(t))}>{Name(t)}</color>";
    }
}
