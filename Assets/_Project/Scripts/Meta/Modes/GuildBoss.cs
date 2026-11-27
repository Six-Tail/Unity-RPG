using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;
using SubcultureRPG.Battle.UI;
using SubcultureRPG.Meta.Cloud;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 길드 보스(주간 · 협동): 길드마다 보스 1마리, 길드원이 준 피해를 한 주 동안 합쳐 체력을 깎습니다.
    /// · 단계: 체력을 다 깎으면 다음 단계(더 강하고 체력이 큼 — <see cref="StageHps"/>). 1단계 쉬움 Lv.30 → 2단계 보통 → 3단계 어려움 → 4단계부터 극악 Lv.100
    /// · 도전: 하루 2회(05:00 충전), 전투 틀(5분 · 격노)은 주간 보스와 같음(<see cref="WorldRaid.FightWorld"/>). 끝까지 싸운 전투만 횟수를 씀
    /// · 길드 합계: 실제 길드 = 길드원들이 올린 이번 주 피해의 합, 가상 길드 = 나 + 가상 길드원들이 날마다 보탠 피해
    /// · 보상: 길드가 깬 단계마다(길드원 각자 받기) + 지난주 길드 순위(월요일에 우편)
    /// </summary>
    public static class GuildBoss
    {
        public const string BossName = "종말의 군주";
        public const int AttemptsPerDay = 2, MaxStage = 10;
        /// <summary>
        /// 단계별 체력(튜닝 지점). 시뮬레이션(2026-11-26, 에디터 '주간 보스 밸런스 시뮬레이션'에 guild=1) 한 판 피해:
        /// 풀 육성(6★+6 · 장비) 1단계 2억 → 4단계 1억 → 7단계 7,700만 · 장비 없는 Lv.100 700만 → 130만 · Lv.40 108만 → 16만.
        /// 한 사람 일주일 14판 기준(시뮬레이션 예상) → 풀 육성 1명 5단계 · 4명 7단계 · 10명 8단계(10단계는 큰 강한 길드의 목표),
        /// 장비 없는 Lv.100 4명 2단계 · 10명 3단계 · Lv.40 4명 1단계(누구나 첫 보상은 받게).
        /// (처음엔 5,000만 × 3배씩이었는데 7단계 한 단계에만 풀 육성 470판이 들어 벽이 됨)
        /// </summary>
        public static readonly long[] StageHps =
        {
            20_000_000, 60_000_000, 150_000_000, 350_000_000, 700_000_000,
            1_200_000_000, 2_000_000_000, 3_200_000_000, 5_000_000_000, 8_000_000_000,
        };

        public static long StageHp(int stage) => StageHps[Mathf.Clamp(stage, 1, MaxStage) - 1];

        /// <summary>stage 단계를 깨는 데 필요한 누적 피해(1 ~ stage 단계 체력 합).</summary>
        public static long ClearAt(int stage)
        {
            long sum = 0;
            for (int s = 1; s <= Mathf.Clamp(stage, 0, MaxStage); s++) sum += StageHp(s);
            return sum;
        }

        /// <summary>누적 피해 → 깬 단계 수(0 ~ MaxStage).</summary>
        public static int Cleared(long total)
        {
            int n = 0;
            while (n < MaxStage && total >= ClearAt(n + 1)) n++;
            return n;
        }

        /// <summary>지금 싸우는 단계(1 ~ MaxStage, 다 깼으면 MaxStage).</summary>
        public static int Current(long total) => Mathf.Min(MaxStage, Cleared(total) + 1);

        /// <summary>단계 → 주간 보스 난이도(격노 · 비례 피해 배율 · 레벨).</summary>
        public static int DifficultyOf(int stage) => Mathf.Clamp(stage - 1, 0, WorldRaid.DifficultyCount - 1);
        public static int LevelOf(int stage) => stage <= WorldRaid.DifficultyCount ? WorldRaid.BossLevel(DifficultyOf(stage)) : 100;

        // ---------------- 보상 ----------------

        public class StageReward { public string label; public Func<RewardBundle> bundle; }

        public static readonly StageReward[] StageRewards =
        {
            new() { label = "금화 30,000", bundle = () => new RewardBundle().Gold(30000) },
            new() { label = "크리스탈 50", bundle = () => new RewardBundle().Crystals(50) },
            new() { label = "대형 경험치 물약 ×5", bundle = () => new RewardBundle().Mat(MaterialType.ExpLarge, 5) },
            new() { label = "크리스탈 100", bundle = () => new RewardBundle().Crystals(100) },
            new() { label = "상급 스킬 교본 ×3", bundle = () => new RewardBundle().Mat(MaterialType.SkillBookPurple, 3) },
            new() { label = "크리스탈 150", bundle = () => new RewardBundle().Crystals(150) },
            new() { label = "장비 정수 80", bundle = () => new RewardBundle().Mat(MaterialType.EquipEssence, 80) },
            new() { label = "전설의 비전서 ×1", bundle = () => new RewardBundle().Mat(MaterialType.SkillLegend, 1) },
            new() { label = "크리스탈 200", bundle = () => new RewardBundle().Crystals(200) },
            new() { label = "픽업 뽑기권 ×1", bundle = () => new RewardBundle().Pickup(1) },
        };

        /// <summary>지난주 길드 보스 순위 보상(우편).</summary>
        /// <summary>단계 보상마다 함께 주는 길드 코인.</summary>
        public const int StageCoins = 30;

        public static RewardBundle RankReward(int rank, out string tier)
        {
            var r = RankRewardBase(rank, out tier);
            int coins = rank == 1 ? 300 : rank <= 3 ? 200 : rank <= 10 ? 150 : rank <= 30 ? 100 : rank <= 100 ? 70 : 50;
            return r.Mat(MaterialType.GuildCoin, coins);
        }

        private static RewardBundle RankRewardBase(int rank, out string tier)
        {
            if (rank == 1) { tier = "1위"; return new RewardBundle().Crystals(500).Gold(100000); }
            if (rank <= 3) { tier = "2~3위"; return new RewardBundle().Crystals(300).Gold(80000); }
            if (rank <= 10) { tier = "4~10위"; return new RewardBundle().Crystals(200).Gold(60000); }
            if (rank <= 30) { tier = "11~30위"; return new RewardBundle().Crystals(120).Gold(40000); }
            if (rank <= 100) { tier = "31~100위"; return new RewardBundle().Crystals(80).Gold(30000); }
            tier = "참가"; return new RewardBundle().Crystals(40).Gold(20000);
        }

        public static long GuildTotal => PlayerGuild.Instance.GuildBossTotal();

        public static bool CanClaim(int stage)
        {
            var g = PlayerGuild.Instance;
            return g.Joined && stage >= 1 && stage <= MaxStage && Cleared(GuildTotal) >= stage && !g.BossClaimed(stage);
        }

        public static int ClaimableCount
        {
            get
            {
                var g = PlayerGuild.Instance;
                if (!g.Joined) return 0;
                int cleared = Cleared(g.GuildBossTotal()), n = 0;
                for (int s = 1; s <= cleared; s++) if (!g.BossClaimed(s)) n++;
                return n;
            }
        }

        public static RewardBundle Claim(int stage)
        {
            if (!CanClaim(stage)) return null;
            var b = StageRewards[stage - 1].bundle().Mat(MaterialType.GuildCoin, StageCoins);   // 단계마다 길드 코인
            b.Grant();
            PlayerGuild.Instance.MarkBossClaimed(stage);
            return b;
        }

        /// <summary>주가 바뀔 때: 지난주 길드 순위 보상을 우편으로(내가 한 번이라도 싸웠을 때만).</summary>
        internal static void SettleWeek(long guildTotal, long myDamage, int rank, int rankTotal)
        {
            if (myDamage <= 0) return;
            var mail = UnityEngine.Object.FindAnyObjectByType<PlayerMail>();
            if (mail == null) return;
            var reward = RankReward(rank > 0 ? rank : int.MaxValue, out string tier);
            int cleared = Cleared(guildTotal);
            mail.Send("지난주 길드 보스 보상",
                $"길드가 {BossName}을(를) {cleared}단계까지 깼습니다.\n길드 순위 {(rank > 0 ? $"{rank}위 / {rankTotal}길드" : "집계 없음")} — {tier} 보상\n내 피해 {myDamage:N0}", reward);
        }

        // ---------------- 전투 ----------------

        public static int AttemptsLeft => Mathf.Max(0, AttemptsPerDay - PlayerGuild.Instance.BossUsedToday);

        private static BattleUnitData _boss;
        private static List<CharacterData> _party;
        private static int _stage;

        public static BattleUnitData CreateBoss(int stage)
        {
            int diff = DifficultyOf(stage);
            var u = WorldRaid.CreateBoss(LevelOf(stage), diff);
            u.displayName = BossName;
            // 4단계 넘게는 단계마다 방어 +12%(같은 파티라도 단계가 오를수록 피해가 조금씩 줄어듦)
            if (stage > WorldRaid.DifficultyCount)
            {
                float k = 1f + 0.12f * (stage - WorldRaid.DifficultyCount);
                u.baseStats.def = Mathf.RoundToInt(u.baseStats.def * k);
                u.baseStats.defGrowth = Mathf.RoundToInt(u.baseStats.defGrowth * k);
            }
            string[] names = { "멸망의 손아귀", "종말의 파동", "최후의 심판" };
            for (int i = 0; i < u.skills.Count && i < names.Length; i++) if (u.skills[i] != null) u.skills[i].skillName = names[i];
            return u;
        }

        /// <summary>도전. 실패 사유(성공이면 null).</summary>
        public static string Fight(IList<CharacterData> party)
        {
            var g = PlayerGuild.Instance;
            if (!g.Joined) return "길드에 가입해 있지 않습니다";
            if (AttemptsLeft <= 0) return "오늘 도전 횟수를 모두 썼습니다 (매일 05:00 충전)";
            Cleanup();
            _stage = Current(GuildTotal);
            _boss = CreateBoss(_stage);
            _party = new List<CharacterData>(party);
            bool ok = WorldRaid.Instance.FightWorld(_party, _boss, "길드로", OnEnd, () => Fight(_party),
                DifficultyOf(_stage), new[] { "Backgrounds/battle_guild", "Backgrounds/guild", "Backgrounds/battle" });
            return ok ? null : "전투를 시작하지 못했습니다";
        }

        private static void OnEnd(long dmg)
        {
            var g = PlayerGuild.Instance;
            g.UseBossAttempt();   // 끝까지 싸운 전투만 횟수를 씀
            long before = GuildTotal;
            g.AddBossDamage(dmg);
            long after = GuildTotal;
            int c0 = Cleared(before), c1 = Cleared(after);
            g.RefreshSeen();
            GuildLink.Instance.RequestSync();

            var raid = WorldRaid.Instance;
            int cur = Current(after);
            long into = after - ClearAt(cur - 1);
            var hud = UnityEngine.Object.FindAnyObjectByType<BattleHUD>();
            hud?.SetResultDetail($"<size=110%>길드 보스 — {BossName}  <size=80%>{_stage}단계 · Lv.{LevelOf(_stage)}</size></size>\n" +
                                 $"<color=#FFD36B>준 피해</color>  <b>{dmg:N0}</b>\n" +
                                 (c1 > c0 ? $"<color=#7CFF9A>{c0 + 1}{(c1 > c0 + 1 ? $" ~ {c1}" : "")}단계 격파! 길드 화면에서 보상을 받으세요</color>\n" : "") +
                                 $"<color=#9AA3B5>길드 합계</color>  {after:N0}   <color=#9AA3B5>{cur}단계 진행</color>  {Math.Min(into, StageHp(cur)):N0} / {StageHp(cur):N0}\n" +
                                 $"<size=80%><color=#9AA3B5>오늘 남은 도전 {AttemptsLeft} / {AttemptsPerDay}  ·  버틴 시간 {raid.SurvivedSeconds}초 / {WorldRaid.TimeLimit:0}초  ·  격노 {raid.EnrageStage}단계</color></size>");
            Cleanup();
        }

        private static void Cleanup()
        {
            if (_boss == null) return;
            if (_boss.skills != null) foreach (var sk in _boss.skills) if (sk != null) UnityEngine.Object.Destroy(sk, 1f);
            UnityEngine.Object.Destroy(_boss, 1f);
            _boss = null;
        }
    }
}
