#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 게임 폰트(TMP) 관리. 메뉴 한 번으로 폰트 에셋 생성·설정, 폴백 연결,
    /// TMP 기본 폰트 지정, 프로젝트의 모든 씬에 적용까지 처리합니다.
    /// 메뉴: SubcultureRPG > 폰트 > 게임 폰트 적용 (EF Firenze).
    ///
    /// 한글 깨짐(□) 원인: 동적 아틀라스가 가득 차면 새 글자를 넣을 수 없음.
    /// → Multi Atlas Textures를 켜서 가득 차면 텍스처를 추가하도록 하고,
    ///   샘플 크기를 줄여 아틀라스 한 장에 더 많은 글자가 들어가게 합니다.
    /// </summary>
    public static class GameFont
    {
        public const string FontDir = "Assets/_Project/Fonts";
        public const string SourceFontPath = FontDir + "/EF_Firenze.ttf";
        public const string FontAssetPath = FontDir + "/EF_Firenze SDF.asset";
        private const string FallbackSearch = "NanumGothic t:TMP_FontAsset";

        // 한글 동적 폰트 권장값 (기본 생성값 90/9/1024는 아틀라스 한 장에 약 80자뿐)
        private const int SamplingPointSize = 56;
        private const int Padding = 6;
        private const int AtlasSize = 1024;

        /// <summary>생성기들이 쓰는 게임 폰트. 없으면 나눔고딕 → 아무 TMP 폰트.</summary>
        public static TMP_FontAsset Find()
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (f != null) return f;
            foreach (var g in AssetDatabase.FindAssets(FallbackSearch)) return Load(g);
            foreach (var g in AssetDatabase.FindAssets("t:TMP_FontAsset")) return Load(g);
            return null;
        }

        [MenuItem("SubcultureRPG/폰트/게임 폰트 적용 (EF Firenze)", false, 20)]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("게임 폰트 적용", "플레이 모드를 끄고 실행하세요.", "확인");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                EditorUtility.DisplayDialog("게임 폰트 적용", "폰트 파일을 찾을 수 없습니다:\n" + SourceFontPath, "확인");
                return;
            }

            // 1) 폰트 에셋 생성(없을 때만) + 설정
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            bool created = false;
            if (font == null) { font = Create(source); created = true; }
            Configure(font, clear: !created);

            // 2) 나눔고딕: 폴백으로 연결 + 꽉 찬 아틀라스 정리(Multi Atlas 켜고 동적 데이터 초기화)
            TMP_FontAsset fallback = null;
            foreach (var g in AssetDatabase.FindAssets(FallbackSearch))
            {
                var fb = Load(g);
                if (fb == null || fb == font) continue;
                Configure(fb, clear: true);
                if (fallback == null) fallback = fb;
            }
            font.fallbackFontAssetTable = fallback != null
                ? new List<TMP_FontAsset> { fallback }
                : new List<TMP_FontAsset>();
            EditorUtility.SetDirty(font);

            // 3) TMP 기본 폰트 지정(폰트 미지정 텍스트도 새 폰트 사용)
            bool defaultSet = SetTmpDefault(font);

            AssetDatabase.SaveAssets();

            // 4) 모든 씬의 우리 컴포넌트 font 필드에 적용
            int changed = ApplyToAllScenes(font, out int sceneCount);

            EditorUtility.DisplayDialog(
                "게임 폰트 적용 완료",
                $"폰트: {FontAssetPath}{(created ? " (새로 생성)" : "")}" +
                $"\n설정: Dynamic · Multi Atlas · {SamplingPointSize}pt · {AtlasSize}px" +
                $"\n폴백: {(fallback != null ? fallback.name : "없음")}" +
                $"\nTMP 기본 폰트: {(defaultSet ? "지정됨" : "지정 실패(TMP Settings 없음)")}" +
                $"\n씬 {sceneCount}개 확인, 컴포넌트 {changed}개 갱신",
                "확인");
        }

        // ---------------- 내부 ----------------

        private static TMP_FontAsset Create(Font source)
        {
            var fa = TMP_FontAsset.CreateFontAsset(source, SamplingPointSize, Padding,
                GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            fa.name = "EF_Firenze SDF";
            AssetDatabase.CreateAsset(fa, FontAssetPath);

            // 아틀라스 텍스처·머티리얼을 서브에셋으로 저장(유니티의 Create > Font Asset과 동일)
            if (fa.atlasTextures != null && fa.atlasTextures.Length > 0 && fa.atlasTextures[0] != null)
            {
                fa.atlasTextures[0].name = fa.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            }
            if (fa.material != null)
            {
                fa.material.name = fa.name + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }
            EditorUtility.SetDirty(fa);
            return fa;
        }

        /// <summary>동적 + Multi Atlas로 설정. clear면 동적 데이터(가득 찬 아틀라스) 초기화.</summary>
        private static void Configure(TMP_FontAsset fa, bool clear)
        {
            fa.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fa.isMultiAtlasTexturesEnabled = true;

            if (clear)
            {
                // TMP 버전 차이에 안전하도록 리플렉션으로 호출(인스펙터의 'Clear Dynamic Data'와 같은 동작)
                var m = typeof(TMP_FontAsset).GetMethod("ClearFontAssetData",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(bool) }, null);
                if (m != null) m.Invoke(fa, new object[] { true });
                else Debug.LogWarning("[GameFont] ClearFontAssetData를 찾지 못했습니다. 인스펙터에서 'Clear Dynamic Data'를 눌러주세요: " + fa.name);
            }
            EditorUtility.SetDirty(fa);
        }

        private static bool SetTmpDefault(TMP_FontAsset font)
        {
            foreach (var g in AssetDatabase.FindAssets("t:TMP_Settings"))
            {
                var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(AssetDatabase.GUIDToAssetPath(g));
                if (settings == null) continue;
                var so = new SerializedObject(settings);
                var prop = so.FindProperty("m_defaultFontAsset");
                if (prop == null) return false;
                prop.objectReferenceValue = font;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                return true;
            }
            return false;
        }

        /// <summary>Assets/_Project 아래 모든 씬을 열어, 우리 컴포넌트의 TMP_FontAsset 필드를 교체.</summary>
        private static int ApplyToAllScenes(TMP_FontAsset font, out int sceneCount)
        {
            string startPath = EditorSceneManager.GetActiveScene().path;
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project" });
            sceneCount = guids.Length;
            int changed = 0;

            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int before = changed;

                foreach (var root in scene.GetRootGameObjects())
                    foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (mb == null) continue;
                        var type = mb.GetType();
                        if (type.Namespace == null || !type.Namespace.StartsWith("SubcultureRPG")) continue;

                        foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            if (f.FieldType != typeof(TMP_FontAsset)) continue;
                            if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;
                            if ((Object)f.GetValue(mb) == font) continue;
                            f.SetValue(mb, font);
                            EditorUtility.SetDirty(mb);
                            changed++;
                        }
                    }

                if (changed > before)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }

            if (!string.IsNullOrEmpty(startPath))
                EditorSceneManager.OpenScene(startPath, OpenSceneMode.Single);
            return changed;
        }

        private static TMP_FontAsset Load(string guid) =>
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
    }
}
#endif
