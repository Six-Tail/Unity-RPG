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
    }

    /// <summary>값을 치르는 수단.</summary>
    public enum ShopCurrency { Gold, Crystal, Material }

    /// <summary>구매 제한 초기화 주기.</summary>
    public enum ShopReset { None, Daily, Weekly }

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

        public bool IsGold => rewardGold > 0;
        public bool IsTicket => rewardTicket;
        public int Grade => IsTicket ? 6 : IsGold ? 4 : Materials.Grade(reward);

        public string RewardName => IsTicket ? PlayerWallet.TicketName(ticketType) : IsGold ? $"금화 {rewardGold:N0}" : Materials.Name(reward);
        public string RewardRichName => IsTicket
            ? $"<color={(ticketType == SubcultureRPG.Gacha.GachaBannerType.Pickup ? "#FFB3E6" : "#9FD8FF")}>{PlayerWallet.TicketName(ticketType)}</color>"
            : IsGold ? $"<color=#FFD36B>금화 {rewardGold:N0}</color>" : Materials.RichName(reward);

        /// <summary>아이콘 그림 경로(Resources/Art 기준).</summary>
        public string IconPath => IsTicket
            ? (ticketType == SubcultureRPG.Gacha.GachaBannerType.Pickup ? "Items/PickupTicket" : "Items/StandardTicket")
            : IsGold ? "Items/Gold" : "Items/" + reward;

        /// <summary>qty회 구매로 받는 것(예: '픽업 뽑기권 ×2').</summary>
        public string GotText(int qty) => IsGold
            ? $"<color=#FFD36B>금화 {(long)rewardGold * amount * qty:N0}</color>"
            : $"{RewardRichName} ×{(long)amount * qty:N0}";

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
            _ => "교환소",
        };

        public static string TabHint(ShopTab t) => t switch
        {
            ShopTab.General => "금화로 육성 재료 보충 · 매일 05:00 초기화",
            ShopTab.Crystal => "크리스탈로 귀한 재료 · 매주 월요일 05:00 초기화",
            _ => "남는 재료를 필요한 재료로",
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

                // ---- 크리스탈 상점 (매주) ----
                new ShopItem { id = "cry_gold", tab = ShopTab.Crystal, rewardGold = 50000, currency = ShopCurrency.Crystal, price = 100, limit = 5, reset = ShopReset.Weekly },
                Crystal("cry_exp_crystal", MaterialType.ExpCrystal, 3, 120, 3),
                Crystal("cry_book3", MaterialType.SkillBookPurple, 3, 150, 3),
                Crystal("cry_legend", MaterialType.SkillLegend, 1, 300, 2),

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

        private static ShopItem Crystal(string id, MaterialType t, int amount, int price, int weeklyLimit) => new()
        {
            id = id, tab = ShopTab.Crystal, reward = t, amount = amount,
            currency = ShopCurrency.Crystal, price = price, limit = weeklyLimit, reset = ShopReset.Weekly,
        };
    }
}
