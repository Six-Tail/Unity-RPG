using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using PlayerSaveOptions = Unity.Services.CloudSave.Models.Data.Player.SaveOptions;
using PlayerLoadOptions = Unity.Services.CloudSave.Models.Data.Player.LoadOptions;
using PlayerQueryOptions = Unity.Services.CloudSave.Models.Data.Player.QueryOptions;
using PublicWrite = Unity.Services.CloudSave.Models.Data.Player.PublicWriteAccessClassOptions;
using PublicRead = Unity.Services.CloudSave.Models.Data.Player.PublicReadAccessClassOptions;

namespace SubcultureRPG.Meta.Cloud
{
    /// <summary>
    /// 실제 플레이어 길드(서버 함수 없이 각자 자기 Cloud Save 공개 데이터만 씀 — 친구 신청 <see cref="FriendLink"/>와 같은 방식).
    ///
    /// | 키 | 누가 씀 | 내용 |
    /// | g_info | 길드장 | 이름 · 소개 · 가입 방식 · 길드원 목록 · 추방/거절 목록 · 길드 경험치 · 이번 주 기록(순위표용) |
    /// | g_open | 길드장 | "1" 모집 중 · "0" 모집 안 함 · "" 길드 없음 → 추천 · 검색(= "1") · 순위표(≥ "0") |
    /// | g_id | 모든 길드원 | 내가 속한 길드 id(= 길드장 플레이어 id) → 길드원 찾기 |
    /// | g_req | 가입 신청자 | 신청한 길드 id → 길드장이 신청 목록을 찾음 |
    /// | g_st | 모든 길드원 | 이번 주 기여도 · 누적 기여도 · 길드 보스 피해 · 길드전 별 |
    ///
    /// 대시보드 Cloud Save 인덱스(Player · Public · 문자열): g_id · g_open · g_req (docs/guild.md)
    /// 로그인 중 2분마다(길드 화면을 열면 곧바로) 동기화. 내 기록이 바뀌면 곧 올림.
    /// </summary>
    public class GuildLink : MonoBehaviour
    {
        public const string KId = "g_id", KInfo = "g_info", KOpen = "g_open", KReq = "g_req", KStat = "g_st";
        private const int ListCap = 60;

        [Serializable] public class Seen { public string p; public long t; }

        /// <summary>길드장 공개 데이터(g_info).</summary>
        [Serializable]
        public class Info
        {
            public string name = "", intro = "", leaderName = "";
            public int minLv = 1;
            public bool free = true, disbanded;
            public List<string> members = new();    // 길드장 제외
            public List<string> kicked = new(), declined = new();
            public List<Seen> seen = new();         // 길드원별 마지막으로 더한 누적 기여도
            public long exp;                        // 길드 누적 경험치
            public string week = "";
            public long boss;                       // 이번 주 길드 보스 피해
            public int war;                         // 이번 주 길드전 별
            public long synced;                     // 길드장이 마지막으로 정리한 때(UTC ticks)
            public int Count => (members?.Count ?? 0) + 1;
        }

        /// <summary>길드원 한 명의 공개 기록(g_st).</summary>
        [Serializable]
        public class Stat
        {
            public string w = "";   // 주
            public long c, t, b;    // 이번 주 기여도 · 누적 기여도 · 이번 주 길드 보스 피해
            public string s = "";   // 길드전 상대별 별("3,2,0,…")
        }

        private static GuildLink _instance;
        public static GuildLink Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<GuildLink>();
                if (_instance == null) _instance = new GameObject("GuildLink").AddComponent<GuildLink>();
                return _instance;
            }
        }

        private void Awake() { if (_instance == null) _instance = this; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { _ = Instance; }

        public static bool Online => PublicProfile.Online;

        public string Error { get; private set; }
        public bool Busy { get; private set; }
        public DateTime SyncedAt { get; private set; } = DateTime.MinValue;
        public event Action OnChanged;

        /// <summary>모집 중인 실제 길드(마지막 검색 값).</summary>
        public List<GuildInfo> Found { get; private set; } = new();
        public bool Searching { get; private set; }
        /// <summary>순위표용 실제 길드 전체(마지막으로 불러온 값).</summary>
        public List<GuildInfo> Ranked { get; private set; } = new();
        public DateTime RankedAt { get; private set; } = DateTime.MinValue;

        private bool _dirty, _infoDirty, _disband;
        private float _nextSync = 3f, _nextSearch = float.MaxValue;   // 검색은 길드 화면이 부탁할 때만
        private Info _mine;   // 내가 길드장일 때 내 g_info

        public static void MarkDirty() => Instance._dirty = true;
        public void RequestSync() => _nextSync = 0f;
        public void RequestSearch() => _nextSearch = 0f;

        private void Update()
        {
            if (!Online || Busy || !CloudAccount.Synced) return;
            float now = Time.unscaledTime;
            if (now >= _nextSync) _ = SyncAsync();
            else if (_dirty || _infoDirty || _disband) _ = PublishOnlyAsync();
            else if (now >= _nextSearch) { _nextSearch = float.MaxValue; if (!PlayerGuild.Instance.Joined) _ = SearchAsync(); }
        }

        // ---------------- 길드장 동작 ----------------

        internal void CreateInfo(GuildInfo g)
        {
            _mine = new Info { name = g.name, intro = g.intro ?? "", minLv = Mathf.Max(1, g.minLevel), free = g.free, leaderName = PlayerProfile.Instance.DisplayName, week = PlayerGuild.Instance.Week };
            _infoDirty = true; _disband = false;
            RequestSync();
        }

        internal void EditInfo(string intro, bool free, int minLv)
        {
            if (_mine == null) return;
            _mine.intro = intro ?? ""; _mine.free = free; _mine.minLv = minLv;
            _infoDirty = true;
            OnChanged?.Invoke();
        }

        internal void Disband()
        {
            _mine = null;
            _disband = true;
            _dirty = true;
        }

        public bool Accept(string pid)
        {
            if (_mine == null || string.IsNullOrEmpty(pid)) return false;
            if (_mine.Count >= PlayerGuild.MaxMembers) return false;
            if (!_mine.members.Contains(pid)) _mine.members.Add(pid);
            _mine.declined.Remove(pid); _mine.kicked.Remove(pid);
            var g = PlayerGuild.Instance;
            g.Requests.RemoveAll(s => s.pid == pid);
            g.ApplyRealInfo(_mine);
            _infoDirty = true;
            g.Changed();
            return true;
        }

        public void Decline(string pid)
        {
            if (_mine == null || string.IsNullOrEmpty(pid)) return;
            AddCapped(_mine.declined, pid);
            var g = PlayerGuild.Instance;
            g.Requests.RemoveAll(s => s.pid == pid);
            _infoDirty = true;
            g.Changed();
        }

        public void Kick(string pid)
        {
            if (_mine == null || string.IsNullOrEmpty(pid)) return;
            _mine.members.Remove(pid);
            AddCapped(_mine.kicked, pid);
            var g = PlayerGuild.Instance;
            g.RealMembers.RemoveAll(m => m.pid == pid);
            g.ApplyRealInfo(_mine);
            _infoDirty = true;
            g.Changed();
        }

        private static void AddCapped(List<string> list, string pid)
        {
            if (!list.Contains(pid)) list.Add(pid);
            if (list.Count > ListCap) list.RemoveRange(0, list.Count - ListCap);
        }

        // ---------------- 올리기 ----------------

        public static Stat MyStat()
        {
            var g = PlayerGuild.Instance;
            return new Stat { w = g.Week, c = g.Contribution, t = g.ContributionTotal, b = g.MyBossDamage, s = string.Join(",", g.MyWarStars) };
        }

        private async Task PublishAsync()
        {
            var g = PlayerGuild.Instance;
            var data = new Dictionary<string, object>
            {
                { KId, g.IsReal ? g.Gid : "" },
                { KReq, g.RequestedGid ?? "" },
                { KStat, JsonUtility.ToJson(MyStat()) },
            };
            if (_disband)
            {
                data[KInfo] = "";
                data[KOpen] = "";
            }
            else if (g.IsReal && g.IsLeader && _mine != null)   // 길드장은 올릴 때마다 g_info도(순위표 · 길드원이 읽음)
            {
                _mine.leaderName = PlayerProfile.Instance.DisplayName;
                data[KInfo] = JsonUtility.ToJson(_mine);
                data[KOpen] = _mine.Count < PlayerGuild.MaxMembers ? "1" : "0";
            }
            _dirty = false; _infoDirty = false;
            bool disbanding = _disband;
            await CloudSaveService.Instance.Data.Player.SaveAsync(data, new PlayerSaveOptions(new PublicWrite()));
            if (disbanding) _disband = false;
        }

        private async Task PublishOnlyAsync()
        {
            Busy = true;
            try { await PublishAsync(); }
            catch (Exception e) { Debug.LogWarning($"[GuildLink] 길드 정보를 올리지 못했습니다: {e.Message}"); _dirty = true; _nextSync = Mathf.Min(_nextSync, Time.unscaledTime + 30f); }
            finally { Busy = false; }
        }

        // ---------------- 읽기 도우미 ----------------

        private static async Task<Dictionary<string, string>> LoadKeys(string pid, params string[] keys)
        {
            try
            {
                var res = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string>(keys), new PlayerLoadOptions(new PublicRead(pid)));
                var d = new Dictionary<string, string>();
                if (res != null)
                    foreach (var kv in res)
                    {
                        if (kv.Value?.Value == null) continue;
                        try { d[kv.Key] = kv.Value.Value.GetAs<string>() ?? ""; } catch { }
                    }
                return d;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GuildLink] {pid} 길드 정보를 읽지 못했습니다: {e.Message}");
                return null;
            }
        }

        public static Info ParseInfo(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var i = JsonUtility.FromJson<Info>(json);
                if (i == null || i.disbanded || string.IsNullOrEmpty(i.name)) return null;
                i.members ??= new(); i.kicked ??= new(); i.declined ??= new(); i.seen ??= new();
                i.name = PlayerGuild.CleanName(i.name);
                i.intro = PlayerGuild.CleanName(i.intro, PlayerGuild.MaxIntroLength);
                return i;
            }
            catch { return null; }
        }

        public static Stat ParseStat(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<Stat>(json); } catch { return null; }
        }

        public static int[] Stars(Stat st)
        {
            var a = new int[GuildWar.Defenders];
            if (st == null || string.IsNullOrEmpty(st.s)) return a;
            var parts = st.s.Split(',');
            for (int i = 0; i < a.Length && i < parts.Length; i++) if (int.TryParse(parts[i], out int v)) a[i] = Mathf.Clamp(v, 0, 3);
            return a;
        }

        public static GuildInfo ToGuildInfo(string gid, Info i)
        {
            var (lv, _) = PlayerGuild.LevelOf(i.exp);
            string week = PlayerGuild.Instance.Week;
            bool thisWeek = i.week == week;
            return new GuildInfo
            {
                seed = -1, gid = gid, name = i.name, intro = i.intro, level = lv, members = i.Count, minLevel = Mathf.Max(1, i.minLv), free = i.free,
                exp = i.exp, bossDamage = thisWeek ? Math.Max(0, i.boss) : 0, warStars = thisWeek ? Mathf.Clamp(i.war, 0, GuildWar.MaxStars) : 0, leaderName = i.leaderName ?? "",
            };
        }

        /// <summary>검색: key == value 인 플레이어들의 공개 키(최대 100명).</summary>
        private static async Task<List<(string pid, Dictionary<string, string> data)>> Query(string key, string value, FieldFilter.OpOptions op, params string[] keys)
        {
            var q = new Query(new List<FieldFilter> { new FieldFilter(key, value, op, true) }, new HashSet<string>(keys), 0, 100);
            var rows = await CloudSaveService.Instance.Data.Player.QueryAsync(q, new PlayerQueryOptions());
            var list = new List<(string, Dictionary<string, string>)>();
            if (rows == null) return list;
            foreach (var row in rows)
            {
                var d = new Dictionary<string, string>();
                if (row.Data != null)
                    foreach (var it in row.Data)
                        if (it?.Value != null) try { d[it.Key] = it.Value.GetAs<string>() ?? ""; } catch { }
                list.Add((row.Id, d));
            }
            return list;
        }

        private static string IndexHint(Exception e, string what) =>
            e.Message != null && e.Message.ToLowerInvariant().Contains("index")
                ? "서버 검색 설정(Cloud Save 인덱스 g_id · g_open · g_req)이 없어 " + what + " 불러오지 못했습니다"
                : what + " 불러오지 못했습니다";

        // ---------------- 동기화 ----------------

        public async Task SyncAsync()
        {
            if (Busy || !Online) return;
            Busy = true; Error = null;
            _nextSync = Time.unscaledTime + 120f;
            var g = PlayerGuild.Instance;
            string me = CloudAccount.PlayerId;
            try
            {
                if (g.IsReal && g.IsLeader && _mine == null && !_disband)
                {
                    // 길드장: 내 g_info 불러오기(다른 기기에서 만든 길드 등)
                    var own = await LoadKeys(me, KInfo);
                    _mine = own != null && own.TryGetValue(KInfo, out var j) ? ParseInfo(j) : null;
                    if (_mine == null && own != null) _mine = new Info { name = g.Name, intro = g.Intro, week = g.Week, leaderName = PlayerProfile.Instance.DisplayName };
                    _infoDirty = true;
                }
                // 길드와 상관없는 플레이어는 아무것도 올리지 않음(나간 뒤 한 번은 비운 값을 올림)
                if (_dirty || _infoDirty || _disband || g.IsReal || !string.IsNullOrEmpty(g.RequestedGid)) await PublishAsync();

                // 1) 가입 신청 결과
                if (!g.Joined && !string.IsNullOrEmpty(g.RequestedGid))
                {
                    string gid = g.RequestedGid;
                    var keys = await LoadKeys(gid, KInfo);
                    if (keys != null)
                    {
                        var info = keys.TryGetValue(KInfo, out var j) ? ParseInfo(j) : null;
                        if (info == null) g.ClearRequest($"'{g.RequestedName}' 길드가 해산되어 가입 신청이 취소되었습니다");
                        else if (info.members.Contains(me)) { g.EnterReal(ToGuildInfo(gid, info), false); g.Notices.Add($"'{info.name}' 길드에 가입했습니다"); }
                        else if (info.declined.Contains(me) || info.kicked.Contains(me)) g.ClearRequest($"'{info.name}' 길드가 가입 신청을 거절했습니다");
                    }
                }

                // 2) 내 실제 길드
                if (g.IsReal)
                {
                    string gid = g.Gid;
                    Info info;
                    if (g.IsLeader) info = _mine;
                    else
                    {
                        var keys = await LoadKeys(gid, KInfo);
                        info = keys == null ? g.RealInfo : (keys.TryGetValue(KInfo, out var j) ? ParseInfo(j) : null);
                        if (keys != null && info == null) { g.Removed("길드가 해산되었습니다"); info = null; }
                        else if (info != null && info.kicked.Contains(me)) { g.Removed($"'{info.name}' 길드에서 추방되었습니다"); info = null; }
                        else if (info != null && info.declined.Contains(me)) { g.Removed($"'{info.name}' 길드의 정원이 가득 차 가입하지 못했습니다"); info = null; }
                        else if (info != null && !info.members.Contains(me) && info.synced > g.JoinedAt
                                 && DateTime.UtcNow.Ticks - g.JoinedAt > TimeSpan.FromMinutes(5).Ticks)
                        { g.Removed($"'{info.name}' 길드에서 나가게 되었습니다"); info = null; }
                    }

                    if (info != null && g.IsReal)
                    {
                        // 길드원(이 길드 id를 내건 플레이어)
                        var rows = await Query(KId, gid, FieldFilter.OpOptions.EQ, PublicProfile.KProfile, KStat);
                        var claim = new Dictionary<string, (ProfileSnap snap, Stat st)>();
                        foreach (var (pid, d) in rows)
                        {
                            if (pid == me) continue;
                            ProfileSnap snap = d.TryGetValue(PublicProfile.KProfile, out var pj) ? PublicProfile.ParseSnap(pj, pid) : null;
                            claim[pid] = (snap, d.TryGetValue(KStat, out var sj) ? ParseStat(sj) : null);
                        }

                        if (g.IsLeader) await LeaderReconcile(info, claim, me);

                        // 길드원 목록(표시용)
                        string week = g.Week;
                        var members = new List<GuildMember>();
                        foreach (var kv in claim)
                        {
                            bool isLeader = kv.Key == gid;
                            if (!isLeader && !info.members.Contains(kv.Key)) continue;
                            var st = kv.Value.st;
                            bool cur = st != null && st.w == week;
                            var stars = cur ? Stars(st) : new int[GuildWar.Defenders];
                            int sum = 0; foreach (int s in stars) sum += s;
                            members.Add(new GuildMember
                            {
                                pid = kv.Key, snap = kv.Value.snap ?? new ProfileSnap { pid = kv.Key, name = "지휘관", real = true, level = 0 },
                                role = isLeader ? "길드장" : "길드원",
                                contribution = cur ? st.c : 0, bossDamage = cur ? st.b : 0, warStars = sum, warDetail = stars,
                            });
                        }
                        g.RealMembers = members;
                        g.ApplyRealInfo(info);
                        // 지난주 정산 · 순위표용
                        if ((DateTime.Now - RankedAt).TotalMinutes > 10) await RefreshRankAsync();
                        g.RefreshSeen();
                        if (g.IsLeader)
                        {
                            _mine.week = week; _mine.boss = g.GuildBossTotal(); _mine.war = g.GuildWarStars();
                            _mine.synced = DateTime.UtcNow.Ticks;
                            _infoDirty = true;
                            await PublishAsync();
                        }
                    }
                }
                SyncedAt = DateTime.Now;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GuildLink] 길드 동기화 실패: {e.Message}");
                Error = IndexHint(e, "길드 정보를");
                _nextSync = Time.unscaledTime + 30f;
            }
            finally
            {
                Busy = false;
                OnChanged?.Invoke();
                g.Changed();
            }
        }

        /// <summary>길드장: 길드원 목록 정리(자유 가입 · 탈퇴) · 받은 신청 · 경험치.</summary>
        private async Task LeaderReconcile(Info info, Dictionary<string, (ProfileSnap snap, Stat st)> claim, string me)
        {
            var g = PlayerGuild.Instance;
            string gid = g.Gid;
            var reqRows = await Query(KReq, gid, FieldFilter.OpOptions.EQ, PublicProfile.KProfile);
            var reqPids = new HashSet<string>();
            var requests = new List<ProfileSnap>();
            foreach (var (pid, d) in reqRows)
            {
                if (pid == me) continue;
                reqPids.Add(pid);
                if (info.members.Contains(pid) || info.kicked.Contains(pid) || info.declined.Contains(pid)) continue;
                var snap = d.TryGetValue(PublicProfile.KProfile, out var pj) ? PublicProfile.ParseSnap(pj, pid) : null;
                requests.Add(snap ?? new ProfileSnap { pid = pid, name = "지휘관", real = true, level = 0 });
            }

            // 자유 가입: 이 길드 id를 내건 사람을 목록에 넣음(정원 · 추방 확인)
            foreach (var pid in claim.Keys)
            {
                if (info.members.Contains(pid) || info.kicked.Contains(pid)) continue;
                if (info.free && info.Count < PlayerGuild.MaxMembers) { info.members.Add(pid); info.declined.Remove(pid); }
                else if (!info.declined.Contains(pid)) AddCapped(info.declined, pid);   // 정원 초과 · 승인제 → 들어오지 못함
            }
            // 탈퇴: 목록에 있는데 길드 id를 내리지 않았고(가입 확인 전 신청자는 남김)
            info.members.RemoveAll(pid => !claim.ContainsKey(pid) && !reqPids.Contains(pid));
            // 추방 · 거절 목록 정리: 더 이상 이 길드를 가리키지 않는 사람은 뺌
            info.kicked.RemoveAll(pid => !claim.ContainsKey(pid) && !reqPids.Contains(pid));
            info.declined.RemoveAll(pid => !claim.ContainsKey(pid) && !reqPids.Contains(pid));

            // 길드 경험치: 길드원(나 포함) 누적 기여도가 늘어난 만큼 더함
            void AddFrom(string pid, long total)
            {
                var s = info.seen.Find(x => x.p == pid);
                if (s == null) { s = new Seen { p = pid, t = 0 }; info.seen.Add(s); }
                if (total > s.t) { info.exp += total - s.t; s.t = total; }
                else if (total < s.t) s.t = total;   // 다시 들어와 0부터 — 기준만 낮춤
            }
            AddFrom(me, g.ContributionTotal);
            foreach (var pid in info.members) if (claim.TryGetValue(pid, out var c) && c.st != null) AddFrom(pid, c.st.t);
            info.seen.RemoveAll(s => s.p != me && !info.members.Contains(s.p));

            g.Requests = requests;
            _infoDirty = true;
        }

        // ---------------- 검색 · 순위 ----------------

        /// <summary>모집 중인 실제 길드 검색(g_open == "1").</summary>
        public async Task SearchAsync()
        {
            if (!Online || Searching) return;
            Searching = true;
            try
            {
                var rows = await Query(KOpen, "1", FieldFilter.OpOptions.EQ, KInfo);
                var list = new List<GuildInfo>();
                foreach (var (pid, d) in rows)
                {
                    if (pid == CloudAccount.PlayerId) continue;
                    var info = d.TryGetValue(KInfo, out var j) ? ParseInfo(j) : null;
                    if (info != null && info.Count < PlayerGuild.MaxMembers) list.Add(ToGuildInfo(pid, info));
                }
                list.Sort((a, b) => b.members.CompareTo(a.members));
                Found = list;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GuildLink] 길드 검색 실패: {e.Message}");
                Error = IndexHint(e, "플레이어 길드를");
            }
            finally { Searching = false; OnChanged?.Invoke(); }
        }

        /// <summary>순위표용 실제 길드 전체(g_open ≥ "0" — 모집 여부와 상관없이).</summary>
        public async Task RefreshRankAsync()
        {
            if (!Online) return;
            try
            {
                var rows = await Query(KOpen, "0", FieldFilter.OpOptions.GE, KInfo);
                var list = new List<GuildInfo>();
                foreach (var (pid, d) in rows)
                {
                    var info = d.TryGetValue(KInfo, out var j) ? ParseInfo(j) : null;
                    if (info != null) list.Add(ToGuildInfo(pid, info));
                }
                Ranked = list;
                RankedAt = DateTime.Now;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GuildLink] 길드 순위 불러오기 실패: {e.Message}");
                Error = IndexHint(e, "길드 순위를");
            }
            finally { OnChanged?.Invoke(); }
        }
    }
}
