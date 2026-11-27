using System;
using System.Collections.Generic;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta.Shop
{
    /// <summary>상점 탭.</summary>
    public enum ShopTab
    {
        General,   // 일반 상점 — 금화, 매일 초기화
        Crystal,   // 크리스탈 상점 — 크리스탈, 매주 초기화
        Exchange,  // 교환소 — 재료 → 재료
        Guild,     // 길드 상점 — 길드 코인, 매주 초기화(길드 가입해야)
        Package,   // 패키지 — 출시 때 인앱 결제, 지금은 크리스탈로 임시 판매
        Monthly,   // 월정액 — MonthlyPass(30일 동안 매일 우편)
    }

    /// <summary>값을 치르는 수단.</summary>
    public enum ShopCurrency { Gold, Crystal, Material }

    /// <summary>구매 제한 초기화 주기.</summary>
    public enum ShopReset { None, Daily, Weekly, Monthly }

    /// <summary>
    /// 상점 상품 1개. 보상은 재료(reward × amount) 또는 금화(rewardGold × amount).
    /// id는 세이브 키이므로 한 번 정하면 바꾸지 마세요.
    /// </summary>
    public sealed class ShopItem
    {
        public string id;
        public ShopTab tab;

        public MaterialType reward;
        public int rewardGold;          // > 0 이면 금화 상품(reward 무시)
        public bool rewardTicket;       // 뽑기권 상품(reward 무시)
        public SubcultureRPG.Gacha.GachaBannerType ticketType;
        public int amount = 1;          // 1회 구매당 지급 수

        public ShopCurrency currency;
        public MaterialType costMaterial;   // currency == Material 일 때
        public int price;                   // 1회 구매당 가격

        public int limit;               // 0 = 무제한
        public ShopReset reset;

        // 패키지(묶음 상품)
        public string title;                 // 상품 이름(묶음)
        public Func<RewardBundle> bundle;    // 1회 구매로 받는 묶음
        public string icon;                  // 아이콘 경로(있으면 우선)
        /// <summary>출시 때 인앱 결제 가격(예: "₩5,900"). 비어 있지 않으면 지금은 크리스탈로 임시 판매 중이라는 표시.</summary>
        public string realPrice;

        public bool IsBundle => bundle != null;
        public bool IsIap => !string.IsNullOrEmpty(realPrice);
        public bool IsGold => !IsBundle && rewardGold > 0;
        public bool IsTicket => !IsBundle && rewardTicket;
        public int Grade => IsBundle ? 6 : IsTicket ? 6 : IsGold ? 4 : Materials.Grade(reward);

        public string RewardName => IsBundle ? title : IsTicket ? PlayerWallet.TicketName(ticketType) : IsGold ? $"금화 {rewardGold:N0}" : Materials.Name(reward);
        public string RewardRichName => IsBundle ? $"<color=#FFD98A>{title}</color>" : IsTicket
            ? $"<color={(ticketType == SubcultureRPG.Gacha.GachaBannerType.Pickup ? "#FFB3E6" : "#9FD8FF")}>{PlayerWallet.TicketName(ticketType)}</color>"
            : IsGold ? $"<color=#FFD36B>금화 {rewardGold:N0}</color>" : Materials.RichName(reward);

        /// <summary>아이콘 그림 경로(Resources/Art 기준).</summary>
        public string IconPath => !string.IsNullOrEmpty(icon) ? icon : IsTicket
            ? (ticketType == SubcultureRPG.Gacha.GachaBannerType.Pickup ? "Items/PickupTicket" : "Items/StandardTicket")
            : IsGold ? "Items/Gold" : "Items/" + reward;

        /// <summary>qty회 구매로 받는 것(예: '픽업 뽑기권 ×2').</summary>
        public string GotText(int qty) => IsBundle
            ? (qty > 1 ? $"{RewardRichName} ×{qty}" : RewardRichName)
            : IsGold
            ? $"<color=#FFD36B>금화 {(long)rewardGold * amount * qty:N0}</color>"
            : $"{RewardRichName} ×{(long)amount * qty:N0}";

        /// <summary>묶음 내용(패키지).</summary>
        public string BundleText(string sep = "  ·  ") => IsBundle ? bundle().Describe(sep) : "";

        /// <summary>가격 표시(예: '금화 4,000', '크리스탈 120', '상급 스킬 교본 ×8').</summary>
        public string PriceText(int qty = 1)
        {
            long total = (long)price * qty;
            return currency switch
            {
                ShopCurrency.Gold => $"<color=#FFD36B>금화 {total:N0}</color>",
                ShopCurrency.Crystal => $"<color=#8FE9FF>크리스탈 {total:N0}</color>",
                _ => $"{Materials.RichName(costMaterial)} ×{total:N0}",
            };
        }
    }

    /// <summary>
    /// 상점 상품 목록(튜닝 지점). 가격은 스테이지·던전 보상 규모에 맞춰 잡았습니다.
    /// - 일반 상점: 던전에서 모자란 재료를 금화로 보충 (매일 초기화)
    /// - 크리스탈 상점: 금화 묶음 · 경험치 결정 · 전설의 비전서 (매주 초기화)
    /// - 교환소: 별의 조각 → 뽑기권, 상급 교본 → 비전서, 같은 속성 돌파 재료 하위 3개 → 상위 1개
    /// </summary>
    public static class ShopCatalog
    {
        /// <summary>일/주간 초기화 시각(로컬, 시).</summary>
        public const int ResetHour = 5;

        /// <summary>돌파 재료 상위 변환 비율(하위 N개 → 상위 1개).</summary>
        public const int AscensionConvertRate = 3;

        private static List<ShopItem> _all;

        public static IReadOnlyList<ShopItem> All => _all ??= Build();

        public static List<ShopItem> Of(ShopTab tab)
        {
            var list = new List<ShopItem>();
            foreach (var it in All) if (it.tab == tab) list.Add(it);
            return list;
        }

        public static ShopItem Find(string id)
        {
            foreach (var it in All) if (it.id == id) return it;
            return null;
        }

        public static string TabName(ShopTab t) => t switch
        {
            ShopTab.General => "일반 상점",
            ShopTab.Crystal => "크리스탈 상점",
            ShopTab.Guild => "길드 상점",
            ShopTab.Package => "패키지",
            ShopTab.Monthly => "월정액",
            _ => "교환소",
        };

        public static string TabHint(ShopTab t) => t switch
        {
            ShopTab.General => "금화로 육성 재료 보충 · 매일 05:00 초기화",
            ShopTab.Crystal => "크리스탈로 귀한 재료 · 매주 월요일 05:00 초기화",
            ShopTab.Guild => "길드 코인으로 교환 · 길드에 가입해야 이용 · 매주 월요일 05:00 초기화",
            ShopTab.Package => "묶음 상품 · 지금은 크리스탈로 판매(출시 때 결제 상품으로 바뀜)",
            ShopTab.Monthly => "30일 동안 매일 05:00에 우편으로 보상 · 지금은 크리스탈로 판매(출시 때 결제 상품으로 바뀜)",
            _ => "남는 재료를 필요한 재료로",
        };

        /// <summary>탭의 초기화 주기(제목 옆 남은 시간 표시용).</summary>
        public static ShopReset TabReset(ShopTab t) => t switch
        {
            ShopTab.General => ShopReset.Daily,
            ShopTab.Crystal or ShopTab.Guild => ShopReset.Weekly,
            _ => ShopReset.None,
        };

        private static List<ShopItem> Build()
        {
            var l = new List<ShopItem>
            {
                // ---- 일반 상점 (금화 · 매일) ----
                Gold("gen_exp_small", MaterialType.ExpSmall, 10, 1500, 5),
                Gold("gen_exp_potion", MaterialType.ExpPotion, 5, 4000, 5),
                Gold("gen_exp_large", MaterialType.ExpLarge, 3, 10000, 3),
                Gold("gen_book1", MaterialType.SkillBook, 3, 3000, 3),
                Gold("gen_book2", MaterialType.SkillBookBlue, 2, 8000, 3),
                Gold("gen_book3", MaterialType.SkillBookPurple, 1, 15000, 2),
                Gold("gen_ore", MaterialType.EquipOre, 10, 5000, 5),
                Gold("gen_stamina", MaterialType.StaminaSmall, 1, 8000, 2),
                Gold("gen_sweep", MaterialType.SweepTicket, 5, 3000, 2),   // 소탕권
                Gold("gen_gift1", MaterialType.GiftSmall, 2, 5000, 3),     // 호감도 선물
                Gold("gen_gift2", MaterialType.GiftMedium, 1, 12000, 2),

                // ---- 크리스탈 상점 (매주) ----
                new ShopItem { id = "cry_gold", tab = ShopTab.Crystal, rewardGold = 50000, currency = ShopCurrency.Crystal, price = 100, limit = 5, reset = ShopReset.Weekly },
                Crystal("cry_exp_crystal", MaterialType.ExpCrystal, 3, 120, 3),
                Crystal("cry_gift3", MaterialType.GiftLarge, 1, 60, 5),
                Crystal("cry_sweep", MaterialType.SweepTicket, 20, 40, 3),
                Crystal("cry_refine", MaterialType.RefineStone, 1, 500, 1),   // 재련석(주 1개)
                Crystal("cry_book3", MaterialType.SkillBookPurple, 3, 150, 3),
                Crystal("cry_legend", MaterialType.SkillLegend, 1, 300, 2),
                Crystal("cry_stamina", MaterialType.StaminaLarge, 1, 60, 6),

                // ---- 길드 상점 (길드 코인 · 매주) ----
                Guild("guild_legend", MaterialType.SkillLegend, 1, 300, 2),
                Guild("guild_refine", MaterialType.RefineStone, 1, 400, 1),
                Guild("guild_book3", MaterialType.SkillBookPurple, 5, 150, 3),
                Guild("guild_essence", MaterialType.EquipEssence, 50, 120, 3),
                Guild("guild_exp_crystal", MaterialType.ExpCrystal, 3, 100, 3),
                Guild("guild_stamina", MaterialType.StaminaLarge, 1, 80, 3),
                Guild("guild_gift3", MaterialType.GiftLarge, 1, 100, 3),
                new ShopItem
                {
                    id = "guild_pickup", tab = ShopTab.Guild, rewardTicket = true, ticketType = SubcultureRPG.Gacha.GachaBannerType.Pickup, amount = 1,
                    currency = ShopCurrency.Material, costMaterial = MaterialType.GuildCoin, price = 600, limit = 1, reset = ShopReset.Weekly,
                },

                // ---- 패키지 (출시 때 인앱 결제 — 지금은 크리스탈) ----
                Package("pkg_daily", "일일 특가", "Items/PkgDaily", "₩1,200", 60, 1, ShopReset.Daily,
                    () => new RewardBundle().Mat(MaterialType.StaminaLarge, 2).Mat(MaterialType.SweepTicket, 10)),
                Package("pkg_growth", "주간 성장 패키지", "Items/PkgGrowth", "₩5,900", 600, 1, ShopReset.Weekly,
                    () => new RewardBundle().Mat(MaterialType.SkillBookPurple, 10).Mat(MaterialType.ExpLarge, 20).Gold(100000)),
                Package("pkg_equip", "장비 성장 패키지", "Items/PkgEquip", "₩5,900", 500, 1, ShopReset.Weekly,
                    () => new RewardBundle().Mat(MaterialType.EquipOre, 50).Mat(MaterialType.EquipEssence, 100).Mat(MaterialType.RefineStone, 1)),
                Package("pkg_gacha", "월간 소환 패키지", "Items/PkgGacha", "₩12,000", 1200, 1, ShopReset.Monthly,
                    () => new RewardBundle().Pickup(5).Standard(5)),
                Package("pkg_newbie", "신규 지휘관 패키지", "Items/PkgNewbie", "₩9,900", 2000, 1, ShopReset.None,
                    () => new RewardBundle().Pickup(10).Gold(200000).Mat(MaterialType.ExpCrystal, 10).Mat(MaterialType.Select5, 1)),

                // ---- 교환소 ----
                // 별의 조각 → 뽑기권(무제한)
                new ShopItem
                {
                    id = "ex_ticket_pickup", tab = ShopTab.Exchange, rewardTicket = true, ticketType = SubcultureRPG.Gacha.GachaBannerType.Pickup,
                    amount = 1, currency = ShopCurrency.Material, costMaterial = MaterialType.StarShard, price = StarShards.PickupTicketPrice,
                },
                new ShopItem
                {
                    id = "ex_ticket_standard", tab = ShopTab.Exchange, rewardTicket = true, ticketType = SubcultureRPG.Gacha.GachaBannerType.Standard,
                    amount = 1, currency = ShopCurrency.Material, costMaterial = MaterialType.StarShard, price = StarShards.StandardTicketPrice,
                },
                new ShopItem
                {
                    id = "ex_legend", tab = ShopTab.Exchange, reward = MaterialType.SkillLegend, amount = 1,
                    currency = ShopCurrency.Material, costMaterial = MaterialType.SkillBookPurple, price = 8,
                    limit = 2, reset = ShopReset.Weekly,
                },
            };

            // 돌파 재료 상위 변환(같은 속성): 초급 3 → 중급 1, 중급 3 → 상급 1 (무제한)
            foreach (var e in Traits.AllElements)
                for (int tier = 1; tier < Materials.AscensionTiers; tier++)
                    l.Add(new ShopItem
                    {
                        id = $"ex_asc_{(int)e}_{tier}", tab = ShopTab.Exchange,
                        reward = Materials.Ascension(e, tier + 1), amount = 1,
                        currency = ShopCurrency.Material, costMaterial = Materials.Ascension(e, tier),
                        price = AscensionConvertRate,
                    });
            return l;
        }

        private static ShopItem Gold(string id, MaterialType t, int amount, int price, int dailyLimit) => new()
        {
            id = id, tab = ShopTab.General, reward = t, amount = amount,
            currency = ShopCurrency.Gold, price = price, limit = dailyLimit, reset = ShopReset.Daily,
        };

        private static ShopItem Guild(string id, MaterialType t, int amount, int coins, int weeklyLimit) => new()
        {
            id = id, tab = ShopTab.Guild, reward = t, amount = amount,
            currency = ShopCurrency.Material, costMaterial = MaterialType.GuildCoin, price = coins, limit = weeklyLimit, reset = ShopReset.Weekly,
        };

        /// <summary>패키지: 지금은 크리스탈(crystals)로 판매, realPrice는 출시 때 결제 가격.</summary>
        private static ShopItem Package(string id, string title, string icon, string realPrice, int crystals, int limit, ShopReset reset, Func<RewardBundle> bundle) => new()
        {
            id = id, tab = ShopTab.Package, title = title, icon = icon, realPrice = realPrice, bundle = bundle,
            currency = ShopCurrency.Crystal, price = crystals, limit = limit, reset = reset, amount = 1,
        };

        private static ShopItem Crystal(string id, MaterialType t, int amount, int price, int weeklyLimit) => new()
        {
            id = id, tab = ShopTab.Crystal, reward = t, amount = amount,
            currency = ShopCurrency.Crystal, price = price, limit = weeklyLimit, reset = ShopReset.Weekly,
        };
    }
}
