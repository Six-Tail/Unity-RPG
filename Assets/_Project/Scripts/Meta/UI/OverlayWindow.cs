using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 어느 화면 위에나 띄우는 창(우편함 · 출석부 · 별 보상 · 캐릭터 선택권 · 알림): 자기 캔버스 + 어두운 바탕 + 가운데 창.
    /// 닫으면 통째로 사라집니다. 여러 개를 겹쳐 열 수 있고(나중에 연 것이 위), 기본 순서는 200.
    /// </summary>
    public class OverlayWindow
    {
        private static int _openCount;
        /// <summary>열려 있는 창이 있는지(자동 팝업 · 튜토리얼 안내가 겹치지 않게).</summary>
        public static bool AnyOpen => _openCount > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _openCount = 0;

        public GameObject Root { get; private set; }
        public RectTransform Panel { get; private set; }
        public TMP_FontAsset Font { get; private set; }
        public bool IsOpen => Root != null;
        public event System.Action OnClosed;

        /// <summary>게임 폰트(홈 화면 → 전투 화면 순으로 찾음).</summary>
        public static TMP_FontAsset DefaultFont()
        {
            var home = Object.FindAnyObjectByType<HomeScreen>();
            if (home != null && home.Font != null) return home.Font;
            var hud = Object.FindAnyObjectByType<SubcultureRPG.Battle.UI.BattleHUD>();
            return hud != null ? hud.Font : null;
        }

        public static OverlayWindow Open(string title, float w, float h, TMP_FontAsset font = null, bool dimCloses = true, int order = 200, bool closeButton = true)
        {
            var win = new OverlayWindow { Font = font != null ? font : DefaultFont() };
            var go = new GameObject("OverlayWindow");
            win.Root = go;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order + _openCount;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                SubcultureRPG.UI.UIInput.AddModule(es);
            }
            _openCount++;

            var dim = UIFactory.AddImage("Dim", go.transform, new Color(0, 0, 0, 0.74f), null, raycast: true);
            UIFactory.Stretch(dim.rectTransform);
            if (dimCloses)
            {
                var db = dim.gameObject.AddComponent<Button>();
                db.targetGraphic = dim; db.transition = Selectable.Transition.None;
                db.onClick.AddListener(win.Close);
            }

            // 금색 테두리(창 뒤에 2px 크게) + 창 — 바탕과 형제라 창을 눌러도 닫히지 않음
            var rim = UIFactory.AddImage("Rim", go.transform, new Color(1f, 0.85f, 0.45f, 0.45f), ProceduralSprites.Rounded);
            rim.type = Image.Type.Sliced; rim.preserveAspect = false;
            UIFactory.Anchor(rim.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rim.rectTransform.sizeDelta = new Vector2(w + 4, h + 4);
            var panel = UIFactory.AddImage("Panel", go.transform, new Color(0.07f, 0.08f, 0.12f, 1f), ProceduralSprites.Rounded, raycast: true);
            panel.type = Image.Type.Sliced; panel.preserveAspect = false;
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panel.rectTransform.sizeDelta = new Vector2(w, h);
            win.Panel = panel.rectTransform;

            if (!string.IsNullOrEmpty(title))
            {
                var t = win.Text(win.Panel, $"<b>{title}</b>", 34, new Color(1f, 0.9f, 0.62f), TextAlignmentOptions.Left, 34, -20, w - 240, 52);
                t.enableAutoSizing = true; t.fontSizeMin = 20; t.fontSizeMax = 34;
            }
            if (closeButton)
            {
                var close = UIFactory.AddButton("Close", win.Panel, "닫기", 20, win.Font, new Color(1, 1, 1, 0.14f), Color.white);
                var crt = (RectTransform)close.transform;
                UIFactory.Anchor(crt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
                crt.sizeDelta = new Vector2(120, 46); crt.anchoredPosition = new Vector2(-22, -22);
                close.onClick.AddListener(win.Close);
            }
            return win;
        }

        public void Close()
        {
            if (Root == null) return;
            Object.Destroy(Root);
            Root = null;
            _openCount = Mathf.Max(0, _openCount - 1);
            OnClosed?.Invoke();
        }

        // ---------------- 도우미(왼쪽 위 기준 좌표) ----------------

        public static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            UIFactory.Anchor(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, y);
        }

        public TextMeshProUGUI Text(Transform parent, string s, float size, Color c, TextAlignmentOptions align, float x, float y, float w, float h)
        {
            var t = UIFactory.AddText("T", parent, s, size, Font, c, align);
            t.richText = true;
            Place(t.rectTransform, x, y, w, h);
            return t;
        }

        public Image Box(Transform parent, Color c, float x, float y, float w, float h, bool raycast = false)
        {
            var img = UIFactory.AddImage("Box", parent, c, ProceduralSprites.Rounded, raycast);
            img.type = Image.Type.Sliced; img.preserveAspect = false;
            Place(img.rectTransform, x, y, w, h);
            return img;
        }

        public Button Btn(Transform parent, string label, float size, Color c, float x, float y, float w, float h, System.Action onClick, bool enabled = true)
        {
            var b = UIFactory.AddButton("Btn", parent, label, size, Font, enabled ? c : new Color(1, 1, 1, 0.08f), enabled ? Color.white : new Color(0.6f, 0.62f, 0.68f));
            Place((RectTransform)b.transform, x, y, w, h);
            var lbl = b.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) lbl.richText = true;
            b.interactable = enabled;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            return b;
        }

        public Image Icon(Transform parent, Sprite sp, float x, float y, float size)
        {
            var img = UIFactory.AddImage("Icon", parent, Color.white, sp);
            img.preserveAspect = true;
            img.enabled = sp != null;
            Place(img.rectTransform, x, y, size, size);
            return img;
        }

        /// <summary>세로로 쌓이는 스크롤 목록(창 안 여백 = 왼 · 아래 · 오른 · 위). 반환 = 내용 칸.</summary>
        public RectTransform ScrollList(float l, float b, float r, float t, float spacing = 8)
        {
            var view = UIFactory.NewRect("View", Panel);
            UIFactory.Stretch(view, l, b, r, t);
            var content = UIFactory.NewRect("Content", view);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = spacing; vl.padding = new RectOffset(4, 4, 4, 4);
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandHeight = false;
            UIFactory.MakeVerticalScroll(view, content);
            return content;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var ch = t.GetChild(i);
                ch.SetParent(null, false);
                Object.Destroy(ch.gameObject);
            }
        }

        public static readonly Color Blue = new(0.25f, 0.55f, 0.9f, 0.96f), Green = new(0.3f, 0.62f, 0.42f, 0.96f),
            Gold = new(0.8f, 0.58f, 0.22f, 0.96f), Dim = new(1, 1, 1, 0.14f), Red = new(0.7f, 0.32f, 0.32f, 0.96f);
    }

    /// <summary>
    /// 알림 팝업(안내 · 보상 획득 · 기능 개방 · 튜토리얼 설명). 여러 개를 부르면 차례로 하나씩 보여 줍니다.
    /// 바탕을 눌러도 닫히지 않고 버튼으로만 닫힙니다. 순서 450(다른 창 위).
    /// </summary>
    public static class Popups
    {
        private struct Item
        {
            public string title, body, ok, alt, icon;
            public System.Action onOk, onAlt;
        }

        private static readonly Queue<Item> Queue = new();
        private static OverlayWindow _current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Queue.Clear(); _current = null; }

        public static bool IsOpen => (_current != null && _current.IsOpen) || Queue.Count > 0;

        /// <summary>알림 띄우기. alt = 두 번째 버튼(예: '바로 가기' · '나중에'). icon = Resources/Art 아래 그림 경로.</summary>
        public static void Show(string title, string body, string ok = "확인", System.Action onOk = null,
            string alt = null, System.Action onAlt = null, string icon = null)
        {
            Queue.Enqueue(new Item { title = title, body = body, ok = ok, onOk = onOk, alt = alt, onAlt = onAlt, icon = icon });
            if (_current == null || !_current.IsOpen) Next();
        }

        /// <summary>보상 획득 알림.</summary>
        public static void Reward(string title, RewardBundle r, System.Action onOk = null)
        {
            SoundManager.Play(Sfx.Reward);
            if (r == null) return;
            string icon = null;
            var parts = r.Parts();
            if (parts.Count > 0) icon = parts[0].icon;
            Show(title, r.Describe("\n"), "확인", onOk, icon: icon);
        }

        private static void Next()
        {
            if (Queue.Count == 0) { _current = null; return; }
            var it = Queue.Dequeue();
            var sp = !string.IsNullOrEmpty(it.icon) ? GameArt.Load(it.icon) : null;
            float h = sp != null ? 560 : 460;
            var w = OverlayWindow.Open(it.title, 860, h, null, dimCloses: false, order: 450, closeButton: false);
            _current = w;
            float top = -84;
            if (sp != null) { w.Icon(w.Panel, sp, 430 - 60, -80, 120); top = -210; }
            var body = w.Text(w.Panel, it.body, 25, new Color(0.9f, 0.92f, 0.96f), TextAlignmentOptions.Center, 44, top, 772, h + top - 116);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = 10f;
            body.enableAutoSizing = true; body.fontSizeMin = 17; body.fontSizeMax = 25;

            void Done(System.Action act)
            {
                w.Close();
                act?.Invoke();
                if (_current == w) Next();   // 버튼 동작이 새 알림을 띄웠으면 그것이 이미 다음 차례
            }
            bool two = !string.IsNullOrEmpty(it.alt);
            var ok = w.Btn(w.Panel, it.ok ?? "확인", 24, OverlayWindow.Blue, two ? 430 - 270 : 430 - 130, 0, 260, 62, () => Done(it.onOk));
            UIFactory.Anchor((RectTransform)ok.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            ((RectTransform)ok.transform).anchoredPosition = new Vector2(two ? 430 - 270 : 430 - 130, 30);
            if (two)
            {
                var alt = w.Btn(w.Panel, it.alt, 24, OverlayWindow.Dim, 440, 0, 260, 62, () => Done(it.onAlt));
                UIFactory.Anchor((RectTransform)alt.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
                ((RectTransform)alt.transform).anchoredPosition = new Vector2(440, 30);
            }
        }
    }

    /// <summary>
    /// 빨간 느낌표 알림: 받을 보상이 있는 버튼 · 타일의 오른쪽 위에 붙입니다. 보상을 받으면 Set(false)로 사라짐.
    /// </summary>
    public class NotifyBadge : MonoBehaviour
    {
        private RectTransform _rt;

        public static NotifyBadge Attach(Component target, TMP_FontAsset font, float x = 6, float y = 6, float size = 32)
        {
            var bg = UIFactory.AddImage("Notify", target.transform, Color.white, ProceduralSprites.Circle);   // 흰 테두리
            var rt = bg.rectTransform;
            UIFactory.Anchor(rt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = new Vector2(size, size); rt.anchoredPosition = new Vector2(x - size * 0.5f, y - size * 0.5f);
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;   // 줄 세우는 부모 안에서도 제자리
            var red = UIFactory.AddImage("Red", rt, new Color(0.93f, 0.2f, 0.2f, 1f), ProceduralSprites.Circle);
            UIFactory.Stretch(red.rectTransform, 3, 3, 3, 3);
            var t = UIFactory.AddText("T", rt, "!", size * 0.72f, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(t.rectTransform);
            var b = bg.gameObject.AddComponent<NotifyBadge>();
            b._rt = rt;
            bg.gameObject.SetActive(false);
            return b;
        }

        public void Set(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        private void Update()
        {
            float s = 1f + 0.1f * Mathf.Sin(Time.unscaledTime * 5f);
            _rt.localScale = new Vector3(s, s, 1f);
        }
    }
}
