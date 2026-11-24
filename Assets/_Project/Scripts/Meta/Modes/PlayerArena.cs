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
    /// · 하루 도전권 5장(05:00 초기화) · 랭크 점수(시작 1000) · 티어(브론즈 ~ 마스터)
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

        public static (string name, Color color) Tier(int points) => points switch
        {
            < 1100 => ("브론즈", new Color(0.8f, 0.55f, 0.35f)),
            < 1300 => ("실버", new Color(0.82f, 0.86f, 0.95f)),
            < 1600 => ("골드", new Color(1f, 0.83f, 0.4f)),
            < 2000 => ("플래티넘", new Color(0.5f, 0.95f, 0.85f)),
            < 2500 => ("다이아", new Color(0.6f, 0.75f, 1f)),
            _ => ("마스터", new Color(1f, 0.6f, 0.95f)),
        };

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
            if (_loaded) return;
            _loaded = true;
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            try { _d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)) ?? new SaveData(); }
            catch { _d = new SaveData(); }
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
            detail += $"\n<size=80%><color=#9AA3B5>남은 도전권 {Tickets} / {MaxTickets}</color></size>";
            Changed();
            if (_hud != null) _hud.SetResultDetail(detail);
        }

        [ContextMenu("테스트: 도전권 가득 채우기")]
        private void FillTickets() { _d.tickets = MaxTickets; Changed(); }
    }
}
