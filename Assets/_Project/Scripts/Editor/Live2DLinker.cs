#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// Live2D 모델 폴더를 캐릭터에 연결합니다.
    ///
    ///   Assets/_Project/Art/Characters/Live2D/{캐릭터 id}/   ← Cubism Editor 에서 내보낸 모델 폴더를 여기에
    ///       {이름}.model3.json · .moc3 · 텍스처 · physics3.json · motions/*.motion3.json
    ///
    /// Cubism SDK 가 임포트돼 있으면 .model3.json 에서 프리팹을, .motion3.json 에서 AnimationClip 을 자동으로 만듭니다.
    /// 이 도구는 그 프리팹과 대기 모션을 CharacterData(live2DModelPrefab · live2DIdleMotion)에 넣어 줍니다.
    /// 프리팹·모션이 임포트될 때 자동으로 실행되고, 메뉴로도 실행할 수 있습니다.
    /// </summary>
    public class Live2DLinker : AssetPostprocessor
    {
        public const string Root = "Assets/_Project/Art/Characters/Live2D";

        private static bool _queued;

        /// <summary>Cubism SDK for Unity 가 프로젝트에 있는가.</summary>
        public static bool SdkInstalled =>
            Type.GetType("Live2D.Cubism.Core.CubismModel, Live2D.Cubism") != null;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var p in imported)
            {
                if (!p.StartsWith(Root + "/", StringComparison.Ordinal)) continue;
                if (!p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) && !p.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) continue;
                if (_queued) return;
                _queued = true;
                EditorApplication.delayCall += () => { _queued = false; LinkAll(report: false); };
                return;
            }
        }

        [MenuItem("SubcultureRPG/Live2D/모델 연결 (폴더 → 캐릭터)", false, 30)]
        public static void LinkMenu() => LinkAll(report: true);

        [MenuItem("SubcultureRPG/Live2D/모델 폴더 열기", false, 31)]
        public static void RevealFolder()
        {
            EnsureFolder();
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Root);
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Root)) return;
            string parent = "Assets";
            foreach (var part in Root.Substring("Assets/".Length).Split('/'))
            {
                string next = parent + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, part);
                parent = next;
            }
        }

        /// <summary>모든 CharacterData 를 훑어 {Root}/{id}/ 폴더의 프리팹·대기 모션을 연결.</summary>
        public static void LinkAll(bool report)
        {
            var linked = new List<string>();
            var noPrefab = new List<string>();
            var noFolder = new List<string>();
            var seen = new HashSet<string>();
            int changed = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:CharacterData"))
            {
                var c = AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(guid));
                if (c == null || string.IsNullOrEmpty(c.id)) continue;
                bool first = seen.Add(c.id);   // 같은 id 의 에셋이 여럿이어도 보고는 한 번만

                string folder = Root + "/" + c.id;
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    if (first && c.rarity.UsesLive2D()) noFolder.Add($"{c.displayName} ({c.id})");
                    continue;
                }

                var prefab = FindPrefab(folder, c.id);
                var idle = FindIdle(folder);
                if (prefab == null)
                {
                    if (first) noPrefab.Add($"{c.displayName} ({c.id})");
                    continue;
                }

                if (c.live2DModelPrefab != prefab || c.live2DIdleMotion != idle)
                {
                    c.live2DModelPrefab = prefab;
                    c.live2DIdleMotion = idle;
                    EditorUtility.SetDirty(c);
                    changed++;
                }
                if (first) linked.Add($"{c.displayName} ({c.id}) — {prefab.name}" + (idle != null ? $" · 대기 모션 {idle.name}" : " · 대기 모션 없음"));
            }

            if (changed > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[Live2DLinker] 캐릭터 에셋 {changed}개에 Live2D 모델을 연결했습니다.");
            }
            if (!report) return;

            var sb = new StringBuilder();
            sb.AppendLine(SdkInstalled
                ? "Cubism SDK: 설치됨"
                : "Cubism SDK: 없음 — SDK 를 임포트해야 모델 프리팹이 만들어집니다 (docs/live2d.md).");
            sb.AppendLine();
            sb.AppendLine($"연결됨 {linked.Count}명");
            foreach (var s in linked) sb.AppendLine("  · " + s);
            if (noPrefab.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"폴더는 있는데 프리팹이 없음 {noPrefab.Count}명" + (SdkInstalled ? " — .model3.json 이 있는지 확인하세요." : " — SDK 임포트 후 폴더를 다시 임포트하세요."));
                foreach (var s in noPrefab) sb.AppendLine("  · " + s);
            }
            if (noFolder.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"모델 폴더 없음(6성) {noFolder.Count}명 — 정적 일러스트로 표시됩니다.");
                foreach (var s in noFolder) sb.AppendLine("  · " + s);
            }
            sb.AppendLine();
            sb.AppendLine("폴더 위치: " + Root + "/{캐릭터 id}/");
            EditorUtility.DisplayDialog("Live2D 모델 연결", sb.ToString(), "확인");
        }

        /// <summary>폴더 안의 모델 프리팹: 폴더 바로 아래 것 우선, 이름이 id 와 같으면 최우선.</summary>
        private static GameObject FindPrefab(string folder, string id)
        {
            GameObject best = null;
            int bestScore = int.MinValue;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                int depth = path.Substring(folder.Length).Split('/').Length;
                int score = -depth * 10 + (string.Equals(go.name, id, StringComparison.OrdinalIgnoreCase) ? 100 : 0);
                if (score > bestScore) { bestScore = score; best = go; }
            }
            return best;
        }

        /// <summary>대기 모션: 이름에 idle 이 들어간 클립 우선, 없으면 이름순 첫 클립.</summary>
        private static AnimationClip FindIdle(string folder)
        {
            var clips = new List<AnimationClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null) clips.Add(clip);
            }
            if (clips.Count == 0) return null;
            clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (var clip in clips)
                if (clip.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0) return clip;
            return clips[0];
        }
    }
}
#endif
