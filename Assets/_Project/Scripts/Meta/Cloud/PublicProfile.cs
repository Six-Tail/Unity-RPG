using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;
using SubcultureRPG.Meta.Shop;
using PlayerSaveOptions = Unity.Services.CloudSave.Models.Data.Player.SaveOptions;
using PlayerLoadOptions = Unity.Services.CloudSave.Models.Data.Player.LoadOptions;
using PlayerQueryOptions = Unity.Services.CloudSave.Models.Data.Player.QueryOptions;
using PublicWrite = Unity.Services.CloudSave.Models.Data.Player.PublicWriteAccessClassOptions;
using PublicRead = Unity.Services.CloudSave.Models.Data.Player.PublicReadAccessClassOptions;

namespace SubcultureRPG.Meta.Cloud
{
    /// <summary>공개 프로필의 캐릭터 1명(육성 상태 + 장착 장비). 능력치는 볼 때 계산(<see cref="SnapStats"/>).</summary>
    [Serializable]
    public class CharSnap
    {
        public string id;
        public int level = 1, asc, star, transcend, bond = 1;
        public int[] skills = { 1, 1, 1 };       // 기본 공격 · 스킬 · 궁극기(재료로 올린 레벨, 초월 보너스 제외)
        public List<EquipmentItem> gear = new();
        public string outfit = "";               // 입은 의상의 그림 접미사("" 기본 · "_bloom" 개화) — 내 옷장 선택과 상관없이 이 사람의 것
    }

    /// <summary>다른 지휘관 정보창에 보이는 공개 프로필(실제 플레이어 또는 가상 지휘관).</summary>
    [Serializable]
    public class ProfileSnap
    {
        public string pid = "", uid = "", name = "지휘관", titleId = "", frameId = "", avatarId = "", greeting = "";
        public string avatarOutfit = "";        // 대표 캐릭터 의상 접미사
        public int level = 1;
        public int arenaPoints, arenaBest;
        public long updated;                    // DateTime.UtcNow.Ticks
        public List<CharSnap> top = new();      // 전투력 상위 5명
        [NonSerialized] public bool real;       // 실제 플레이어(서버)
        [NonSerialized] public int seed = -1;   // 가상 지휘관 번호(친구 추가용)
    }

    /// <summary>스냅숏 캐릭터의 실제 능력치 · 전투력(내 캐릭터와 같은 계산: 레벨 · 성급 · 초월 · 스킬 · 장비 · 호감도).</summary>
    public static class SnapStats
    {
        public class Result
        {
            public CharacterData ch;
            public int power, hp, atk, def, spd;
            public float critRate, critDmg, penetration, breakEffect, energyRegen, healBonus, effectHit, effectRes, elementDmg, allDmg;
            public int[] effectiveSkills = { 1, 1, 1 };
            public string[] skillNames = { "기본 공격", "스킬", "궁극기" };
            public string[] skillDescs = { "", "", "" };
        }

        private static BattleConfig _cfg;

        public static Result Evaluate(CharSnap s, GameDatabase db)
        {
            var ch = db != null && s != null ? db.GetCharacter(s.id) : null;
            if (ch == null) return null;
            if (_cfg == null) _cfg = BattleConfig.CreateDefault();
            int tr = s.star >= Transcendence.MaxStar ? s.transcend : 0;
            var levels = new int[3];
            for (int i = 0; i < 3; i++)
                levels[i] = Mathf.Clamp(s.skills != null && i < s.skills.Length ? s.skills[i] : 1, 1, Leveling.MaxSkillLevel) + Leveling.SkillBonus(tr, (SkillSlot)i);
            int maxSkill = Mathf.Max(levels[0], Mathf.Max(levels[1], levels[2]));
            var data = RuntimeUnits.FromCharacter(ch, Mathf.Max(1, s.level), null, maxSkill, s.star, tr, levels);
            var b = new StatBonus();
            if (s.gear != null) b.Add(EquipRules.BonusOf(s.gear));
            float p = (Mathf.Clamp(s.bond, 1, PlayerBond.MaxLevel) - 1) * PlayerBond.StatPerLevel;
            b.hpPct += p; b.atkPct += p; b.defPct += p;
            data.bonus = b;
            var u = new BattleUnit(data, Team.Ally, _cfg);
            var r = new Result
            {
                ch = ch, power = CombatPower.Of(u, levels), hp = u.maxHP, atk = u.atk, def = u.def, spd = u.spd,
                critRate = u.critRate, critDmg = u.critDmg, penetration = u.penetration, breakEffect = u.breakEffect, energyRegen = u.energyRegen,
                healBonus = u.healBonus, effectHit = u.effectHit, effectRes = u.effectRes, elementDmg = u.ElementDmg(ch.element), allDmg = u.allDmg,
                effectiveSkills = levels,
            };
            if (data.skills != null)
                for (int i = 0; i < 3 && i < data.skills.Count; i++)
                    if (data.skills[i] != null) { r.skillNames[i] = data.skills[i].skillName; r.skillDescs[i] = data.skills[i].description; }
            if (data.skills != null) foreach (var sk in data.skills) if (sk != null) UnityEngine.Object.Destroy(sk);
            UnityEngine.Object.Destroy(data);
            return r;
        }

        public static long TotalPower(ProfileSnap snap, GameDatabase db)
        {
            long sum = 0;
            if (snap?.top != null) foreach (var c in snap.top) { var r = Evaluate(c, db); if (r != null) sum += r.power; }
            return sum;
        }
    }

    /// <summary>
    /// 공개 프로필: 내 정보(이름 · 칭호 · 테두리 · 레벨 · 결투장 점수 · 전투력 상위 5명의 육성 상태와 장비)를
    /// Cloud Save 공개 데이터 'pp'에 올리고, 다른 플레이어의 것을 읽음. 가상 지휘관도 같은 모양(<see cref="FromCommander"/>)으로 만들어
    /// 정보창 하나로 둘 다 보여 줌.
    /// · 올리기: 로그인 중 2분마다 바뀌었으면(+ 결투장 전투 뒤) — 키 pp(JSON) · ar_season(이번 시즌 주) · ar_pts(결투장 점수)
    /// · 결투장 순위: ar_season = 이번 시즌인 플레이어를 검색(대시보드 인덱스 ar_season 필요 — docs/player-info.md)
    /// </summary>
    public class PublicProfile : MonoBehaviour
    {
        public const string KProfile = "pp", KSeason = "ar_season", KPoints = "ar_pts";
        private const string SaveKey = "pubprof";
        private const float PublishEvery = 120f, ArenaRefreshEvery = 120f;

        private static PublicProfile _instance;
        public static PublicProfile Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<PublicProfile>();
                if (_instance == null) _instance = new GameObject("PublicProfile").AddComponent<PublicProfile>();
                _instance.EnsureLoaded();
                return _instance;
            }
        }

        /// <summary>결투장 시즌마다 마지막으로 본 실제 플레이어 점수(시즌 정산 때 순위 계산용).</summary>
        [Serializable] public class ArenaSeen { public string pid, name; public int points; }
        [Serializable]
        private class SaveData
        {
            public string season = "";
            public List<ArenaSeen> players = new();
            public string lastSeason = "";
            public List<ArenaSeen> lastPlayers = new();
        }

        private SaveData _d = new();
        private bool _loaded;
        public event Action OnChanged;

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
            _d.players ??= new(); _d.lastPlayers ??= new();
        }

        private void Save() { SaveStore.SetString(SaveKey, JsonUtility.ToJson(_d)); SaveStore.Save(); }

        public static bool Online => CloudAccount.SignedIn;
        public static string SeasonKey => PlayerShop.PeriodKey(ShopReset.Weekly, DateTime.Now);

        // ---------------- 내 프로필 ----------------

        public static ProfileSnap BuildMine()
        {
            var pf = PlayerProfile.Instance;
            var roster = FindAnyObjectByType<PlayerRoster>();
            var equip = FindAnyObjectByType<PlayerEquipment>();
            var s = new ProfileSnap
            {
                pid = CloudAccount.PlayerId, uid = pf.Uid, name = pf.DisplayName, titleId = pf.TitleId, frameId = pf.FrameId,
                greeting = pf.Greeting, level = pf.Level, arenaPoints = PlayerArena.Instance.Points, arenaBest = PlayerArena.Instance.BestPoints,
                updated = DateTime.UtcNow.Ticks, real = true,
            };
            var avatar = SubcultureRPG.Meta.UI.ProfileAvatar.CharacterOf(pf, roster);
            if (avatar != null) { s.avatarId = PlayerRoster.KeyOf(avatar); s.avatarOutfit = CharacterOutfits.SuffixOf(avatar); }
            if (roster == null) return s;
            var bond = PlayerBond.Instance;
            foreach (var kv in CombatPower.All(roster, equip))
            {
                if (s.top.Count >= 5) break;
                var c = kv.Key;
                var o = roster.GetOwned(c);
                if (o == null) continue;
                var cs = new CharSnap
                {
                    id = PlayerRoster.KeyOf(c), level = o.level, asc = o.ascension, star = roster.StarOf(c), transcend = roster.TranscendOf(c),
                    bond = bond.Level(c), skills = roster.SkillLevels(c), outfit = CharacterOutfits.SuffixOf(c),
                };
                if (equip != null)
                    foreach (var it in equip.EquippedOn(c))
                        cs.gear.Add(JsonUtility.FromJson<EquipmentItem>(JsonUtility.ToJson(it)));   // 복사본
                s.top.Add(cs);
            }
            return s;
        }

        private string _lastPublished;
        private float _nextPublish, _nextArena;
        private bool _publishing;

        private void Update()
        {
            if (!Online || !CloudAccount.Synced) return;
            float now = Time.unscaledTime;
            if (now >= _nextPublish && !_publishing) { _nextPublish = now + PublishEvery; _ = PublishAsync(false); }
            if (now >= _nextArena && !ArenaFetching) { _nextArena = now + ArenaRefreshEvery; _ = RefreshArenaAsync(); }
        }

        /// <summary>곧(1초 뒤) 공개 프로필을 다시 올림 — 결투장 점수가 바뀐 직후 등.</summary>
        public void RequestPublish()
        {
            _lastPublished = null;
            _nextPublish = Mathf.Min(_nextPublish, Time.unscaledTime + 1f);
        }

        /// <summary>내 공개 프로필을 올림(바뀌지 않았으면 건너뜀, force면 무조건).</summary>
        public async Task PublishAsync(bool force)
        {
            if (!Online || _publishing) return;
            _publishing = true;
            try
            {
                var snap = BuildMine();
                snap.updated = 0;
                string json = JsonUtility.ToJson(snap);
                if (!force && json == _lastPublished) return;
                snap.updated = DateTime.UtcNow.Ticks;
                var data = new Dictionary<string, object>
                {
                    { KProfile, JsonUtility.ToJson(snap) }, { KSeason, SeasonKey }, { KPoints, PlayerArena.Instance.Points },
                };
                await CloudSaveService.Instance.Data.Player.SaveAsync(data, new PlayerSaveOptions(new PublicWrite()));
                _lastPublished = json;
            }
            catch (Exception e) { Debug.LogWarning($"[PublicProfile] 공개 프로필을 올리지 못했습니다: {e.Message}"); }
            finally { _publishing = false; }
        }

        // ---------------- 다른 플레이어 ----------------

        private readonly Dictionary<string, (ProfileSnap snap, float at)> _cache = new();

        /// <summary>플레이어 한 명의 공개 프로필(1분 동안은 캐시). 없으면 null.</summary>
        public async Task<ProfileSnap> LoadAsync(string pid)
        {
            if (string.IsNullOrEmpty(pid)) return null;
            if (pid == CloudAccount.PlayerId) return BuildMine();
            if (_cache.TryGetValue(pid, out var c) && Time.unscaledTime - c.at < 60f) return c.snap;
            if (!Online) return null;
            try
            {
                var res = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { KProfile }, new PlayerLoadOptions(new PublicRead(pid)));
                if (res != null && res.TryGetValue(KProfile, out var item) && item?.Value != null)
                {
                    var snap = Parse(item.Value.GetAs<string>(), pid);
                    if (snap != null) _cache[pid] = (snap, Time.unscaledTime);
                    return snap;
                }
            }
            catch (Exception e) { Debug.LogWarning($"[PublicProfile] {pid} 프로필을 읽지 못했습니다: {e.Message}"); }
            return null;
        }

        private static ProfileSnap Parse(string json, string pid)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var s = JsonUtility.FromJson<ProfileSnap>(json);
                if (s == null) return null;
                s.pid = pid; s.real = true;
                s.top ??= new();
                return s;
            }
            catch { return null; }
        }

        // ---------------- 결투장 실제 플레이어 ----------------

        /// <summary>이번 시즌 결투장에 기록이 있는 실제 플레이어(나 제외). 마지막으로 불러온 값.</summary>
        public List<ProfileSnap> ArenaPlayers { get; private set; } = new();
        public bool ArenaFetching { get; private set; }
        public string ArenaError { get; private set; }

        public async Task RefreshArenaAsync()
        {
            if (!Online || ArenaFetching) return;
            ArenaFetching = true; ArenaError = null;
            try
            {
                await PublishAsync(false);
                string season = SeasonKey, me = CloudAccount.PlayerId;
                var list = new List<ProfileSnap>();
                for (int offset = 0; offset < 1000; offset += 100)
                {
                    var q = new Query(new List<FieldFilter> { new FieldFilter(KSeason, season, FieldFilter.OpOptions.EQ, true) },
                        new HashSet<string> { KProfile, KPoints }, offset, 100);
                    var rows = await CloudSaveService.Instance.Data.Player.QueryAsync(q, new PlayerQueryOptions());
                    if (rows == null) break;
                    foreach (var row in rows)
                    {
                        if (row.Id == me) continue;
                        ProfileSnap snap = null; int pts = PlayerArena.StartPoints;
                        if (row.Data != null)
                            foreach (var it in row.Data)
                            {
                                if (it?.Value == null) continue;
                                if (it.Key == KProfile) { try { snap = Parse(it.Value.GetAs<string>(), row.Id); } catch { } }
                                else if (it.Key == KPoints) { try { pts = it.Value.GetAs<int>(); } catch { } }
                            }
                        if (snap == null) continue;
                        snap.arenaPoints = pts;
                        _cache[row.Id] = (snap, Time.unscaledTime);
                        list.Add(snap);
                    }
                    if (rows.Count < 100) break;
                }
                ArenaPlayers = list;
                RememberSeason(season, list);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PublicProfile] 결투장 순위를 불러오지 못했습니다: {e.Message}");
                ArenaError = e.Message != null && e.Message.ToLowerInvariant().Contains("index") ? "서버 검색 설정(인덱스 ar_season)이 없습니다" : "서버 순위를 불러오지 못했습니다";
            }
            finally
            {
                ArenaFetching = false;
                OnChanged?.Invoke();
            }
        }

        private void RememberSeason(string season, List<ProfileSnap> list)
        {
            if (_d.season != season)
            {
                if (!string.IsNullOrEmpty(_d.season)) { _d.lastSeason = _d.season; _d.lastPlayers = _d.players; }
                _d.season = season; _d.players = new();
            }
            _d.players.Clear();
            foreach (var s in list) _d.players.Add(new ArenaSeen { pid = s.pid, name = s.name, points = s.arenaPoints });
            Save();
        }

        /// <summary>그 시즌에 마지막으로 본 실제 플레이어 점수(결투장 순위 · 정산용). 이번 시즌이면 최신 목록.</summary>
        public List<ArenaSeen> SeenInSeason(string season)
        {
            EnsureLoaded();
            if (season == _d.season) return _d.players;
            if (season == _d.lastSeason) return _d.lastPlayers;
            return new List<ArenaSeen>();
        }

        // ---------------- 가상 지휘관 ----------------

        /// <summary>
        /// 가상 지휘관의 공개 프로필: 번호(seed)로 늘 같은 상위 5명 · 장비를 만들어 냄(지휘관 레벨이 높을수록 높은 성급 · 레벨 · 장비).
        /// </summary>
        public static ProfileSnap FromCommander(Commander c, GameDatabase db)
        {
            var s = new ProfileSnap
            {
                uid = c.uid, name = c.name, titleId = c.titleId, frameId = c.frameId, avatarId = c.avatarId, greeting = c.greeting,
                level = c.level, seed = c.seed, real = false,
            };
            if (db == null || db.Characters.Count == 0) return s;
            var rng = new System.Random(c.seed * 131 + 7);
            var pool = new List<CharacterData>();
            foreach (var ch in db.Characters)
                if (ch != null && ((int)ch.rarity <= 4 || rng.NextDouble() < Mathf.Clamp01(c.level / 90f + ((int)ch.rarity == 5 ? 0.25f : 0f))))
                    pool.Add(ch);
            var lead = Commanders.Avatar(c, db);
            if (lead != null) { pool.Remove(lead); pool.Insert(0, lead); }
            for (int i = 1; i < pool.Count; i++) { int j = 1 + rng.Next(pool.Count - 1); (pool[i], pool[j]) = (pool[j], pool[i]); }

            int gearRarity = c.level < 25 ? 4 : c.level < 50 ? 5 : 6;
            for (int i = 0; i < pool.Count && s.top.Count < 5; i++)
            {
                var ch = pool[i];
                int lv = Mathf.Clamp(c.level - rng.Next(0, 8), 1, Leveling.MaxLevel);
                int star = Mathf.Clamp((int)ch.rarity + (c.level >= 60 && rng.Next(3) == 0 ? 1 : 0), (int)ch.rarity, 6);
                int tr = star >= 6 ? rng.Next(0, c.level >= 70 ? 4 : c.level >= 50 ? 2 : 1) : 0;
                int sk = Mathf.Clamp(1 + c.level / 9, 1, Leveling.MaxSkillLevel);
                var cs = new CharSnap
                {
                    id = PlayerRoster.KeyOf(ch), level = lv, asc = Mathf.Clamp((lv - 1) / 20, 0, Leveling.MaxAscension), star = star, transcend = tr,
                    bond = Mathf.Clamp(1 + c.level / 9 + rng.Next(-1, 2), 1, PlayerBond.MaxLevel),
                    skills = new[] { Mathf.Clamp(sk + rng.Next(-1, 1), 1, 10), Mathf.Clamp(sk + rng.Next(-1, 1), 1, 10), Mathf.Clamp(sk - rng.Next(0, 2), 1, 10) },
                };
                // 장비: 레벨 20부터 6부위, 그 전에는 레벨에 따라 일부. 강화는 지휘관 레벨에 비례
                int slots = c.level >= 20 ? 6 : Mathf.Clamp(c.level / 4, 0, 6);
                string set4 = EquipSets.All[rng.Next(EquipSets.All.Length)].id, set2 = EquipSets.All[rng.Next(EquipSets.All.Length)].id;
                for (int k = 0; k < slots; k++)
                {
                    var it = EquipRules.Generate(k < 4 ? set4 : set2, rng, gearRarity, EquipStats.AllSlots[k]);
                    int target = Mathf.RoundToInt(EquipStats.MaxLevelOf(gearRarity) * Mathf.Clamp01(c.level / 80f));
                    while (it.level < target) EquipRules.EnhanceOnce(it, rng);
                    cs.gear.Add(it);
                }
                s.top.Add(cs);
            }
            return s;
        }
    }
}
