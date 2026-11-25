using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace SubcultureRPG.UI
{
    /// <summary>
    /// 연출용 폰트(한글 UI 폰트와 별도):
    /// · Number  = Russo One — 피해 숫자 · 총 피해량 · 타이머 등 숫자
    /// · Display = Cinzel Black — FINISH! · BREAK! · TIME UP · TAP TO START 같은 영어 연출 글자
    /// 폰트 파일은 Resources/Fonts/ (OFL, 라이선스 파일 같은 폴더). TMP 에셋은 실행 중 동적으로 만들고,
    /// 한글이 섞여 있으면(예: '치명' + 숫자) 넘겨받은 게임 폰트로 대신 그림(폴백).
    /// 파일이 없으면 게임 폰트를 그대로 돌려줌.
    /// </summary>
    public static class GameFonts
    {
        private static TMP_FontAsset _number, _display;
        private static bool _triedNumber, _triedDisplay;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _number = null; _display = null; _triedNumber = _triedDisplay = false; }

        public static TMP_FontAsset Number(TMP_FontAsset fallback) => Get(ref _number, ref _triedNumber, "Fonts/RussoOne-Regular", fallback);
        public static TMP_FontAsset Display(TMP_FontAsset fallback) => Get(ref _display, ref _triedDisplay, "Fonts/Cinzel-Black", fallback);

        /// <summary>
        /// 떠오르는 글자에 맞는 폰트: 숫자가 있으면 Number, 숫자 없이 영어만 있으면 Display(BREAK! 등), 한글뿐이면 게임 폰트.
        /// 리치 텍스트 태그(&lt;size=…&gt; 등)는 무시하고 판단.
        /// </summary>
        public static TMP_FontAsset For(string text, TMP_FontAsset fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            bool digit = false, latin = false, inTag = false;
            foreach (char c in text)
            {
                if (c == '<') { inTag = true; continue; }
                if (c == '>') { inTag = false; continue; }
                if (inTag) continue;
                if (c >= '0' && c <= '9') digit = true;
                else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) latin = true;
            }
            if (digit) return Number(fallback);
            if (latin) return Display(fallback);
            return fallback;
        }

        private static TMP_FontAsset Get(ref TMP_FontAsset cache, ref bool tried, string path, TMP_FontAsset fallback)
        {
            if (cache == null && !tried)
            {
                tried = true;
                var font = Resources.Load<Font>(path);
                if (font != null)
                {
                    try
                    {
                        cache = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                        if (cache != null) cache.name = font.name + " (런타임)";
                    }
                    catch (System.Exception e) { Debug.LogWarning($"[GameFonts] {path} 폰트 에셋을 만들지 못했습니다: {e.Message}"); }
                }
                else Debug.LogWarning($"[GameFonts] Resources/{path} 폰트 파일이 없습니다 — 게임 폰트로 대신합니다.");
            }
            if (cache == null) return fallback;
            if (fallback != null && fallback != cache)
            {
                cache.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
                if (!cache.fallbackFontAssetTable.Contains(fallback)) cache.fallbackFontAssetTable.Add(fallback);
            }
            return cache;
        }
    }
}
