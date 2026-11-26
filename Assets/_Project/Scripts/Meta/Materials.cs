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
        StarShard = 10,       // 별의 조각 (풀 초월 캐릭터 중복 → 교환소에서 뽑기권)  영웅
        StaminaSmall = 11,    // 소형 행동력 물약 (행동력 +60)  희귀
        StaminaLarge = 12,    // 행동력 물약 (행동력 +120)      영웅
        Select5 = 13,         // 태생 5★ 캐릭터 선택권           영웅 — 신규 출석 3일째
        Select6 = 14,         // 태생 6★ 캐릭터 선택권(픽업 제외) 전설 — 신규 출석 7일째
        GiftSmall = 15,       // 들꽃 꽃다발 (호감도 +50)       고급 — 상점
        GiftMedium = 16,      // 수제 쿠키 상자 (호감도 +150)   희귀 — 상점
        GiftLarge = 17,       // 별빛 브로치 (호감도 +400)      영웅 — 크리스탈 상점
        SweepTicket = 18,     // 소탕권 (소탕 1회에 1장)         희귀 — 일일 · 주간 임무, 상점
        EquipEssence = 19,    // 장비 정수 (선택 제작)           영웅 — 장비 분해
        RefineStone = 20,     // 재련석 (주옵션 재련)            전설 — 무한의 탑 · 주간 보스 · 크리스탈 상점(주 1)
        Select6All = 21,      // 태생 6★ 캐릭터 선택권(픽업 포함) 전설 — 운영 선물

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
                MaterialType.StarShard => "별의 조각",
                MaterialType.StaminaSmall => "소형 행동력 물약",
                MaterialType.StaminaLarge => "행동력 물약",
                MaterialType.Select5 => "태생 5★ 캐릭터 선택권",
                MaterialType.Select6 => "태생 6★ 캐릭터 선택권",
                MaterialType.Select6All => "태생 6★ 캐릭터 선택권(픽업 포함)",
                MaterialType.GiftSmall => "들꽃 꽃다발",
                MaterialType.GiftMedium => "수제 쿠키 상자",
                MaterialType.GiftLarge => "별빛 브로치",
                MaterialType.SweepTicket => "소탕권",
                MaterialType.EquipEssence => "장비 정수",
                MaterialType.RefineStone => "재련석",
                _ => t.ToString(),
            };
        }

        /// <summary>아이템 등급(3 고급 · 4 희귀 · 5 영웅 · 6 전설).</summary>
        public static int Grade(MaterialType t)
        {
            if (IsAscension(t)) return 3 + TierOf(t);   // 초급 희귀 · 중급 영웅 · 상급 전설
            return t switch
            {
                MaterialType.ExpSmall or MaterialType.SkillBook or MaterialType.GiftSmall => 3,
                MaterialType.GiftMedium or MaterialType.SweepTicket => 4,
                MaterialType.GiftLarge or MaterialType.EquipEssence => 5,
                MaterialType.ExpPotion or MaterialType.SkillBookBlue or MaterialType.EquipOre => 4,
                MaterialType.StaminaSmall => 4,
                MaterialType.ExpLarge or MaterialType.SkillBookPurple or MaterialType.StarShard or MaterialType.StaminaLarge or MaterialType.Select5 => 5,
                _ => 6,   // 경험치 결정 · 전설의 비전서
            };
        }

        /// <summary>가방 분류.</summary>
        public enum Category { Exp, Skill, Ascension, Equipment, Etc }

        public static Category CategoryOf(MaterialType t)
        {
            if (IsAscension(t)) return Category.Ascension;
            if (ExpValue(t) > 0) return Category.Exp;
            if (t == MaterialType.EquipOre || t == MaterialType.EquipEssence || t == MaterialType.RefineStone) return Category.Equipment;
            if (t == MaterialType.StarShard || StaminaValue(t) > 0 || IsSelector(t) || GiftExp(t) > 0 || t == MaterialType.SweepTicket) return Category.Etc;
            return Category.Skill;
        }

        public static string CategoryName(Category c) => c switch
        {
            Category.Exp => "경험치 재료",
            Category.Skill => "스킬 재료",
            Category.Ascension => "돌파 재료",
            Category.Etc => "기타",
            _ => "장비 재료",
        };

        /// <summary>가방에 보여 줄 모든 재료.</summary>
        public static List<MaterialType> All()
        {
            var list = new List<MaterialType>(ExpMaterials)
            {
                MaterialType.SkillBook, MaterialType.SkillBookBlue, MaterialType.SkillBookPurple, MaterialType.SkillLegend,
                MaterialType.EquipOre, MaterialType.EquipEssence, MaterialType.RefineStone, MaterialType.StarShard, MaterialType.StaminaSmall, MaterialType.StaminaLarge, MaterialType.SweepTicket,
                MaterialType.Select5, MaterialType.Select6, MaterialType.Select6All,
                MaterialType.GiftSmall, MaterialType.GiftMedium, MaterialType.GiftLarge,
            };
            foreach (var e in Traits.AllElements)
                for (int tier = 1; tier <= AscensionTiers; tier++) list.Add(Ascension(e, tier));
            return list;
        }

        /// <summary>선물이 주는 호감도(선물이 아니면 0).</summary>
        public static int GiftExp(MaterialType t) => t switch
        {
            MaterialType.GiftSmall => 50,
            MaterialType.GiftMedium => 150,
            MaterialType.GiftLarge => 400,
            _ => 0,
        };

        /// <summary>선물(호감도 작은 순).</summary>
        public static readonly MaterialType[] Gifts = { MaterialType.GiftSmall, MaterialType.GiftMedium, MaterialType.GiftLarge };

        /// <summary>캐릭터 선택권인지.</summary>
        public static bool IsSelector(MaterialType t) => t == MaterialType.Select5 || t == MaterialType.Select6 || t == MaterialType.Select6All;

        /// <summary>선택권으로 고를 수 있는 태생 성급(선택권이 아니면 0).</summary>
        public static int SelectorRarity(MaterialType t) => t switch
        {
            MaterialType.Select5 => 5,
            MaterialType.Select6 or MaterialType.Select6All => 6,
            _ => 0,
        };

        /// <summary>행동력 물약이 주는 행동력(그 외는 0).</summary>
        public static int StaminaValue(MaterialType t) => t switch
        {
            MaterialType.StaminaSmall => 60,
            MaterialType.StaminaLarge => 120,
            _ => 0,
        };

        public static readonly MaterialType[] StaminaPotions = { MaterialType.StaminaSmall, MaterialType.StaminaLarge };

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
                       $"<color=#FFD36B>획득처</color>\n돌파 던전 · {el} {TierName(tier)}" +
                       (tier > 1 ? $"\n상점 교환소 (같은 속성 {TierName(tier - 1)} ×3 → 1)" : "");
            }
            if (t == MaterialType.SweepTicket)
                return head + "<color=#FFD36B>용도</color>\n★3으로 클리어한 스테이지 · 특수 던전을 전투 없이 바로 클리어(소탕 1회에 1장 + 행동력)\n스테이지 상세 → '소탕 1회' · '소탕 N회'\n\n" +
                       "<color=#FFD36B>획득처</color>\n일일 임무 '게임 접속' ×5 · 주간 임무 '전투 20회 승리' ×10\n상점 (일반 상점 · 매일 / 크리스탈 상점 · 매주)\n신규 지휘관 환영 선물";
            int gift = GiftExp(t);
            if (gift > 0)
                return head + $"<color=#FFD36B>용도</color>\n캐릭터에게 선물하면 호감도 <b>+{gift}</b>\n도감 → 캐릭터 → '호감도', 또는 홈의 대표 캐릭터 이름표\n\n" +
                       "<color=#FFD36B>획득처</color>\n" + (t == MaterialType.GiftLarge ? "상점 (크리스탈 상점 · 매주)" : "상점 (일반 상점 · 매일)");
            int exp = ExpValue(t);
            if (exp > 0)
                return head + $"<color=#FFD36B>용도</color>\n캐릭터 경험치 <b>+{exp:N0}</b>\n육성 → 레벨 탭에서 재료를 골라 사용 (금화 필요)\n\n" +
                       "<color=#FFD36B>획득처</color>\n메인 스테이지 · 특수 던전 · 경험치 던전\n" +
                       (t == MaterialType.ExpCrystal ? "상점 (크리스탈 상점)" : "상점 (일반 상점)");
            return head + t switch
            {
                MaterialType.SkillBook => "<color=#FFD36B>용도</color>\n스킬 Lv.1 → 3 (기본 공격·스킬·궁극기 각각)\n\n<color=#FFD36B>획득처</color>\n스킬 교본 던전 1~2단계\n상점 (일반 상점)",
                MaterialType.SkillBookBlue => "<color=#FFD36B>용도</color>\n스킬 Lv.3 → 5\n\n<color=#FFD36B>획득처</color>\n스킬 교본 던전 2단계 이상\n상점 (일반 상점)",
                MaterialType.SkillBookPurple => "<color=#FFD36B>용도</color>\n스킬 Lv.5 → 10\n\n<color=#FFD36B>획득처</color>\n스킬 교본 던전 3단계 이상\n상점 (일반 · 크리스탈 상점)",
                MaterialType.SkillLegend => "<color=#FFD36B>용도</color>\n스킬 Lv.7 → 8 · 9 · 10 (상급 교본과 함께)\n\n<color=#FFD36B>획득처</color>\n상점 (크리스탈 상점 · 교환소: 상급 스킬 교본 ×8 → 1)\n이벤트 (준비 중)",
                MaterialType.StaminaSmall or MaterialType.StaminaLarge =>
                    $"<color=#FFD36B>용도</color>\n사용하면 행동력 +{StaminaValue(t)}\n최대치(240)를 넘어 쌓을 수 있고(최대 999), 넘은 동안은 6분마다 오르는 자연 회복이 멈춥니다.\n가방에서 선택 후 '사용', 또는 스테이지 화면 행동력 옆 '+'\n\n<color=#FFD36B>획득처</color>\n" +
                    (t == MaterialType.StaminaSmall ? "상점 (일반 상점 · 매일 2개)" : "상점 (크리스탈 상점 · 매주 6개)"),
                MaterialType.StarShard => "<color=#FFD36B>용도</color>\n상점 교환소에서 뽑기권으로 교환 (픽업 뽑기권 1장 = 조각 " + StarShards.PickupTicketPrice + "개 · 상시 뽑기권 1장 = " + StarShards.StandardTicketPrice + "개)\n\n<color=#FFD36B>획득처</color>\n이미 풀 초월(6★ +6)한 캐릭터를 다시 뽑으면 자동 변환\n태생 6★ " + StarShards.PerCopy(6) + "개 · 5★ " + StarShards.PerCopy(5) + "개 · 4★ " + StarShards.PerCopy(4) + "개 · 3★ " + StarShards.PerCopy(3) + "개",
                MaterialType.Select5 => "<color=#FFD36B>용도</color>\n태생 5★ 캐릭터 1명을 골라 영입합니다. 이미 가진 캐릭터를 고르면 합성 · 초월에 쓰는 중복이 됩니다.\n가방에서 선택 후 '사용'\n\n<color=#FFD36B>획득처</color>\n신규 지휘관 출석 3일째",
                MaterialType.Select6 => "<color=#FFD36B>용도</color>\n태생 6★ 캐릭터 1명을 골라 영입합니다(지금 픽업 중인 캐릭터는 제외). 이미 가진 캐릭터를 고르면 초월에 쓰는 중복이 됩니다.\n가방에서 선택 후 '사용'\n\n<color=#FFD36B>획득처</color>\n신규 지휘관 출석 7일째",
                MaterialType.Select6All => "<color=#FFD36B>용도</color>\n태생 6★ 캐릭터 1명을 골라 영입합니다. <color=#FFD36B>지금 픽업 중인 캐릭터도 고를 수 있습니다.</color> 이미 가진 캐릭터를 고르면 초월에 쓰는 중복이 됩니다.\n가방에서 선택 후 '사용'\n\n<color=#FFD36B>획득처</color>\n운영 선물",
                MaterialType.EquipEssence => "<color=#FFD36B>용도</color>\n장비 선택 제작 (장비 화면 '제작')\n세트 · 부위 · 주옵션 중 고르는 항목이 많을수록 많이 필요합니다\n\n<color=#FFD36B>획득처</color>\n장비 분해 (6★ 8개 · 5★ 4개 · 4★ 2개 · 3★ 1개, 강화 4단계마다 +1)",
                MaterialType.RefineStone => "<color=#FFD36B>용도</color>\n장비 주옵션 재련 (몸 · 발 · 목걸이 · 반지의 주옵션을 다른 것으로 다시 뽑기)\n\n<color=#FFD36B>획득처</color>\n무한의 탑 25층마다 · 주간 보스 누적 점수 최종 단계 · 크리스탈 상점(주 1개)",
                MaterialType.EquipOre => "<color=#FFD36B>용도</color>\n장비 강화 (금화 필요)\n\n<color=#FFD36B>획득처</color>\n특수 던전 · 장비 던전\n장비 분해 · 상점 (일반 상점)",
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

    /// <summary>
    /// 별의 조각: 이미 풀 초월(6★ +6)한 캐릭터를 또 뽑으면 그 1장이 조각으로 바뀝니다.
    /// 조각은 상점 교환소에서 뽑기권으로 교환(튜닝 지점).
    /// </summary>
    public static class StarShards
    {
        public const int PickupTicketPrice = 20;
        public const int StandardTicketPrice = 15;

        /// <summary>중복 1장당 조각(태생 성급 기준).</summary>
        public static int PerCopy(int nativeStar) => nativeStar switch { >= 6 => 50, 5 => 15, 4 => 5, _ => 2 };

        public static int PerCopy(SubcultureRPG.Characters.Rarity native) => PerCopy((int)native);
    }
}
