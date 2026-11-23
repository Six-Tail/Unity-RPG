#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 렌더 파이프라인(URP) 설정. Live2D Cubism SDK(5-r.5 이후)가 URP 전용이라 프로젝트를 URP로 씁니다.
    ///
    ///   Assets/_Project/Settings/GameURP.asset            파이프라인 에셋(그래픽 설정의 기본 파이프라인)
    ///   Assets/_Project/Settings/GameURP_Renderer.asset   렌더러 — Cubism SDK 의 렌더러(Cubism 렌더 패스 포함)를 복사한 것
    ///
    /// 에셋이 없거나 그래픽 설정에 파이프라인이 비어 있으면 에디터가 로드될 때 자동으로 맞춥니다.
    /// 메뉴: SubcultureRPG > Live2D > URP 설정 적용.
    /// </summary>
    public static class RenderPipelineSetup
    {
        public const string Folder = "Assets/_Project/Settings";
        public const string PipelinePath = Folder + "/GameURP.asset";
        public const string RendererPath = Folder + "/GameURP_Renderer.asset";

        /// <summary>Cubism SDK 가 제공하는 URP 렌더러(Cubism 렌더 패스 기능이 들어 있음).</summary>
        public const string CubismRendererPath = "Assets/Live2D/Cubism/Rendering/URP/CubismURPRenderer.asset";

        [InitializeOnLoadMethod]
        private static void AutoApply()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (IsValid()) return;
                Apply(log: true);
            };
        }

        /// <summary>그래픽 설정이 우리 파이프라인 에셋(렌더러 포함)을 가리키는가.</summary>
        public static bool IsValid()
        {
            var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            return urp != null && HasRenderer(urp);
        }

        [MenuItem("SubcultureRPG/Live2D/URP 설정 적용", false, 40)]
        public static void ApplyMenu()
        {
            bool ok = Apply(log: true);
            EditorUtility.DisplayDialog("URP 설정",
                ok ? "URP 파이프라인을 적용했습니다.\n" + PipelinePath : "적용하지 못했습니다. 콘솔을 확인하세요.", "확인");
        }

        /// <summary>배치 모드(-executeMethod)용.</summary>
        public static void ApplyBatch()
        {
            bool ok = Apply(log: true);
            Debug.Log("[RenderPipelineSetup] batch result: " + (ok ? "OK" : "FAILED"));
            if (!ok) EditorApplication.Exit(1);
        }

        public static bool Apply(bool log)
        {
            EnsureFolder();

            // 1) 렌더러: 없으면 Cubism SDK 의 렌더러를 복사(없으면 기본 렌더러)
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            if (renderer == null)
            {
                if (AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(CubismRendererPath) != null)
                {
                    if (!AssetDatabase.CopyAsset(CubismRendererPath, RendererPath))
                        Debug.LogError("[RenderPipelineSetup] Cubism 렌더러를 복사하지 못했습니다: " + CubismRendererPath);
                    AssetDatabase.ImportAsset(RendererPath);
                    renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
                    if (renderer != null) renderer.name = "GameURP_Renderer";
                }
                if (renderer == null)
                {
                    // SDK 가 없는 환경: Cubism 패스가 없는 기본 렌더러(Live2D 는 안 나오지만 게임은 동작)
                    var data = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(data, RendererPath);
                    renderer = data;
                    if (log) Debug.LogWarning("[RenderPipelineSetup] Cubism SDK 렌더러가 없어 기본 렌더러를 만들었습니다. SDK 임포트 후 " +
                                              RendererPath + " 를 지우고 다시 적용하세요.");
                }
                EditorUtility.SetDirty(renderer);
            }

            // 2) 파이프라인 에셋
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(renderer);
                urp.name = "GameURP";
                AssetDatabase.CreateAsset(urp, PipelinePath);
            }
            else if (!HasRenderer(urp))
            {
                // 렌더러 참조가 끊긴 경우(예: 에셋 삭제) 다시 연결
                var so = new SerializedObject(urp);
                var list = so.FindProperty("m_RendererDataList");
                if (list != null)
                {
                    if (list.arraySize == 0) list.arraySize = 1;
                    list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            EditorUtility.SetDirty(urp);

            // 3) 그래픽 설정(품질 단계별 설정은 비워 두어 기본값을 따름)
            if (GraphicsSettings.defaultRenderPipeline != urp) GraphicsSettings.defaultRenderPipeline = urp;

            AssetDatabase.SaveAssets();
            bool ok = IsValid();
            if (log) Debug.Log("[RenderPipelineSetup] URP " + (ok ? "적용 완료: " : "적용 실패: ") + PipelinePath);
            return ok;
        }

        private static bool HasRenderer(UniversalRenderPipelineAsset urp)
        {
            var list = urp.rendererDataList;
            return list.Length > 0 && list[0] != null;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project", "Settings");
        }
    }
}
#endif
