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
            if (!assetImporter.importSettingsMissing && IsSprite(ti) && BorderOk(ti, path)) return;
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
            if (IsButton(path))
            {
                ti.spriteBorder = ButtonBorder;                                   // 9-slice
                ti.textureCompression = TextureImporterCompression.Uncompressed;  // 얇은 테두리가 뭉개지지 않게
            }
        }

        // 버튼 그림(UI/button_*): 9-slice 테두리 = 왼 · 아래 · 오른 · 위 (tools/art/generate-buttons.js 의 규격과 같아야 함)
        private static readonly Vector4 ButtonBorder = new Vector4(96, 44, 96, 44);
        private static bool IsButton(string path) => path.StartsWith(ArtRoot + "UI/button_");
        private static bool BorderOk(TextureImporter ti, string path) => !IsButton(path) || ti.spriteBorder == ButtonBorder;

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

        /// <summary>Resources/Art 아래에서 Sprite가 아니거나 규칙보다 작게 임포트된 이미지를 바로잡아 다시 임포트. 바꾼 개수를 돌려줍니다.</summary>
        private static int FixNonSprites()
        {
            string folder = ArtRoot.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder)) return 0;
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                // Sprite 이고 크기도 규칙 이상이면 그대로 둠. 규칙보다 작게 들어온 것(크기 규칙이 컴파일되기 전에 임포트된 새 폴더 등)은 다시 맞춤
                if (IsSprite(ti) && BorderOk(ti, path) && ti.maxTextureSize >= MaxSizeFor(path.Substring(ArtRoot.Length))) continue;
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
            if (relative.StartsWith("Characters/") || relative.StartsWith("Cutouts/") || relative.StartsWith("Figures/")) return 1024;
            if (relative.StartsWith("Enemies/")) return 512;
            if (relative.StartsWith("Fx/")) return 512;                 // 스킬 이펙트 텍스처(크게 늘려 써서 256이면 뭉개짐)
            if (relative.StartsWith("Title/keyart")) return 2048;       // 타이틀 키 아트(화면 전체)
            if (relative.StartsWith("Events/") && relative.Contains("_bg")) return 2048;     // 이벤트 배경
            if (relative.StartsWith("Events/") && relative.Contains("_host")) return 1024;   // 이벤트 진행 캐릭터 일러스트
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
