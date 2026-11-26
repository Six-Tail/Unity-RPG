#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SubcultureRPG.Meta;
using SubcultureRPG.Meta.UI;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 길드 자동 시험(에디터 모드 · 로그인 없이 = 가상 길드 경로): 길드 화면의 모든 탭을 실제로 그려 보고
    /// 길드 보스 단계 계산 · 길드전 방어 팀 · 순위표를 확인합니다. 세이브의 길드 기록(guild · guildwar)은 끝나면 원래대로 돌려 둠.
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod SubcultureRPG.EditorTools.GuildSelfTest.RunBatch [-guildTestOut 폴더]
    ///   에디터: 메뉴 SubcultureRPG > 길드 자동 시험 → Temp/GuildSelfTest/report.txt
    /// </summary>
    [InitializeOnLoad]
    public static class GuildSelfTest
    {
        // 에디터가 열려 있을 때: Temp/guild_test_request.txt 가 생기면 실행(플레이 중이 아닐 때) → Temp/GuildSelfTest/report.txt
        private const string Request = "Temp/guild_test_request.txt";
        private static double _next;
        static GuildSelfTest() { EditorApplication.update += Poll; }
        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
            File.Delete(Request);
            Run("Temp/GuildSelfTest");
        }

        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private static readonly string[] Keys = { "guild", "guildwar", "mail", "arena" };   // 시험이 건드릴 수 있는 세이브 키

        public static void RunBatch()
        {
            bool ok = Run(Arg("-guildTestOut") ?? "Temp/GuildSelfTest");
            EditorApplication.Exit(ok ? 0 : 1);
        }

        [MenuItem("SubcultureRPG/길드 자동 시험", false, 62)]
        private static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("플레이 중에는 쓸 수 없습니다"); return; }
            Run("Temp/GuildSelfTest");
            EditorUtility.RevealInFinder(Path.GetFullPath("Temp/GuildSelfTest/report.txt"));
        }

        private static bool Run(string outDir)
        {
            var r = new StringBuilder();
            bool ok = true;
            void Check(bool cond, string what) { r.AppendLine((cond ? "  OK   " : "  FAIL ") + what); if (!cond) ok = false; }

            // 세이브 백업
            var backup = new string[Keys.Length];
            var had = new bool[Keys.Length];
            for (int i = 0; i < Keys.Length; i++) { had[i] = PlayerPrefs.HasKey(Keys[i]); backup[i] = PlayerPrefs.GetString(Keys[i], ""); }

            GameObject screenGo = null;
            UnityEngine.SceneManagement.Scene temp = default;
            var prevActive = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            try
            {
                // 배치 모드에서만 메인 씬을 엶(에디터가 열려 있으면 지금 씬을 건드리지 않음 — 저장 안 된 변경 보호)
                if (Application.isBatchMode && File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); prevActive = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); }
                // 시험이 만드는 오브젝트(길드 싱글턴 · 화면)는 임시 씬에 — 끝나면 통째로 닫아 메인 씬에 남지 않게
                temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(temp);

                // --- 계산 ---
                r.AppendLine("[길드 보스 단계]");
                for (int s = 1; s <= 4; s++) r.AppendLine($"  {s}단계 체력 {GuildBoss.StageHp(s):N0} · 누적 {GuildBoss.ClearAt(s):N0} · Lv.{GuildBoss.LevelOf(s)} · 난이도 {GuildBoss.DifficultyOf(s)}");
                Check(GuildBoss.Cleared(0) == 0 && GuildBoss.Current(0) == 1, "피해 0 → 1단계");
                Check(GuildBoss.Cleared(GuildBoss.ClearAt(1)) == 1 && GuildBoss.Current(GuildBoss.ClearAt(1)) == 2, "1단계 체력만큼 → 2단계");
                Check(GuildBoss.Cleared(GuildBoss.ClearAt(3) - 1) == 2, "3단계 직전 → 2단계까지 깸");
                Check(GuildBoss.Cleared(long.MaxValue / 4) == GuildBoss.MaxStage, "아주 큰 피해 → 모든 단계");
                Check(GuildWar.StarsFor(4, 4) == 3 && GuildWar.StarsFor(2, 4) == 2 && GuildWar.StarsFor(1, 4) == 1 && GuildWar.StarsFor(0, 4) == 0, "길드전 별 규칙");
                var pool = VirtualGuilds.Pool("W20261123", 0.5f);
                Check(pool.Count == VirtualGuilds.RankPool, $"가상 길드 {pool.Count}곳");
                var a = VirtualGuilds.Info(77); var b = VirtualGuilds.Info(77);
                Check(a.name == b.name && a.level == b.level, $"가상 길드 번호가 같으면 같은 길드('{a.name}' Lv.{a.level})");
                Check(VirtualGuilds.BossDamage(77, 20, 5, "W1", 0.2f) <= VirtualGuilds.BossDamage(77, 20, 5, "W1", 0.8f), "주가 지날수록 가상 길드 보스 피해가 늘어남");

                // --- 길드 상태 + 화면 ---
                // 에디터 모드라 Awake가 안 돌았음 — 세이브의 보유 캐릭터 · 장비를 먼저 불러 둠(길드전 상대 강함이 내 전투력 기준)
                var roster = UnityEngine.Object.FindAnyObjectByType<PlayerRoster>(FindObjectsInactive.Include);
                if (roster != null) roster.Load();
                var equip = UnityEngine.Object.FindAnyObjectByType<PlayerEquipment>(FindObjectsInactive.Include);
                if (equip != null) typeof(PlayerEquipment).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(equip, null);
                long myPower = 0; int k = 0;
                foreach (var kv in CombatPower.All(roster, equip)) { if (k++ >= 4) break; myPower += kv.Value; }
                r.AppendLine($"[내 주력 4명 전투력] {myPower:N0}");
                var g = PlayerGuild.Instance;
                bool wasJoined = g.Joined;
                r.AppendLine($"[상태] 시작 전 가입={wasJoined} 실제={g.IsReal}");

                screenGo = new GameObject("GuildSelfTestScreen");
                screenGo.SetActive(false);
                var screen = screenGo.AddComponent<GuildScreen>();
                var home = UnityEngine.Object.FindAnyObjectByType<HomeScreen>(FindObjectsInactive.Include);
                if (home != null) screen.SetFont(home.Font);
                Invoke(screen, "Awake");
                screen.Show(true);

                var tabField = typeof(GuildScreen).GetField("_tab", BindingFlags.NonPublic | BindingFlags.Instance);
                var tabType = tabField.FieldType;
                void Draw(string tab)
                {
                    tabField.SetValue(screen, Enum.Parse(tabType, tab));
                    try { Invoke(screen, "Refresh"); Check(true, $"화면 탭 '{tab}' 그리기"); }
                    catch (Exception e) { Check(false, $"화면 탭 '{tab}' 그리기 — {(e.InnerException ?? e).GetType().Name}: {(e.InnerException ?? e).Message}\n{(e.InnerException ?? e).StackTrace}"); }
                }

                if (!wasJoined)
                {
                    Draw("Find"); Draw("Rank");
                    int seed = 1;
                    while (seed < 500 && VirtualGuilds.Info(seed).minLevel > PlayerProfile.Instance.Level) seed++;   // 가입 조건이 맞는 가상 길드
                    string err = g.Join(VirtualGuilds.Info(seed));
                    Check(err == null && g.Joined, "가상 길드 가입" + (err != null ? " — " + err : ""));
                }
                Draw("Members"); Draw("Boss"); Draw("War"); Draw("Rank");
                if (g.IsReal && g.IsLeader) Draw("Requests");

                var members = g.MemberList(null);
                Check(members.Count == g.Members, $"길드원 수 {members.Count} = {g.Members}");
                r.AppendLine($"  길드 보스 합계 {g.GuildBossTotal():N0} ({GuildBoss.Cleared(g.GuildBossTotal())}단계 깸) · 길드전 ★{GuildWar.OurStars()} : ★{GuildWar.TheirStars()} '{GuildWar.Opponent().name}'");

                var defs = GuildWar.Instance.DefenderTeams(roster != null ? roster.Database : null);
                Check(defs.Count == GuildWar.Defenders, $"길드전 방어 팀 {defs.Count}개");
                for (int i = 0; i < defs.Count; i++) r.AppendLine($"  {i + 1}번 {defs[i].name} Lv.{defs[i].level} +{defs[i].transcend} 전투력 {defs[i].power:N0} ({string.Join("/", defs[i].team)})");
                if (defs.Count == GuildWar.Defenders) Check(defs[GuildWar.Defenders - 1].level >= defs[0].level, "뒤 번호일수록 강함(레벨)");
                var opp = GuildWar.Opponent();
                r.AppendLine($"  우리 길드전 점수 {g.WarRating:N0} → 상대 '{opp.name}' 점수 {opp.warRating:N0} · {GuildWar.OpponentRank()}위 · 강함 {GuildWar.Strength(opp.warRating):0.00}");
                Check(Mathf.Abs(opp.warRating - g.WarRating) <= 400, "점수가 가까운 가상 길드와 매칭");
                long maxDef = 0; foreach (var d in defs) maxDef = System.Math.Max(maxDef, d.power);
                Check(myPower <= 0 || maxDef < myPower, $"가장 강한 방어 팀({maxDef:N0})이 내 전투력({myPower:N0})보다 약함 — 내 전투력에 맞추지 않음");
                // 점수가 아주 높은 가상 길드(최상위)도 결투장 최상위 가상 상대 수준인지
                int top = 1; for (int s = 1; s <= VirtualGuilds.RankPool; s++) if (VirtualGuilds.WarRating(s) > VirtualGuilds.WarRating(top)) top = s;
                r.AppendLine($"  최상위 가상 길드 '{VirtualGuilds.Info(top).name}' 점수 {VirtualGuilds.WarRating(top):N0} → 강함 {GuildWar.Strength(VirtualGuilds.WarRating(top)):0.00}(최대 1.40)");

                var (rank, total) = GuildRanking.MyRank(GuildRanking.Tab.Boss);
                Check(rank >= 1 && total >= VirtualGuilds.RankPool, $"순위표에 우리 길드 {rank}위 / {total}");
            }
            catch (Exception e)
            {
                r.AppendLine("EXCEPTION: " + e);
                ok = false;
            }
            finally
            {
                if (screenGo != null) UnityEngine.Object.DestroyImmediate(screenGo);
                if (temp.IsValid())
                {
                    if (prevActive.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(prevActive);
                    EditorSceneManager.CloseScene(temp, true);
                }
                for (int i = 0; i < Keys.Length; i++)
                {
                    if (had[i]) PlayerPrefs.SetString(Keys[i], backup[i]); else PlayerPrefs.DeleteKey(Keys[i]);
                }
                PlayerPrefs.Save();
                r.AppendLine("세이브의 길드 기록을 시험 전으로 되돌림");
            }

            r.AppendLine("result=" + (ok ? "OK" : "FAILED"));
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "report.txt"), r.ToString(), new UTF8Encoding(false));
            Debug.Log("[GuildSelfTest]\n" + r);
            return ok;
        }

        private static void Invoke(object target, string method)
        {
            var m = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (m == null) throw new MissingMethodException(target.GetType().Name, method);
            m.Invoke(target, null);
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
