using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;
using SubcultureRPG.Battle.UI;
using SubcultureRPG.Meta.Shop;

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

        /// <summary>티어(점수 하한 순). 다이아 위 3단계는 이 게임만의 티어: 성운 → 초신성 → 앵커.</summary>
        public static readonly TierDef[] Tiers =
        {
            new() { name = "브론즈", min = 0, frameId = "", color = new Color(0.8f, 0.55f, 0.35f) },
            new() { name = "실버", min = 1100, frameId = "fa_silver", color = new Color(0.82f, 0.86f, 0.95f) },
            new() { name = "골드", min = 1300, frameId = "fa_gold", color = new Color(1f, 0.83f, 0.4f) },
            new() { name = "플래티넘", min = 1600, frameId = "fa_platinum", color = new Color(0.5f, 0.95f, 0.85f) },
            new() { name = "다이아", min = 2000, frameId = "fa_diamond", color = new Color(0.6f, 0.75f, 1f) },
            new() { name = "성운", min = 2500, frameId = "fa_nebula", color = new Color(0.85f, 0.55f, 1f) },
            new() { name = "초신성", min = 3100, frameId = "fa_supernova", color = new Color(1f, 0.7f, 0.35f) },
            new() { name = "앵커", min = 3800, frameId = "fa_anchor", color = new Color(1f, 0.92f, 0.6f) },
        };

        public static int TierIndex(int points)
        {
            int t = 0;
            for (int i = 0; i < Tiers.Length; i++) if (points >= Tiers[i].min) t = i;
            return t;
        }

        public static (string name, Color color) Tier(int points)
        {
            var t = Tiers[TierIndex(points)];
            return (t.name, t.color);
        }

        // ---------------- 랭킹(시즌 = 1주) ----------------

        public const int RankSize = 100;
        public static readonly string[] RankFrames = { "fr_1", "fr_2", "fr_3" };

        public class RankEntry
        {
            public int rank, points, seed;
            public string name;
            public bool me;
        }

        /// <summary>시즌 정산 결과(결투장 화면에 들어갈 때 한 번 보여 주고 지급).</summary>
        public class Settlement
        {
            public int rank, points, crystals, newPoints;
            public string frameId;
        }

        public static int SeasonCrystals(int rank) => rank switch { 1 => 1500, 2 => 1000, 3 => 700, <= 10 => 500, <= 30 => 300, <= 60 => 200, _ => 100 };

        /// <summary>시즌 진행도(0 = 막 시작, 1 = 끝). 다른 지휘관의 점수는 시즌이 갈수록 오름.</summary>
        private static float SeasonProgress()
        {
            var left = PlayerShop.UntilReset(ShopReset.Weekly, DateTime.Now) ?? TimeSpan.Zero;
            return Mathf.Clamp01(1f - (float)(left.TotalDays / 7.0));
        }

        private static List<RankEntry> BuildRanking(string season, float progress, int myPoints)
        {
            int h = Commanders.Hash(season);
            var list = new List<RankEntry>(RankSize);
            for (int i = 0; i < RankSize - 1; i++)
            {
                var rng = new System.Random(h + i * 101);
                float curve = Mathf.Pow((RankSize - 2 - i) / (float)(RankSize - 2), 2.2f);
                int pts = 900 + Mathf.RoundToInt(3000f * curve * (0.8f + 0.2f * progress)) + rng.Next(-30, 31);
                int nameIdx = (i * 37 + h) % Commanders.NameCount;
                list.Add(new RankEntry { points = pts, name = Commanders.NameOf(nameIdx), seed = nameIdx });
            }
            list.Add(new RankEntry { points = myPoints, name = PlayerProfile.Instance.DisplayName, me = true });
            list.Sort((a, b) => a.points != b.points ? b.points.CompareTo(a.points) : b.me.CompareTo(a.me));
            for (int i = 0; i < list.Count; i++) list[i].rank = i + 1;
            return list;
        }

        /// <summary>지금 시즌 랭킹(1위부터).</summary>
        public List<RankEntry> Ranking() { EnsureLoaded(); return BuildRanking(_d.season, SeasonProgress(), _d.points); }

        public int MyRank { get { foreach (var e in Ranking()) if (e.me) return e.rank; return RankSize; } }
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
            int rank = RankSize;
            foreach (var e in BuildRanking(season, 1f, _d.points)) if (e.me) rank = e.rank;
            _d.best = Mathf.Max(_d.best, _d.points);
            _d.settleRank = rank;
            _d.settlePoints = _d.points;
            if (rank <= 3) _d.rankFrames |= 1 << (rank - 1);
            if (_d.points > StartPoints) _d.points = StartPoints + Mathf.RoundToInt((_d.points - StartPoints) * 0.75f);
            _d.opponents.Clear();
        }

        /// <summary>받지 않은 시즌 정산이 있으면 보상을 지급하고 돌려줌(없으면 null).</summary>
        public Settlement TakeSettlement()
        {
            EnsureLoaded();
            if (_d.settleRank <= 0) return null;
            var s = new Settlement
            {
                rank = _d.settleRank, points = _d.settlePoints, newPoints = _d.points, crystals = SeasonCrystals(_d.settleRank),
                frameId = _d.settleRank <= 3 ? RankFrames[_d.settleRank - 1] : null,
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
            public Difficulty Diff => (Difficulty)difficulty;
        }

        [Serializable]
        private class SaveData
        {
            public int points = StartPoints, tickets = MaxTickets, wins, losses;
            public string ticketDay = "";
            public List<Opponent> opponents = new();
            public int best;                 // 역대 최고 점수(티어 테두리 해금)
            public int rankFrames;           // 받은 시즌 순위 테두리(비트: 1위 · 2위 · 3위)
            public string season = "";       // 지금 시즌(주) 키
            public int seasonGames;          // 이번 시즌에 치른 판 수(0이면 정산 없음)
            public int settleRank, settlePoints;   // 아직 보여 주지 않은 정산(순위 > 0)
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
                if (PlayerPrefs.HasKey(SaveKey))
                {
                    try { _d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)) ?? new SaveData(); }
                    catch { _d = new SaveData(); }
                }
                if (_d.opponents == null) _d.opponents = new List<Opponent>();
            }
            CheckSeason();
        }

        private void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_d));
            PlayerPrefs.Save();
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
        public void Refresh(bool notify = true)
        {
            var roster = FindAnyObjectByType<PlayerRoster>();
            var db = roster != null ? roster.Database : null;
            _d.opponents = new List<Opponent>();
            if (db == null || db.Characters.Count == 0) { if (notify) Changed(); return; }

            // 내 주력 4명 평균 레벨
            var mine = CombatPower.All(roster, FindAnyObjectByType<PlayerEquipment>());
            int n = 0, sum = 0;
            foreach (var kv in mine) { if (n >= 4) break; sum += roster.LevelOf(kv.Key); n++; }
            int baseLevel = n > 0 ? sum / n : 1;

            var usedNames = new HashSet<string>();
            for (int i = 0; i < 3; i++)
            {
                var o = new Opponent { difficulty = i };
                do { o.name = Names[UnityEngine.Random.Range(0, Names.Length)]; } while (!usedNames.Add(o.name) && usedNames.Count < Names.Length);
                o.level = Mathf.Clamp(baseLevel + (i - 1) * 6 + UnityEngine.Random.Range(-2, 3), 1, Leveling.MaxLevel);
                o.transcend = i == 2 ? UnityEngine.Random.Range(0, 3) : 0;
                o.skillLevel = Mathf.Clamp(1 + o.level / 9, 1, Leveling.MaxSkillLevel);
                o.points = Mathf.Max(0, _d.points + (i - 1) * 80 + UnityEngine.Random.Range(-40, 41));

                var pool = new List<CharacterData>(db.Characters);
                pool.RemoveAll(c => c == null);
                for (int k = 0; k < 4 && pool.Count > 0; k++)
                {
                    int pick = UnityEngine.Random.Range(0, pool.Count);
                    o.team.Add(!string.IsNullOrEmpty(pool[pick].id) ? pool[pick].id : pool[pick].name);
                    pool.RemoveAt(pick);
                }
                o.power = PowerOf(o, db);
                _d.opponents.Add(o);
            }
            if (notify) Changed(); else Save();
        }

        /// <summary>상대 팀을 전투 유닛으로(적 진영).</summary>
        public List<BattleUnitData> BuildTeam(Opponent o, GameDatabase db)
        {
            var list = new List<BattleUnitData>();
            if (o == null || db == null) return list;
            foreach (var id in o.team)
            {
                var c = db.GetCharacter(id);
                if (c == null) continue;
                int star = o.transcend > 0 ? Transcendence.MaxStar : (int)c.rarity;
                var lv = new[] { o.skillLevel, o.skillLevel, o.skillLevel };
                list.Add(RuntimeUnits.FromCharacter(c, o.level, null, o.skillLevel, star, o.transcend, lv));
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

        public CharacterData Leader(Opponent o, GameDatabase db) => o != null && db != null && o.team.Count > 0 ? db.GetCharacter(o.team[0]) : null;

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
                _hud.SetRetryHandler(() => Fight(_fighting, _party) ? null : "결투장 도전권이 없습니다");
                _hud.SetExitLabel("결투장으로");
            }
            launcher.Launch(_party, enemies);
            return true;
        }

        /// <summary>결투장을 떠날 때(끝나지 않은 전투면 도전권 반환).</summary>
        public void Leave()
        {
            Refund();
            _active = false; _pending = false;
            if (_hud != null) _hud.SetRetryHandler(null);
        }

        private void Refund()
        {
            if (!_paid) return;
            _paid = false;
            _d.tickets = Mathf.Min(MaxTickets, _d.tickets + 1);
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
            var tierBefore = Tier(before).name;
            int bestTierBefore = TierIndex(BestPoints);
            _d.seasonGames++;
            string detail;
            if (winner == Team.Ally)
            {
                int d = (int)o.Diff;
                _d.points += WinPoints[d];
                _d.wins++;
                if (wallet != null) { wallet.Add(WinCrystals[d]); wallet.AddGold(WinGold[d]); }
                detail = $"<size=110%>결투장 — {o.name}  <color=#{ColorUtility.ToHtmlStringRGB(DifficultyColor(o.Diff))}>{DifficultyName(o.Diff)}</color></size>\n" +
                         $"<color=#7CFF9A>랭크 점수 {before:N0} → {_d.points:N0} (+{WinPoints[d]})</color>\n" +
                         $"<color=#FFD36B>획득</color>  <color=#8FE9FF>크리스탈 {WinCrystals[d]}</color>  ·  <color=#FFD36B>금화 {WinGold[d]:N0}</color>";
                Refresh(false);   // 이기면 상대 새로
            }
            else
            {
                _d.points = Mathf.Max(0, _d.points - LosePoints);
                _d.losses++;
                if (wallet != null) wallet.AddGold(LoseGold);
                detail = $"<size=110%>결투장 — {o.name}</size>\n<color=#FF8A8A>랭크 점수 {before:N0} → {_d.points:N0} (-{LosePoints})</color>\n" +
                         $"<color=#FFD36B>참가 보상</color>  금화 {LoseGold:N0}";
            }
            string tierAfter = Tier(_d.points).name;
            if (tierAfter != tierBefore) detail += $"\n<color=#FFD36B>티어 변경: {tierBefore} → {tierAfter}</color>";
            _d.best = Mathf.Max(_d.best, _d.points);
            for (int t = bestTierBefore + 1; t <= TierIndex(_d.best); t++)
                detail += $"\n<color=#7CFF9A>첫 {Tiers[t].name} 달성! 테두리 '{ProfileRewards.Frame(Tiers[t].frameId).name}' 획득</color>";
            detail += $"\n<size=80%><color=#9AA3B5>시즌 순위 {MyRank}위 / {RankSize}명</color></size>";
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
