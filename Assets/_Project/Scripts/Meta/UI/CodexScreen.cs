using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;
using SubcultureRPG.Meta.Codex;
using SubcultureRPG.Meta.Missions;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 캐릭터 도감(2026-11 개편 — 상점 · 임무와 같은 틀).
    ///  · 왼쪽: 세로 성급 탭(전체 · ★6 · ★5 · ★4 · ★3 — 보유 수) · 아래 수집 보상 5단계(받을 것이 있으면 빨간 느낌표)
    ///  · 가운데: 속성 필터(전체 + 5속성 아이콘) · 캐릭터 카드(보유 = 컬러, 미보유 = 어둡게 · 성급 테두리 · 속성 아이콘)
    ///  · 오른쪽: 큰 일러스트(속성 색 빛 · 이름 · 별) · 속성/역할 칩 · 획득처 · 보유 정보 · 크게 보기 · 호감도 · 대표 대사
    /// 코드로 자동 구성 — 참조는 비우면 자동 탐색(PlayerCodex가 없으면 로스터 오브젝트에 추가).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Codex Screen")]
    public class CodexScreen : MonoBehaviour
    {
        [SerializeField] private PlayerCodex codex;
        [SerializeField] private TMP_FontAsset font;

        private GameObject _root, _viewer;
        private Image _portrait, _viewerImage, _artGlow;
        private TextMeshProUGUI _progress, _name, _stars, _source, _owned, _lockLabel, _toast, _line, _gridHead;
        private RectTransform _tabs, _grid, _milestones, _elements, _traitChips, _progressFill;
        private Button _backBtn, _viewBtn, _bondBtn;
        private TextMeshProUGUI _bondLabel;
        private NotifyBadge _bondBadge;
        private ScrollRect _scroll;
        private Action _onBack;

        private int _rarityTab;      // 0 = 전체, 그 외 성급(3~6)
        private int _elementTab = -1; // -1 = 전체, 그 외 Element
        private CharacterData _selected;
        private bool _dirty;
        private Coroutine _toastCo;
        private string _shownKey;
        private readonly Dictionary<CharacterData, Image> _hl = new();

        private static readonly int[] RarityTabs = { 0, 6, 5, 4, 3 };
        private static readonly Color Locked = new(0.16f, 0.18f, 0.24f, 1f), Accent = new(0.85f, 0.65f, 0.25f, 0.98f), SelectColor = new(0.35f, 0.88f, 1f, 1f);

        private void Awake()
        {
            if (codex == null) codex = FindAnyObjectByType<PlayerCodex>();
            if (codex == null)
            {
                var roster = FindAnyObjectByType<PlayerRoster>();
                var host = roster != null ? roster.gameObject : gameObject;
                codex = host.AddComponent<PlayerCodex>();
            }
            BuildUI();
        }

        private void OnEnable()
        {
            if (codex != null) codex.OnChanged += MarkDirty;
            PlayerBond.Instance.OnChanged += OnBondChanged;
            CharacterOutfits.Changed += OnOutfitChanged;
        }

        private void OnOutfitChanged(CharacterData c) => MarkDirty();

        private void OnDisable()
        {
            if (codex != null) codex.OnChanged -= MarkDirty;
            if (FindAnyObjectByType<PlayerBond>() is PlayerBond b) b.OnChanged -= OnBondChanged;
            CharacterOutfits.Changed -= OnOutfitChanged;
        }

        private void OnBondChanged() { if (_selected != null && _bondBtn != null) RefreshDetail(); }

        private void Start() => Refresh();

        /// <summary>코드로 생성할 때 폰트 지정(Awake 전, 비활성 상태에서 호출).</summary>
        public void SetFont(TMP_FontAsset f) { if (f != null) font = f; }

        public void Show(bool on)
        {
            if (_root != null) _root.SetActive(on);
            if (_viewer != null) _viewer.SetActive(false);
            if (on) Refresh();
        }

        public void SetBackAction(Action onBack)
        {
            _onBack = onBack;
            if (_backBtn != null) _backBtn.gameObject.SetActive(onBack != null);
        }

        private void MarkDirty() => _dirty = true;

        private void Update()
        {
            if (_dirty && _root != null && _root.activeSelf) Refresh();
        }

        // ---------------- 갱신 ----------------

        private bool Shown(CharacterData c) =>
            (_rarityTab == 0 || (int)c.rarity == _rarityTab) && (_elementTab < 0 || (int)c.element == _elementTab);

        private void Refresh()
        {
            _dirty = false;
            if (_root == null || codex == null) return;

            var all = codex.All();
            int owned = 0;
            foreach (var c in all) if (codex.IsOwned(c)) owned++;
            int pct = all.Count > 0 ? Mathf.RoundToInt(100f * owned / all.Count) : 0;
            _progress.text = $"<color=#9AA3B5><size=70%>수집</size></color>  <color=#FFD36B><b>{owned}</b></color> <color=#9AA3B5>/ {all.Count}</color>   <size=75%>{pct}%</size>";
            _progressFill.anchorMax = new Vector2(all.Count > 0 ? owned / (float)all.Count : 0f, 1f);

            RebuildTabs(all);
            RebuildElements(all);

            Clear(_grid);
            _hl.Clear();
            var shown = new List<CharacterData>();
            foreach (var c in all) if (Shown(c)) shown.Add(c);
            // 보유한 캐릭터 먼저(같으면 원래 순서)
            var order = new List<int>();
            for (int i = 0; i < shown.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int ha = codex.IsOwned(shown[a]) ? 0 : 1, hb = codex.IsOwned(shown[b]) ? 0 : 1;
                return ha != hb ? ha.CompareTo(hb) : a.CompareTo(b);
            });
            var sorted = new List<CharacterData>();
            foreach (int i in order) sorted.Add(shown[i]);
            if (_selected == null || !sorted.Contains(_selected)) _selected = sorted.Count > 0 ? sorted[0] : null;
            int have = 0;
            foreach (var c in sorted) { BuildCard(c); if (codex.IsOwned(c)) have++; }
            string rname = _rarityTab == 0 ? "전체" : $"<color=#{ColorUtility.ToHtmlStringRGB(StarText.ColorOf(_rarityTab))}>★{_rarityTab}</color>";
            string ename = _elementTab < 0 ? "" : $"  ·  <color={Traits.Hex((Element)_elementTab)}>{Traits.Name((Element)_elementTab)}</color>";
            _gridHead.text = $"<b>{rname}</b>{ename}   <size=75%><color=#9AA3B5>보유 {have} / {sorted.Count}명</color></size>";

            RebuildMilestones(owned);
            RefreshDetail();

            string key = _rarityTab + "/" + _elementTab;
            if (key != _shownKey && _scroll != null) { Canvas.ForceUpdateCanvases(); _scroll.verticalNormalizedPosition = 1f; }
            _shownKey = key;
        }

        private void Select(CharacterData c)
        {
            _selected = c;
            foreach (var kv in _hl) kv.Value.color = kv.Key == c ? SelectColor : new Color(0, 0, 0, 0);
            RefreshDetail();
        }

        // ---------------- 왼쪽: 성급 탭 ----------------

        private void RebuildTabs(List<CharacterData> all)
        {
            Clear(_tabs);
            foreach (int r in RarityTabs)
            {
                int rarity = r;
                int total = 0, have = 0;
                foreach (var c in all)
                    if (rarity == 0 || (int)c.rarity == rarity) { total++; if (codex.IsOwned(c)) have++; }
                if (total == 0) continue;
                bool on = _rarityTab == rarity;

                var bg = UIFactory.AddImage("Tab", _tabs, on ? Accent : new Color(0.08f, 0.09f, 0.13f, 0.9f), ProceduralSprites.Rounded, raycast: true);
                bg.type = Image.Type.Sliced; bg.preserveAspect = false;
                bg.gameObject.AddComponent<LayoutElement>().preferredHeight = 82;
                var b = bg.gameObject.AddComponent<Button>();
                b.targetGraphic = bg;
                b.onClick.AddListener(() => { _rarityTab = rarity; Refresh(); });
                bg.gameObject.AddComponent<ButtonFx>();
                var rt = bg.rectTransform;

                // 아이콘: 전체 = 도감 그림, 성급 = 성급 색 원 + 별
                if (rarity == 0)
                {
                    var sp = GameArt.Load("Items/UiCodex");
                    if (sp != null)
                    {
                        var im = UIFactory.AddImage("Icon", rt, Color.white, sp);
                        im.preserveAspect = true; im.raycastTarget = false;
                        UIFactory.Anchor(im.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                        im.rectTransform.sizeDelta = new Vector2(64, 64); im.rectTransform.anchoredPosition = new Vector2(12, 0);
                    }
                }
                else
                {
                    var col = StarText.ColorOf(rarity);
                    var circ = UIFactory.AddImage("Star", rt, new Color(col.r, col.g, col.b, 0.9f), ProceduralSprites.Circle);
                    UIFactory.Anchor(circ.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                    circ.rectTransform.sizeDelta = new Vector2(60, 60); circ.rectTransform.anchoredPosition = new Vector2(14, 0);
                    var st = UIFactory.AddText("T", circ.rectTransform, $"★{rarity}", 22, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                    UIFactory.Stretch(st.rectTransform);
                    TextOutline.Apply(st, 0.22f, new Color32(8, 10, 18, 230));
                }
                string name = rarity == 0 ? "전체" : $"태생 {rarity}성";
                var t = UIFactory.AddText("T", rt, $"<b>{name}</b>\n<size=72%><color={(on ? "#FFF3D6" : "#9AA3B5")}>보유 {have} / {total}</color></size>", 24, font, Color.white, TextAlignmentOptions.Left);
                t.richText = true; t.lineSpacing = -4f;
                UIFactory.Stretch(t.rectTransform, 90, 8, 10, 8);
            }
        }

        // ---------------- 가운데: 속성 필터 · 카드 ----------------

        private void RebuildElements(List<CharacterData> all)
        {
            Clear(_elements);
            ElementChip(-1, "전체", null);
            foreach (var e in Traits.AllElements) ElementChip((int)e, Traits.Name(e), "Elements/" + e);
        }

        private void ElementChip(int id, string label, string icon)
        {
            bool on = _elementTab == id;
            var bg = UIFactory.AddImage("El", _elements, on ? (id < 0 ? Accent : Traits.ColorOf((Element)id)) : new Color(1, 1, 1, 0.08f), ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = id < 0 ? 96 : 118; le.preferredHeight = 48;
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(() => { _elementTab = id; Refresh(); });
            float x = 10;
            var sp = icon != null ? GameArt.Load(icon) : null;
            if (sp != null)
            {
                var im = UIFactory.AddImage("Icon", bg.rectTransform, Color.white, sp);
                im.preserveAspect = true; im.raycastTarget = false;
                UIFactory.Anchor(im.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                im.rectTransform.sizeDelta = new Vector2(34, 34); im.rectTransform.anchoredPosition = new Vector2(8, 0);
                x = 46;
            }
            var t = UIFactory.AddText("T", bg.rectTransform, label, 20, font, Color.white, sp != null ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(t.rectTransform, sp != null ? x : 4, 2, 4, 2);
            if (on) TextOutline.Apply(t, 0.2f, new Color32(8, 10, 18, 220));
        }

        private void BuildCard(CharacterData c)
        {
            bool have = codex.IsOwned(c);
            int star = (int)c.rarity;
            var sc = StarText.ColorOf(star);

            var card = UIFactory.NewRect("Card", _grid);
            var hl = UIFactory.AddImage("HL", card, c == _selected ? SelectColor : new Color(0, 0, 0, 0), ProceduralSprites.Rounded);
            hl.type = Image.Type.Sliced; hl.preserveAspect = false;
            UIFactory.Stretch(hl.rectTransform, -5, -5, -5, -5);
            _hl[c] = hl;

            var frame = UIFactory.AddImage("Frame", card, new Color(sc.r, sc.g, sc.b, have ? 0.95f : 0.35f), ProceduralSprites.Rounded, raycast: true);
            frame.type = Image.Type.Sliced; frame.preserveAspect = false;
            UIFactory.Stretch(frame.rectTransform);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            var cc = c;
            btn.onClick.AddListener(() => Select(cc));
            frame.gameObject.AddComponent<ButtonFx>();

            var inner = UIFactory.AddImage("Inner", frame.transform, new Color(0.08f, 0.09f, 0.13f, 1f), ProceduralSprites.Rounded);
            inner.type = Image.Type.Sliced; inner.preserveAspect = false;
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);
            inner.gameObject.AddComponent<RectMask2D>();
            var irt = inner.rectTransform;

            var ec = Traits.ColorOf(c.element);
            var glow = UIFactory.AddImage("Glow", irt, new Color(ec.r, ec.g, ec.b, have ? 0.35f : 0.1f), ProceduralSprites.Glow);
            UIFactory.Stretch(glow.rectTransform, -20, 10, -20, -10);

            var sprite = GameArt.Thumb(c);
            var portrait = UIFactory.AddImage("Portrait", irt, have ? Color.white : Locked);
            UIFactory.Stretch(portrait.rectTransform, 4, 32, 4, 4);
            portrait.sprite = sprite; portrait.enabled = sprite != null; portrait.preserveAspect = true;

            if (!have)
            {
                var q = UIFactory.AddText("Lock", irt, "미보유", 17, font, new Color(0.75f, 0.78f, 0.86f), TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Stretch(q.rectTransform, 4, 32, 4, 4);
            }

            var bar = UIFactory.AddImage("Bar", irt, new Color(0, 0, 0, 0.6f));
            UIFactory.Anchor(bar.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            bar.rectTransform.sizeDelta = new Vector2(0, 30); bar.rectTransform.anchoredPosition = Vector2.zero;
            var name = UIFactory.AddText("Name", bar.transform, c.displayName, 15, font, have ? Color.white : new Color(0.7f, 0.72f, 0.78f), TextAlignmentOptions.Center, FontStyles.Bold);
            name.enableAutoSizing = true; name.fontSizeMin = 10; name.fontSizeMax = 15;
            UIFactory.Stretch(name.rectTransform, 4, 0, 4, 0);

            var st = UIFactory.AddText("Star", irt, $"★{star}", 17, font, sc, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIFactory.Anchor(st.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            st.rectTransform.sizeDelta = new Vector2(60, 24); st.rectTransform.anchoredPosition = new Vector2(6, -4);
            TextOutline.Apply(st, 0.25f, new Color32(8, 10, 18, 240));
            var esp = GameArt.Load("Elements/" + c.element);
            if (esp != null)
            {
                var ei = UIFactory.AddImage("El", irt, have ? Color.white : new Color(1, 1, 1, 0.4f), esp);
                ei.preserveAspect = true;
                UIFactory.Anchor(ei.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
                ei.rectTransform.sizeDelta = new Vector2(30, 30); ei.rectTransform.anchoredPosition = new Vector2(-4, -4);
            }
        }

        // ---------------- 왼쪽 아래: 수집 보상 ----------------

        private static (string icon, string count) RewardIcon(MissionReward r) => r.kind switch
        {
            MissionRewardKind.Crystal => ("Items/Crystal", r.amount.ToString("N0")),
            MissionRewardKind.Gold => ("Items/Gold", r.amount.ToString("N0")),
            MissionRewardKind.Ticket => (r.ticket == SubcultureRPG.Gacha.GachaBannerType.Pickup ? "Items/PickupTicket" : "Items/StandardTicket", "×" + r.amount),
            _ => ("Items/" + r.material, "×" + r.amount),
        };

        private void RebuildMilestones(int owned)
        {
            Clear(_milestones);
            foreach (var ms in PlayerCodex.Milestones)
            {
                var m = ms;
                bool claimed = codex.IsClaimed(m);
                bool can = codex.CanClaim(m);

                var row = UIFactory.AddImage("Milestone", _milestones,
                    can ? new Color(0.2f, 0.26f, 0.36f, 0.96f) : new Color(0.08f, 0.09f, 0.13f, 0.92f), ProceduralSprites.Rounded);
                row.type = Image.Type.Sliced; row.preserveAspect = false;
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 74;
                if (claimed) row.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;
                var rt = row.rectTransform;

                var head = UIFactory.AddText("Head", rt, $"<b>{m.count}명</b>", 22, font, owned >= m.count ? new Color(1f, 0.85f, 0.45f) : new Color(0.75f, 0.78f, 0.86f), TextAlignmentOptions.Left);
                head.richText = true;
                Place(head.rectTransform, new Vector2(0, 0.5f), new Vector2(70, 40), new Vector2(12, 0), new Vector2(0, 0.5f));
                float x = 80;
                foreach (var r in m.rewards)
                {
                    var (icon, count) = RewardIcon(r);
                    var sp = GameArt.Load(icon);
                    if (sp != null)
                    {
                        var im = UIFactory.AddImage("Icon", rt, Color.white, sp);
                        im.preserveAspect = true;
                        Place(im.rectTransform, new Vector2(0, 0.5f), new Vector2(40, 40), new Vector2(x, 6), new Vector2(0, 0.5f));
                    }
                    var ct = UIFactory.AddText("C", rt, count, 14, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                    Place(ct.rectTransform, new Vector2(0, 0.5f), new Vector2(56, 20), new Vector2(x - 8, -24), new Vector2(0, 0.5f));
                    TextOutline.Apply(ct, 0.22f, new Color32(8, 10, 18, 240));
                    x += 52;
                }

                string label = claimed ? "받음" : can ? "받기" : $"{Mathf.Min(owned, m.count)}/{m.count}";
                var btn = UIFactory.AddButton("Claim", rt, label, 19, font, can ? new Color(0.25f, 0.62f, 0.95f, 0.98f) : new Color(1, 1, 1, 0.12f), Color.white);
                Place((RectTransform)btn.transform, new Vector2(1, 0.5f), new Vector2(90, 50), new Vector2(-10, 0), new Vector2(1, 0.5f));
                btn.interactable = can;
                btn.onClick.AddListener(() =>
                {
                    if (!codex.Claim(m)) return;
                    Toast($"수집 보상 획득!  {m.RewardText.Replace("\n", "   ")}");
                    Refresh();
                });
                if (can) NotifyBadge.Attach(btn, font, 4, 4, 22).Set(true);
            }
        }

        // ---------------- 오른쪽: 상세 ----------------

        private void TraitChip(string icon, string label, Color color)
        {
            var bg = UIFactory.AddImage("Chip", _traitChips, new Color(color.r, color.g, color.b, 0.22f), ProceduralSprites.Rounded);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 136; le.preferredHeight = 44;
            float x = 10;
            var sp = icon != null ? GameArt.Load(icon) : null;
            if (sp != null)
            {
                var im = UIFactory.AddImage("Icon", bg.rectTransform, Color.white, sp);
                im.preserveAspect = true;
                UIFactory.Anchor(im.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                im.rectTransform.sizeDelta = new Vector2(32, 32); im.rectTransform.anchoredPosition = new Vector2(6, 0);
                x = 42;
            }
            var t = UIFactory.AddText("T", bg.rectTransform, label, 20, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            UIFactory.Stretch(t.rectTransform, x, 2, 4, 2);
        }

        private void RefreshDetail()
        {
            var c = _selected;
            Clear(_traitChips);
            if (c == null)
            {
                Live2DUI.Hide(_portrait);
                _portrait.enabled = false;
                _name.text = ""; _stars.text = ""; _line.text = ""; _source.text = ""; _owned.text = "";
                _lockLabel.text = "";
                _viewBtn.gameObject.SetActive(false);
                _bondBtn.gameObject.SetActive(false);
                return;
            }

            bool have = codex.IsOwned(c);
            var sprite = GameArt.Portrait(c);
            // 보유한 캐릭터에 Live2D 모델이 있으면 움직이는 모델로(미보유는 어두운 정적 그림)
            bool live = have && Live2DUI.Show(_portrait, c);
            if (!live) Live2DUI.Hide(_portrait);
            _portrait.sprite = sprite;
            _portrait.enabled = !live && sprite != null;
            _portrait.color = have ? Color.white : Locked;
            _lockLabel.text = have ? "" : "미보유";
            var ec = Traits.ColorOf(c.element);
            _artGlow.color = new Color(ec.r, ec.g, ec.b, have ? 0.5f : 0.15f);

            int star = (int)c.rarity;
            string starHex = "#" + ColorUtility.ToHtmlStringRGB(StarText.ColorOf(star));
            _name.text = c.displayName;
            _stars.text = $"<color={starHex}>{new string('★', star)}</color>  <size=70%><color=#C8CEDC>태생 {star}성</color></size>";
            TraitChip("Elements/" + c.element, Traits.Name(c.element), ec);
            TraitChip(null, Traits.Name(c.role), new Color(0.6f, 0.65f, 0.75f));
            _line.text = have ? $"「 {c.SignatureLine} 」" : "<color=#9AA3B5>획득하면 대표 대사를 볼 수 있습니다</color>";
            _source.text = $"<color=#FFD36B><b>획득처</b></color>\n{codex.SourceOf(c)}";

            var o = have && codex.Roster != null ? codex.Roster.GetOwned(c) : null;
            if (o != null)
            {
                int curStar = Transcendence.CurrentStar(c.rarity, o.star);
                _owned.text = "<color=#FFD36B><b>보유 정보</b></color>\n" +
                              $"Lv.{o.level}" + (o.ascension > 0 ? $"  ·  돌파 {o.ascension}" : "") + "\n" +
                              $"현재 {StarText.Rich(curStar, o.transcend)}\n" +
                              $"중복 {o.Duplicates}장  ·  <color=#FF9EC0>호감도 Lv.{PlayerBond.Instance.Level(c)}</color>";
            }
            else _owned.text = "<color=#FFD36B><b>보유 정보</b></color>\n<color=#9AA3B5>아직 획득하지 못한 캐릭터입니다</color>";

            _viewBtn.gameObject.SetActive(have && (sprite != null || live));
            _bondBtn.gameObject.SetActive(have);
            if (have)
            {
                _bondLabel.text = $"호감도 Lv.{PlayerBond.Instance.Level(c)}";
                _bondBadge.Set(PlayerBond.Instance.HasNotice(c));
            }
        }

        private void OpenViewer()
        {
            if (_selected == null || !codex.IsOwned(_selected)) return;
            var sprite = GameArt.Portrait(_selected);
            _viewer.SetActive(true);   // 먼저 켜야 Live2D 가 이 자리에 붙음
            bool live = Live2DUI.Show(_viewerImage, _selected);
            if (!live && sprite == null) { _viewer.SetActive(false); return; }
            _viewerImage.sprite = sprite;
            _viewerImage.enabled = !live;
        }

        private void Toast(string msg)
        {
            if (_toast == null) return;
            if (_toastCo != null) StopCoroutine(_toastCo);
            _toastCo = StartCoroutine(ToastRoutine(msg));
        }

        private IEnumerator ToastRoutine(string msg)
        {
            _toast.text = msg;
            _toast.alpha = 1f;
            yield return new WaitForSecondsRealtime(1.8f);
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                _toast.alpha = 1f - t / 0.4f;
                yield return null;
            }
            _toast.alpha = 0f;
            _toastCo = null;
        }

        // ---------------- UI 구성 ----------------

        private Image RoundPanel(Transform parent, Color c)
        {
            var img = UIFactory.AddImage("Panel", parent, c, ProceduralSprites.Rounded);
            img.type = Image.Type.Sliced; img.preserveAspect = false;
            return img;
        }

        private void BuildUI()
        {
            var cgo = UIFactory.NewRect("CodexCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 어떤 화면비에서도 1920×1080 설계 영역이 전부 보이게(남는 쪽은 여백)
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.05f, 0.06f, 0.09f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            BackgroundArt.Create(_root.transform, 0.74f).Set("Backgrounds/codex", "Backgrounds/growth");
            Transform root = UIFactory.Frame(_root.transform);   // 넓은 화면에서도 1920 폭 설계를 가운데에

            var title = UIFactory.AddText("Title", root, "캐릭터 도감", 40, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(400, 56), new Vector2(200, -24), new Vector2(0, 1));

            // 오른쪽 위: 수집 진행
            _progress = UIFactory.AddText("Progress", root, "", 30, font, Color.white, TextAlignmentOptions.Right);
            _progress.richText = true;
            Place(_progress.rectTransform, new Vector2(1, 1), new Vector2(600, 42), new Vector2(-50, -24), new Vector2(1, 1));
            var pbg = UIFactory.AddImage("ProgBg", root, new Color(1, 1, 1, 0.12f), ProceduralSprites.Rounded);
            pbg.type = Image.Type.Sliced; pbg.preserveAspect = false; pbg.pixelsPerUnitMultiplier = 4f;
            Place(pbg.rectTransform, new Vector2(1, 1), new Vector2(360, 12), new Vector2(-50, -74), new Vector2(1, 1));
            var pfill = UIFactory.AddImage("Fill", pbg.transform, new Color(1f, 0.83f, 0.42f), ProceduralSprites.Rounded);
            pfill.type = Image.Type.Sliced; pfill.preserveAspect = false; pfill.pixelsPerUnitMultiplier = 4f;
            _progressFill = pfill.rectTransform;
            _progressFill.anchorMin = Vector2.zero; _progressFill.anchorMax = new Vector2(0, 1); _progressFill.offsetMin = Vector2.zero; _progressFill.offsetMax = Vector2.zero;

            // 왼쪽: 성급 탭
            _tabs = UIFactory.NewRect("Tabs", root);
            Place(_tabs, new Vector2(0, 1), new Vector2(300, 450), new Vector2(40, -110), new Vector2(0, 1));
            var tv = _tabs.gameObject.AddComponent<VerticalLayoutGroup>();
            tv.spacing = 8; tv.childAlignment = TextAnchor.UpperLeft;
            tv.childControlWidth = true; tv.childControlHeight = true;
            tv.childForceExpandWidth = true; tv.childForceExpandHeight = false;

            // 왼쪽 아래: 수집 보상
            var msTitle = UIFactory.AddText("MsTitle", root, "<b>수집 보상</b>", 24, font, Color.white, TextAlignmentOptions.Left);
            msTitle.richText = true;
            Place(msTitle.rectTransform, new Vector2(0, 1), new Vector2(300, 34), new Vector2(44, -566), new Vector2(0, 1));
            _milestones = UIFactory.NewRect("Milestones", root);
            Place(_milestones, new Vector2(0, 1), new Vector2(300, 420), new Vector2(40, -604), new Vector2(0, 1));
            var mv = _milestones.gameObject.AddComponent<VerticalLayoutGroup>();
            mv.spacing = 8; mv.childAlignment = TextAnchor.UpperLeft;
            mv.childControlWidth = true; mv.childControlHeight = true;
            mv.childForceExpandWidth = true; mv.childForceExpandHeight = false;

            // 가운데: 머리 · 속성 필터 · 카드
            var mid = RoundPanel(root, new Color(0.05f, 0.06f, 0.09f, 0.72f));
            Place(mid.rectTransform, new Vector2(0, 1), new Vector2(780, 930), new Vector2(356, -110), new Vector2(0, 1));
            _gridHead = UIFactory.AddText("Head", mid.transform, "", 26, font, Color.white, TextAlignmentOptions.Left);
            _gridHead.richText = true;
            Place(_gridHead.rectTransform, new Vector2(0, 1), new Vector2(740, 40), new Vector2(20, -12), new Vector2(0, 1));
            _elements = UIFactory.NewRect("Elements", mid.transform);
            Place(_elements, new Vector2(0, 1), new Vector2(740, 48), new Vector2(16, -58), new Vector2(0, 1));
            var eh = _elements.gameObject.AddComponent<HorizontalLayoutGroup>();
            eh.spacing = 8; eh.childAlignment = TextAnchor.MiddleLeft;
            eh.childControlWidth = true; eh.childControlHeight = true;
            eh.childForceExpandWidth = false; eh.childForceExpandHeight = false;

            var view = UIFactory.NewRect("View", mid.transform);
            Place(view, new Vector2(0, 1), new Vector2(770, 806), new Vector2(5, -118), new Vector2(0, 1));
            _grid = UIFactory.NewRect("Grid", view);
            var gl = _grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(134, 180); gl.spacing = new Vector2(14, 14);
            gl.padding = new RectOffset(14, 14, 10, 16);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 5;
            _scroll = UIFactory.MakeVerticalScroll(view, _grid);

            // 오른쪽: 상세
            var panel = RoundPanel(root, new Color(0.08f, 0.09f, 0.13f, 0.95f));
            Place(panel.rectTransform, new Vector2(1, 1), new Vector2(740, 930), new Vector2(-40, -110), new Vector2(1, 1));
            var pt = panel.transform;

            // 일러스트(왼쪽 위, 3:4) — 속성 색 빛 위에
            var artBg = RoundPanel(pt, new Color(0, 0, 0, 0.4f));
            Place(artBg.rectTransform, new Vector2(0, 1), new Vector2(400, 560), new Vector2(18, -18), new Vector2(0, 1));
            artBg.gameObject.AddComponent<RectMask2D>();
            _artGlow = UIFactory.AddImage("Glow", artBg.transform, Color.white, ProceduralSprites.Glow);
            UIFactory.Stretch(_artGlow.rectTransform, -60, -40, -60, 40);
            _portrait = UIFactory.AddImage("Portrait", artBg.transform, Color.white);
            UIFactory.Stretch(_portrait.rectTransform);
            _portrait.preserveAspect = true;
            _lockLabel = UIFactory.AddText("Lock", artBg.transform, "", 34, font, new Color(0.8f, 0.83f, 0.9f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_lockLabel.rectTransform);

            // 정보(일러스트 오른쪽)
            const float ix = 434, iw = 286;
            _name = UIFactory.AddText("Name", pt, "", 38, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(_name.rectTransform, new Vector2(0, 1), new Vector2(iw, 50), new Vector2(ix, -22), new Vector2(0, 1));
            _name.enableAutoSizing = true; _name.fontSizeMin = 22; _name.fontSizeMax = 38;
            _stars = UIFactory.AddText("Stars", pt, "", 24, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            _stars.richText = true;
            Place(_stars.rectTransform, new Vector2(0, 1), new Vector2(iw, 34), new Vector2(ix, -76), new Vector2(0, 1));
            _stars.enableAutoSizing = true; _stars.fontSizeMin = 14; _stars.fontSizeMax = 24;
            _traitChips = UIFactory.NewRect("Traits", pt);
            Place(_traitChips, new Vector2(0, 1), new Vector2(iw, 44), new Vector2(ix, -118), new Vector2(0, 1));
            var ch = _traitChips.gameObject.AddComponent<HorizontalLayoutGroup>();
            ch.spacing = 8; ch.childAlignment = TextAnchor.MiddleLeft;
            ch.childControlWidth = true; ch.childControlHeight = true;
            ch.childForceExpandWidth = false; ch.childForceExpandHeight = false;

            var srcBg = RoundPanel(pt, new Color(1, 1, 1, 0.05f));
            Place(srcBg.rectTransform, new Vector2(0, 1), new Vector2(iw, 150), new Vector2(ix, -178), new Vector2(0, 1));
            _source = UIFactory.AddText("Source", srcBg.transform, "", 19, font, new Color(0.88f, 0.9f, 0.96f), TextAlignmentOptions.TopLeft);
            _source.richText = true; _source.lineSpacing = 6f;
            UIFactory.Stretch(_source.rectTransform, 14, 10, 10, 10);
            var ownBg = RoundPanel(pt, new Color(1, 1, 1, 0.05f));
            Place(ownBg.rectTransform, new Vector2(0, 1), new Vector2(iw, 170), new Vector2(ix, -338), new Vector2(0, 1));
            _owned = UIFactory.AddText("Owned", ownBg.transform, "", 19, font, new Color(0.88f, 0.9f, 0.96f), TextAlignmentOptions.TopLeft);
            _owned.richText = true; _owned.lineSpacing = 6f;
            UIFactory.Stretch(_owned.rectTransform, 14, 10, 10, 10);

            _viewBtn = UIFactory.AddButton("View", pt, "크게 보기", 20, font, new Color(0.25f, 0.62f, 0.95f, 0.98f), Color.white);
            Place((RectTransform)_viewBtn.transform, new Vector2(0, 1), new Vector2(iw / 2f - 5, 58), new Vector2(ix, -520), new Vector2(0, 1));
            _viewBtn.onClick.AddListener(OpenViewer);
            // 호감도(보유한 캐릭터만) — 받을 단계 보상 · 새 개인 스토리가 있으면 빨간 느낌표
            _bondBtn = UIFactory.AddButton("Bond", pt, "호감도", 20, font, new Color(0.86f, 0.38f, 0.56f, 0.96f), Color.white);
            Place((RectTransform)_bondBtn.transform, new Vector2(0, 1), new Vector2(iw / 2f - 5, 58), new Vector2(ix + iw / 2f + 5, -520), new Vector2(0, 1));
            _bondLabel = _bondBtn.GetComponentInChildren<TextMeshProUGUI>();
            _bondLabel.enableAutoSizing = true; _bondLabel.fontSizeMin = 14; _bondLabel.fontSizeMax = 20;
            _bondBadge = NotifyBadge.Attach(_bondBtn, font, 6, 6, 26);
            _bondBtn.onClick.AddListener(() => { if (_selected != null) BondWindow.Open(_selected, font); });
            _bondBtn.gameObject.SetActive(false);

            // 대표 대사(일러스트 아래)
            var lineBg = RoundPanel(pt, new Color(1, 1, 1, 0.05f));
            Place(lineBg.rectTransform, new Vector2(0.5f, 1), new Vector2(704, 316), new Vector2(0, -596));
            var q = UIFactory.AddText("Quote", lineBg.transform, "<color=#FFD36B><b>대표 대사</b></color>", 20, font, Color.white, TextAlignmentOptions.TopLeft);
            q.richText = true;
            Place(q.rectTransform, new Vector2(0, 1), new Vector2(300, 28), new Vector2(18, -12), new Vector2(0, 1));
            _line = UIFactory.AddText("Line", lineBg.transform, "", 26, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            _line.richText = true;
            UIFactory.Stretch(_line.rectTransform, 30, 44, 30, 20);
            _line.lineSpacing = 10f;
            _line.overflowMode = TextOverflowModes.Overflow;
            _line.enableAutoSizing = true; _line.fontSizeMin = 16; _line.fontSizeMax = 26;

            // 알림(아래 가운데)
            _toast = UIFactory.AddText("Toast", root, "", 22, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_toast.rectTransform, new Vector2(0.5f, 0), new Vector2(1200, 34), new Vector2(0, 8), new Vector2(0.5f, 0));
            _toast.richText = true; _toast.alpha = 0f;
            TextOutline.Apply(_toast, 0.22f, new Color32(8, 10, 18, 240));

            _backBtn = UIFactory.AddButton("BackBtn", root, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            Place((RectTransform)_backBtn.transform, new Vector2(0, 1), new Vector2(150, 44), new Vector2(30, -30), new Vector2(0, 1));
            _backBtn.onClick.AddListener(() => _onBack?.Invoke());
            _backBtn.gameObject.SetActive(false);

            BuildViewer(canvas.transform);
        }

        /// <summary>일러스트 크게 보기(화면을 누르면 닫힘).</summary>
        private void BuildViewer(Transform canvas)
        {
            var dim = UIFactory.AddImage("Viewer", canvas, new Color(0, 0, 0, 0.92f), null, raycast: true);
            UIFactory.Stretch(dim.rectTransform);
            _viewer = dim.gameObject;
            var close = _viewer.AddComponent<Button>();
            close.targetGraphic = dim;
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(() => _viewer.SetActive(false));

            _viewerImage = UIFactory.AddImage("Image", dim.transform, Color.white);
            UIFactory.Stretch(_viewerImage.rectTransform, 40, 60, 40, 30);
            _viewerImage.preserveAspect = true;

            var hint = UIFactory.AddText("Hint", dim.transform, "화면을 누르면 닫힙니다", 18, font, new Color(0.7f, 0.73f, 0.8f), TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(600, 28), new Vector2(0, 18), new Vector2(0.5f, 0));

            _viewer.SetActive(false);
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
