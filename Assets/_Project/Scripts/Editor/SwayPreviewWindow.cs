#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SubcultureRPG.UI;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 움직이는 일러스트(셰이더 방식) 미리보기: 메뉴 SubcultureRPG > 움직이는 일러스트(셰이더) 미리보기.
    /// 플레이하지 않고 에디터에서 바로 움직임을 보고, 흔들림 · 호흡 · 반짝임 세기를 슬라이더로 바꿔 볼 수 있습니다.
    /// (슬라이더 값은 미리보기에만 적용 — 정해지면 셰이더 기본값을 고치세요)
    /// </summary>
    public class SwayPreviewWindow : EditorWindow
    {
        private readonly List<string> _names = new();
        private int _index;
        private Material _mat;
        private Texture2D _main;
        private bool _on = true;
        private float _sway = 0.012f, _breath = 0.006f, _glow = 0.22f, _speed = 1.6f;

        [MenuItem("SubcultureRPG/움직이는 일러스트(셰이더) 미리보기", false, 30)]
        private static void Open() => GetWindow<SwayPreviewWindow>("움직이는 일러스트").Show();

        private void OnEnable()
        {
            _names.Clear();
            const string dir = "Assets/_Project/Resources/Art/Sway";
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "*_mask.png"))
                    _names.Add(Path.GetFileNameWithoutExtension(f).Replace("_mask", ""));
            Load();
        }

        private void Load()
        {
            if (_names.Count == 0) return;
            string n = _names[Mathf.Clamp(_index, 0, _names.Count - 1)];
            _main = Resources.Load<Texture2D>("Art/Characters/" + n);
            if (_mat == null)
            {
                var shader = Resources.Load<Shader>("Shaders/UI-Sway");
                if (shader != null) _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            if (_mat == null) return;
            _mat.SetTexture("_SwayMask", Resources.Load<Texture2D>(SwayIllustration.Folder + n + "_mask"));
            var blink = Resources.Load<Texture2D>(SwayIllustration.Folder + n + "_blink");
            _mat.SetTexture("_BlinkTex", blink != null ? blink : Texture2D.blackTexture);
        }

        private void OnDisable() { if (_mat != null) DestroyImmediate(_mat); }

        private void Update() => Repaint();

        private void OnGUI()
        {
            if (_names.Count == 0)
            {
                EditorGUILayout.HelpBox("Resources/Art/Sway 에 재료가 없습니다.\nnode tools/art/generate-sway.js --studio <스튜디오 폴더> --name <일러스트 이름> --yes", MessageType.Info);
                return;
            }
            EditorGUI.BeginChangeCheck();
            _index = EditorGUILayout.Popup("일러스트", _index, _names.ToArray());
            if (EditorGUI.EndChangeCheck()) Load();
            _on = EditorGUILayout.Toggle("움직임 켜기", _on);
            _sway = EditorGUILayout.Slider("흔들림", _sway, 0f, 0.04f);
            _speed = EditorGUILayout.Slider("흔들림 속도", _speed, 0.2f, 4f);
            _breath = EditorGUILayout.Slider("호흡", _breath, 0f, 0.02f);
            _glow = EditorGUILayout.Slider("반짝임", _glow, 0f, 0.8f);
            if (_main == null || _mat == null) { EditorGUILayout.HelpBox("일러스트 또는 셰이더를 찾지 못했습니다.", MessageType.Warning); return; }

            float t = (float)EditorApplication.timeSinceStartup;
            _mat.SetFloat("_T", _on ? t : 0f);
            _mat.SetFloat("_Blink", _on ? SwayIllustration.BlinkAt(t, 4f, 0.16f) : 0f);
            _mat.SetFloat("_SwayAmp", _sway);
            _mat.SetFloat("_SwaySpeed", _speed);
            _mat.SetFloat("_BreathAmp", _breath);
            _mat.SetFloat("_GlowAmp", _glow);

            var area = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            float aspect = (float)_main.width / _main.height;
            float hgt = area.height, wid = hgt * aspect;
            if (wid > area.width) { wid = area.width; hgt = wid / aspect; }
            var r = new Rect(area.x + (area.width - wid) / 2f, area.y + (area.height - hgt) / 2f, wid, hgt);
            EditorGUI.DrawRect(area, new Color(0.08f, 0.09f, 0.12f));
            EditorGUI.DrawPreviewTexture(r, _main, _mat);
        }
    }
}
#endif
