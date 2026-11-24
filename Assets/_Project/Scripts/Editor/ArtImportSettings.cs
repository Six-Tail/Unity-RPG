#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// Assets/_Project/Resources/Art/ 아래 이미지를 처음 임포트할 때 Sprite로 설정하고 종류별 최대 크기를 맞춥니다.
    /// (프로젝트 기본 모드가 3D라 그냥 두면 Sprite가 아닌 일반 텍스처로 들어와 GameArt가 찾지 못함)
    /// 이미 .meta가 있는 이미지는 건드리지 않으므로, 인스펙터에서 직접 바꾼 설정은 유지됩니다.
    /// </summary>
    public class ArtImportSettings : AssetPostprocessor
    {
        public const string ArtRoot = "Assets/_Project/Resources/Art/";

        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(ArtRoot)) return;

            var ti = (TextureImporter)assetImporter;
            // 처음 임포트하거나, Sprite가 아닌 채로 들어와 있으면 기본 설정 적용(Sprite인 이미지는 그대로 둠)
            if (!assetImporter.importSettingsMissing && IsSprite(ti)) return;
            Apply(ti, path);
        }

        private static bool IsSprite(TextureImporter ti) =>
            ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Single;

        private static void Apply(TextureImporter ti, string path)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = MaxSizeFor(path.Substring(ArtRoot.Length));
        }

        /// <summary>
        /// 스크립트가 다시 로드될 때마다 Sprite가 아닌 이미지를 찾아 고칩니다.
        /// (이 스크립트가 컴파일되기 전에 임포트된 이미지 — 예: 스크립트와 이미지를 같이 받은 직후 — 를 자동으로 바로잡음)
        /// </summary>
        [InitializeOnLoadMethod]
        private static void FixOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                int n = FixNonSprites();
                if (n > 0) Debug.Log($"[ArtImportSettings] Resources/Art 이미지 {n}개를 Sprite로 다시 임포트했습니다.");
            };
        }

        /// <summary>Resources/Art 아래에서 Sprite가 아닌 이미지를 Sprite로 바꿔 다시 임포트. 바꾼 개수를 돌려줍니다.</summary>
        private static int FixNonSprites()
        {
            string folder = ArtRoot.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder)) return 0;
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti) || IsSprite(ti)) continue;
                Apply(ti, path);
                ti.SaveAndReimport();
                n++;
            }
            if (n > 0) GameArt.ClearCache();
            return n;
        }

        private static int MaxSizeFor(string relative)
        {
            if (relative.StartsWith("Backgrounds/") || relative.StartsWith("Banners/")) return 2048;
            if (relative.StartsWith("Characters/")) return 1024;
            if (relative.StartsWith("Enemies/")) return 512;
            if (relative.StartsWith("Title/keyart")) return 2048;       // 타이틀 키 아트(화면 전체)
            if (relative.StartsWith("Title/")) return 512;              // 로고 엠블럼
            if (relative.StartsWith("Gacha/scroll") || relative.StartsWith("Gacha/circle")) return 1024;   // 가챠 연출의 큰 소품
            return 256;   // Items · Thumbs · Equipment · Status · Elements · 작은 소품
        }

        [MenuItem("SubcultureRPG/아트 다시 임포트 (Resources/Art)", false, 20)]
        public static void ReimportAll()
        {
            string folder = ArtRoot.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder))
            {
                EditorUtility.DisplayDialog("아트 다시 임포트", folder + " 폴더가 없습니다.\n이미지를 먼저 넣어 주세요 (docs/art.md).", "확인");
                return;
            }
            int n = FixNonSprites();
            GameArt.ClearCache();
            EditorUtility.DisplayDialog("아트 다시 임포트", $"Sprite로 바꾼 이미지 {n}개.", "확인");
        }
    }
}
#endif
