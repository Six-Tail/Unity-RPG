using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;

namespace SubcultureRPG.Meta.Cloud
{
    /// <summary>
    /// 계정(Unity Gaming Services): 아이디 · 비밀번호 로그인 + 클라우드 세이브.
    ///
    /// · 세이브 전체(<see cref="SaveStore.Export"/>)를 압축해 Cloud Save의 플레이어 데이터 "save" 한 칸에 둠
    /// · 진행이 바뀌면(<see cref="SaveStore.Changed"/>) 잠시 모았다가 자동으로 올림(마지막 변경 8초 뒤 · 길어도 60초마다)
    /// · 로그인할 때 <see cref="SyncAsync"/>로 이 PC 세이브와 서버 세이브를 맞춤:
    ///   - 이 PC 세이브의 주인이 이 계정이고 서버가 더 새것(다른 PC에서 플레이) → 서버 것을 받아 게임 다시 불러오기
    ///   - 이 PC에서 처음 로그인 + 서버가 비어 있음 → 지금 PC 세이브를 이 계정으로 옮김(올림)
    ///   - 둘 다 진행이 있으면 플레이어가 고름(<see cref="SyncResult.Conflict"/>)
    ///   - 다른 계정의 세이브가 있던 PC → 이 계정의 서버 세이브로 바꿈(없으면 새로 시작)
    /// · 서버에 못 붙으면 오프라인으로 할 수 있고, 바뀐 내용은 다음 로그인 때 올림
    /// 씬을 넘어 살아 있는 오브젝트(DontDestroyOnLoad).
    /// </summary>
    public class CloudAccount : MonoBehaviour
    {
        public const string CloudKey = "save";
        private const string DirtyKey = "__savestore.dirty";        // 서버에 아직 안 올린 변경이 있음(1)
        private const string SyncedKey = "__savestore.syncedAt";    // 마지막으로 서버와 맞춘 세이브 시각(Ticks)
        private const string UserKey = "__savestore.username";      // 마지막 로그인 아이디(입력칸 미리 채움)

        private const float Debounce = 8f, MaxWait = 60f, RetryDelay = 30f;

        private static CloudAccount _instance;
        public static CloudAccount Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = new GameObject("CloudAccount").AddComponent<CloudAccount>();
                DontDestroyOnLoad(_instance.gameObject);
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; Offline = false; Synced = false; }

        /// <summary>이번 실행은 서버 없이(오프라인) 하기로 함.</summary>
        public static bool Offline { get; private set; }
        /// <summary>이번 실행에서 로그인 + 세이브 맞추기를 마침(다시 불러온 뒤 타이틀은 로그인을 건너뜀).</summary>
        public static bool Synced { get; private set; }

        public static bool ServicesReady => UnityServices.State == ServicesInitializationState.Initialized;
        public static bool SignedIn => ServicesReady && AuthenticationService.Instance.IsSignedIn;
        public static string PlayerId => SignedIn ? AuthenticationService.Instance.PlayerId : "";

        public static string LastUsername
        {
            get => PlayerPrefs.GetString(UserKey, "");
            private set => PlayerPrefs.SetString(UserKey, value ?? "");
        }

        private bool _dirty, _uploading;
        private float _firstChange, _lastChange, _retryAt;

        private void Awake()
        {
            _dirty = PlayerPrefs.GetInt(DirtyKey, 0) == 1;
            if (_dirty) _firstChange = _lastChange = Time.unscaledTime;
            SaveStore.Changed += MarkDirty;
        }

        private void OnDestroy()
        {
            SaveStore.Changed -= MarkDirty;
            if (_instance == this) _instance = null;
        }

        private void MarkDirty()
        {
            // 로그인 전(게임이 켜지며 시스템들이 스스로 쓰는 값 — 행동력 회복 · 하루 초기화 등)은 플레이 진행으로 보지 않음.
            // 그래야 다른 PC에서 플레이한 서버 세이브를 받을 때 '양쪽 다 바뀜'으로 잘못 묻지 않음.
            if (!Synced) return;
            float now = Time.unscaledTime;
            if (!_dirty)
            {
                _dirty = true;
                _firstChange = now;
                PlayerPrefs.SetInt(DirtyKey, 1);
            }
            _lastChange = now;
        }

        private void ClearDirty()
        {
            _dirty = false;
            PlayerPrefs.SetInt(DirtyKey, 0);
        }

        /// <summary>마지막으로 서버와 맞춘(올리거나 받은) 세이브의 시각(이 PC 시간). 아직 없으면 null.</summary>
        public static DateTime? LastSavedLocal
        {
            get
            {
                long t = SyncedAt;
                if (t <= 0) return null;
                try { return new DateTime(t, DateTimeKind.Utc).ToLocalTime(); } catch { return null; }
            }
        }

        private static long SyncedAt
        {
            get => long.TryParse(PlayerPrefs.GetString(SyncedKey, "0"), out long v) ? v : 0;
            set => PlayerPrefs.SetString(SyncedKey, value.ToString());
        }

        /// <summary>서버에 아직 안 올린 변경이 있음.</summary>
        public bool HasPending => _dirty;

        // ---------------- 자동 저장 ----------------

        private void Update()
        {
            if (!_dirty || _uploading || !Synced || !SignedIn) return;
            float now = Time.unscaledTime;
            if (now < _retryAt) return;
            if (now - _lastChange >= Debounce || now - _firstChange >= MaxWait) _ = UploadAsync();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && _dirty && Synced && SignedIn && !_uploading) _ = UploadAsync();
        }

        private void OnApplicationQuit()
        {
            // 끝나기 전에 다 못 올려도 '안 올린 변경' 표시가 남아 다음 로그인 때 올림
            SaveStore.Save();
            if (_dirty && Synced && SignedIn && !_uploading) _ = UploadAsync();
        }

        // ---------------- 연결 · 로그인 ----------------

        /// <summary>서비스 준비. 실패하면 오류 문구.</summary>
        public async Task<string> InitAsync()
        {
            if (ServicesReady) return null;
            try { await UnityServices.InitializeAsync(); return null; }
            catch (Exception e) { Debug.LogWarning($"[CloudAccount] 초기화 실패: {e.Message}"); return "서버에 연결할 수 없습니다."; }
        }

        /// <summary>이 PC에 남은 로그인 정보로 자동 로그인(없으면 false).</summary>
        public async Task<bool> TryRestoreAsync()
        {
            if (SignedIn) return true;
            if (!ServicesReady || !AuthenticationService.Instance.SessionTokenExists) return false;
            try { await AuthenticationService.Instance.SignInAnonymouslyAsync(); return SignedIn; }
            catch (Exception e) { Debug.LogWarning($"[CloudAccount] 자동 로그인 실패: {e.Message}"); return false; }
        }

        /// <summary>
        /// 게임의 비밀번호 규칙: 8 ~ <see cref="MaxPassword"/>자, 영문 · 숫자 · 기호 각 1개 이상(대문자 필수 아님).
        /// UGS는 대문자 · 소문자 · 숫자 · 기호를 모두 요구하므로, 서버로 보낼 때 뒤에 고정 글자를 붙여 그 규칙을 늘 채움
        /// (UGS 최대 30자 → 게임에서는 26자까지).
        /// </summary>
        public const int MinPassword = 8, MaxPassword = 26;
        private const string PassSuffix = "Sa1!";
        private static string ServerPass(string pass) => pass + PassSuffix;

        public async Task<string> SignInAsync(string user, string pass)
        {
            try
            {
                await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(user, ServerPass(pass));
            }
            catch (Exception e)
            {
                // 이 규칙 전에 만든 계정(비밀번호를 그대로 보냈던 계정)은 그대로 한 번 더 시도
                bool legacy = false;
                if (pass.Length <= 30)
                    try { await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(user, pass); legacy = true; }
                    catch { /* 처음 오류를 보여 줌 */ }
                if (!legacy) return ErrorText(e, false);
            }
            LastUsername = user; PlayerPrefs.Save();
            return null;
        }

        public async Task<string> SignUpAsync(string user, string pass)
        {
            try
            {
                await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(user, ServerPass(pass));
                LastUsername = user; PlayerPrefs.Save();
                return null;
            }
            catch (Exception e) { return ErrorText(e, true); }
        }

        /// <summary>
        /// 비밀번호 변경(로그인 중). 서버로는 게임 규칙의 덧붙임 글자를 붙여 보냄. 이 규칙 전에 만든 계정은 지금 비밀번호를 그대로 한 번 더 시도.
        /// 성공하면 null, 실패하면 이유 문구.
        /// </summary>
        public async Task<string> ChangePasswordAsync(string current, string next)
        {
            if (!SignedIn) return "로그인한 상태에서만 바꿀 수 있습니다.";
            try
            {
                await AuthenticationService.Instance.UpdatePasswordAsync(ServerPass(current), ServerPass(next));
                return null;
            }
            catch (Exception e)
            {
                if (current.Length <= 30)
                    try { await AuthenticationService.Instance.UpdatePasswordAsync(current, ServerPass(next)); return null; }
                    catch { /* 처음 오류를 보여 줌 */ }
                Debug.LogWarning($"[CloudAccount] 비밀번호 변경 실패: {e}");
                if (e is RequestFailedException rf && (rf.ErrorCode == CommonErrorCodes.TransportError || rf.ErrorCode == CommonErrorCodes.Timeout || rf.ErrorCode == CommonErrorCodes.ServiceUnavailable))
                    return "서버에 연결할 수 없습니다. 인터넷 연결을 확인해 주세요.";
                return "지금 비밀번호가 맞지 않습니다.";
            }
        }

        /// <summary>회원가입 직후: 가입하며 자동으로 된 로그인을 풀고 로그인 창에서 다시 로그인하게 함(아이디는 기억).</summary>
        public void SignOutOnly()
        {
            if (SignedIn) AuthenticationService.Instance.SignOut(true);
        }

        /// <summary>오프라인으로 하기(이번 실행만). 바뀐 내용은 다음 로그인 때 올림.</summary>
        public void GoOffline() { Offline = true; Synced = true; }

        /// <summary>로그아웃: 남은 변경을 올린 뒤 로그인 정보를 지우고 타이틀부터 다시.</summary>
        /// <summary>
        /// 계정 삭제(스토어 필수 기능): 서버의 세이브 · 공개 데이터(프로필 · 순위 · 친구)를 지우고 계정을 없앤 뒤,
        /// 이 기기의 게임 데이터도 지우고 처음부터(게스트) 다시 시작. 실패하면 사유, 성공하면 null(곧 게임을 다시 불러옴).
        /// </summary>
        public async Task<string> DeleteAccountAsync()
        {
            if (!SignedIn) return "로그인 상태가 아닙니다.";
            _dirty = false;   // 지우는 동안 다시 올리지 않게
            try
            {
                try { await CloudSaveService.Instance.Data.Player.DeleteAllAsync(new Unity.Services.CloudSave.Models.Data.Player.DeleteAllOptions(new Unity.Services.CloudSave.Models.Data.Player.PublicWriteAccessClassOptions())); }
                catch (Exception e) { Debug.LogWarning($"[CloudAccount] 공개 데이터 삭제 실패(계속): {e.Message}"); }
                await CloudSaveService.Instance.Data.Player.DeleteAllAsync(new Unity.Services.CloudSave.Models.Data.Player.DeleteAllOptions());
                await AuthenticationService.Instance.DeleteAccountAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudAccount] 계정 삭제 실패: {e}");
                return "계정을 삭제하지 못했습니다. 인터넷 연결을 확인하고 잠시 뒤 다시 시도해 주세요.";
            }
            try { AuthenticationService.Instance.SignOut(true); } catch { }
            SaveStore.ClearAccountData();
            SaveStore.Owner = "";
            Synced = false; Offline = false;
            SaveStore.ReloadGame();
            return null;
        }

        public async Task SignOutAsync()
        {
            if (SignedIn && _dirty && !_uploading) await UploadAsync();
            if (SignedIn) AuthenticationService.Instance.SignOut(true);
            Synced = false; Offline = false;
            SaveStore.ReloadGame();
        }

        private static string ErrorText(Exception e, bool signUp)
        {
            Debug.LogWarning($"[CloudAccount] {(signUp ? "가입" : "로그인")} 실패: {e}");
            if (e is RequestFailedException rf)
            {
                switch (rf.ErrorCode)
                {
                    case CommonErrorCodes.TransportError:
                    case CommonErrorCodes.Timeout:
                    case CommonErrorCodes.ServiceUnavailable:
                        return "서버에 연결할 수 없습니다. 인터넷 연결을 확인해 주세요.";
                    case CommonErrorCodes.TooManyRequests:
                        return "요청이 너무 많습니다. 잠시 뒤 다시 시도해 주세요.";
                    case CommonErrorCodes.Conflict:
                        return signUp ? "이미 사용 중인 아이디입니다." : "이미 로그인되어 있습니다.";
                }
                string m = rf.Message ?? "";
                if (signUp && (m.IndexOf("exist", StringComparison.OrdinalIgnoreCase) >= 0 || m.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0))
                    return "이미 사용 중인 아이디입니다.";
                if (m.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 && signUp)
                    return "비밀번호 규칙에 맞지 않습니다.";
                if (m.IndexOf("username", StringComparison.OrdinalIgnoreCase) >= 0 && signUp)
                    return "아이디 규칙에 맞지 않습니다.";
            }
            return signUp ? "가입하지 못했습니다. 아이디 · 비밀번호 규칙을 확인해 주세요." : "아이디 또는 비밀번호가 맞지 않습니다.";
        }

        // ---------------- 세이브 맞추기 ----------------

        public enum SyncResult
        {
            /// <summary>이 PC 세이브 그대로 계속.</summary>
            Continue,
            /// <summary>서버 세이브로 바꿈 → 게임 다시 불러오기 필요.</summary>
            Reload,
            /// <summary>이 PC와 서버 모두 진행이 있음 → <see cref="ResolveAsync"/>로 하나를 고름.</summary>
            Conflict,
            /// <summary>서버 오류.</summary>
            Error,
        }

        private SaveStore.Snapshot _pendingCloud;

        /// <summary>충돌일 때 비교용: 서버 세이브 시각(로컬 시간).</summary>
        public DateTime PendingCloudTime => _pendingCloud != null ? new DateTime(_pendingCloud.savedAt, DateTimeKind.Utc).ToLocalTime() : DateTime.MinValue;

        public string LastError { get; private set; }

        /// <summary>로그인 직후: 이 PC 세이브와 서버 세이브를 맞춤.</summary>
        public async Task<SyncResult> SyncAsync(bool localHasProgress)
        {
            LastError = null;
            SaveStore.Snapshot cloud;
            try { cloud = await DownloadAsync(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudAccount] 서버 세이브 받기 실패: {e.Message}");
                LastError = "서버 세이브를 받지 못했습니다.";
                return SyncResult.Error;
            }

            string pid = PlayerId, owner = SaveStore.Owner;

            if (owner == pid)
            {
                // 이 PC 세이브가 이 계정 것
                if (cloud == null || cloud.savedAt <= SyncedAt)
                    return await UploadThenContinue(true);                   // 서버가 그대로 → 남은 변경만 올림
                if (!_dirty) { Apply(cloud, pid); return SyncResult.Reload; } // 다른 PC에서 진행 → 받기
                _pendingCloud = cloud; return SyncResult.Conflict;            // 양쪽 다 바뀜
            }

            if (string.IsNullOrEmpty(owner))
            {
                // 이 PC에서 처음 로그인(옛 세이브 또는 새로 설치)
                if (cloud == null) { SaveStore.Owner = pid; return await UploadThenContinue(false); }   // 지금 세이브를 이 계정으로 옮김
                if (!localHasProgress) { Apply(cloud, pid); return SyncResult.Reload; }
                _pendingCloud = cloud; return SyncResult.Conflict;
            }

            // 다른 계정의 세이브가 있던 PC
            if (cloud == null)
            {
                SaveStore.ClearAccountData();                                  // 새 계정 → 처음부터
                SaveStore.Owner = pid;
                SyncedAt = 0;
                Synced = true;
                MarkDirty(); PlayerPrefs.Save();   // 다시 불러온 뒤 새 세이브를 올림
                return SyncResult.Reload;
            }
            Apply(cloud, pid);
            return SyncResult.Reload;
        }

        /// <summary>충돌 해결: useCloud = 서버 세이브로(→ 다시 불러오기), 아니면 이 PC 세이브를 서버에 덮어씀.</summary>
        public async Task<SyncResult> ResolveAsync(bool useCloud)
        {
            string pid = PlayerId;
            if (useCloud && _pendingCloud != null)
            {
                Apply(_pendingCloud, pid);
                _pendingCloud = null;
                return SyncResult.Reload;
            }
            _pendingCloud = null;
            SaveStore.Owner = pid;
            return await UploadThenContinue(false);
        }

        private async Task<SyncResult> UploadThenContinue(bool onlyIfDirty)
        {
            if (!onlyIfDirty || _dirty)
            {
                if (!await UploadAsync()) { LastError = "서버에 세이브를 올리지 못했습니다."; return SyncResult.Error; }
            }
            Synced = true;
            return SyncResult.Continue;
        }

        /// <summary>서버 세이브를 이 PC에 적용(기기 설정은 그대로). 호출한 쪽에서 게임 다시 불러오기.</summary>
        private void Apply(SaveStore.Snapshot cloud, string pid)
        {
            SaveStore.Import(cloud);
            SaveStore.Owner = pid;
            SyncedAt = cloud.savedAt;
            ClearDirty();
            PlayerPrefs.Save();
            Synced = true;
        }

        // ---------------- 올리기 · 받기 ----------------

        /// <summary>지금 세이브 전체를 서버에 올림. 성공하면 true.</summary>
        public async Task<bool> UploadAsync()
        {
            if (!SignedIn || _uploading) return false;
            _uploading = true;
            SaveStore.Save();
            var snap = SaveStore.Export();
            _dirty = false;   // 올리는 동안 바뀌면 다시 true가 됨
            try
            {
                string packed = Pack(snap);
                if (packed.Length > 4_500_000) Debug.LogWarning($"[CloudAccount] 세이브가 큽니다({packed.Length / 1024}KB). Cloud Save 한 칸 한도(5MB)에 가까움.");
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { CloudKey, packed } });
                SyncedAt = snap.savedAt;
                if (!_dirty) PlayerPrefs.SetInt(DirtyKey, 0);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudAccount] 서버 저장 실패(잠시 뒤 다시): {e.Message}");
                if (!_dirty) { _dirty = true; _firstChange = _lastChange = Time.unscaledTime; PlayerPrefs.SetInt(DirtyKey, 1); }
                _retryAt = Time.unscaledTime + RetryDelay;
                return false;
            }
            finally { _uploading = false; }
        }

        private static async Task<SaveStore.Snapshot> DownloadAsync()
        {
            var res = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { CloudKey });
            if (res == null || !res.TryGetValue(CloudKey, out var item) || item?.Value == null) return null;
            string packed = item.Value.GetAs<string>();
            return string.IsNullOrEmpty(packed) ? null : Unpack(packed);
        }

        // JSON → gzip → base64 (세이브 JSON은 반복이 많아 크게 줄어듦)
        private static string Pack(SaveStore.Snapshot snap)
        {
            byte[] raw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(snap));
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) gz.Write(raw, 0, raw.Length);
            return Convert.ToBase64String(ms.ToArray());
        }

        private static SaveStore.Snapshot Unpack(string packed)
        {
            using var src = new MemoryStream(Convert.FromBase64String(packed));
            using var gz = new GZipStream(src, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gz.CopyTo(outMs);
            return JsonUtility.FromJson<SaveStore.Snapshot>(Encoding.UTF8.GetString(outMs.ToArray()));
        }
    }
}
