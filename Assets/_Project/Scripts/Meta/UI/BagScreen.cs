using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;
using SubcultureRPG.Meta.Shop;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 가방(인벤토리) 화면(2026-11 개편 — 상점 · 임무와 같은 틀).
    ///  · 왼쪽: 세로 분류 탭(전체 · 경험치 · 스킬 · 돌파 · 장비 재료 · 기타 — 대표 아이콘 · 보유 종류 수) + '보유한 것만'
    ///  · 가운데: 둥근 칸 격자. 칸 바탕은 아이템 등급 색(고급 초록 · 희귀 파랑 · 영웅 보라 · 전설 금색), 없는 것은 흐리게
    ///  · 오른쪽: 상세(큰 아이콘 · 이름 · 등급 · 보유 수 · 용도 · 획득처 · 사용 버튼)
    /// 칸을 누르면 목록은 그대로 두고 선택 테두리와 상세만 바꿈.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Bag Screen")]
    public class BagScreen : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private TMP_FontAsset font;

        private GameObject _root;
        private TextMeshProUGUI _detName, _detGrade, _detCount, _detDesc, _countLabel, _ownedLabel, _empty;
        private Image _detIcon, _detGlow, _detEdge, _ownedBtnImg;
        private RectTransform _tabs, _content, _descContent, _srcGrid;
        private TextMeshProUGUI _srcHead;
        private ScreenNavigator _nav;
        private ScrollRect _scroll, _descScroll;
        private Button _backBtn, _useBtn;
        private System.Action _onBack;

        private int _tab = -1;   // -1 = 전체, 그 외 Materials.Category
        private bool _ownedOnly;
        private int _shownTab = -99;
        private MaterialType _selected = MaterialType.ExpSmall;
        private readonly Dictionary<MaterialType, Image> _hl = new();

        private static readonly Materials.Category[] Cats =
            { Materials.Category.Exp, Materials.Category.Skill, Materials.Category.Ascension, Materials.Category.Equipment, Materials.Category.Etc };

        private static readonly Color Accent = new(0.85f, 0.65f, 0.25f, 0.98f), Select = new(0.35f, 0.88f, 1f, 1f);

        private void Awake()
        {
            if (inventory == null) inventory = FindAnyObjectByType<PlayerInventory>();
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            _nav = FindAnyObjectByType<ScreenNavigator>();
            BuildUI();
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.OnChanged += Refresh;
            if (wallet != null) wallet.OnChanged += Refresh;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.OnChanged -= Refresh;
            if (wallet != null) wallet.OnChanged -= Refresh;
        }

        private void Start() => Refresh();

        public void Show(bool on)
        {
            if (_root != null) _root.SetActive(on);
            if (on) Refresh();
        }

        public void SetBackAction(System.Action onBack)
        {
            _onBack = onBack;
            if (_backBtn != null) _backBtn.gameObject.SetActive(onBack != null);
        }

        private int Count(MaterialType t) => inventory != null ? inventory.Count(t) : 0;

        // ---------------- 목록 ----------------

        private List<MaterialType> Items(int tab, bool ownedOnly)
        {
            var list = new List<MaterialType>();
            foreach (var t in Materials.All())
                if ((tab < 0 || (int)Materials.CategoryOf(t) == tab) && (!ownedOnly || Count(t) > 0)) list.Add(t);
            // 분류 순 → (돌파 재료는 속성·등급 순 유지) → 등급 높은 순
            list.Sort((a, b) =>
            {
                var ca = Materials.CategoryOf(a); var cb = Materials.CategoryOf(b);
                if (ca != cb) return ca.CompareTo(cb);
                if (ca == Materials.Category.Ascension) return ((int)a).CompareTo((int)b);
                int ga = Materials.Grade(a), gb = Materials.Grade(b);
                return ga != gb ? gb.CompareTo(ga) : ((int)a).CompareTo((int)b);
            });
            return list;
        }

        private void Refresh()
        {
            if (_root == null) return;

            RebuildTabs();
            Clear(_content);
            _hl.Clear();

            var items = Items(_tab, _ownedOnly);
            if (!items.Contains(_selected) && items.Count > 0) _selected = items[0];
            foreach (var t in items) BuildCell(t);

            var all = Items(_tab, false);
            int owned = 0;
            foreach (var t in all) if (Count(t) > 0) owned++;
            _countLabel.text = $"<b>{(_tab < 0 ? "전체" : Materials.CategoryName((Materials.Category)_tab))}</b>   <size=80%><color=#9AA3B5>보유</color> {owned} <color=#9AA3B5>/ {all.Count}종</color></size>";

            _empty.gameObject.SetActive(items.Count == 0);

            _ownedBtnImg.color = _ownedOnly ? Accent : new Color(1, 1, 1, 0.12f);
            _ownedLabel.text = _ownedOnly ? "보유한 것만  <color=#FFF3D6>ON</color>" : "보유한 것만  <color=#9AA3B5>OFF</color>";

            UpdateDetail(items.Count > 0);

            // 탭을 바꾸면 맨 위부터
            if (_shownTab != _tab) { Canvas.ForceUpdateCanvases(); _scroll.verticalNormalizedPosition = 1f; }
            _shownTab = _tab;
        }

        private void SelectItem(MaterialType t)
        {
            _selected = t;
            foreach (var kv in _hl) kv.Value.color = kv.Key == t ? Select : new Color(0, 0, 0, 0);
            UpdateDetail(true);
        }

        // ---------------- 탭 ----------------

        private static string CatIcon(int id) => id switch
        {
            (int)Materials.Category.Exp => "Items/ExpLarge",
            (int)Materials.Category.Skill => "Items/SkillBookPurple",
            (int)Materials.Category.Ascension => "Items/FireAsc3",
            (int)Materials.Category.Equipment => "Items/EquipOre",
            (int)Materials.Category.Etc => "Items/GiftMedium",
            _ => "Items/UiBag",
        };

        private void RebuildTabs()
        {
            Clear(_tabs);
            Tab("전체", -1);
            foreach (var c in Cats) Tab(Materials.CategoryName(c), (int)c);
        }

        private void Tab(string label, int id)
        {
            bool on = _tab == id;
            var all = Items(id, false);
            int owned = 0;
            foreach (var t in all) if (Count(t) > 0) owned++;

            var bg = UIFactory.AddImage("Tab", _tabs, on ? Accent : new Color(0.08f, 0.09f, 0.13f, 0.9f), ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight = 92;
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(() => { _tab = id; Refresh(); });
            bg.gameObject.AddComponent<ButtonFx>();
            var rt = bg.rectTransform;
            var sp = GameArt.Load(CatIcon(id));
            if (sp != null)
            {
                var im = UIFactory.AddImage("Icon", rt, Color.white, sp);
                im.preserveAspect = true; im.raycastTarget = false;
                UIFactory.Anchor(im.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                im.rectTransform.sizeDelta = new Vector2(68, 68); im.rectTransform.anchoredPosition = new Vector2(12, 0);
            }
            var t2 = UIFactory.AddText("T", rt, $"<b>{label}</b>\n<size=72%><color={(on ? "#FFF3D6" : "#9AA3B5")}>보유 {owned}/{all.Count}종</color></size>", 24, font, Color.white, TextAlignmentOptions.Left);
            t2.richText = true; t2.lineSpacing = -4f;
            t2.enableAutoSizing = true; t2.fontSizeMin = 14; t2.fontSizeMax = 24;
            UIFactory.Stretch(t2.rectTransform, 92, 8, 10, 8);
        }

        // ---------------- 칸 ----------------

        private void BuildCell(MaterialType t)
        {
            int n = Count(t);
            int grade = Materials.Grade(t);
            var gcol = Grades.ColorOf(grade);

            var cell = UIFactory.NewRect("Item", _content);
            // 선택 테두리(칸 뒤로 살짝 크게)
            var hl = UIFactory.AddImage("HL", cell, t == _selected ? Select : new Color(0, 0, 0, 0), ProceduralSprites.Rounded);
            hl.type = Image.Type.Sliced; hl.preserveAspect = false;
            UIFactory.Stretch(hl.rectTransform, -5, -5, -5, -5);
            _hl[t] = hl;

            // 등급 색 테두리 + 어두운 안쪽 + 위쪽 등급 색 빛
            var frame = UIFactory.AddImage("Frame", cell, new Color(gcol.r, gcol.g, gcol.b, n > 0 ? 0.95f : 0.35f), ProceduralSprites.Rounded, raycast: true);
            frame.type = Image.Type.Sliced; frame.preserveAspect = false;
            UIFactory.Stretch(frame.rectTransform);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            btn.onClick.AddListener(() => SelectItem(t));
            frame.gameObject.AddComponent<ButtonFx>();

            var inner = UIFactory.AddImage("Inner", frame.transform, new Color(0.07f, 0.08f, 0.12f, 0.96f), ProceduralSprites.Rounded);
            inner.type = Image.Type.Sliced; inner.preserveAspect = false;
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);
            var group = inner.gameObject.AddComponent<CanvasGroup>();
            group.alpha = n > 0 ? 1f : 0.42f;
            var irt = inner.rectTransform;

            var glow = UIFactory.AddImage("Glow", irt, new Color(gcol.r, gcol.g, gcol.b, 0.32f), ProceduralSprites.Glow);
            UIFactory.Anchor(glow.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f));
            glow.rectTransform.sizeDelta = new Vector2(170, 150); glow.rectTransform.anchoredPosition = new Vector2(0, -66);

            var icon = GameArt.MaterialIcon(t);
            if (icon != null)
            {
                var im = UIFactory.AddImage("Icon", irt, Color.white, icon);
                im.preserveAspect = true;
                UIFactory.Anchor(im.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f));
                im.rectTransform.sizeDelta = new Vector2(104, 104); im.rectTransform.anchoredPosition = new Vector2(0, -66);
            }

            // 등급 띠(왼쪽 위 작은 표시)
            var tag = Label(irt, $"<color={Grades.Hex(grade)}>{Grades.Name(grade)}</color>", 14, FontStyles.Bold, new Vector2(0, 1), new Vector2(8, -6), new Vector2(80, 20), TextAlignmentOptions.TopLeft);
            TextOutline.Apply(tag, 0.2f, new Color32(8, 10, 18, 230));

            // 보유 수(오른쪽 아래)
            var count = Label(irt, n > 0 ? $"×{n:N0}" : "0", 24, FontStyles.Bold, new Vector2(1, 1), new Vector2(-10, -98), new Vector2(150, 30), TextAlignmentOptions.TopRight);
            count.color = n > 0 ? Color.white : new Color(0.45f, 0.47f, 0.52f);
            TextOutline.Apply(count, 0.22f, new Color32(8, 10, 18, 240));

            // 이름 띠(아래)
            var strip = UIFactory.AddImage("Strip", irt, new Color(0, 0, 0, 0.35f));
            UIFactory.Anchor(strip.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            strip.rectTransform.sizeDelta = new Vector2(0, 50); strip.rectTransform.anchoredPosition = Vector2.zero;
            string sub = Materials.IsAscension(t)
                ? $"<color={Traits.Hex(Materials.ElementOf(t))}>{Traits.Name(Materials.ElementOf(t))}</color> · {Materials.TierTargetLevel(Materials.TierOf(t))} 돌파"
                : Materials.ExpValue(t) > 0 ? $"<color=#9AA3B5>+{Materials.ExpValue(t):N0} EXP</color>" : "";
            var name = UIFactory.AddText("Name", strip.rectTransform, sub != "" ? $"{Materials.Name(t)}\n<size=78%>{sub}</size>" : Materials.Name(t), 17, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            name.richText = true; name.lineSpacing = -8f;
            name.enableAutoSizing = true; name.fontSizeMin = 11; name.fontSizeMax = 17;
            UIFactory.Stretch(name.rectTransform, 6, 2, 6, 2);
        }

        // ---------------- 상세 ----------------

        private void UpdateDetail(bool any)
        {
            _detIcon.transform.parent.gameObject.SetActive(any);
            _useBtn.gameObject.SetActive(false);
            Clear(_srcGrid);
            _srcHead.gameObject.SetActive(false);
            if (!any) { _detName.text = ""; _detGrade.text = ""; _detCount.text = ""; _detDesc.text = ""; return; }

            var t = _selected;
            int g = Materials.Grade(t);
            var gcol = Grades.ColorOf(g);
            var icon = GameArt.MaterialIcon(t);
            _detIcon.sprite = icon; _detIcon.enabled = icon != null;
            _detGlow.color = new Color(gcol.r, gcol.g, gcol.b, 0.45f);
            _detEdge.color = new Color(gcol.r, gcol.g, gcol.b, 0.9f);
            _detName.text = Materials.RichName(t);
            string cat = Materials.CategoryName(Materials.CategoryOf(t));
            _detGrade.text = $"<color={Grades.Hex(g)}>{Grades.Name(g)}</color>  <color=#5B6272>|</color>  <color=#9AA3B5>{cat}</color>";
            int n = Count(t);
            _detCount.text = $"<color=#9AA3B5>보유</color>  <color={(n > 0 ? "#FFD36B" : "#6B7280")}>{n:N0}</color>";

            // 설명 앞의 '등급' 줄은 위에 따로 보여 주므로 뺌
            string desc = Materials.Description(t);
            string head = $"{Grades.Tag(g)} 등급\n\n";
            if (desc.StartsWith(head)) desc = desc.Substring(head.Length);
            _detDesc.text = desc;
            Canvas.ForceUpdateCanvases();
            _descScroll.verticalNormalizedPosition = 1f;

            bool usable = Materials.StaminaValue(t) > 0 || (Materials.IsSelector(t) && n > 0);
            _useBtn.gameObject.SetActive(usable);

            // 바로 가기(획득처 · 사용처) — '사용' 버튼이 있으면 한 줄(3개)까지
            var srcs = Sources(t);
            int max = usable ? 3 : 6;
            _srcHead.gameObject.SetActive(srcs.Count > 0 && _nav != null);
            if (_nav != null)
                for (int i = 0; i < srcs.Count && i < max; i++) SourceButton(srcs[i]);
            _useBtn.interactable = n > 0;
            _useBtn.image.color = n > 0 ? new Color(0.25f, 0.6f, 0.45f, 0.95f) : new Color(1, 1, 1, 0.12f);
        }

        // ---------------- 바로 가기 ----------------

        private readonly struct Source
        {
            public readonly string label, icon;
            public readonly System.Action<ScreenNavigator> go;
            public Source(string label, string icon, System.Action<ScreenNavigator> go) { this.label = label; this.icon = icon; this.go = go; }
        }

        private static string ShopIcon(ShopTab tab) => tab switch
        {
            ShopTab.General => "Items/ShopTabGeneral",
            ShopTab.Crystal => "Items/ShopTabCrystal",
            ShopTab.Guild => "Items/ShopTabGuild",
            _ => "Items/ShopTabExchange",
        };

        /// <summary>
        /// 재료의 획득처(던전 · 콘텐츠 · 상점 탭)와 사용처(교환소 · 길드 상점) 바로 가기.
        /// 상점은 카탈로그에서 그 재료를 파는 탭 / 그 재료로 값을 치르는 탭을 찾아 자동으로 붙임(상품 추가 시 따로 고칠 것 없음).
        /// </summary>
        private static List<Source> Sources(MaterialType t)
        {
            var list = new List<Source>();
            if (Materials.ExpValue(t) > 0)
            {
                list.Add(new Source("경험치 던전", "Items/ExpLarge", n => n.OpenDungeon(StageKind.ExpDungeon)));
                list.Add(new Source("메인 스토리", "Items/UiSortie", n => n.Go(HubScreen.Stage)));
            }
            if (t == MaterialType.SkillBook || t == MaterialType.SkillBookBlue || t == MaterialType.SkillBookPurple)
                list.Add(new Source("스킬 교본 던전", "Items/SkillBookPurple", n => n.OpenDungeon(StageKind.SkillBookDungeon)));
            if (Materials.IsAscension(t))
            {
                var el = Materials.ElementOf(t);
                list.Add(new Source($"돌파 던전 · {Traits.Name(el)}", "Items/" + t, n => n.OpenDungeon(StageKind.AscensionDungeon, el)));
            }
            if (t == MaterialType.EquipOre) list.Add(new Source("장비 던전", "Items/EquipOre", n => n.OpenDungeon(StageKind.EquipmentDungeon)));
            if (t == MaterialType.RefineStone)
            {
                list.Add(new Source("무한의 탑", "Items/TowerIcon", n => n.Go(HubScreen.Tower)));
                list.Add(new Source("주간 보스", "Items/RaidIcon", n => n.Go(HubScreen.Raid)));
            }
            if (t == MaterialType.SweepTicket) list.Add(new Source("임무", "Items/UiMission", n => n.Go(HubScreen.Mission)));
            if (t == MaterialType.StarShard) list.Add(new Source("소환", "Items/UiSummon", n => n.Go(HubScreen.Gacha)));
            if (t == MaterialType.GuildCoin) list.Add(new Source("길드", "Items/GuildIcon", n => n.Go(HubScreen.Guild)));

            // 상점: 파는 탭(획득처) → 값으로 받는 탭(사용처)
            ShopTab[] tabs = { ShopTab.General, ShopTab.Crystal, ShopTab.Exchange, ShopTab.Guild };
            foreach (var tab in tabs)
            {
                bool sells = false;
                foreach (var it in ShopCatalog.Of(tab))
                    if (!it.IsBundle && !it.IsGold && !it.rewardTicket && it.reward == t) { sells = true; break; }
                if (!sells) continue;
                var tb = tab;
                list.Add(new Source(ShopCatalog.TabName(tab), ShopIcon(tab), n => n.OpenShop(tb, t)));
            }
            foreach (var tab in tabs)
            {
                bool spends = false;
                foreach (var it in ShopCatalog.Of(tab))
                    if (it.currency == ShopCurrency.Material && it.costMaterial == t) { spends = true; break; }
                if (!spends || list.Exists(s => s.label == ShopCatalog.TabName(tab))) continue;
                var tb = tab;
                list.Add(new Source(ShopCatalog.TabName(tab) + "<size=75%> (사용)</size>", ShopIcon(tab), n => n.OpenShop(tb)));
            }
            return list;
        }

        private void SourceButton(Source s)
        {
            var bg = UIFactory.AddImage("Src", _srcGrid, new Color(0.16f, 0.2f, 0.3f, 0.95f), ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            bg.gameObject.AddComponent<ButtonFx>();
            var go = s.go;
            b.onClick.AddListener(() => { if (_nav != null) go(_nav); });
            var rt = bg.rectTransform;
            var sp = GameArt.Load(s.icon);
            if (sp != null)
            {
                var im = UIFactory.AddImage("Icon", rt, Color.white, sp);
                im.preserveAspect = true; im.raycastTarget = false;
                UIFactory.Anchor(im.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                im.rectTransform.sizeDelta = new Vector2(40, 40); im.rectTransform.anchoredPosition = new Vector2(6, 0);
            }
            var t = UIFactory.AddText("T", rt, s.label, 18, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            t.richText = true;
            t.enableAutoSizing = true; t.fontSizeMin = 12; t.fontSizeMax = 18;
            UIFactory.Stretch(t.rectTransform, sp != null ? 50 : 10, 4, 6, 4);
        }

        // ---------------- UI 구성 ----------------

        private void BuildUI()
        {
            var cgo = UIFactory.NewRect("BagCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 72;
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 어떤 화면비에서도 1920×1080 설계 영역이 전부 보이게(남는 쪽은 여백)
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.05f, 0.06f, 0.09f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            BackgroundArt.Create(_root.transform, 0.72f).Set("Backgrounds/bag");
            Transform root = UIFactory.Frame(_root.transform);   // 넓은 화면에서도 1920 폭 설계를 가운데에

            var title = UIFactory.AddText("Title", root, "가방", 40, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(400, 56), new Vector2(200, -24), new Vector2(0, 1));

            CurrencyBar.Create(root, font, wallet, new Vector2(-50, -32), Currency.Crystal, Currency.Gold);

            // 왼쪽: 세로 분류 탭 + 보유한 것만
            _tabs = UIFactory.NewRect("Tabs", root);
            Place(_tabs, new Vector2(0, 1), new Vector2(290, 620), new Vector2(40, -130), new Vector2(0, 1));
            var tv = _tabs.gameObject.AddComponent<VerticalLayoutGroup>();
            tv.spacing = 10; tv.childAlignment = TextAnchor.UpperLeft;
            tv.childControlWidth = true; tv.childControlHeight = true;
            tv.childForceExpandWidth = true; tv.childForceExpandHeight = false;

            var ownedBtn = UIFactory.AddButton("OwnedOnly", root, "보유한 것만", 22, font, new Color(1, 1, 1, 0.12f), Color.white);
            Place((RectTransform)ownedBtn.transform, new Vector2(0, 1), new Vector2(290, 60), new Vector2(40, -780), new Vector2(0, 1));
            _ownedBtnImg = ownedBtn.image;
            _ownedLabel = ownedBtn.GetComponentInChildren<TextMeshProUGUI>();
            _ownedLabel.richText = true;
            ownedBtn.onClick.AddListener(() => { _ownedOnly = !_ownedOnly; Refresh(); });

            // 가운데: 개수 + 격자
            var mid = UIFactory.AddImage("Grid", root, new Color(0.05f, 0.06f, 0.09f, 0.72f), ProceduralSprites.Rounded);
            mid.type = Image.Type.Sliced; mid.preserveAspect = false;
            Place(mid.rectTransform, new Vector2(0, 1), new Vector2(980, 920), new Vector2(350, -130), new Vector2(0, 1));
            _countLabel = UIFactory.AddText("Count", mid.transform, "", 26, font, Color.white, TextAlignmentOptions.Left);
            _countLabel.richText = true;
            Place(_countLabel.rectTransform, new Vector2(0, 1), new Vector2(900, 40), new Vector2(26, -14), new Vector2(0, 1));

            var view = UIFactory.NewRect("View", mid.transform);
            Place(view, new Vector2(0, 1), new Vector2(960, 852), new Vector2(10, -60), new Vector2(0, 1));
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);   // 빈 곳도 끌어서 스크롤
            _content = UIFactory.NewRect("Content", view);
            UIFactory.Anchor(_content, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            _content.sizeDelta = Vector2.zero; _content.anchoredPosition = Vector2.zero;
            var grid = _content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(170, 196); grid.spacing = new Vector2(16, 18);
            grid.padding = new RectOffset(18, 18, 10, 18);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 5;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll = view.gameObject.AddComponent<ScrollRect>();
            _scroll.content = _content; _scroll.viewport = view;
            _scroll.horizontal = false; _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;
            _empty = UIFactory.AddText("Empty", mid.transform, "보유한 아이템이 없습니다", 24, font, new Color(0.6f, 0.63f, 0.7f), TextAlignmentOptions.Center);
            Place(_empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(600, 40), Vector2.zero, new Vector2(0.5f, 0.5f));
            _empty.gameObject.SetActive(false);

            // 오른쪽: 상세
            var panel = UIFactory.AddImage("Detail", root, new Color(0.08f, 0.09f, 0.13f, 0.95f), ProceduralSprites.Rounded);
            panel.type = Image.Type.Sliced; panel.preserveAspect = false;
            Place(panel.rectTransform, new Vector2(1, 1), new Vector2(540, 920), new Vector2(-40, -130), new Vector2(1, 1));
            var prt = panel.rectTransform;

            // 큰 아이콘(등급 색 빛 + 둥근 테두리)
            _detEdge = UIFactory.AddImage("IconFrame", prt, Color.white, ProceduralSprites.Rounded);
            _detEdge.type = Image.Type.Sliced; _detEdge.preserveAspect = false;
            Place(_detEdge.rectTransform, new Vector2(0.5f, 1), new Vector2(250, 250), new Vector2(0, -30));
            var iconBg = UIFactory.AddImage("Inner", _detEdge.rectTransform, new Color(0.06f, 0.07f, 0.1f, 1f), ProceduralSprites.Rounded);
            iconBg.type = Image.Type.Sliced; iconBg.preserveAspect = false;
            UIFactory.Stretch(iconBg.rectTransform, 4, 4, 4, 4);
            _detGlow = UIFactory.AddImage("Glow", iconBg.rectTransform, Color.white, ProceduralSprites.Glow);
            UIFactory.Stretch(_detGlow.rectTransform, 0, 0, 0, 0);
            _detIcon = UIFactory.AddImage("Icon", iconBg.rectTransform, Color.white);
            _detIcon.preserveAspect = true;
            UIFactory.Stretch(_detIcon.rectTransform, 26, 26, 26, 26);

            _detName = UIFactory.AddText("Name", prt, "", 32, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            _detName.richText = true;
            _detName.enableAutoSizing = true; _detName.fontSizeMin = 20; _detName.fontSizeMax = 32;
            Place(_detName.rectTransform, new Vector2(0.5f, 1), new Vector2(500, 46), new Vector2(0, -296));
            _detGrade = UIFactory.AddText("Grade", prt, "", 22, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            _detGrade.richText = true;
            Place(_detGrade.rectTransform, new Vector2(0.5f, 1), new Vector2(500, 32), new Vector2(0, -344));
            _detCount = UIFactory.AddText("Count", prt, "", 30, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            _detCount.richText = true;
            Place(_detCount.rectTransform, new Vector2(0.5f, 1), new Vector2(500, 40), new Vector2(0, -380));

            var line = UIFactory.AddImage("Line", prt, new Color(1, 1, 1, 0.1f));
            Place(line.rectTransform, new Vector2(0.5f, 1), new Vector2(470, 2), new Vector2(0, -432));

            // 용도 · 획득처(길면 스크롤)
            var dview = UIFactory.NewRect("DescView", prt);
            Place(dview, new Vector2(0.5f, 1), new Vector2(480, 250), new Vector2(0, -446));
            _descContent = UIFactory.NewRect("DescContent", dview);
            var dl = _descContent.gameObject.AddComponent<VerticalLayoutGroup>();
            dl.childControlWidth = true; dl.childControlHeight = true; dl.childForceExpandWidth = true; dl.childForceExpandHeight = false;
            _detDesc = UIFactory.AddText("Desc", _descContent, "", 21, font, new Color(0.88f, 0.9f, 0.96f), TextAlignmentOptions.TopLeft);
            _detDesc.richText = true; _detDesc.lineSpacing = 8f;
            _descScroll = UIFactory.MakeVerticalScroll(dview, _descContent);

            // 바로 가기(획득처 · 사용처로 이동)
            _srcHead = UIFactory.AddText("SrcHead", prt, "<color=#FFD36B>바로 가기</color>", 22, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            _srcHead.richText = true;
            Place(_srcHead.rectTransform, new Vector2(0.5f, 1), new Vector2(480, 30), new Vector2(0, -706));
            _srcGrid = UIFactory.NewRect("Sources", prt);
            Place(_srcGrid, new Vector2(0.5f, 1), new Vector2(480, 120), new Vector2(0, -742));
            var sg = _srcGrid.gameObject.AddComponent<GridLayoutGroup>();
            sg.cellSize = new Vector2(153, 54); sg.spacing = new Vector2(10, 10);
            sg.constraint = GridLayoutGroup.Constraint.FixedColumnCount; sg.constraintCount = 3;
            sg.childAlignment = TextAnchor.UpperLeft;

            // 사용(행동력 물약 · 캐릭터 선택권)
            _useBtn = UIFactory.AddButton("Use", prt, "사용", 26, font, new Color(0.25f, 0.6f, 0.45f, 0.95f), Color.white);
            Place((RectTransform)_useBtn.transform, new Vector2(0.5f, 0), new Vector2(320, 68), new Vector2(0, 26), new Vector2(0.5f, 0));
            _useBtn.onClick.AddListener(() =>
            {
                if (Materials.IsSelector(_selected)) CharacterSelector.Open(_selected, font);   // 캐릭터 선택권
                else StaminaDialog.Open(font, _selected);
            });
            _useBtn.gameObject.SetActive(false);

            _backBtn = UIFactory.AddButton("BackBtn", root, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            Place((RectTransform)_backBtn.transform, new Vector2(0, 1), new Vector2(150, 44), new Vector2(30, -30), new Vector2(0, 1));
            _backBtn.onClick.AddListener(() => _onBack?.Invoke());
            _backBtn.gameObject.SetActive(false);
        }

        private TextMeshProUGUI Label(Transform parent, string text, float size, FontStyles style, Vector2 anchor, Vector2 pos, Vector2 dim, TextAlignmentOptions align)
        {
            var t = UIFactory.AddText("T", parent, text, size, font, Color.white, align, style);
            var rt = t.rectTransform;
            UIFactory.Anchor(rt, anchor, anchor, anchor);
            rt.sizeDelta = dim; rt.anchoredPosition = pos;
            t.richText = true;
            return t;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos, Vector2? pivot = null)
        {
            UIFactory.Anchor(rt, anchor, anchor, pivot ?? new Vector2(0.5f, 1));
            rt.sizeDelta = size; rt.anchoredPosition = pos;
        }

        private static void Clear(RectTransform rt)
        {
            for (int i = rt.childCount - 1; i >= 0; i--)
            {
                var ch = rt.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            SubcultureRPG.UI.UIInput.AddModule(es);
        }
    }
}
