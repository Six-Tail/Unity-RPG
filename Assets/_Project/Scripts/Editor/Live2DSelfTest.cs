#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SubcultureRPG.Characters;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// Live2D 표시 자동 시험. 메인 씬을 플레이 모드로 돌려 캐릭터의 Live2D 모델을 무대(Live2DStage)에 올리고,
    /// 무대가 그린 텍스처를 PNG 로 저장합니다(모델이 실제로 그려지는지 · 움직이는지 확인용).
    ///
    /// 배치 모드:
    ///   Unity.exe -batchmode -projectPath . -executeMethod SubcultureRPG.EditorTools.Live2DSelfTest.RunBatch
    ///             -live2dTestId c_seraphine -live2dTestOut "C:/…/out"      (-quit 는 붙이지 않음: 끝나면 스스로 종료)
    /// 에디터: 메뉴 SubcultureRPG > Live2D > 표시 시험 (플레이 모드로 들어가 결과를 Temp/Live2DSelfTest 에 저장)
    /// </summary>
    public static class Live2DSelfTest
    {
        private const string Flag = "SubcultureRPG.Live2DSelfTest.Running";
        private const string OutKey = "SubcultureRPG.Live2DSelfTest.Out";
        private const string IdKey = "SubcultureRPG.Live2DSelfTest.Id";
        private const string BatchKey = "SubcultureRPG.Live2DSelfTest.Batch";
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";

        private static int _frames;
        private static double _started;
        private static RawImage _user;
        private static RenderTexture _rt;
        private static CharacterData _character;
        private static readonly StringBuilder Report = new();

        public static void RunBatch()
        {
            string id = Arg("-live2dTestId") ?? "c_seraphine";
            string outDir = Arg("-live2dTestOut") ?? "Temp/Live2DSelfTest";
            Begin(id, outDir, batch: true);
        }

        [MenuItem("SubcultureRPG/Live2D/표시 시험 (세라핀)", false, 50)]
        public static void RunMenu() => Begin("c_seraphine", "Temp/Live2DSelfTest", batch: false);

        private static void Begin(string id, string outDir, bool batch)
        {
            Live2DLinker.LinkAll(report: false);
            SessionState.SetString(IdKey, id);
            SessionState.SetString(OutKey, outDir);
            SessionState.SetBool(BatchKey, batch);
            SessionState.SetBool(Flag, true);
            if (!EditorApplication.isPlaying)
            {
                if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
                EditorApplication.EnterPlaymode();
            }
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            _frames = 0;
            _started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            // 플레이 모드에 못 들어가거나 멈췄을 때를 대비한 제한 시간
            if (EditorApplication.timeSinceStartup - _started > 180) { Finish(false, "timeout"); return; }
            if (!EditorApplication.isPlaying) return;

            _frames++;
            try
            {
                if (_frames == 20) Setup();
                else if (_frames == 90) Capture("frame_a.png");
                else if (_frames == 150) Capture("frame_b.png");
                else if (_frames == 160) Finish(true, "done");
            }
            catch (Exception e)
            {
                Report.AppendLine("EXCEPTION: " + e);
                Finish(false, "exception");
            }
        }

        private static void Setup()
        {
            string id = SessionState.GetString(IdKey, "c_seraphine");
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterData"))
            {
                var c = AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null && c.id == id && Live2DStage.Has(c)) { _character = c; break; }
            }
            Report.AppendLine("sdkInstalled=" + Live2DLinker.SdkInstalled);
            Report.AppendLine("pipeline=" + (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name : "(built-in)"));
            if (_character == null) { Report.AppendLine("character '" + id + "' has no Live2D model linked"); Finish(false, "no model"); return; }

            Report.AppendLine("character=" + _character.displayName + " prefab=" + _character.live2DModelPrefab +
                              " idle=" + (_character.live2DIdleMotion != null ? _character.live2DIdleMotion.name : "(none)"));

            // 무대는 '표시하는 곳'이 살아 있어야 유지되므로, 시험용 RawImage 를 하나 둠
            var canvasGO = new GameObject("Live2DSelfTestCanvas", typeof(Canvas));
            var rawGO = new GameObject("User", typeof(RectTransform), typeof(RawImage));
            rawGO.transform.SetParent(canvasGO.transform, false);
            _user = rawGO.GetComponent<RawImage>();
            _rt = Live2DStage.Instance.Show(_character, _user) as RenderTexture;
            _user.texture = _rt;
            Report.AppendLine("stageTexture=" + (_rt != null ? _rt.width + "x" + _rt.height + " " + _rt.format : "null"));
        }

        private static void Capture(string file)
        {
            if (_rt == null) { Report.AppendLine(file + ": no texture"); return; }
            var stage = Live2DStage.Instance;
            int renderers = 0;
            Bounds b = default; bool any = false;
            foreach (var r in stage.GetComponentsInChildren<Renderer>())
            {
                renderers++;
                if (r.bounds.size.sqrMagnitude <= 0f) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            var cam = stage.GetComponentInChildren<Camera>();

            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            // 얼마나 그려졌는지(알파가 있는 픽셀 비율) + 평균 색
            var px = tex.GetPixels32();
            long covered = 0, sumA = 0, sumR = 0, sumG = 0, sumB = 0;
            foreach (var p in px) { if (p.a > 8) covered++; sumA += p.a; sumR += p.r; sumG += p.g; sumB += p.b; }
            string outDir = SessionState.GetString(OutKey, "Temp/Live2DSelfTest");
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, file), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);

            Report.AppendLine($"{file}: frame={_frames} time={Time.time:0.00} renderers={renderers} bounds={(any ? b.size.ToString("0.00") : "none")} " +
                              $"camOrtho={(cam != null ? cam.orthographicSize.ToString("0.000") : "-")} camEnabled={(cam != null && cam.enabled)} " +
                              $"coverage={(100.0 * covered / px.Length):0.0}% avgRGBA=({sumR / px.Length},{sumG / px.Length},{sumB / px.Length},{sumA / px.Length})");
        }

        private static void Finish(bool ok, string why)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Flag, false);
            Report.AppendLine("result=" + (ok ? "OK" : "FAILED") + " (" + why + ")");
            string outDir = SessionState.GetString(OutKey, "Temp/Live2DSelfTest");
            try
            {
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, "report.txt"), Report.ToString(), Encoding.UTF8);
            }
            catch (Exception e) { Debug.LogError("[Live2DSelfTest] report write failed: " + e.Message); }
            Debug.Log("[Live2DSelfTest]\n" + Report);
            Report.Clear();

            if (SessionState.GetBool(BatchKey, false)) EditorApplication.Exit(ok ? 0 : 1);
            else if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
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
