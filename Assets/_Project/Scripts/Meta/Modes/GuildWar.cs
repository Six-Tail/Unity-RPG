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
    /// 길드전(주간 PVP): 매주 가상 길드 1곳과 대전합니다(실제 길드도 상대는 가상 길드).
    /// · 결투장처럼: 길드마다 길드전 점수(시작 1,000)가 있고, 매주 점수가 가까운 가상 길드와 매칭. 이기면 점수가 오르고 지면 내림
    /// · 상대 길드원 10명이 방어 팀을 둠. 강함은 상대 길드의 점수로 정함(결투장 가상 상대와 같은 규칙 — 내 전투력과 상관없음).
    ///   점수가 높은(순위가 높은) 길드일수록 강하지만, 가장 강한 가상 길드도 결투장 최상위 가상 상대 수준
    /// · 공격: 하루 3회(05:00 충전). 이기면 별 — 아군이 모두 살아 있으면 ★3, 절반 이상 ★2, 그보다 적으면 ★1
    /// · 길드 별 = 상대 길드원별로 우리 길드원이 얻은 최고 별의 합(최대 30). 상대 길드도 날마다 우리를 공략해 별을 쌓음
    /// · 주가 끝나면(월요일 05:00) 별이 많은 쪽이 승리 → 결과 보상을 우편으로(이번 주에 한 번이라도 공격했을 때)
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(GuildWar.Instance).
    /// </summary>
    public class GuildWar : MonoBehaviour
    {
        public const int Defenders = 10, MaxStars = Defenders * 3, AttemptsPerDay = 3;
        private const string SaveKey = "guildwar";
        private const int Version = 2;   // 방어 팀 만드는 규칙이 바뀌면 올림(그 주 방어 팀을 다시 만듦)

        [Serializable]
        private class SaveData
        {
            public string week = "", guild = "";
            public int oppSeed;
            public long basePower;
            public int version;
            public List<PlayerArena.Opponent> defenders = new();
        }

        private SaveData _d = new();
        private bool _loaded;
        public event Action OnChanged;

        private static GuildWar _instance;
        public static GuildWar Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<GuildWar>();
                if (_instance == null) _instance = new GameObject("GuildWar").AddComponent<GuildWar>();
                _instance.EnsureLoaded();
                return _instance;
            }
        }

        private void Awake() { if (_instance == null) _instance = this; EnsureLoaded(); }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            if (SaveStore.HasKey(SaveKey))
            {
                try { _d = JsonUtility.FromJson<SaveData>(SaveStore.GetString(SaveKey)) ?? new SaveData(); }
                catch { _d = new SaveData(); }
            }
            _d.defenders ??= new();
        }

        private void Save() { SaveStore.SetString(SaveKey, JsonUtility.ToJson(_d)); SaveStore.Save(); }

        // ---------------- 길드전 점수 · 매칭 ----------------

        /// <summary>
        /// 길드전 점수: 시작 1,000(결투장 점수와 같은 눈금). 길드전은 한 주에 한 번 끝나므로 별 차이만큼 더 크게 움직임:
        /// 승리 +60 + 별 차이 × 6(최대 +200) · 무승부 +10 · 패배 −30 − 별 차이 × 3(최대 −120).
        /// 크게 이기는 길드는 주마다 약 +150 ~ 200 → 서너 달이면 최상위 가상 길드(약 3,800점)와 만남.
        /// </summary>
        public const int StartRating = 1000, WinRating = 60, DrawRating = 10, LoseRating = -30;
        /// <summary>매칭 후보: 내 점수와 가까운 가상 길드 이만큼 중 하나(주마다 바뀜).</summary>
        public const int MatchPool = 6;

        public static int RatingDelta(int ours, int theirs) =>
            ours > theirs ? Mathf.Min(200, WinRating + (ours - theirs) * 6)
            : ours == theirs ? DrawRating
            : Mathf.Max(-120, LoseRating - (theirs - ours) * 3);

        /// <summary>
        /// 길드 점수 → 상대 길드원 방어 팀의 강함(0 ~ 1.4). 결투장 가상 상대와 같은 규칙(<see cref="PlayerArena.TierStrength"/>):
        /// 1,000점 = 0(Lv.12 · 장비 보정 없음) … 3,800점 이상 = 1.4(Lv.99 · +6 · 스킬 Lv.10 · 장비 보정 +126%).
        /// 내 전투력과는 상관없음 — 가장 강한 가상 길드도 결투장 최상위 가상 상대 수준(최대로 키운 플레이어보다 약함).
        /// </summary>
        public static float Strength(int rating) => PlayerArena.TierStrength(rating);

        /// <summary>내 길드를 가리키는 이름(실제 = 길드 id, 가상 = 번호).</summary>
        private static string GuildKey()
        {
            var g = PlayerGuild.Instance;
            return !g.Joined ? "" : g.IsReal ? g.Gid : "v" + g.Seed;
        }

        /// <summary>이번 주 상대: 순위표의 가상 길드 중 길드전 점수가 내 점수와 가장 가까운 몇 곳 중 하나(결투장 매칭처럼).</summary>
        public static int OpponentSeed()
        {
            var g = PlayerGuild.Instance;
            int my = g.WarRating;
            var cands = new List<(int seed, int diff)>();
            for (int s = 1; s <= VirtualGuilds.RankPool; s++)
            {
                if (!g.IsReal && s == g.Seed) continue;
                cands.Add((s, Math.Abs(VirtualGuilds.WarRating(s) - my)));
            }
            cands.Sort((a, b) => a.diff != b.diff ? a.diff.CompareTo(b.diff) : a.seed.CompareTo(b.seed));
            int h = Commanders.Hash(GuildKey() + "|" + g.Week);
            int pick = (int)((uint)h % (uint)Mathf.Min(MatchPool, cands.Count));
            return cands[pick].seed;
        }

        /// <summary>이번 주 상대 가상 길드(점수 · 레벨 포함).</summary>
        public static GuildInfo Opponent() => VirtualGuilds.Info(OpponentSeed());

        /// <summary>상대 길드의 길드전 순위(순위표 기준).</summary>
        public static int OpponentRank()
        {
            int seed = OpponentSeed();
            var list = GuildRanking.Build(GuildRanking.Tab.War);
            for (int i = 0; i < list.Count; i++) if (!list[i].Real && !list[i].mine && list[i].seed == seed) return i + 1;
            return 0;
        }

        /// <summary>
        /// 상대 길드가 지금까지 우리 길드에게서 얻은 별(날마다 늘어남). 상대 점수가 우리보다 높을수록 많이 얻음.
        /// </summary>
        public static int TheirStars()
        {
            var g = PlayerGuild.Instance;
            if (!g.Joined) return 0;
            var o = Opponent();
            var rng = new System.Random(o.seed * 613 + Commanders.Hash(GuildKey() + g.Week));
            double edge = (o.warRating - g.WarRating) / 1500.0;
            double final = MaxStars * Mathf.Clamp(0.55f + (float)edge + (float)(rng.NextDouble() * 0.2 - 0.1), 0.25f, 0.95f);
            return Mathf.Clamp((int)Math.Round(final * Math.Min(1.0, VirtualGuilds.WeekProgress() * 1.35)), 0, MaxStars);
        }

        public static int OurStars() => PlayerGuild.Instance.GuildWarStars();
        public static int AttemptsLeft => Mathf.Max(0, AttemptsPerDay - PlayerGuild.Instance.WarUsedToday);

        /// <summary>상대 길드원(방어 팀) 목록 — 주마다 한 번 만들어 둠(그 주 동안 그대로).</summary>
        public List<PlayerArena.Opponent> DefenderTeams(GameDatabase db)
        {
            EnsureLoaded();
            var g = PlayerGuild.Instance;
            if (!g.Joined || db == null) return new List<PlayerArena.Opponent>();
            int seed = OpponentSeed();
            if (_d.week != g.Week || _d.guild != GuildKey() || _d.oppSeed != seed || _d.defenders.Count != Defenders || _d.version != Version)
            {
                _d.week = g.Week; _d.guild = GuildKey(); _d.oppSeed = seed; _d.version = Version;
                float t = Strength(VirtualGuilds.WarRating(seed));
                _d.defenders = new List<PlayerArena.Opponent>();
                for (int i = 0; i < Defenders; i++) _d.defenders.Add(MakeDefender(i, seed, t, db));
                Save();
            }
            return _d.defenders;
        }

        public Commander DefenderCommander(int i, GameDatabase db) =>
            Commanders.FromSeed((OpponentSeed() * 37 + i * 7919 + 5) % Commanders.MaxSeed, db);

        /// <summary>
        /// i번 방어 팀: 길드 강함 t를 중심으로 앞 번호는 조금 약하게(×0.7) · 뒤 번호는 조금 강하게(×1.15), 최대 1.4.
        /// 고성급 캐릭터를 고를 확률도 결투장처럼 강함을 따라 오름.
        /// </summary>
        private PlayerArena.Opponent MakeDefender(int i, int oppSeed, float guildT, GameDatabase db)
        {
            var rng = new System.Random(oppSeed * 131 + i * 17);
            var com = DefenderCommander(i, db);
            var o = new PlayerArena.Opponent { name = com.name, frameId = com.frameId ?? "", avatarId = com.avatarId ?? "" };
            float t = Mathf.Clamp(guildT * (0.7f + 0.05f * i) + 0.015f * i, 0f, 1.4f);
            var pool = new List<CharacterData>(db.Characters);
            pool.RemoveAll(c => c == null);
            float highChance = Mathf.Clamp01(0.15f + 0.6f * t);
            for (int k = 0; k < 4 && pool.Count > 0; k++)
            {
                bool wantHigh = rng.NextDouble() < highChance;
                var cands = pool.FindAll(c => ((int)c.rarity >= 5) == wantHigh);
                if (cands.Count == 0) cands = pool;
                var ch = cands[rng.Next(cands.Count)];
                o.team.Add(!string.IsNullOrEmpty(ch.id) ? ch.id : ch.name);
                pool.Remove(ch);
            }
            Shape(o, t);
            o.points = Mathf.RoundToInt(1000 + t * 2000);
            o.power = (int)Math.Min(int.MaxValue, PowerOf(o, db));
            o.difficulty = Mathf.Clamp(i * 3 / Defenders, 0, 2);
            return o;
        }

        /// <summary>강함 t(0 ~ 1.4) → 레벨 · 초월 · 스킬 · 장비 대신 보정(결투장 가상 상대와 같은 규칙).</summary>
        private static void Shape(PlayerArena.Opponent o, float t)
        {
            o.level = Mathf.Clamp(Mathf.RoundToInt(12 + 62 * t), 1, Leveling.AwakenedMaxLevel);
            o.transcend = t < 0.55f ? 0 : Mathf.Clamp(Mathf.FloorToInt((t - 0.55f) / 0.13f), 0, Transcendence.MaxTranscend);
            o.skillLevel = Mathf.Clamp(1 + Mathf.RoundToInt(9 * t), 1, Leveling.MaxSkillLevel);
            o.gearPct = 0.9f * t;
        }

        private static long PowerOf(PlayerArena.Opponent o, GameDatabase db)
        {
            var cfg = BattleConfig.CreateDefault();
            long sum = 0;
            foreach (var data in PlayerArena.Instance.BuildTeam(o, db))
            {
                sum += CombatPower.Of(new BattleUnit(data, Team.Enemy, cfg), new[] { o.skillLevel, o.skillLevel, o.skillLevel });
                if (data.skills != null) foreach (var sk in data.skills) if (sk != null) Destroy(sk);
                Destroy(data);
            }
            Destroy(cfg);
            return sum;
        }

        // ---------------- 정산 ----------------

        internal static void SettleWeek(int ours, int theirs, string opponent, int myStars, int ratingBefore, int ratingAfter)
        {
            if (myStars <= 0) return;
            var mail = FindAnyObjectByType<PlayerMail>();
            if (mail == null) return;
            string result; RewardBundle reward;
            if (ours > theirs) { result = "승리"; reward = new RewardBundle().Crystals(150).Gold(50000).Mat(MaterialType.EquipEssence, 40); }
            else if (ours == theirs) { result = "무승부"; reward = new RewardBundle().Crystals(90).Gold(30000); }
            else { result = "패배"; reward = new RewardBundle().Crystals(50).Gold(20000); }
            int d = ratingAfter - ratingBefore;
            mail.Send($"지난주 길드전 {result}",
                $"상대 '{opponent}'\n우리 길드 ★{ours} : ★{theirs}\n내가 얻은 별 ★{myStars}\n길드전 점수 {ratingBefore:N0} → {ratingAfter:N0} ({(d >= 0 ? "+" : "")}{d})", reward);
        }

        // ---------------- 전투 ----------------

        private int _target = -1;
        private bool _pending, _active;
        private readonly List<BattleUnitData> _spawned = new();
        private BattleController _controller;
        private BattleHUD _hud;
        private int _partySize;

        /// <summary>i번 상대 길드원 공격. 실패 사유(성공이면 null).</summary>
        public string Fight(int i, IList<CharacterData> party)
        {
            var g = PlayerGuild.Instance;
            if (!g.Joined) return "길드에 가입해 있지 않습니다";
            if (AttemptsLeft <= 0) return "오늘 공격 횟수를 모두 썼습니다 (매일 05:00 충전)";
            var roster = FindAnyObjectByType<PlayerRoster>();
            var launcher = FindAnyObjectByType<BattleLauncher>();
            if (roster == null || launcher == null || party == null || party.Count == 0) return "전투를 시작하지 못했습니다";
            var defs = DefenderTeams(roster.Database);
            if (i < 0 || i >= defs.Count) return "상대를 찾을 수 없습니다";

            Hook();
            Cleanup();
            var enemies = PlayerArena.Instance.BuildTeam(defs[i], roster.Database);
            _spawned.AddRange(enemies);
            _target = i;
            _partySize = party.Count;
            _pending = true;
            if (_hud != null)
            {
                _hud.SetBackground("Backgrounds/battle_guild", "Backgrounds/guild", "Backgrounds/battle_arena", "Backgrounds/battle");
                _hud.SetRetryHidden(true);
                _hud.SetExitLabel("길드로");
                _hud.SetRoundLimit(0);
            }
            launcher.Launch(new List<CharacterData>(party), enemies);
            return null;
        }

        public void Leave()
        {
            _active = false; _pending = false;
            if (_hud != null) _hud.SetRetryHidden(false);
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

        /// <summary>승리 별: 아군이 모두 살아 있으면 3, 절반 이상 2, 그보다 적으면 1.</summary>
        public static int StarsFor(int alive, int total) =>
            alive <= 0 ? 0 : alive >= total ? 3 : alive * 2 >= total ? 2 : 1;

        private void HandleEnd(Team winner)
        {
            if (!_active || _target < 0) return;
            _active = false;
            var g = PlayerGuild.Instance;
            g.UseWarAttempt();
            int alive = 0;
            var eng = _controller != null ? _controller.Engine : null;
            if (eng != null) foreach (var u in eng.allies) if (u.IsAlive) alive++;
            int stars = winner == Team.Ally ? StarsFor(alive, Mathf.Max(1, _partySize)) : 0;
            int before = OurStars();
            bool better = g.SetWarStars(_target, stars);
            int after = OurStars();
            g.RefreshSeen();
            GuildLink.Instance.RequestSync();

            var defs = _d.defenders;
            string name = _target < defs.Count ? defs[_target].name : "상대";
            string starText = stars > 0 ? $"<color=#FFD36B>{new string('★', stars)}{new string('☆', 3 - stars)}</color>" : "<color=#FF8A8A>패배</color>";
            string detail = $"<size=110%>길드전 — {Opponent().name}  <size=80%>{_target + 1}번 {name}</size></size>\n" +
                            $"결과  {starText}" + (better ? "  <color=#7CFF9A>내 최고 기록!</color>" : "") + "\n" +
                            $"<color=#9AA3B5>우리 길드</color>  ★{after}{(after > before ? $" <color=#7CFF9A>(+{after - before})</color>" : "")}   <color=#9AA3B5>상대 길드</color>  ★{TheirStars()}\n" +
                            $"<size=80%><color=#9AA3B5>오늘 남은 공격 {AttemptsLeft} / {AttemptsPerDay}</color></size>";
            _hud?.SetResultDetail(detail);
            _target = -1;
            OnChanged?.Invoke();
        }
    }
}
