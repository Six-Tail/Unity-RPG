using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Gacha;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 홈(메인 메뉴) 화면. 재화·재료 현황, 픽업 캐릭터, 가챠/육성/편성 버튼.
    /// 코드로 자동 구성 — navigator 등은 비우면 자동 탐색.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Home Screen")]
    public class HomeScreen : MonoBehaviour
    {
        [SerializeField] private ScreenNavigator navigator;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerRoster roster;
        [SerializeField] private GachaController gacha;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private string gameTitle = "서브컬쳐 RPG";

        private GameObject _root;
        private Image _featPortrait;
        private TextMeshProUGUI _crystalText, _matText, _rosterText, _featName, _featBanner;

        private void Awake()
        {
            if (navigator == null) navigator = FindAnyObjectByType<ScreenNavigator>();
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            if (inventory == null) inventory = FindAnyObjectByType<PlayerInventory>();
            if (roster == null) roster = FindAnyObjectByType<PlayerRoster>();
            if (gacha == null) gacha = FindAnyObjectByType<GachaController>();
            BuildUI();
        }

        private void OnEnable()
        {
            if (wallet != null) wallet.OnChanged += Refresh;
            if (inventory != null) inventory.OnChanged += Refresh;
            if (roster != null) roster.OnRosterChanged += Refresh;
        }

        private void OnDisable()
        {
            if (wallet != null) wallet.OnChanged -= Refresh;
            if (inventory != null) inventory.OnChanged -= Refresh;
            if (roster != null) roster.OnRosterChanged -= Refresh;
        }

        private void Start() => Refresh();

        public void Show(bool on)
        {
            if (_root != null) _root.SetActive(on);
            if (on) Refresh();
        }

        private int AscensionTotal()
        {
            int n = 0;
            for (int e = 0; e <= (int)Element.Wind; e++)
                for (int t = 1; t <= Materials.AscensionTiers; t++)
                    n += inventory.Count(Materials.Ascension((Element)e, t));
            return n;
        }

        public void Refresh()
        {
            if (_crystalText != null)
                _crystalText.text = wallet != null ? $"크리스탈  {wallet.Crystals:N0}    <color=#FFD36B>금화  {wallet.Gold:N0}</color>" : "";

            if (_matText != null)
                _matText.text = inventory == null ? "" :
                    $"물약 {inventory.Count(MaterialType.ExpPotion)}   결정 {inventory.Count(MaterialType.ExpCrystal)}   " +
                    $"교본 {inventory.Count(MaterialType.SkillBook)}   돌파 재료 {AscensionTotal()}";

            if (_rosterText != null)
                _rosterText.text = roster != null ? $"보유 캐릭터  {roster.Owned.Count}명" : "";

            var banner = gacha != null ? gacha.Banner : null;
            var feat = banner != null ? banner.featured6 : null;
            if (feat != null)
            {
                var sprite = feat.thumbnail != null ? feat.thumbnail : feat.portrait;
                _featPortrait.sprite = sprite;
                _featPortrait.enabled = sprite != null;
                _featName.text = $"{new string('★', (int)feat.rarity)}  {feat.displayName}";
                _featBanner.text = $"픽업 진행 중 · {banner.bannerName}";
            }
            else
            {
                _featPortrait.enabled = false;
                _featName.text = "";
                _featBanner.text = "";
            }
        }

        private void Nav(HubScreen screen)
        {
            if (navigator == null) navigator = FindAnyObjectByType<ScreenNavigator>();
            if (navigator != null) navigator.Go(screen);
        }

        // ---------------- UI 구성 ----------------

        private void BuildUI()
        {
            var cgo = UIFactory.NewRect("HomeCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.07f, 0.06f, 0.11f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);

            // 타이틀(좌상단)
            var title = UIFactory.AddText("Title", _root.transform, gameTitle, 52, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            var trt = title.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            trt.sizeDelta = new Vector2(800, 70); trt.anchoredPosition = new Vector2(60, -40);

            // 재화/재료(우상단)
            _crystalText = UIFactory.AddText("Crystal", _root.transform, "", 26, font, new Color(0.6f, 0.9f, 1f), TextAlignmentOptions.Right, FontStyles.Bold);
            var crt = _crystalText.rectTransform;
            UIFactory.Anchor(crt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            crt.sizeDelta = new Vector2(600, 36); crt.anchoredPosition = new Vector2(-50, -40);

            _matText = UIFactory.AddText("Materials", _root.transform, "", 19, font, new Color(0.8f, 0.85f, 0.95f), TextAlignmentOptions.Right);
            var mrt = _matText.rectTransform;
            UIFactory.Anchor(mrt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            mrt.sizeDelta = new Vector2(800, 30); mrt.anchoredPosition = new Vector2(-50, -82);

            // 픽업 캐릭터(좌중앙)
            var featBg = UIFactory.AddImage("Featured", _root.transform, new Color(1f, 0.78f, 0.25f, 0.9f));
            var fbrt = featBg.rectTransform;
            UIFactory.Anchor(fbrt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            fbrt.sizeDelta = new Vector2(380, 500); fbrt.anchoredPosition = new Vector2(-380, -10);

            var featInner = UIFactory.AddImage("Inner", featBg.transform, new Color(0.1f, 0.1f, 0.14f, 1f));
            UIFactory.Stretch(featInner.rectTransform, 5, 5, 5, 5);

            _featPortrait = UIFactory.AddImage("Portrait", featInner.transform, new Color(1, 1, 1, 0.12f));
            UIFactory.Stretch(_featPortrait.rectTransform, 12, 80, 12, 12);
            _featPortrait.preserveAspect = true;

            _featName = UIFactory.AddText("Name", featInner.transform, "", 26, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var fnrt = _featName.rectTransform;
            UIFactory.Anchor(fnrt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            fnrt.sizeDelta = new Vector2(0, 36); fnrt.anchoredPosition = new Vector2(0, 38);

            _featBanner = UIFactory.AddText("Banner", featInner.transform, "", 16, font, new Color(1f, 0.82f, 0.4f), TextAlignmentOptions.Center);
            var fbnrt = _featBanner.rectTransform;
            UIFactory.Anchor(fbnrt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            fbnrt.sizeDelta = new Vector2(0, 26); fbnrt.anchoredPosition = new Vector2(0, 10);

            // 메뉴 버튼(우측)
            MenuButton("스테이지\n<size=52%>출격하고 보상 획득</size>", new Color(0.85f, 0.42f, 0.25f, 0.95f), new Vector2(360, 210), () => Nav(HubScreen.Stage));
            MenuButton("가챠\n<size=52%>캐릭터 뽑기</size>", new Color(0.8f, 0.6f, 0.2f, 0.95f), new Vector2(360, 70), () => Nav(HubScreen.Gacha));
            MenuButton("육성\n<size=52%>레벨 · 돌파 · 스킬 · 초월</size>", new Color(0.35f, 0.55f, 0.4f, 0.95f), new Vector2(360, -70), () => Nav(HubScreen.Growth));
            MenuButton("편성 · 연습 전투\n<size=52%>보상 없는 자유 전투</size>", new Color(0.45f, 0.3f, 0.75f, 0.95f), new Vector2(360, -210), () => Nav(HubScreen.Party));

            // 보유 수(좌하단)
            _rosterText = UIFactory.AddText("Roster", _root.transform, "", 22, font, new Color(0.85f, 0.85f, 0.9f), TextAlignmentOptions.Left, FontStyles.Bold);
            var rrt = _rosterText.rectTransform;
            UIFactory.Anchor(rrt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            rrt.sizeDelta = new Vector2(500, 34); rrt.anchoredPosition = new Vector2(60, 40);
        }

        private void MenuButton(string label, Color bg, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var btn = UIFactory.AddButton("Menu", _root.transform, label, 40, font, bg, Color.white);
            var rt = (RectTransform)btn.transform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = new Vector2(460, 120); rt.anchoredPosition = pos;
            btn.onClick.AddListener(onClick);
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
