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
    /// · 상대 길드원 10명이 방어 팀을 둠. 강함은 내 주력 4명 전투력의 55% ~ 132%(번호가 클수록 강함) — 레벨 · 초월 · 스킬로 맞추고,
    ///   최대로도 모자라면 장비 대신 보정(체력 · 공격 · 방어 %)을 더 올림
    /// · 공격: 하루 3회(05:00 충전). 이기면 별 — 아군이 모두 살아 있으면 ★3, 절반 이상 ★2, 그보다 적으면 ★1
    /// · 길드 별 = 상대 길드원별로 우리 길드원이 얻은 최고 별의 합(최대 30). 상대 길드도 날마다 우리를 공략해 별을 쌓음
    /// · 주가 끝나면(월요일 05:00) 별이 많은 쪽이 승리 → 결과 보상을 우편으로(이번 주에 한 번이라도 공격했을 때)
    /// 씬에 없어도 처음 쓸 때 스스로 만들어짐(GuildWar.Instance).
    /// </summary>
    public class GuildWar : MonoBehaviour
    {
        public const int Defenders = 10, MaxStars = Defenders * 3, AttemptsPerDay = 3;
        private const string SaveKey = "guildwar";

        [Serializable]
        private class SaveData
        {
            public string week = "", guild = "";
            public int oppSeed;
            public long basePower;
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

        // ---------------- 상대 길드 ----------------

        /// <summary>내 길드를 가리키는 이름(실제 = 길드 id, 가상 = 번호).</summary>
        private static string GuildKey()
        {
            var g = PlayerGuild.Instance;
            return !g.Joined ? "" : g.IsReal ? g.Gid : "v" + g.Seed;
        }

        public static int OpponentSeed()
        {
            var g = PlayerGuild.Instance;
            int h = Commanders.Hash(GuildKey() + "|" + g.Week);
            int seed = 1_000 + (int)((uint)h % 98_000u);
            if (!g.IsReal && seed == g.Seed) seed++;
            return seed;
        }

        /// <summary>이번 주 상대 가상 길드.</summary>
        public static GuildInfo Opponent()
        {
            var o = VirtualGuilds.Info(OpponentSeed());
            o.level = Mathf.Clamp(PlayerGuild.Instance.Level + (OpponentSeed() % 3) - 1, 1, PlayerGuild.MaxLevel);
            return o;
        }

        /// <summary>상대 길드가 지금까지 우리 길드에게서 얻은 별(날마다 늘어남).</summary>
        public static int TheirStars()
        {
            if (!PlayerGuild.Instance.Joined) return 0;
            var o = Opponent();
            return VirtualGuilds.WarStars(o.seed, o.level, PlayerGuild.Instance.Week, VirtualGuilds.WeekProgress());
        }

        public static int OurStars() => PlayerGuild.Instance.GuildWarStars();
        public static int AttemptsLeft => Mathf.Max(0, AttemptsPerDay - PlayerGuild.Instance.WarUsedToday);

        /// <summary>상대 길드원(방어 팀) 목록 — 주마다 한 번 만들어 둠.</summary>
        public List<PlayerArena.Opponent> DefenderTeams(GameDatabase db)
        {
            EnsureLoaded();
            var g = PlayerGuild.Instance;
            if (!g.Joined || db == null) return new List<PlayerArena.Opponent>();
            int seed = OpponentSeed();
            if (_d.week != g.Week || _d.guild != GuildKey() || _d.oppSeed != seed || _d.defenders.Count != Defenders)
            {
                _d.week = g.Week; _d.guild = GuildKey(); _d.oppSeed = seed;
                _d.basePower = MyPower();
                _d.defenders = new List<PlayerArena.Opponent>();
                for (int i = 0; i < Defenders; i++) _d.defenders.Add(MakeDefender(i, seed, _d.basePower, db));
                Save();
            }
            return _d.defenders;
        }

        public Commander DefenderCommander(int i, GameDatabase db) =>
            Commanders.FromSeed((OpponentSeed() * 37 + i * 7919 + 5) % Commanders.MaxSeed, db);

        private static long MyPower()
        {
            long sum = 0; int n = 0;
            foreach (var kv in CombatPower.All(FindAnyObjectByType<PlayerRoster>(), FindAnyObjectByType<PlayerEquipment>())) { if (n++ >= 4) break; sum += kv.Value; }
            return Math.Max(1, sum);
        }

        /// <summary>i번 방어 팀: 내 전투력 × (0.55 + 0.085 × i)에 가깝게 강함을 맞춤.</summary>
        private PlayerArena.Opponent MakeDefender(int i, int oppSeed, long myPower, GameDatabase db)
        {
            var rng = new System.Random(oppSeed * 131 + i * 17);
            var com = DefenderCommander(i, db);
            var o = new PlayerArena.Opponent { name = com.name, frameId = com.frameId ?? "", avatarId = com.avatarId ?? "" };
            var pool = new List<CharacterData>(db.Characters);
            pool.RemoveAll(c => c == null);
            for (int k = 0; k < 4 && pool.Count > 0; k++)
            {
                var ch = pool[rng.Next(pool.Count)];
                o.team.Add(!string.IsNullOrEmpty(ch.id) ? ch.id : ch.name);
                pool.Remove(ch);
            }
            long target = (long)(myPower * (0.55 + 0.085 * i));
            float lo = 0f, hi = 1.4f;
            for (int it = 0; it < 8; it++)
            {
                float t = (lo + hi) / 2f;
                Shape(o, t);
                if (PowerOf(o, db) < target) lo = t; else hi = t;
            }
            Shape(o, (lo + hi) / 2f);
            if (PowerOf(o, db) < target * 0.95)
            {
                // 최대(Lv.100 · +6)로도 모자라면(장비를 잘 갖춘 최종 육성) 장비 대신 보정을 더 올려 맞춤
                Shape(o, 1.4f);
                float glo = o.gearPct, ghi = 40f;
                for (int it = 0; it < 10; it++)
                {
                    o.gearPct = (glo + ghi) / 2f;
                    if (PowerOf(o, db) < target) glo = o.gearPct; else ghi = o.gearPct;
                }
                o.gearPct = (glo + ghi) / 2f;
            }
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

        internal static void SettleWeek(int ours, int theirs, string opponent, int myStars)
        {
            if (myStars <= 0) return;
            var mail = FindAnyObjectByType<PlayerMail>();
            if (mail == null) return;
            string result; RewardBundle reward;
            if (ours > theirs) { result = "승리"; reward = new RewardBundle().Crystals(150).Gold(50000).Mat(MaterialType.EquipEssence, 40); }
            else if (ours == theirs) { result = "무승부"; reward = new RewardBundle().Crystals(90).Gold(30000); }
            else { result = "패배"; reward = new RewardBundle().Crystals(50).Gold(20000); }
            mail.Send($"지난주 길드전 {result}", $"상대 '{opponent}'\n우리 길드 ★{ours} : ★{theirs}\n내가 얻은 별 ★{myStars}", reward);
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
