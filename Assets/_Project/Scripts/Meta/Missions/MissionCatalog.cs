using System;
using System.Collections.Generic;
using SubcultureRPG.Gacha;

namespace SubcultureRPG.Meta
{
    /// <summary>임무가 세는 행동. 게임 곳곳에서 <see cref="MissionEvents.Report"/> 로 알립니다.</summary>
    public enum MissionGoal
    {
        Login,        // 접속(플레이 중이면 항상 달성)
        BattleWin,    // 스테이지·던전 전투 승리
        DungeonClear, // 특수 던전 클리어
        GachaPull,    // 뽑기 횟수(10연 = 10)
        Growth,       // 육성(레벨업 · 돌파 · 스킬 레벨업)
        ShopBuy,      // 상점 구매·교환
        DailyClaim,   // 일일 임무 보상 수령(보너스 임무용)
        WeeklyClaim,  // 주간 임무 보상 수령(보너스 임무용)
    }

    /// <summary>
    /// 임무 진행 알림 창구. 화면·시스템은 임무 시스템을 몰라도 되고, 한 줄만 호출하면 됩니다:
    /// <c>MissionEvents.Report(MissionGoal.ShopBuy);</c>
    /// </summary>
    public static class MissionEvents
    {
        public static event Action<MissionGoal, int> OnProgress;

        public static void Report(MissionGoal goal, int amount = 1)
        {
            if (amount > 0) OnProgress?.Invoke(goal, amount);
        }
    }
}

namespace SubcultureRPG.Meta.Missions
{
    public enum MissionPeriod { Daily, Weekly }

    public enum MissionRewardKind { Crystal, Gold, Material, Ticket }

    /// <summary>임무 보상 1종.</summary>
    public sealed class MissionReward
    {
        public MissionRewardKind kind;
        public MaterialType material;      // kind == Material
        public GachaBannerType ticket;     // kind == Ticket
        public int amount;

        /// <summary>'크리스탈 20', '픽업 뽑기권 ×2' 같은 표시(리치 텍스트).</summary>
        public string Text => kind switch
        {
            MissionRewardKind.Crystal => $"<color=#8FE9FF>크리스탈 {amount:N0}</color>",
            MissionRewardKind.Gold => $"<color=#FFD36B>금화 {amount:N0}</color>",
            MissionRewardKind.Ticket => $"<color=#FFB3E6>{PlayerWallet.TicketName(ticket)} ×{amount:N0}</color>",
            _ => $"{Materials.RichName(material)} ×{amount:N0}",
        };
    }

    /// <summary>임무 1개. id는 세이브 키이므로 한 번 정하면 바꾸지 마세요.</summary>
    public sealed class Mission
    {
        public string id;
        public MissionPeriod period;
        public MissionGoal goal;
        public int target = 1;
        public string title;
        public MissionReward[] rewards = Array.Empty<MissionReward>();

        /// <summary>다른 임무 보상을 받은 수로 달성하는 보너스 임무(자신은 수령 수에 세지 않음).</summary>
        public bool IsBonus => goal == MissionGoal.DailyClaim || goal == MissionGoal.WeeklyClaim;

        public string RewardText
        {
            get
            {
                var parts = new List<string>();
                foreach (var r in rewards) parts.Add(r.Text);
                return string.Join("   ", parts);
            }
        }
    }

    /// <summary>
    /// 임무 목록(튜닝 지점). 뽑기권의 정식 획득처입니다 — 일일 보너스 = 상시 뽑기권, 주간 = 픽업 · 상시 뽑기권.
    /// 일일은 매일 05:00, 주간은 매주 월요일 05:00(로컬)에 초기화(상점과 같은 시각).
    /// </summary>
    public static class MissionCatalog
    {
        private static List<Mission> _all;

        public static IReadOnlyList<Mission> All => _all ??= Build();

        public static List<Mission> Of(MissionPeriod period)
        {
            var l = new List<Mission>();
            foreach (var m in All) if (m.period == period) l.Add(m);
            return l;
        }

        public static Mission Find(string id)
        {
            foreach (var m in All) if (m.id == id) return m;
            return null;
        }

        public static string PeriodName(MissionPeriod p) => p == MissionPeriod.Daily ? "일일 임무" : "주간 임무";

        private static List<Mission> Build() => new()
        {
            // ---- 일일 ----
            Daily("d_login", MissionGoal.Login, 1, "게임 접속", Crystal(20)),
            Daily("d_battle", MissionGoal.BattleWin, 3, "전투 3회 승리", Gold(10000)),
            Daily("d_dungeon", MissionGoal.DungeonClear, 2, "특수 던전 2회 클리어", Mat(MaterialType.ExpPotion, 5)),
            Daily("d_gacha", MissionGoal.GachaPull, 1, "뽑기 1회", Gold(5000)),
            Daily("d_growth", MissionGoal.Growth, 1, "캐릭터 육성 1회 (레벨업 · 돌파 · 스킬)", Mat(MaterialType.SkillBook, 3)),
            Daily("d_shop", MissionGoal.ShopBuy, 1, "상점에서 1회 구매", Crystal(10)),
            Daily("d_all", MissionGoal.DailyClaim, 5, "일일 임무 5개 완료", Ticket(GachaBannerType.Standard, 1), Crystal(30)),

            // ---- 주간 ----
            Weekly("w_battle", MissionGoal.BattleWin, 20, "전투 20회 승리", Crystal(100)),
            Weekly("w_dungeon", MissionGoal.DungeonClear, 10, "특수 던전 10회 클리어", Mat(MaterialType.SkillBookPurple, 3)),
            Weekly("w_gacha", MissionGoal.GachaPull, 10, "뽑기 10회", Gold(50000)),
            Weekly("w_growth", MissionGoal.Growth, 10, "캐릭터 육성 10회", Mat(MaterialType.ExpLarge, 10)),
            Weekly("w_daily", MissionGoal.DailyClaim, 20, "일일 임무 20개 완료", Ticket(GachaBannerType.Pickup, 2)),
            Weekly("w_all", MissionGoal.WeeklyClaim, 4, "주간 임무 4개 완료", Ticket(GachaBannerType.Standard, 3), Crystal(100)),
        };

        private static Mission Daily(string id, MissionGoal goal, int target, string title, params MissionReward[] rewards) =>
            new() { id = id, period = MissionPeriod.Daily, goal = goal, target = target, title = title, rewards = rewards };

        private static Mission Weekly(string id, MissionGoal goal, int target, string title, params MissionReward[] rewards) =>
            new() { id = id, period = MissionPeriod.Weekly, goal = goal, target = target, title = title, rewards = rewards };

        private static MissionReward Crystal(int n) => new() { kind = MissionRewardKind.Crystal, amount = n };
        private static MissionReward Gold(int n) => new() { kind = MissionRewardKind.Gold, amount = n };
        private static MissionReward Mat(MaterialType t, int n) => new() { kind = MissionRewardKind.Material, material = t, amount = n };
        private static MissionReward Ticket(GachaBannerType t, int n) => new() { kind = MissionRewardKind.Ticket, ticket = t, amount = n };
    }
}
