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
    /// 실제 플레이어끼리의 친구 신청 · 수락(서버 함수 없이 각자 자기 공개 데이터만 씀).
    /// · 신청: 보낸 사람이 자기 공개 데이터 fr_to0 · fr_to1 · fr_to2(신청 칸 3개)에 상대 플레이어 id를 적음
    /// · 받은 신청: fr_toN == 내 id 인 플레이어를 검색(대시보드 인덱스 fr_to0 · fr_to1 · fr_to2 필요)
    /// · 수락: 받은 사람이 자기 fr_list(친구 id 목록)에 보낸 사람을 넣음 → 보낸 사람이 다음 동기화 때 그걸 보고 친구로 확정 · 칸을 비움
    /// · 거절: 받은 사람의 fr_dec(거절 목록)에 넣음 → 보낸 사람이 보고 신청을 지움
    /// · 삭제: 내 fr_list에서 빼면, 상대가 다음 동기화 때 '내 목록에도 신청 칸에도 상대가 없음'을 보고 같이 지움
    /// 로그인 중 2분마다 + 친구 화면을 열 때 동기화. 내 목록이 바뀌면 곧바로 올림.
    /// </summary>
    public class FriendLink : MonoBehaviour
    {
        public const int Slots = 3;
        public const string KList = "fr_list", KDec = "fr_dec";
        public static string KTo(int i) => "fr_to" + i;

        private static FriendLink _instance;
        public static FriendLink Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<FriendLink>();
                if (_instance == null) _instance = new GameObject("FriendLink").AddComponent<FriendLink>();
                return _instance;
            }
        }

        private void Awake() { if (_instance == null) _instance = this; }

        /// <summary>나에게 온 실제 플레이어의 친구 신청(마지막 동기화 값).</summary>
        public List<ProfileSnap> Incoming { get; private set; } = new();
        public string Error { get; private set; }
        public bool Busy { get; private set; }
        public DateTime SyncedAt { get; private set; } = DateTime.MinValue;
        public event Action OnChanged;

        /// <summary>동기화 중 생긴 알림(수락됨 · 거절됨) — 친구 화면이 보여 주고 비움.</summary>
        public readonly List<string> Notices = new();

        private bool _dirty;
        private float _nextSync;

        public static bool Online => PublicProfile.Online;

        /// <summary>내 친구 목록 · 신청이 바뀜 → 곧 올림.</summary>
        public static void MarkDirty() => Instance._dirty = true;

        /// <summary>곧바로 동기화(친구 화면을 열 때 등).</summary>
        public void RequestSync() => _nextSync = 0f;

        public void RemoveIncoming(string pid)
        {
            if (Incoming.RemoveAll(s => s.pid == pid) > 0) OnChanged?.Invoke();
        }

        private void Update()
        {
            if (!Online || Busy || !CloudAccount.Synced) return;
            if (Time.unscaledTime >= _nextSync) _ = SyncAsync();
            else if (_dirty) _ = PublishOnlyAsync();
        }

        // ---------------- 올리기 ----------------

        private async Task PublishAsync()
        {
            var s = PlayerSocial.Instance;
            var data = new Dictionary<string, object>();
            var outgoing = s.Outgoing;
            for (int i = 0; i < Slots; i++) data[KTo(i)] = i < outgoing.Count ? outgoing[i].pid : "";
            var ids = new List<string>();
            foreach (var f in s.RealFriends) ids.Add(f.pid);
            data[KList] = string.Join(",", ids);
            data[KDec] = string.Join(",", s.Declined);
            _dirty = false;
            await CloudSaveService.Instance.Data.Player.SaveAsync(data, new PlayerSaveOptions(new PublicWrite()));
        }

        private async Task PublishOnlyAsync()
        {
            Busy = true;
            try { await PublishAsync(); }
            catch (Exception e) { Debug.LogWarning($"[FriendLink] 친구 목록을 올리지 못했습니다: {e.Message}"); _dirty = true; _nextSync = Mathf.Min(_nextSync, Time.unscaledTime + 30f); }
            finally { Busy = false; }
        }

        // ---------------- 동기화 ----------------

        private static bool Has(string csv, string id)
        {
            if (string.IsNullOrEmpty(csv) || string.IsNullOrEmpty(id)) return false;
            foreach (var p in csv.Split(',')) if (p == id) return true;
            return false;
        }

        /// <summary>그 플레이어의 공개 키 몇 개(문자열). 못 읽으면 null.</summary>
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
                Debug.LogWarning($"[FriendLink] {pid} 친구 정보를 읽지 못했습니다: {e.Message}");
                return null;
            }
        }

        public async Task SyncAsync()
        {
            if (Busy || !Online) return;
            Busy = true; Error = null;
            _nextSync = Time.unscaledTime + 120f;
            try
            {
                var s = PlayerSocial.Instance;
                string me = CloudAccount.PlayerId;
                await PublishAsync();

                // 1) 받은 신청: 신청 칸 어딘가에 내 id를 적은 플레이어
                var found = new Dictionary<string, ProfileSnap>();
                for (int i = 0; i < Slots; i++)
                {
                    var q = new Query(new List<FieldFilter> { new FieldFilter(KTo(i), me, FieldFilter.OpOptions.EQ, true) },
                        new HashSet<string> { PublicProfile.KProfile }, 0, 100);
                    var rows = await CloudSaveService.Instance.Data.Player.QueryAsync(q, new PlayerQueryOptions());
                    if (rows == null) continue;
                    foreach (var row in rows)
                    {
                        if (row.Id == me || found.ContainsKey(row.Id)) continue;
                        ProfileSnap snap = null;
                        if (row.Data != null)
                            foreach (var it in row.Data)
                                if (it?.Value != null && it.Key == PublicProfile.KProfile)
                                    try { snap = PublicProfile.ParseSnap(it.Value.GetAs<string>(), row.Id); } catch { }
                        found[row.Id] = snap ?? new ProfileSnap { pid = row.Id, name = "지휘관", real = true, level = 0 };
                    }
                }
                s.PruneDeclined(found.Keys);   // 신청을 거둔 사람은 거절 목록에서도 뺌
                var incoming = new List<ProfileSnap>();
                foreach (var kv in found)
                    if (!s.IsRealFriend(kv.Key) && !s.IsDeclined(kv.Key) && !s.IsPending(kv.Key)) incoming.Add(kv.Value);
                // 서로 신청했으면 바로 친구
                foreach (var kv in found)
                    if (s.IsPending(kv.Key) && s.ConfirmAccepted(kv.Key)) Notices.Add($"{s.NameOf(kv.Key)} 님과 친구가 되었습니다");
                Incoming = incoming;

                // 2) 보낸 신청의 결과
                foreach (var o in new List<PlayerSocial.RealFriend>(s.Outgoing))
                {
                    var keys = await LoadKeys(o.pid, KList, KDec);
                    if (keys == null) continue;
                    if (keys.TryGetValue(KList, out var list) && Has(list, me))
                    {
                        if (s.ConfirmAccepted(o.pid)) Notices.Add($"{o.name} 님이 친구 신청을 수락했습니다");
                    }
                    else if (keys.TryGetValue(KDec, out var dec) && Has(dec, me))
                    {
                        s.DropOutgoing(o.pid);
                        Notices.Add($"{o.name} 님이 친구 신청을 거절했습니다");
                    }
                }

                // 3) 상대가 친구를 삭제했는지(상대 목록에도 · 신청 칸에도 내가 없으면)
                var probe = new List<string> { KList };
                for (int i = 0; i < Slots; i++) probe.Add(KTo(i));
                foreach (var f in new List<PlayerSocial.RealFriend>(s.RealFriends))
                {
                    var keys = await LoadKeys(f.pid, probe.ToArray());
                    if (keys == null || !keys.ContainsKey(KList)) continue;   // 못 읽었거나 예전 버전 — 그대로 둠
                    bool listed = Has(keys[KList], me);
                    for (int i = 0; i < Slots && !listed; i++) listed = keys.TryGetValue(KTo(i), out var to) && to == me;
                    if (!listed) s.DropFriend(f.pid);
                }

                if (_dirty) await PublishAsync();   // 위에서 친구 확정 · 정리로 바뀐 내 목록
                SyncedAt = DateTime.Now;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FriendLink] 친구 동기화 실패: {e.Message}");
                bool index = e.Message != null && e.Message.ToLowerInvariant().Contains("index");
                Error = index ? "서버 검색 설정(인덱스 fr_to0 · fr_to1 · fr_to2)이 없어 받은 신청을 불러오지 못했습니다" : "친구 정보를 동기화하지 못했습니다";
                _nextSync = Time.unscaledTime + 30f;
            }
            finally
            {
                Busy = false;
                OnChanged?.Invoke();
                PlayerSocial.Instance.Ping();   // 홈 알림
            }
        }
    }
}
