using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;
using SubcultureRPG.Battle.UI;
using SubcultureRPG.Meta.Shop;
using SubcultureRPG.Meta.Cloud;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 결투장(PVP): 다른 지휘관의 팀(실제 캐릭터로 만든 AI 팀)과 겨룸.
    /// · 하루 도전권 5장(05:00 초기화) · 랭크 점수(시작 1000) · 티어 8단계(브론즈 ~ 다이아 → 성운 → 초신성 → 앵커)
    /// · 처음 오른 티어마다 테두리 지급(최고 점수 기준) · 시즌(1주) 랭킹 100명 — 정산 때 순위 보상, 1 ~ 3위는 한정 테두리
    /// · 상대 3명: 쉬움 · 보통 · 어려움(내 주력 레벨 −6 / ±0 / +6, 어려움은 초월도 일부) — 승리하면 새로 뽑힘, '새로고침' 가능
    /// · 승리: 점수 + 크리스탈 + 금화 / 패배: 점수 −8 + 금화 조금. 도전권은 출격할 때 쓰고, 끝나기 전에 나가면 돌려받음
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(PlayerArena.Instance).
    /// </summary>
    public class PlayerArena : MonoBehaviour
    {
        public const int MaxTickets = 5, StartPoints = 1000, LosePoints = 8, LoseGold = 1000;
        private const string SaveKey = "arena";

        public enum Difficulty { Easy, Normal, Hard }

        public static readonly int[] WinPoints = { 12, 20, 32 }, WinCrystals = { 20, 30, 50 }, WinGold = { 3000, 5000, 8000 };

        /// <summary>크리스탈로 도전권 구매: 하루 최대 10장, 2장마다 값이 오름(05:00 초기화).</summary>
        public static readonly int[] BuyCosts = { 50, 100, 150, 200, 250, 300, 400, 500, 600, 700 };   // 1장 살 때마다 오름(50 → 300까지 50씩, 그 뒤 100씩)
        public static string DifficultyName(Difficulty d) => d switch { Difficulty.Easy => "쉬움", Difficulty.Normal => "보통", _ => "어려움" };
        public static Color DifficultyColor(Difficulty d) => d switch
        {
            Difficulty.Easy => new Color(0.5f, 0.9f, 0.55f), Difficulty.Normal => new Color(0.55f, 0.78f, 1f), _ => new Color(1f, 0.5f, 0.45f),
        };

        // ---------------- 티어 ----------------

        public class TierDef
        {
            public string name, frameId;
            public int min;
            public Color color;
        }

        /// <summary>
        /// 티어. 브론즈 ~ 다이아는 점수로 정하고 각 3단계(3 → 2 → 1, 1이 높음) — 시작 1,000점 = 브론즈 3.
        /// 그 위는 점수 + 시즌 순위: 다이아 점수(1,990점) 이상이면서 <b>100위 이내 = 성운 · 50위 이내 = 초신성 · 10위 이내 = 앵커</b>.
        /// </summary>
        public static readonly TierDef[] Tiers =
        {
            new() { name = "브론즈", min = 0, frameId = "", color = new Color(0.8f, 0.55f, 0.35f) },
            new() { name = "실버", min = 1180, frameId = "fa_silver", color = new Color(0.82f, 0.86f, 0.95f) },
            new() { name = "골드", min = 1390, frameId = "fa_gold", color = new Color(1f, 0.83f, 0.4f) },
            new() { name = "플래티넘", min = 1660, frameId = "fa_platinum", color = new Color(0.5f, 0.95f, 0.85f) },
            new() { name = "다이아", min = 1990, frameId = "fa_diamond", color = new Color(0.6f, 0.75f, 1f) },
            new() { name = "성운", min = int.MaxValue, frameId = "fa_nebula", color = new Color(0.85f, 0.55f, 1f) },
            new() { name = "초신성", min = int.MaxValue, frameId = "fa_supernova", color = new Color(1f, 0.7f, 0.35f) },
            new() { name = "앵커", min = int.MaxValue, frameId = "fa_anchor", color = new Color(1f, 0.92f, 0.6f) },
        };

        /// <summary>순위 티어(다이아 점수 이상이면서): 성운 100위 · 초신성 50위 · 앵커 10위 이내.</summary>
        public const int NebulaRank = 100, SupernovaRank = 50, AnchorRank = 10;
        public const int PointTiers = 5;                       // 브론즈 ~ 다이아(점수로 정함)
        public const int NebulaIndex = 5, SupernovaIndex = 6, AnchorIndex = 7;
        private const int BronzeFloor = 1000, TopBand = 600;   // 브론즈 단계는 1,000점부터 · 다이아 단계 폭

        /// <summary>점수로 정해지는 티어(브론즈 ~ 다이아).</summary>
        public static int TierIndex(int points)
        {
            int t = 0;
            for (int i = 0; i < PointTiers; i++) if (points >= Tiers[i].min) t = i;
            return t;
        }

        /// <summary>점수 + 순위로 정해지는 티어 번호(0 ~ 7).</summary>
        public static int TierIndex(int points, int rank)
        {
            if (rank >= 1 && points >= Tiers[PointTiers - 1].min)
            {
                if (rank <= AnchorRank) return AnchorIndex;
                if (rank <= SupernovaRank) return SupernovaIndex;
                if (rank <= NebulaRank) return NebulaIndex;
            }
            return TierIndex(points);
        }

        private static readonly string[] IconNames = { "bronze", "silver", "gold", "platinum", "diamond", "nebula", "supernova", "anchor" };

        /// <summary>티어 아이콘 경로(Resources/Art 기준): 점수 티어는 단계마다 다름(Tiers/gold_2), 순위 티어는 하나(Tiers/anchor).</summary>
        public static string IconKey(int points, int rank = 0)
        {
            int idx = TierIndex(points, rank);
            return idx >= PointTiers ? $"Tiers/{IconNames[idx]}" : $"Tiers/{IconNames[idx]}_{Division(points)}";
        }

        public static Sprite Icon(int points, int rank = 0) => GameArt.Load(IconKey(points, rank)) ?? GameArt.Load("Items/ArenaCoin");

        /// <summary>점수 티어 단계 번호: 0 = 브론즈 3 … 14 = 다이아 1.</summary>
        public static int LevelOf(int points) => TierIndex(points) * 3 + (3 - Division(points));

        public static string LevelName(int level)
        {
            level = Mathf.Clamp(level, 0, PointTiers * 3 - 1);
            return $"{Tiers[level / 3].name} {3 - level % 3}";
        }

        public static string LevelIconKey(int level)
        {
            level = Mathf.Clamp(level, 0, PointTiers * 3 - 1);
            return $"Tiers/{IconNames[level / 3]}_{3 - level % 3}";
        }

        /// <summary>
        /// 승급 보상(그 단계에 처음 오를 때 한 번): 단계 승급 = 크리스탈 30 + 티어 × 15 · 금화 5,000 × (티어+1),
        /// 새 티어 진입(3단계) = 크리스탈 100 + 티어 × 50 · 금화 20,000 × (티어+1), 골드부터는 상시 뽑기권 1장 추가.
        /// </summary>
        public static RewardBundle PromotionReward(int level)
        {
            int tier = level / 3;
            bool newTier = level % 3 == 0;
            var r = new RewardBundle()
                .Crystals(newTier ? 100 + tier * 50 : 30 + tier * 15)
                .Gold(newTier ? 20000 * (tier + 1) : 5000 * (tier + 1));
            if (newTier && tier >= 2) r.Standard(1);
            return r;
        }

        /// <summary>순위 티어 조건 문구.</summary>
        public static string RankTierCondition(int index) => index switch
        {
            AnchorIndex => $"다이아 이상 · 시즌 {AnchorRank}위 이내",
            SupernovaIndex => $"다이아 이상 · 시즌 {SupernovaRank}위 이내",
            NebulaIndex => $"다이아 이상 · 시즌 {NebulaRank}위 이내",
            _ => "",
        };

        /// <summary>순위 티어 테두리 비트(결투장 정산 때 기록): 앵커 8 · 초신성 16 · 성운 32 — 높은 티어는 아래 티어 테두리도 포함.</summary>
        public static int RankTierBits(int index) => index switch { AnchorIndex => 8 | 16 | 32, SupernovaIndex => 16 | 32, NebulaIndex => 32, _ => 0 };

        /// <summary>티어 안의 단계(3 · 2 · 1 — 1이 높음).</summary>
        public static int Division(int points)
        {
            int i = TierIndex(points);
            int lo = i == 0 ? BronzeFloor : Tiers[i].min;
            int hi = i + 1 < PointTiers ? Tiers[i + 1].min : Tiers[i].min + TopBand;
            float step = (hi - lo) / 3f;
            int d = Mathf.Clamp(Mathf.FloorToInt((points - lo) / step), 0, 2);
            return 3 - d;
        }

        /// <summary>티어 이름(예: '골드 2', 순위 티어는 '성운' · '초신성' · '앵커') · 색. rank = 시즌 순위(모르면 0 — 점수 티어만).</summary>
        public static (string name, Color color) Tier(int points, int rank = 0)
        {
            int idx = TierIndex(points, rank);
            var t = Tiers[idx];
            return idx >= PointTiers ? (t.name, t.color) : ($"{t.name} {Division(points)}", t.color);
        }

        // ---------------- 랭킹(시즌 = 1주) ----------------

        /// <summary>
        /// 시즌 랭킹 규모: 가상 지휘관 5,000명(점수는 분포로 계산 — 필요한 줄만 만듦) + 실제 서버 플레이어 + 나.
        /// 순위표에는 상위 100명 + 내 주변(위아래 3명)을 보여 줌.
        /// </summary>
        public const int VirtualPlayers = 5000, ShowTop = 100, AroundSpan = 3;
        public static readonly string[] RankFrames = { "fr_1", "fr_2", "fr_3" };

        /// <summary>현실적으로 가능한 최대 점수(하루 15판 × 7일 × 최대 +32 + 지난 시즌 이월보다 넉넉히). 넘으면 순위에서 뺌(조작 방지).</summary>
        public const int MaxPlausiblePoints = 12000;

        public class RankEntry
        {
            public int rank, points, seed;
            public string name;
            public bool me;
            public string pid;      // 실제 플레이어(서버) id — 가상 지휘관이면 null
            public SubcultureRPG.Meta.Cloud.ProfileSnap snap;   // 실제 플레이어의 공개 프로필(불러왔으면)
            public bool Real => !string.IsNullOrEmpty(pid);
        }

        /// <summary>시즌 정산 결과(결투장 화면에 들어갈 때 한 번 보여 주고 지급).</summary>
        public class Settlement
        {
            public int rank, points, crystals, newPoints, total;
            public string frameId;
            public int tierIndex;   // 정산 때 티어(성운 · 초신성 · 앵커면 그 테두리)
        }

        /// <summary>시즌 정산 크리스탈: 1 · 2 · 3위 · 10위 · 100위 · 상위 5 · 10 · 30% · 그 밖.</summary>
        public static int SeasonCrystals(int rank, int total)
        {
            if (rank == 1) return 1500;
            if (rank == 2) return 1000;
            if (rank == 3) return 700;
            if (rank <= 10) return 500;
            if (rank <= 100) return 300;
            float pct = rank / (float)Mathf.Max(1, total);
            return pct <= 0.05f ? 200 : pct <= 0.10f ? 150 : pct <= 0.30f ? 120 : 100;
        }

        /// <summary>순위 → '상위 x%'(0.1% 단위).</summary>
        public static string Percent(int rank, int total)
        {
            float p = 100f * rank / Mathf.Max(1, total);
            return p < 1f ? $"상위 {Mathf.Max(0.1f, p):0.0}%" : $"상위 {p:0}%";
        }

        /// <summary>시즌 진행도(0 = 막 시작, 1 = 끝). 다른 지휘관의 점수는 시즌이 갈수록 오름.</summary>
        private static float SeasonProgress()
        {
            var left = PlayerShop.UntilReset(ShopReset.Weekly, DateTime.Now) ?? TimeSpan.Zero;
            return Mathf.Clamp01(1f - (float)(left.TotalDays / 7.0));
        }

        /// <summary>가상 지휘관 i번째(0 = 1등)의 점수 — 소수의 최상위 + 두꺼운 중간층(대부분 브론즈 ~ 골드).</summary>
        private static int VirtualPoints(int i, float progress)
        {
            double x = i / (double)(VirtualPlayers - 1);
            double grow = 0.6 + 0.4 * progress;
            return 900 + (int)Math.Round((2400 * Math.Exp(-x / 0.02) + 900 * Math.Exp(-x / 0.25)) * grow);
        }

        /// <summary>점수가 pts보다 높은 가상 지휘관 수(이분 탐색).</summary>
        private static int VirtualAbove(int pts, float progress)
        {
            int lo = 0, hi = VirtualPlayers;
            while (lo < hi) { int mid = (lo + hi) / 2; if (VirtualPoints(mid, progress) > pts) lo = mid + 1; else hi = mid; }
            return lo;
        }

        private static int VirtualSeed(int h, int i) => 1000 + (int)((uint)(h + i * 7919) % 899_000u);

        /// <summary>그 시즌의 실제 플레이어(나 제외).</summary>
        private static List<RankEntry> RealEntries(string season)
        {
            var list = new List<RankEntry>();
            var pp = SubcultureRPG.Meta.Cloud.PublicProfile.Instance;
            string me = SubcultureRPG.Meta.Cloud.CloudAccount.PlayerId;
            foreach (var seen in pp.SeenInSeason(season))
            {
                if (seen == null || string.IsNullOrEmpty(seen.pid) || seen.pid == me) continue;
                if (seen.points < 0 || seen.points > MaxPlausiblePoints) continue;
                var snap = season == SubcultureRPG.Meta.Cloud.PublicProfile.SeasonKey ? pp.ArenaPlayers.Find(x => x.pid == seen.pid) : null;
                list.Add(new RankEntry { points = seen.points, name = seen.name, seed = -1, pid = seen.pid, snap = snap });
            }
            list.Sort((a, b) => b.points.CompareTo(a.points));
            return list;
        }

        /// <summary>순위표 한 화면 분량: 상위 100명 · 내 주변 · 나 · 친구.</summary>
        public class Board
        {
            public List<RankEntry> top = new(), around = new(), friends = new();
            public RankEntry me;
            public int total;
        }

        // 순위 규칙: 같은 점수면 나 → 실제 플레이어 → 가상 지휘관 순으로 위
        private static int RankOfVirtual(int i, float progress, List<RankEntry> reals, int myPoints)
        {
            int vp = VirtualPoints(i, progress), above = i;
            foreach (var r in reals) if (r.points >= vp) above++;
            if (myPoints >= vp) above++;
            return above + 1;
        }

        private static int RankOfReal(int idx, float progress, List<RankEntry> reals, int myPoints)
        {
            int pts = reals[idx].points, above = VirtualAbove(pts, progress) + idx;   // reals는 점수 순
            if (myPoints >= pts) above++;
            return above + 1;
        }

        private static int RankOfMe(float progress, List<RankEntry> reals, int myPoints)
        {
            int above = VirtualAbove(myPoints, progress);
            foreach (var r in reals) if (r.points > myPoints) above++;
            return above + 1;
        }

        /// <summary>점수 pts인 사람(나 · 실제 플레이어가 아닌 다른 지휘관)이 받을 순위.</summary>
        private static int RankOfOther(int pts, float progress, List<RankEntry> reals, int myPoints)
        {
            int above = VirtualAbove(pts, progress);
            foreach (var r in reals) if (r.points >= pts) above++;
            if (myPoints >= pts) above++;
            return above + 1;
        }

        private RankEntry VirtualEntry(int h, int i, float progress, List<RankEntry> reals, int myPoints)
        {
            int seed = VirtualSeed(h, i);
            return new RankEntry { points = VirtualPoints(i, progress), name = Commanders.FromSeed(seed, null).name, seed = seed, rank = RankOfVirtual(i, progress, reals, myPoints) };
        }

        /// <summary>가상 친구(가상 지휘관)의 이번 시즌 점수 — 지휘관 레벨이 높을수록, 시즌이 갈수록 높음.</summary>
        private static int FriendPoints(int seed, string season, float progress)
        {
            var c = Commanders.FromSeed(seed, null);
            int jitter = (int)((uint)(Commanders.Hash(season) + seed * 31) % 80u);
            return 950 + Mathf.RoundToInt(c.level * 26f * (0.6f + 0.4f * progress)) + jitter;
        }

        /// <summary>순위표(전체 + 친구).</summary>
        public Board BuildBoard()
        {
            EnsureLoaded();
            string season = _d.season;
            float progress = SeasonProgress();
            int h = Commanders.Hash(season), my = _d.points;
            var reals = RealEntries(season);
            var b = new Board { total = VirtualPlayers + reals.Count + 1 };
            b.me = new RankEntry { points = my, name = PlayerProfile.Instance.DisplayName, me = true, rank = RankOfMe(progress, reals, my) };

            // 상위 100명
            var top = new List<RankEntry>();
            for (int i = 0; i < Mathf.Min(ShowTop, VirtualPlayers); i++) top.Add(VirtualEntry(h, i, progress, reals, my));
            for (int k = 0; k < reals.Count; k++) { reals[k].rank = RankOfReal(k, progress, reals, my); top.Add(reals[k]); }
            top.Add(b.me);
            top.RemoveAll(e => e.rank > ShowTop);
            top.Sort((x, y) => x.rank.CompareTo(y.rank));
            b.top = top;

            // 내 주변(100위 밖이면)
            if (b.me.rank > ShowTop)
            {
                int v = VirtualAbove(my, progress);
                var around = new List<RankEntry>();
                for (int i = Mathf.Max(0, v - AroundSpan - 1); i < Mathf.Min(VirtualPlayers, v + AroundSpan + 1); i++) around.Add(VirtualEntry(h, i, progress, reals, my));
                foreach (var r in reals) around.Add(r);
                around.Add(b.me);
                around.RemoveAll(e => Math.Abs(e.rank - b.me.rank) > AroundSpan);
                around.Sort((x, y) => x.rank.CompareTo(y.rank));
                b.around = around;
            }

            // 친구: 실제 플레이어 친구(이번 시즌 기록이 있으면) + 가상 친구
            var social = PlayerSocial.Instance;
            var friends = new List<RankEntry> { b.me };
            foreach (var f in social.RealFriends)
            {
                var r = reals.Find(x => x.pid == f.pid);
                if (r != null) friends.Add(r);
            }
            foreach (int seed in social.Friends)
            {
                int pts = FriendPoints(seed, season, progress);
                friends.Add(new RankEntry { points = pts, name = Commanders.FromSeed(seed, null).name, seed = seed, rank = RankOfOther(pts, progress, reals, my) });
            }
            friends.Sort((x, y) => x.rank != y.rank ? x.rank.CompareTo(y.rank) : y.me.CompareTo(x.me));
            b.friends = friends;
            return b;
        }

        /// <summary>내 시즌 순위 · 전체 인원(가볍게 — 이름은 만들지 않음).</summary>
        private (int rank, int total) MyRankOf(string season, float progress, int points)
        {
            var reals = RealEntries(season);
            return (RankOfMe(progress, reals, points), VirtualPlayers + reals.Count + 1);
        }


        public int MyRank { get { EnsureLoaded(); return MyRankOf(_d.season, SeasonProgress(), _d.points).rank; } }
        public int RankTotal { get { EnsureLoaded(); return MyRankOf(_d.season, SeasonProgress(), _d.points).total; } }
        /// <summary>내 티어(10위 이내면 앵커).</summary>
        public (string name, Color color) MyTier => Tier(Points, MyRank);
        public int BestPoints => Mathf.Max(_d.best, _d.points);
        public int RankFrameBits => _d.rankFrames;
        public static TimeSpan UntilSeasonEnd => PlayerShop.UntilReset(ShopReset.Weekly, DateTime.Now) ?? TimeSpan.Zero;

        /// <summary>시즌이 바뀌었으면 정산: 지난 시즌 순위 기록 → 점수 일부 초기화(1000 초과분의 75% 유지).</summary>
        private void CheckSeason()
        {
            string now = PlayerShop.PeriodKey(ShopReset.Weekly, DateTime.Now);
            if (_d.season == now) return;
            if (!string.IsNullOrEmpty(_d.season) && _d.seasonGames > 0) Settle(_d.season);
            _d.season = now;
            _d.seasonGames = 0;
            Save();
        }

        private void Settle(string season)
        {
            var (rank, total) = MyRankOf(season, 1f, _d.points);
            _d.best = Mathf.Max(_d.best, _d.points);
            _d.settleRank = rank;
            _d.settleTotal = total;
            _d.settlePoints = _d.points;
            if (rank <= 3) _d.rankFrames |= 1 << (rank - 1);
            _d.rankFrames |= RankTierBits(TierIndex(_d.points, rank));   // 성운 · 초신성 · 앵커 테두리
            if (_d.points > StartPoints) _d.points = StartPoints + Mathf.RoundToInt((_d.points - StartPoints) * 0.75f);
            _d.opponents.Clear();
        }

        /// <summary>아직 보여 주지 않은 승급(from → to 단계)과 그동안 받은 보상. 없으면 null.</summary>
        public (int from, int to, RewardBundle reward)? TakePromotion()
        {
            EnsureLoaded();
            if (_d.promoTo < 0) return null;
            int from = _d.promoFrom, to = _d.promoTo;
            var total = new RewardBundle();
            for (int l = Mathf.Max(0, from + 1); l <= to; l++)
            {
                var r = PromotionReward(l);
                total.Crystals(r.crystals).Gold(r.gold).Standard(r.standard);
            }
            _d.promoFrom = -1; _d.promoTo = -1;
            Save();
            return (from, to, total);
        }

        /// <summary>받지 않은 시즌 정산이 있으면 보상을 지급하고 돌려줌(없으면 null).</summary>
        public Settlement TakeSettlement()
        {
            EnsureLoaded();
            if (_d.settleRank <= 0) return null;
            var s = new Settlement
            {
                rank = _d.settleRank, points = _d.settlePoints, newPoints = _d.points, total = Mathf.Max(_d.settleTotal, _d.settleRank),
                crystals = SeasonCrystals(_d.settleRank, Mathf.Max(_d.settleTotal, _d.settleRank)),
                frameId = _d.settleRank <= 3 ? RankFrames[_d.settleRank - 1] : null, tierIndex = TierIndex(_d.settlePoints, _d.settleRank),
            };
            var wallet = FindAnyObjectByType<PlayerWallet>();
            if (wallet != null) wallet.Add(s.crystals);
            _d.settleRank = 0;
            Changed();
            return s;
        }

        // ---------------- 저장 ----------------

        [Serializable]
        public class Opponent
        {
            public string name;
            public int difficulty;
            public int level, transcend, skillLevel, power, points;
            public List<string> team = new();   // 캐릭터 id
            // 실제 플레이어 상대(pid가 있으면): 그 사람의 공개 프로필(상위 5명의 육성 · 장비) 그대로 싸움
            public string pid = "", snapJson = "", frameId = "", avatarId = "", avatarOutfit = "";
            public float gearPct;   // 가상 상대: 장비 대신 체력 · 공격 · 방어 보정(티어가 높을수록 큼)
            public List<int> levels = new(), stars = new();
            public bool Real => !string.IsNullOrEmpty(pid);
            public ProfileSnap Snap => Real && !string.IsNullOrEmpty(snapJson) ? PublicProfile.ParseSnap(snapJson, pid) : null;
            public Difficulty Diff => (Difficulty)difficulty;
        }

        [Serializable]
        private class SaveData
        {
            public int points = StartPoints, tickets = MaxTickets, wins, losses;
            public string ticketDay = "";
            public int bought; public string boughtDay = "";   // 오늘 크리스탈로 산 도전권
            public List<Opponent> opponents = new();
            public int best;                 // 역대 최고 점수(티어 테두리 해금)
            public int rankFrames;           // 받은 시즌 순위 테두리(비트: 1위 · 2위 · 3위)
            public string season = "";       // 지금 시즌(주) 키
            public int seasonGames;          // 이번 시즌에 치른 판 수(0이면 정산 없음)
            public int settleRank, settlePoints, settleTotal;   // 아직 보여 주지 않은 정산(순위 > 0)
            public int bestLevel = -1;       // 오른 적 있는 가장 높은 단계(승급 보상은 처음 한 번)
            public long refreshReady;        // 무료 '상대 새로고침'이 다시 되는 시각(UTC ticks)
            public int promoFrom = -1, promoTo = -1;   // 아직 보여 주지 않은 승급 축하
        }

        private SaveData _d = new();
        private bool _loaded;

        public event Action OnChanged;

        private static PlayerArena _instance;
        public static PlayerArena Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<PlayerArena>();
                if (_instance == null) _instance = new GameObject("PlayerArena").AddComponent<PlayerArena>();
                _instance.EnsureLoaded();
                return _instance;
            }
        }

        public int Points => _d.points;
        public int Wins => _d.wins;
        public int Losses => _d.losses;
        public IReadOnlyList<Opponent> Opponents { get { EnsureOpponents(); return _d.opponents; } }

        public int Tickets
        {
            get
            {
                string today = PlayerShop.PeriodKey(ShopReset.Daily, DateTime.Now);
                if (_d.ticketDay != today) { _d.ticketDay = today; _d.tickets = Mathf.Max(_d.tickets, MaxTickets); Save(); }
                return _d.tickets;
            }
        }

        /// <summary>오늘 크리스탈로 산 도전권 수.</summary>
        public int BoughtToday { get { _ = Tickets; return _d.boughtDay == _d.ticketDay ? _d.bought : 0; } }

        /// <summary>다음 1장 값(크리스탈). 오늘 더 살 수 없으면 -1.</summary>
        public int NextBuyCost => BoughtToday < BuyCosts.Length ? BuyCosts[BoughtToday] : -1;

        /// <summary>크리스탈로 도전권 1장 구매(최대 개수를 넘어도 쌓임). 실패 사유를 돌려줌(성공이면 null).</summary>
        public string BuyTicket()
        {
            int b = BoughtToday, cost = NextBuyCost;
            if (cost < 0) return "오늘은 더 구매할 수 없습니다 (매일 05:00 초기화)";
            var wallet = FindAnyObjectByType<PlayerWallet>();
            if (wallet == null || !wallet.Spend(cost)) return $"크리스탈이 부족합니다 (필요 {cost:N0})";
            _d.tickets++;
            _d.bought = b + 1; _d.boughtDay = _d.ticketDay;
            Changed();
            return null;
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
            EnsureLoaded();
        }

        private void EnsureLoaded()
        {
            if (!_loaded)
            {
                _loaded = true;
                if (SaveStore.HasKey(SaveKey))
                {
                    try { _d = JsonUtility.FromJson<SaveData>(SaveStore.GetString(SaveKey)) ?? new SaveData(); }
                    catch { _d = new SaveData(); }
                }
                if (_d.opponents == null) _d.opponents = new List<Opponent>();
                // 승급 보상이 생기기 전의 기록은 이미 오른 것으로(보상을 소급하지 않음)
                if (_d.bestLevel < 0) _d.bestLevel = LevelOf(Mathf.Max(_d.points, _d.best));
            }
            CheckSeason();
        }

        private void Save()
        {
            SaveStore.SetString(SaveKey, JsonUtility.ToJson(_d));
            SaveStore.Save();
        }

        private void Changed() { Save(); OnChanged?.Invoke(); }

        // ---------------- 상대 ----------------

        private static readonly string[] Names =
        {
            "별빛여행자", "새벽의검", "고양이집사", "푸른달", "불꽃손", "얼음공주", "바람개비", "번개도령", "철벽수호자", "밤하늘",
            "검은고양이", "은빛날개", "모험왕", "초보지휘관", "전설의시작", "달빛산책", "용사지망생", "한입만", "오늘도출근", "닻내림",
        };

        private void EnsureOpponents()
        {
            if (_d.opponents != null && _d.opponents.Count == 3) return;
            Refresh(false);
        }

        /// <summary>상대 3명을 새로 뽑음(쉬움 · 보통 · 어려움).</summary>
        /// <summary>'상대 새로고침': 무료는 3분에 한 번, 그 전에는 크리스탈로(누를 때마다 다시 3분). 이기면 상대가 무료로 새로 나옴(쿨타임과 별개).</summary>
        public const int RefreshCooldown = 180, RefreshCost = 20;

        /// <summary>무료 새로고침까지 남은 초(0이면 무료).</summary>
        public int RefreshSecondsLeft
        {
            get
            {
                EnsureLoaded();
                double s = (new DateTime(Math.Max(0, _d.refreshReady), DateTimeKind.Utc) - DateTime.UtcNow).TotalSeconds;
                return s <= 0 ? 0 : Mathf.Min(RefreshCooldown, Mathf.CeilToInt((float)s));
            }
        }

        /// <summary>버튼으로 상대 새로고침. 쿨타임 중이면 크리스탈을 씀(paid). 실패 사유(성공이면 null).</summary>
        public string ManualRefresh(bool paid)
        {
            if (RefreshSecondsLeft > 0)
            {
                if (!paid) return "쿨타임 중입니다";
                var wallet = FindAnyObjectByType<PlayerWallet>();
                if (wallet == null || !wallet.Spend(RefreshCost)) return $"크리스탈이 부족합니다 (필요 {RefreshCost})";
            }
            _d.refreshReady = DateTime.UtcNow.AddSeconds(RefreshCooldown).Ticks;
            Refresh();
            return null;
        }

        public void Refresh(bool notify = true)
        {
            var roster = FindAnyObjectByType<PlayerRoster>();
            var db = roster != null ? roster.Database : null;
            _d.opponents = new List<Opponent>();
            if (db == null || db.Characters.Count == 0) { if (notify) Changed(); return; }

            // 내 주력 전투력(실제 플레이어 매칭용 — 가상 상대의 강함은 상대 점수로 정함)
            var mine = CombatPower.All(roster, FindAnyObjectByType<PlayerEquipment>());

            // 실제 플레이어 후보(이번 시즌 결투장 기록이 있는 사람) — 내 주력 4명 전투력 대비로 난이도 칸에 나눔
            long myPower = 0; int m = 0;
            foreach (var kv in mine) { if (m++ >= 4) break; myPower += kv.Value; }
            var buckets = RealCandidates(db, myPower);

            var usedNames = new HashSet<string>();
            for (int i = 0; i < 3; i++)
            {
                if (buckets[i].Count > 0)
                {
                    var (snap, power) = buckets[i][UnityEngine.Random.Range(0, buckets[i].Count)];
                    _d.opponents.Add(MakeReal(snap, power, i, db));
                    usedNames.Add(snap.name);
                    continue;
                }
                var o = new Opponent { difficulty = i };
                do { o.name = Names[UnityEngine.Random.Range(0, Names.Length)]; } while (!usedNames.Add(o.name) && usedNames.Count < Names.Length);
                o.points = Mathf.Max(0, _d.points + (i - 1) * 80 + UnityEngine.Random.Range(-40, 41));
                // 강함은 내 전투력이 아니라 상대의 점수(티어 분포)로 정함: 높은 티어일수록 레벨 · 초월 · 스킬 · 장비 보정 · 고성급 캐릭터
                float t = TierStrength(o.points);
                o.level = Mathf.Clamp(Mathf.RoundToInt(12 + 62 * t) + UnityEngine.Random.Range(-3, 4), 1, Leveling.AwakenedMaxLevel);
                o.transcend = t < 0.55f ? 0 : Mathf.Clamp(Mathf.FloorToInt((t - 0.55f) / 0.13f) + UnityEngine.Random.Range(0, 2), 0, Transcendence.MaxTranscend);
                o.skillLevel = Mathf.Clamp(1 + Mathf.RoundToInt(9 * t), 1, Leveling.MaxSkillLevel);
                o.gearPct = 0.9f * t;

                var pool = new List<CharacterData>(db.Characters);
                pool.RemoveAll(c => c == null);
                float highChance = Mathf.Clamp01(0.15f + 0.6f * t);   // 태생 5★ 이상을 고를 확률
                for (int k = 0; k < 4 && pool.Count > 0; k++)
                {
                    bool wantHigh = UnityEngine.Random.value < highChance;
                    var cands = pool.FindAll(c => ((int)c.rarity >= 5) == wantHigh);
                    if (cands.Count == 0) cands = pool;
                    var ch = cands[UnityEngine.Random.Range(0, cands.Count)];
                    o.team.Add(!string.IsNullOrEmpty(ch.id) ? ch.id : ch.name);
                    pool.Remove(ch);
                }
                o.power = PowerOf(o, db);
                _d.opponents.Add(o);
            }
            if (notify) Changed(); else Save();
        }

        /// <summary>
        /// 난이도 칸(쉬움 · 보통 · 어려움)별 실제 플레이어 후보. 상대 주력 4명 전투력 ÷ 내 전투력:
        /// 0.9 미만 = 쉬움, 0.9 ~ 1.12 = 보통, 1.12 ~ 2.0 = 어려움(2배 넘게 강하면 제외). 방금 이긴 상대는 빼고.
        /// </summary>
        private List<(ProfileSnap snap, long power)>[] RealCandidates(GameDatabase db, long myPower)
        {
            var b = new[] { new List<(ProfileSnap, long)>(), new List<(ProfileSnap, long)>(), new List<(ProfileSnap, long)>() };
            if (!PublicProfile.Online || db == null || myPower <= 0) return b;
            foreach (var s in PublicProfile.Instance.ArenaPlayers)
            {
                if (s == null || s.top == null || s.top.Count == 0 || s.pid == _lastBeaten || s.pid == CloudAccount.PlayerId) continue;
                long p = FriendlyBattle.LineupPower(s, db);
                if (p <= 0) continue;
                double r = p / (double)myPower;
                int d = r < 0.9 ? 0 : r < 1.12 ? 1 : r <= 2.0 ? 2 : -1;
                if (d >= 0) b[d].Add((s, p));
            }
            return b;
        }

        private string _lastBeaten = "";

        private static Opponent MakeReal(ProfileSnap s, long power, int difficulty, GameDatabase db)
        {
            var o = new Opponent
            {
                name = s.name, difficulty = difficulty, points = s.arenaPoints, power = (int)Math.Min(int.MaxValue, power),
                pid = s.pid, snapJson = JsonUtility.ToJson(s), frameId = s.frameId ?? "", avatarId = s.avatarId ?? "", avatarOutfit = s.avatarOutfit ?? "",
            };
            int sum = 0;
            var lineup = FriendlyBattle.Lineup(s, db);
            foreach (var (cs, _) in lineup)
            {
                o.team.Add(cs.id); o.levels.Add(cs.level); o.stars.Add(cs.star);
                sum += cs.level;
                if (cs.star >= Transcendence.MaxStar) o.transcend = Mathf.Max(o.transcend, cs.transcend);
            }
            o.level = lineup.Count > 0 ? sum / lineup.Count : 1;
            return o;
        }

        /// <summary>점수 → 가상 상대의 강함(0 = 1,000점 브론즈 · 1 = 3,000점 성운급 · 최대 1.4).</summary>
        public static float TierStrength(int points) => Mathf.Clamp((points - 1000) / 2000f, 0f, 1.4f);

        /// <summary>상대 팀을 전투 유닛으로(적 진영).</summary>
        public List<BattleUnitData> BuildTeam(Opponent o, GameDatabase db)
        {
            var list = new List<BattleUnitData>();
            if (o == null || db == null) return list;
            if (o.Real)
            {
                // 실제 플레이어: 공개 프로필의 상위 4명을 키운 그대로(레벨 · 성급 · 초월 · 스킬 · 장비 · 호감도)
                var snap = o.Snap;
                if (snap != null)
                    foreach (var (cs, _) in FriendlyBattle.Lineup(snap, db))
                    {
                        var u = SnapStats.BuildUnit(cs, db);
                        if (u != null) list.Add(u);
                    }
                return list;
            }
            foreach (var id in o.team)
            {
                var c = db.GetCharacter(id);
                if (c == null) continue;
                int star = o.transcend > 0 ? Transcendence.MaxStar : (int)c.rarity;
                var lv = new[] { o.skillLevel, o.skillLevel, o.skillLevel };
                var data = RuntimeUnits.FromCharacter(c, o.level, null, o.skillLevel, star, o.transcend, lv);
                if (o.gearPct > 0f) data.bonus = new StatBonus { hpPct = o.gearPct, atkPct = o.gearPct, defPct = o.gearPct * 0.8f };   // 장비 대신
                list.Add(data);
            }
            return list;
        }

        private int PowerOf(Opponent o, GameDatabase db)
        {
            var cfg = BattleConfig.CreateDefault();
            int sum = 0;
            foreach (var data in BuildTeam(o, db))
            {
                sum += CombatPower.Of(new BattleUnit(data, Team.Enemy, cfg), new[] { o.skillLevel, o.skillLevel, o.skillLevel });
                if (data.skills != null) foreach (var sk in data.skills) if (sk != null) Destroy(sk);
                Destroy(data);
            }
            Destroy(cfg);
            return sum;
        }

        public CharacterData Leader(Opponent o, GameDatabase db)
        {
            if (o == null || db == null) return null;
            if (o.Real && !string.IsNullOrEmpty(o.avatarId)) { var a = db.GetCharacter(o.avatarId); if (a != null) return a; }
            return o.team.Count > 0 ? db.GetCharacter(o.team[0]) : null;
        }

        /// <summary>지금 상대 중 실제 플레이어가 있는가.</summary>
        public bool HasRealOpponent { get { foreach (var o in Opponents) if (o.Real) return true; return false; } }

        // ---------------- 전투 ----------------

        private Opponent _fighting;
        private List<CharacterData> _party;
        private bool _paid, _pending, _active;
        private readonly List<BattleUnitData> _spawned = new();
        private BattleController _controller;
        private BattleHUD _hud;

        /// <summary>도전(편성 화면의 '출격!'). 도전권이 없으면 false.</summary>
        public bool Fight(Opponent o, IList<CharacterData> party)
        {
            if (o == null || party == null || party.Count == 0) return false;
            var launcher = FindAnyObjectByType<BattleLauncher>();
            var roster = FindAnyObjectByType<PlayerRoster>();
            if (launcher == null || roster == null || roster.Database == null) return false;
            Refund();
            if (Tickets <= 0) { _hud?.ShowResultToast("결투장 도전권이 없습니다 (매일 05:00 충전)"); return false; }
            _d.tickets--;
            _paid = true;
            Changed();

            Hook();
            Cleanup();
            var enemies = BuildTeam(o, roster.Database);
            _spawned.AddRange(enemies);
            _fighting = o;
            _party = new List<CharacterData>(party);
            _pending = true;
            if (_hud != null)
            {
                _hud.SetBackground("Backgrounds/battle_arena", "Backgrounds/arena", "Backgrounds/battle");
                _hud.SetRetryHidden(true);   // 같은 상대와 연달아 싸우지 않음 — 결투장으로 돌아가 새로 고름
                _hud.SetExitLabel("결투장으로");
                _hud.SetRoundLimit(0);
            }
            launcher.Launch(_party, enemies);
            return true;
        }

        /// <summary>결투장을 떠날 때(끝나지 않은 전투면 도전권 반환).</summary>
        public void Leave()
        {
            Refund();
            _active = false; _pending = false;
            if (_hud != null) { _hud.SetRetryHandler(null); _hud.SetRetryHidden(false); }
        }

        private void Refund()
        {
            if (!_paid) return;
            _paid = false;
            _d.tickets += 1;   // 산 도전권도 그대로 돌려줌
            Changed();
        }

        private void Hook()
        {
            if (_controller != null) return;
            _controller = FindAnyObjectByType<BattleController>();
            _hud = FindAnyObjectByType<BattleHUD>();
            if (_controller == null) return;
            _controller.OnBattleStart += _ => { _active = _pending; _pending = false; };
            _controller.OnBattleEnd += HandleEnd;
        }

        private void Cleanup()
        {
            foreach (var u in _spawned)
            {
                if (u == null) continue;
                if (u.skills != null) foreach (var sk in u.skills) if (sk != null) Destroy(sk);
                Destroy(u);
            }
            _spawned.Clear();
        }

        private void HandleEnd(Team winner)
        {
            if (!_active || _fighting == null) return;
            _active = false;
            _paid = false;   // 끝까지 싸웠으면 도전권 소모 확정
            var o = _fighting;
            var wallet = FindAnyObjectByType<PlayerWallet>();
            int before = _d.points;
            var tierBefore = Tier(before).name;   // 결과 문구는 점수 티어(앵커는 랭킹에서)
            int bestTierBefore = TierIndex(BestPoints);
            _d.seasonGames++;
            SubcultureRPG.Meta.Cloud.PublicProfile.Instance.RequestPublish();   // 서버 순위표에 새 점수(1초 뒤 — 점수 반영 후)
            string detail;
            if (winner == Team.Ally)
            {
                int d = (int)o.Diff;
                _d.points += WinPoints[d];
                _d.wins++;
                if (wallet != null) { wallet.Add(WinCrystals[d]); wallet.AddGold(WinGold[d]); }
                detail = $"<size=110%>결투장 — {o.name}{(o.Real ? "  <size=70%><color=#7CFF9A>● 플레이어</color></size>" : "")}  <color=#{ColorUtility.ToHtmlStringRGB(DifficultyColor(o.Diff))}>{DifficultyName(o.Diff)}</color></size>\n" +
                         $"<color=#7CFF9A>랭크 점수 {before:N0} → {_d.points:N0} (+{WinPoints[d]})</color>\n" +
                         $"<color=#FFD36B>획득</color>  <color=#8FE9FF>크리스탈 {WinCrystals[d]}</color>  ·  <color=#FFD36B>금화 {WinGold[d]:N0}</color>";
                _lastBeaten = o.pid ?? "";
                Refresh(false);   // 이기면 상대 새로(방금 이긴 실제 플레이어는 바로 다시 나오지 않음)
            }
            else
            {
                _d.points = Mathf.Max(0, _d.points - LosePoints);
                _d.losses++;
                if (wallet != null) wallet.AddGold(LoseGold);
                detail = $"<size=110%>결투장 — {o.name}{(o.Real ? "  <size=70%><color=#7CFF9A>● 플레이어</color></size>" : "")}</size>\n<color=#FF8A8A>랭크 점수 {before:N0} → {_d.points:N0} (-{LosePoints})</color>\n" +
                         $"<color=#FFD36B>참가 보상</color>  금화 {LoseGold:N0}";
            }
            // 승급: 처음 오르는 단계마다 보상, 결투장 화면으로 돌아가면 축하 창
            int lv = LevelOf(_d.points);
            if (lv > _d.bestLevel)
            {
                var total = new RewardBundle();
                for (int l = _d.bestLevel + 1; l <= lv; l++)
                {
                    var r = PromotionReward(l);
                    r.Grant();
                    total.Crystals(r.crystals).Gold(r.gold).Standard(r.standard);
                }
                _d.promoFrom = _d.promoFrom >= 0 ? _d.promoFrom : _d.bestLevel;
                _d.promoTo = lv;
                _d.bestLevel = lv;
                detail += $"\n<color=#FFD36B>승급! {LevelName(lv)}</color>  <color=#8FE9FF>{total.Describe()}</color>";
            }
            string tierAfter = Tier(_d.points).name;
            if (tierAfter != tierBefore) detail += $"\n<color=#FFD36B>티어 변경: {tierBefore} → {tierAfter}</color>";
            _d.best = Mathf.Max(_d.best, _d.points);
            for (int t = bestTierBefore + 1; t <= TierIndex(_d.best) && t < PointTiers; t++)
                detail += $"\n<color=#7CFF9A>첫 {Tiers[t].name} 달성! 테두리 '{ProfileRewards.Frame(Tiers[t].frameId).name}' 획득</color>";
            var (mr, mt) = MyRankOf(_d.season, SeasonProgress(), _d.points);
            detail += $"\n<size=80%><color=#9AA3B5>시즌 순위 {mr:N0}위 / {mt:N0}명 ({Percent(mr, mt)})</color></size>";
            detail += $"\n<size=80%><color=#9AA3B5>남은 도전권 {Tickets} / {MaxTickets}</color></size>";
            Changed();
            if (_hud != null) _hud.SetResultDetail(detail);
        }

        [ContextMenu("테스트: 랭크 점수 +300")]
        private void TestPoints() { _d.points += 300; _d.best = Mathf.Max(_d.best, _d.points); _d.seasonGames++; Changed(); }

        [ContextMenu("테스트: 시즌 정산(지금 순위로)")]
        private void TestSettle() { Settle(_d.season); Changed(); }

        [ContextMenu("테스트: 도전권 가득 채우기")]
        private void FillTickets() { _d.tickets = MaxTickets; Changed(); }
    }
}
