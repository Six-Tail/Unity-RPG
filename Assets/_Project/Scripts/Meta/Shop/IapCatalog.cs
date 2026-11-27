using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Meta.Missions;

namespace SubcultureRPG.Meta.Shop
{
    /// <summary>실제 돈으로 파는 상품 종류.</summary>
    public enum IapKind { TopUp, Package, Monthly, Season }

    /// <summary>
    /// 인앱 결제 상품 1개. id = 스토어(구글 플레이 · 앱 스토어)에 등록하는 상품 ID 그대로 — 한 번 정하면 바꾸지 마세요.
    /// 모두 '소모성(Consumable)' 상품: 구매 제한 · 기간은 게임이 기록(패키지 한도 · 월정액 기간 · 시즌 패스 1회).
    /// </summary>
    public sealed class IapProduct
    {
        public string id, title, icon;
        public IapKind kind;
        public int krw;                      // 원화 가격(스토어 가격을 못 불러왔을 때 표시)
        public Func<string> block;           // 지금 살 수 없는 이유(살 수 있으면 null)
        public Func<bool> grant;             // 지급(게임이 아직 준비되지 않았으면 false → 나중에 다시)
        public string KrwText => $"₩{krw:N0}";
    }

    /// <summary>
    /// 결제 상품 목록: 크리스탈 충전 6종 + 패키지(ShopCatalog 의 realPrice 상품) + 월정액 4종 + 시즌 패스 프리미엄.
    /// 스토어 등록 표: docs/iap.md
    /// </summary>
    public static class IapCatalog
    {
        // ---------------- 크리스탈 충전 ----------------

        public sealed class TopUpDef
        {
            public string id;
            public int crystals, bonus, krw;   // bonus = 첫 구매가 아닐 때 덤. 첫 구매는 crystals 만큼 2배
            public string icon;
        }

        /// <summary>충전 상품(뽑기 1회 = 크리스탈 160). 각 상품의 첫 구매는 2배(크리스탈 × 2).</summary>
        public static readonly TopUpDef[] TopUps =
        {
            new() { id = "crystal_60", crystals = 60, bonus = 0, krw = 1200, icon = "Items/TopUp1" },
            new() { id = "crystal_300", crystals = 300, bonus = 30, krw = 5900, icon = "Items/TopUp2" },
            new() { id = "crystal_980", crystals = 980, bonus = 110, krw = 19000, icon = "Items/TopUp3" },
            new() { id = "crystal_1980", crystals = 1980, bonus = 260, krw = 39000, icon = "Items/TopUp4" },
            new() { id = "crystal_3280", crystals = 3280, bonus = 600, krw = 65000, icon = "Items/TopUp5" },
            new() { id = "crystal_6480", crystals = 6480, bonus = 1600, krw = 119000, icon = "Items/TopUp6" },
        };

        private const string FirstKey = "iap.first";   // 첫 구매 2배를 이미 받은 충전 상품 id(계정 데이터)

        public static bool FirstBonusAvailable(TopUpDef t)
        {
            string s = SaveStore.GetString(FirstKey, "");
            return Array.IndexOf(s.Split(','), t.id) < 0;
        }

        /// <summary>이번에 받을 크리스탈(첫 구매 2배 / 아니면 기본 + 덤).</summary>
        public static int TopUpAmount(TopUpDef t) => FirstBonusAvailable(t) ? t.crystals * 2 : t.crystals + t.bonus;

        private static bool GrantTopUp(TopUpDef t)
        {
            var wallet = UnityEngine.Object.FindAnyObjectByType<PlayerWallet>();
            if (wallet == null) return false;
            int n = TopUpAmount(t);
            if (FirstBonusAvailable(t))
            {
                string s = SaveStore.GetString(FirstKey, "");
                SaveStore.SetString(FirstKey, string.IsNullOrEmpty(s) ? t.id : s + "," + t.id);
            }
            wallet.Add(n);
            SaveStore.Save();
            return true;
        }

        // ---------------- 시즌 패스 ----------------

        public const string SeasonPremiumId = "season_premium";
        public const int SeasonPremiumKrw = 11000;

        // ---------------- 전체 목록 ----------------

        private static List<IapProduct> _all;
        public static IReadOnlyList<IapProduct> All => _all ??= Build();

        public static IapProduct Find(string id) => All is List<IapProduct> l ? l.Find(p => p.id == id) : null;

        private static List<IapProduct> Build()
        {
            var l = new List<IapProduct>();
            foreach (var t in TopUps)
            {
                var tt = t;
                l.Add(new IapProduct
                {
                    id = t.id, kind = IapKind.TopUp, title = $"크리스탈 {t.crystals:N0}", icon = t.icon, krw = t.krw,
                    block = () => null, grant = () => GrantTopUp(tt),
                });
            }
            foreach (var it in ShopCatalog.All)
            {
                if (!it.IsIap) continue;
                var item = it;
                l.Add(new IapProduct
                {
                    id = it.id, kind = IapKind.Package, title = it.title, icon = it.icon, krw = ParseKrw(it.realPrice),
                    block = () => Shop() is PlayerShop s ? s.PaidBlockReason(item) : "상점을 찾을 수 없습니다",
                    grant = () => Shop() is PlayerShop s && s.GrantPaid(item),
                });
            }
            foreach (var p in MonthlyPass.Passes)
            {
                var pass = p;
                l.Add(new IapProduct
                {
                    id = p.id, kind = IapKind.Monthly, title = p.name, icon = p.icon, krw = ParseKrw(p.realPrice),
                    block = () => MonthlyPass.Instance.PaidBlockReason(pass),
                    grant = () => MonthlyPass.Instance.Activate(pass),
                });
            }
            l.Add(new IapProduct
            {
                id = SeasonPremiumId, kind = IapKind.Season, title = "시즌 패스 프리미엄", icon = "Items/UiPass", krw = SeasonPremiumKrw,
                block = () => SeasonPass.Instance.Premium ? "이미 이번 시즌 프리미엄 패스를 열었습니다" : null,
                grant = () => SeasonPass.Instance.GrantPremium(),
            });
            return l;
        }

        private static PlayerShop Shop() => UnityEngine.Object.FindAnyObjectByType<PlayerShop>();

        /// <summary>"₩5,900" → 5900.</summary>
        public static int ParseKrw(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int n = 0;
            foreach (char ch in s) if (ch >= '0' && ch <= '9') n = n * 10 + (ch - '0');
            return n;
        }
    }
}
