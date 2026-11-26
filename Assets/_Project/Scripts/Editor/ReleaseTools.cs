using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 출시 도구(메뉴 Tools ▸ 출시): 출시 설정 적용 · 버전 올리기 · Windows / Android / iOS 빌드 · 출시 화면 미리보기.
    /// 결과물은 프로젝트 폴더의 Builds/ 아래(깃에 올리지 않음).
    /// · Windows: Builds/Windows/스텔라 앵커.exe → 배포용 zip(Builds/StellaAnchor-Windows-{버전}.zip, 디버그 기호 폴더 제외)
    /// · Android: Builds/Android/StellaAnchor-{버전}.aab(구글 플레이 업로드용). 출시 키스토어가 없으면 경고
    /// · iOS: Builds/iOS(Xcode 프로젝트) → Mac의 Xcode에서 서명 · 업로드
    /// 설정 변경은 ProjectSettings에 저장됨(커밋 여부는 직접 결정).
    /// </summary>
    public static class ReleaseTools
    {
        public const string Company = "Six-Tail";
        public const string Product = "스텔라 앵커";
        public const string BundleId = "com.sixtail.stellaanchor";
        private const string FileBase = "StellaAnchor";

        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string Builds => Path.Combine(Root, "Builds");

        // ---------------- 설정 ----------------

        [MenuItem("Tools/출시/1. 출시 설정 적용", priority = 1)]
        public static void ApplySettings()
        {
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = Product;
            foreach (var t in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone })
                PlayerSettings.SetApplicationIdentifier(t, BundleId);

            // 가로 화면만(왼쪽 · 오른쪽 회전)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // 안드로이드: IL2CPP · ARM64(구글 플레이 64비트 필수) · 앱 번들(aab) · 최신 대상 API
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.Android.renderOutsideSafeArea = false;   // 노치 · 펀치홀 부분엔 그리지 않음(버튼이 가려지지 않게)
            PlayerSettings.Android.splitApplicationBinary = true;   // 큰 리소스는 설치 시 함께 받는 애셋 팩으로(구글 플레이 기본 모듈 용량 제한 대비)

            // iOS
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);

            // PC: IL2CPP(코드를 기계어로 — 분해 · 수정이 어려움. Visual Studio 'C++를 사용한 데스크톱 개발' 필요) ·
            //     전체 화면(창 테두리 없음) · 창 크기 조절 · 다른 창을 봐도 계속 실행
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion)) PlayerSettings.bundleVersion = "1.0.0";
            if (string.IsNullOrEmpty(PlayerSettings.iOS.buildNumber) || PlayerSettings.iOS.buildNumber == "0")
                PlayerSettings.iOS.buildNumber = PlayerSettings.Android.bundleVersionCode.ToString();

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("출시 설정",
                $"적용했습니다.\n\n회사 {Company} · 이름 {Product}\n번들 ID {BundleId}\n버전 {PlayerSettings.bundleVersion} (빌드 번호 {PlayerSettings.Android.bundleVersionCode})\n\n" +
                "가로 화면 고정 · 안드로이드 IL2CPP/ARM64/aab · iOS 13 이상 · PC 전체 화면(창 크기 조절 가능)", "확인");
        }

        [MenuItem("Tools/출시/2. 버전 올리기 (1.0.0 → 1.0.1)", priority = 2)]
        public static void BumpVersion()
        {
            var parts = (PlayerSettings.bundleVersion ?? "1.0.0").Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToList();
            while (parts.Count < 3) parts.Add(0);
            parts[2]++;
            PlayerSettings.bundleVersion = string.Join(".", parts.Take(3));
            PlayerSettings.Android.bundleVersionCode++;
            PlayerSettings.iOS.buildNumber = PlayerSettings.Android.bundleVersionCode.ToString();
            AssetDatabase.SaveAssets();
            Debug.Log($"[출시] 버전 {PlayerSettings.bundleVersion} · 빌드 번호 {PlayerSettings.Android.bundleVersionCode}");
        }

        // ---------------- 빌드 ----------------

        private static string[] Scenes() => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        private static bool Build(BuildTarget target, BuildTargetGroup group, string path, BuildOptions options = BuildOptions.None)
        {
            var scenes = Scenes();
            if (scenes.Length == 0) { EditorUtility.DisplayDialog("빌드", "Build Settings에 켜진 씬이 없습니다.", "확인"); return false; }
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Builds);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = path, target = target, targetGroup = group, options = options,
            });
            var s = report.summary;
            bool ok = s.result == BuildResult.Succeeded;
            Debug.Log($"[출시] {target} 빌드 {s.result} — {s.totalSize / (1024f * 1024f):0.0} MB, {s.totalTime.TotalMinutes:0.0}분, 오류 {s.totalErrors} → {path}");
            if (!ok) EditorUtility.DisplayDialog("빌드 실패", $"{target} 빌드가 실패했습니다({s.result}). Console의 오류를 확인하세요.", "확인");
            return ok;
        }

        [MenuItem("Tools/출시/3. Windows 빌드 (zip)", priority = 20)]
        public static void BuildWindows()
        {
            string dir = Path.Combine(Builds, "Windows");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            if (!Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, Path.Combine(dir, Product + ".exe"))) return;

            // 배포용 zip(디버그 기호 폴더 제외)
            string zip = Path.Combine(Builds, $"{FileBase}-Windows-{PlayerSettings.bundleVersion}.zip");
            if (File.Exists(zip)) File.Delete(zip);
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    string rel = file.Substring(dir.Length + 1);
                    if (rel.Contains("_DoNotShip")) continue;
                    archive.CreateEntryFromFile(file, Path.Combine(Product, rel).Replace('\\', '/'), System.IO.Compression.CompressionLevel.Optimal);
                }
            }
            EditorUtility.RevealInFinder(zip);
            Debug.Log($"[출시] Windows 배포 파일: {zip} ({new FileInfo(zip).Length / (1024f * 1024f):0.0} MB)");
        }

        [MenuItem("Tools/출시/4. Android 빌드 (aab)", priority = 21)]
        public static void BuildAndroid()
        {
            if (!PlayerSettings.Android.useCustomKeystore &&
                !EditorUtility.DisplayDialog("출시 키스토어 없음",
                    "출시용 키스토어가 설정되지 않아 디버그 키로 서명됩니다. 구글 플레이에는 올릴 수 없습니다(기기 설치 테스트용).\n\n" +
                    "Edit ▸ Project Settings ▸ Player ▸ Android ▸ Publishing Settings ▸ Keystore Manager에서 키스토어를 만든 뒤 다시 빌드하세요.\n" +
                    "(키스토어 파일과 비밀번호는 잃어버리면 앱을 다시는 업데이트할 수 없으니 따로 보관)",
                    "그래도 빌드", "취소")) return;
            EditorUserBuildSettings.buildAppBundle = true;
            string path = Path.Combine(Builds, "Android", $"{FileBase}-{PlayerSettings.bundleVersion}.aab");
            if (Build(BuildTarget.Android, BuildTargetGroup.Android, path)) EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Tools/출시/5. Android 테스트용 apk", priority = 22)]
        public static void BuildAndroidApk()
        {
            bool aab = EditorUserBuildSettings.buildAppBundle;
            EditorUserBuildSettings.buildAppBundle = false;
            try
            {
                string path = Path.Combine(Builds, "Android", $"{FileBase}-{PlayerSettings.bundleVersion}-test.apk");
                if (Build(BuildTarget.Android, BuildTargetGroup.Android, path)) EditorUtility.RevealInFinder(path);
            }
            finally { EditorUserBuildSettings.buildAppBundle = aab; }
        }

        [MenuItem("Tools/출시/6. iOS 빌드 (Xcode 프로젝트)", priority = 23)]
        public static void BuildIOS()
        {
            string path = Path.Combine(Builds, "iOS");
            if (Build(BuildTarget.iOS, BuildTargetGroup.iOS, path))
            {
                EditorUtility.RevealInFinder(path);
                EditorUtility.DisplayDialog("iOS", "Xcode 프로젝트를 만들었습니다.\n이 폴더를 Mac으로 옮겨 Xcode에서 열고, Apple 개발자 계정으로 서명한 뒤 Archive → App Store Connect에 업로드하세요.", "확인");
            }
        }

        // ---------------- 출시 화면 미리보기 ----------------

        private const string PreviewMenu = "Tools/출시/출시 화면 미리보기(테스트 기능 숨김)";

        [MenuItem(PreviewMenu, priority = 40)]
        private static void TogglePreview()
        {
            bool hide = !EditorPrefs.GetBool(SubcultureRPG.UI.DevTools.HideKey, false);
            EditorPrefs.SetBool(SubcultureRPG.UI.DevTools.HideKey, hide);
            Debug.Log(hide ? "[출시] 테스트 기능을 숨깁니다(다음 플레이부터 · 화면을 다시 만들 때 반영)" : "[출시] 테스트 기능을 다시 보입니다");
        }

        [MenuItem(PreviewMenu, true)]
        private static bool TogglePreviewCheck()
        {
            Menu.SetChecked(PreviewMenu, EditorPrefs.GetBool(SubcultureRPG.UI.DevTools.HideKey, false));
            return true;
        }
    }
}
