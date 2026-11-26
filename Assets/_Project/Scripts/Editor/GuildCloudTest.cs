#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using SubcultureRPG.Meta;
using SubcultureRPG.Meta.Cloud;
using PlayerSaveOptions = Unity.Services.CloudSave.Models.Data.Player.SaveOptions;
using PublicWrite = Unity.Services.CloudSave.Models.Data.Player.PublicWriteAccessClassOptions;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 실제 길드 서버 시험: 이 프로젝트의 UGS에 시험용 익명 플레이어 2명(A · B)을 만들어 실제 코드(PlayerGuild · GuildLink)로
    /// 창설 → 검색 → 승인제 가입 신청 → 수락 → 길드원 확인 → 순위표 → 추방 → 자유 가입 → 해산까지 돌리고,
    /// 끝나면 두 플레이어의 공개 데이터를 비우고 계정을 삭제합니다. 내 계정 · 세이브는 건드리지 않음(별도 로그인 프로필 · 길드 세이브 키 백업 후 복원).
    ///
    ///   Unity.exe -batchmode -projectPath . -executeMethod SubcultureRPG.EditorTools.GuildCloudTest.RunBatch [-guildCloudOut 폴더]
    ///   (-quit는 붙이지 않음: 끝나면 스스로 종료) · 대시보드 Cloud Save 인덱스 g_id · g_open · g_req 필요
    /// </summary>
    public static class GuildCloudTest
    {
        private static readonly string[] Keys = { "guild", "guildwar", "mail", "arena", "__savestore.keys" };
        private static Task<bool> _task;
        private static string _out;
        private static readonly StringBuilder R = new();
        private static bool _ok = true;

        private const string Flag = "SubcultureRPG.GuildCloudTest.Running", OutKey = "SubcultureRPG.GuildCloudTest.Out";
        private static double _started;

        /// <summary>UGS는 플레이 모드에서만 초기화됨 → 빈 씬으로 플레이 모드에 들어가서 시험(타이틀 · 로그인이 뜨지 않아 내 계정은 로그인 안 됨).</summary>
        public static void RunBatch()
        {
            SessionState.SetString(OutKey, Arg("-guildCloudOut") ?? "Temp/GuildCloudTest");
            SessionState.SetBool(Flag, true);
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            _started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Wait;
        }

        private static void Wait()
        {
            if (_task == null)
            {
                if (EditorApplication.timeSinceStartup - _started > 120) { R.AppendLine("플레이 모드에 들어가지 못함"); _task = Task.FromResult(false); }
                else if (!EditorApplication.isPlaying) return;
                else { _out = SessionState.GetString(OutKey, "Temp/GuildCloudTest"); _task = Run(); return; }
            }
            if (!_task.IsCompleted) return;
            SessionState.SetBool(Flag, false);
            _out ??= SessionState.GetString(OutKey, "Temp/GuildCloudTest");
            EditorApplication.update -= Wait;
            bool ok = !_task.IsFaulted && _task.Result;
            if (_task.IsFaulted) R.AppendLine("EXCEPTION: " + _task.Exception);
            R.AppendLine("result=" + (ok ? "OK" : "FAILED"));
            Directory.CreateDirectory(_out);
            File.WriteAllText(Path.Combine(_out, "report.txt"), R.ToString(), new UTF8Encoding(false));
            Debug.Log("[GuildCloudTest]\n" + R);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static void Log(string s) { R.AppendLine(s); Debug.Log("[GuildCloudTest] " + s); }
        private static void Check(bool cond, string what) { Log((cond ? "  OK   " : "  FAIL ") + what); if (!cond) _ok = false; }

        // ---------------- 계정 · 상태 바꾸기 ----------------

        private static string _stateA, _stateB, _pidA, _pidB;

        private static async Task<string> SignIn(string profile)
        {
            var auth = AuthenticationService.Instance;
            if (auth.IsSignedIn) auth.SignOut();
            auth.SwitchProfile(profile);
            await auth.SignInAnonymouslyAsync();
            return auth.PlayerId;
        }

        /// <summary>이 계정의 길드 세이브를 PlayerPrefs에 넣고 길드 싱글턴을 새로 만듦(다른 기기에서 켠 것처럼).</summary>
        private static void Load(string state)
        {
            if (string.IsNullOrEmpty(state)) PlayerPrefs.DeleteKey("guild"); else PlayerPrefs.SetString("guild", state);
            foreach (var t in new[] { typeof(PlayerGuild), typeof(GuildLink), typeof(GuildWar) })
                foreach (var o in UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Include, FindObjectsSortMode.None))
                    UnityEngine.Object.DestroyImmediate(((Component)o).gameObject);
        }

        private static string Keep() => PlayerPrefs.GetString("guild", "");

        private static object Call(object target, string method, params object[] args)
        {
            var m = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (m == null) throw new MissingMethodException(target.GetType().Name, method);
            return m.Invoke(target, args);
        }

        private static async Task Sync(string who)
        {
            await GuildLink.Instance.SyncAsync();
            if (GuildLink.Instance.Error != null) Log($"  ({who} 동기화 오류: {GuildLink.Instance.Error})");
        }

        /// <summary>서버 검색이 반영될 때까지 몇 번 다시 시도.</summary>
        private static async Task<bool> Retry(Func<Task<bool>> f, int tries = 8)
        {
            for (int i = 0; i < tries; i++)
            {
                if (await f()) return true;
                await Task.Delay(1500);
            }
            return false;
        }

        // ---------------- 시험 ----------------

        private static async Task<bool> Run()
        {
            var backup = new string[Keys.Length];
            var had = new bool[Keys.Length];
            for (int i = 0; i < Keys.Length; i++) { had[i] = PlayerPrefs.HasKey(Keys[i]); backup[i] = PlayerPrefs.GetString(Keys[i], ""); }
            string guildName = "시험길드" + UnityEngine.Random.Range(100, 999);
            try
            {
                await UnityServices.InitializeAsync(new InitializationOptions().SetProfile("guildtest_a"));
                Log($"UGS 초기화 · 시험 길드 이름 '{guildName}'");

                // 1) A: 승인제 길드 창설
                _pidA = await SignIn("guildtest_a");
                Load(null);
                Log($"[A] 로그인 {_pidA}");
                var gA = PlayerGuild.Instance;
                var info = new GuildInfo { seed = -1, gid = _pidA, name = guildName, intro = "자동 시험", level = 1, members = 1, minLevel = 1, free = false };
                Call(gA, "EnterReal", info, true);
                Call(GuildLink.Instance, "CreateInfo", info);
                await Sync("A");
                Check(gA.IsReal && gA.IsLeader, "A 길드장으로 실제 길드 창설");
                _stateA = Keep();

                // 2) B: 검색 → 가입 신청
                _pidB = await SignIn("guildtest_b");
                Load(null);
                Log($"[B] 로그인 {_pidB}");
                GuildInfo found = null;
                bool searched = await Retry(async () =>
                {
                    await GuildLink.Instance.SearchAsync();
                    found = GuildLink.Instance.Found.Find(x => x.gid == _pidA);
                    return found != null;
                });
                Check(searched, $"B 검색(g_open 인덱스)으로 A 길드 찾음 — 모집 중 플레이어 길드 {GuildLink.Instance.Found.Count}곳" + (GuildLink.Instance.Error != null ? $" / {GuildLink.Instance.Error}" : ""));
                if (found == null) return false;
                Check(!found.free && found.name == guildName, "승인제 · 이름 그대로");
                string err = PlayerGuild.Instance.Join(found);
                Check(err == null && PlayerGuild.Instance.RequestedGid == _pidA && !PlayerGuild.Instance.Joined, "B 가입 신청(아직 가입 전)" + (err != null ? " — " + err : ""));
                await Sync("B");
                _stateB = Keep();

                // 3) A: 받은 신청 확인 → 수락
                await SignIn("guildtest_a");
                Load(_stateA);
                bool got = await Retry(async () => { await Sync("A"); return PlayerGuild.Instance.Requests.Exists(s => s.pid == _pidB); });
                Check(got, $"A 받은 가입 신청(g_req 인덱스)에 B — {PlayerGuild.Instance.Requests.Count}건");
                Check(GuildLink.Instance.Accept(_pidB), "A가 수락");
                await Sync("A");
                _stateA = Keep();

                // 4) B: 수락 확인 → 가입 · 길드원 목록에 길드장
                await SignIn("guildtest_b");
                Load(_stateB);
                await Sync("B");
                var gB = PlayerGuild.Instance;
                Check(gB.Joined && gB.IsReal && gB.Gid == _pidA, "B 가입 완료(다음 동기화 때 수락 확인)");
                bool seesLeader = await Retry(async () => { await Sync("B"); return gB.RealMembers.Exists(m => m.pid == _pidA && m.role == "길드장"); });
                Check(seesLeader, $"B의 길드원 목록(g_id 인덱스)에 길드장 A — {gB.RealMembers.Count}명");
                Check(gB.Name == guildName && gB.Members == 2, $"B가 본 길드 정보: '{gB.Name}' 길드원 {gB.Members}명");
                // 기여도 · 길드 보스 기록 올리기
                gB.Attend();
                Call(gB, "AddBossDamage", 12_345_678L);
                await Sync("B");
                _stateB = Keep();

                // 5) A: 길드원 B 확인 · 길드 경험치 · 순위표 → 추방
                await SignIn("guildtest_a");
                Load(_stateA);
                gA = PlayerGuild.Instance;
                bool seesB = await Retry(async () => { await Sync("A"); return gA.RealMembers.Exists(m => m.pid == _pidB); });
                Check(seesB, "A의 길드원 목록에 B");
                var mB = gA.RealMembers.Find(m => m.pid == _pidB);
                Check(mB != null && mB.bossDamage == 12_345_678L && mB.contribution == PlayerGuild.AttendExp, $"B의 기록이 A에게 보임(보스 피해 {mB?.bossDamage:N0} · 기여도 {mB?.contribution})");
                Check(gA.GuildBossTotal() >= 12_345_678L, $"길드 보스 합계 {gA.GuildBossTotal():N0}");
                Check(gA.RealInfo != null && gA.RealInfo.exp >= PlayerGuild.AttendExp, $"길드 경험치(길드원 누적 기여도 합) {gA.RealInfo?.exp}");
                bool ranked = await Retry(async () => { await GuildLink.Instance.RefreshRankAsync(); return GuildLink.Instance.Ranked.Exists(x => x.gid == _pidA); });
                Check(ranked, $"순위표 검색(g_open ≥ \"0\")에 A 길드 — 플레이어 길드 {GuildLink.Instance.Ranked.Count}곳");
                var (rank, total) = GuildRanking.MyRank(GuildRanking.Tab.Boss);
                Check(rank >= 1, $"길드 보스 순위 {rank}위 / {total}");
                GuildLink.Instance.Kick(_pidB);
                await Sync("A");
                _stateA = Keep();

                // 6) B: 추방 알림 → 길드 없음
                await SignIn("guildtest_b");
                Load(_stateB);
                await Sync("B");
                gB = PlayerGuild.Instance;
                Check(!gB.Joined && gB.Notices.Exists(n => n.Contains("추방")), "B 추방됨(알림: " + string.Join(" / ", gB.Notices) + ")");
                await Sync("B");   // g_id 비운 값 올림
                _stateB = Keep();

                // 7) A: 자유 가입으로 바꿈(추방 목록 정리 위해 한 번 동기화)
                await SignIn("guildtest_a");
                Load(_stateA);
                await Sync("A");
                PlayerGuild.Instance.EditRecruit("자유 가입 시험", true, 1);
                await Sync("A");
                Check(PlayerGuild.Instance.RealInfo != null && PlayerGuild.Instance.RealInfo.free, "A 가입 방식 → 자유 가입");
                _stateA = Keep();

                // 8) B: 자유 가입(바로) → A가 목록에 넣음
                await SignIn("guildtest_b");
                Load(_stateB);
                GuildInfo again = null;
                await Retry(async () => { await GuildLink.Instance.SearchAsync(); again = GuildLink.Instance.Found.Find(x => x.gid == _pidA && x.free); return again != null; });
                Check(again != null, "B가 자유 가입으로 바뀐 A 길드를 찾음");
                if (again != null) Check(PlayerGuild.Instance.Join(again) == null && PlayerGuild.Instance.Joined, "B 자유 가입(바로)");
                await Sync("B");
                _stateB = Keep();

                await SignIn("guildtest_a");
                Load(_stateA);
                gA = PlayerGuild.Instance;
                bool added = await Retry(async () => { await Sync("A"); return gA.RealInfo != null && gA.RealInfo.members.Contains(_pidB); });
                Check(added, "A가 자유 가입한 B를 길드원 목록에 넣음");
                // 9) A 해산
                gA.Leave();
                await Sync("A");
                Check(!gA.Joined, "A 해산(길드장이 나감)");
                _stateA = Keep();

                // 10) B: 해산 알림
                await SignIn("guildtest_b");
                Load(_stateB);
                await Sync("B");
                gB = PlayerGuild.Instance;
                Check(!gB.Joined && gB.Notices.Exists(n => n.Contains("해산")), "B 해산 알림: " + string.Join(" / ", gB.Notices));
                return _ok;
            }
            finally
            {
                // 정리: 두 시험 플레이어의 공개 데이터를 비우고 계정 삭제 → 내 세이브 키 복원
                foreach (var profile in new[] { "guildtest_a", "guildtest_b" })
                {
                    try
                    {
                        string pid = await SignIn(profile);
                        var empty = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { GuildLink.KInfo, "" }, { GuildLink.KOpen, "" }, { GuildLink.KId, "" }, { GuildLink.KReq, "" }, { GuildLink.KStat, "" },
                        };
                        await CloudSaveService.Instance.Data.Player.SaveAsync(empty, new PlayerSaveOptions(new PublicWrite()));
                        await AuthenticationService.Instance.DeleteAccountAsync();
                        Log($"정리: 시험 플레이어 {pid} 공개 데이터 비움 · 계정 삭제");
                    }
                    catch (Exception e) { Log($"정리 실패({profile}): {e.Message}"); _ok = false; }
                }
                Load(null);
                for (int i = 0; i < Keys.Length; i++)
                {
                    if (had[i]) PlayerPrefs.SetString(Keys[i], backup[i]); else PlayerPrefs.DeleteKey(Keys[i]);
                }
                PlayerPrefs.Save();
                Log("내 세이브의 길드 기록 복원");
            }
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
#endif
