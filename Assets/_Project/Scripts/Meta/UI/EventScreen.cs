using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 기간 한정 이벤트 화면. 왼쪽: 테마(이름 · 남은 기간 · 보너스 속성 · 수호자). 오른쪽: 코인 · 도전 횟수 · 난이도 7단계(스크롤, 잠긴 단계는 개방 조건),
    /// 아래 탭 `누적 보상` · `이벤트 상점`(맨 위 한정 축제 스킨 2벌). '도전' → 편성(출격 모드) → 전투.
    /// </summary>
    public class EventScreen : ModeScreenBase
    {
        /// <summary>'도전'을 눌렀을 때(난이도 번호).</summary>
        public event System.Action<int> OnChallenge;

        private TextMeshProUGUI _themeName, _tagline, _period, _bonus, _weak, _summary;
        private Image _halo, _boss, _emblem, _artFrame;
        private RectTransform _difficulties, _list;
        private Button _tabRewards, _tabShop;
        private bool _shopTab;
        private ScrollRect _diffScroll;
        private bool _diffScrolled;

        private void Awake()
        {
            // 배경: 이벤트 전용 그림(Events/{테마}_bg) → 없으면 테마 전투 배경
            var theme0 = LimitedEvent.CurrentTheme;
            BuildBase("기간 한정 이벤트",
                GameArt.Load(theme0.ArtBackground) != null ? theme0.ArtBackground : "Backgrounds/" + theme0.background, 73);
            CurrencyBar.Create(_frame, font, null, new Vector2(-50, -32), Currency.Crystal, Currency.Gold);

            // ---- 왼쪽: 테마 ----
            _halo = UIFactory.AddImage("Halo", _frame, new Color(1, 1, 1, 0.3f), ProceduralSprites.Glow);
            _halo.preserveAspect = false;
            Top(_halo.rectTransform, -480, -150, 820, 820, new Vector2(0.5f, 1));
            // 진행 캐릭터 일러스트(3:4) — 없으면 수호자 그림. 일러스트일 때는 테마 색 액자를 둠
            _artFrame = UIFactory.AddImage("ArtFrame", _frame, Color.white);
            Top(_artFrame.rectTransform, -480, -214, 474, 628, new Vector2(0.5f, 1));
            var frameShadow = _artFrame.gameObject.AddComponent<Shadow>();
            frameShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            frameShadow.effectDistance = new Vector2(0f, -6f);
            _boss = UIFactory.AddImage("Art", _frame, Color.white);
            _boss.preserveAspect = true;
            Top(_boss.rectTransform, -480, -220, 620, 616, new Vector2(0.5f, 1));

            // 테마 엠블럼(이름 왼쪽)
            _emblem = UIFactory.AddImage("Emblem", _frame, Color.white);
            _emblem.preserveAspect = true;
            Top(_emblem.rectTransform, -770, -98, 96, 96, new Vector2(0.5f, 1));

            _themeName = Text(_frame, "", 56, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            Top(_themeName.rectTransform, -480, -104, 760, 70, new Vector2(0.5f, 1));
            _tagline = Text(_frame, "", 24, new Color(0.85f, 0.88f, 0.95f), FontStyles.Italic, TextAlignmentOptions.Center);
            Top(_tagline.rectTransform, -480, -172, 760, 36, new Vector2(0.5f, 1));
            _bonus = Text(_frame, "", 24, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            Top(_bonus.rectTransform, -480, -846, 820, 40, new Vector2(0.5f, 1));
            _weak = Text(_frame, "", 22, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            Top(_weak.rectTransform, -480, -888, 820, 34, new Vector2(0.5f, 1));
            _period = Text(_frame, "", 20, new Color(0.75f, 0.78f, 0.86f), FontStyles.Normal, TextAlignmentOptions.Center);
            Top(_period.rectTransform, -480, -926, 820, 30, new Vector2(0.5f, 1));

            // ---- 오른쪽 위: 코인 · 도전 ----
            var info = Panel(_frame, new Color(0.06f, 0.07f, 0.11f, 0.9f));
            Top(info.rectTransform, 420, -108, 860, 92, new Vector2(0.5f, 1));
            var coinIcon = GameArt.Load(LimitedEvent.CoinIcon);
            if (coinIcon != null)
            {
                var ci = UIFactory.AddImage("Coin", info.transform, Color.white, coinIcon);
                ci.preserveAspect = true;
                UIFactory.Anchor(ci.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                ci.rectTransform.sizeDelta = new Vector2(70, 70); ci.rectTransform.anchoredPosition = new Vector2(18, 0);
            }
            _summary = Text(info.transform, "", 26, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            UIFactory.Stretch(_summary.rectTransform, 100, 10, 24, 10);

            // 난이도 7단계(스크롤 — 처음 열면 깰 수 있는 가장 높은 단계가 보이게)
            var dview = UIFactory.NewRect("DifficultyView", _frame);
            Top(dview, 420, -210, 860, 330, new Vector2(0.5f, 1));
            _difficulties = UIFactory.NewRect("Difficulties", dview);
            var dl = _difficulties.gameObject.AddComponent<VerticalLayoutGroup>();
            dl.spacing = 8; dl.childControlWidth = true; dl.childControlHeight = true; dl.childForceExpandHeight = false;
            _diffScroll = UIFactory.MakeVerticalScroll(dview, _difficulties);

            // ---- 오른쪽 아래: 탭 ----
            _tabRewards = TabButton("누적 보상", 420 - 215, () => { _shopTab = false; Refresh(); });
            _tabShop = TabButton("이벤트 상점", 420 + 215, () => { _shopTab = true; Refresh(); });
            var view = UIFactory.NewRect("ListView", _frame);
            Top(view, 420, -620, 860, 400, new Vector2(0.5f, 1));
            _list = UIFactory.NewRect("List", view);
            var vl = _list.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 8; vl.padding = new RectOffset(4, 4, 4, 4);
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandHeight = false;
            UIFactory.MakeVerticalScroll(view, _list);

            _root.SetActive(false);
            LimitedEvent.Instance.OnChanged += () => { if (_root != null && _root.activeSelf) Refresh(); };
        }

        private Button TabButton(string label, float x, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIFactory.AddButton("Tab", _frame, label, 24, font, new Color(1, 1, 1, 0.12f), Color.white);
            Top((RectTransform)b.transform, x, -556, 420, 54, new Vector2(0.5f, 1));
            b.onClick.AddListener(onClick);
            return b;
        }

        private float _clock;

        private void Update()
        {
            // 남은 기간은 1분 단위로만 바뀌므로 가끔 갱신
            if (_root == null || !_root.activeSelf) return;
            _clock += Time.unscaledDeltaTime;
            if (_clock < 30f) return;
            _clock = 0f;
            Refresh();
        }

        protected override void Refresh()
        {
            var ev = LimitedEvent.Instance;
            var theme = LimitedEvent.CurrentTheme;
            string themeHex = "#" + ColorUtility.ToHtmlStringRGB(theme.color);

            _themeName.text = $"<color={themeHex}><b>{theme.name}</b></color>";
            var hostArt = GameArt.Load(theme.ArtHost);
            _tagline.text = hostArt != null ? $"{theme.tagline}  <size=80%><color=#9AA3B5>· 진행 {theme.host}</color></size>" : theme.tagline;
            _halo.color = new Color(theme.color.r, theme.color.g, theme.color.b, hostArt != null ? 0.2f : 0.32f);
            var art = hostArt != null ? hostArt : GameArt.Enemy(theme.guardian);
            _boss.sprite = art; _boss.enabled = art != null;
            _artFrame.enabled = hostArt != null;
            _artFrame.color = theme.color;
            var emblem = GameArt.Load(theme.ArtEmblem);
            _emblem.sprite = emblem; _emblem.enabled = emblem != null;

            string el = $"<color={Traits.Hex(theme.element)}><b>{Traits.Name(theme.element)}</b></color>";
            _bonus.text = $"보너스 속성 {el}  <size=80%><color=#9AA3B5>파티 1명당 코인 +{LimitedEvent.BonusPerMember * 100f:0}% (최대 +{LimitedEvent.BonusPerMember * 400f:0}%)</color></size>";
            var weak = LimitedEvent.Weaknesses;
            if (weak.Count > 0)
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var e in weak) parts.Add($"<color={Traits.Hex(e)}>{Traits.Name(e)}</color>");
                _weak.text = $"<color=#9AA3B5>적 약점</color>  {string.Join(" · ", parts)}";
            }
            else _weak.text = "";
            var t = LimitedEvent.Remaining;
            _period.text = $"종료까지 {(t.Days > 0 ? $"{t.Days}일 " : "")}{t.Hours}시간 {t.Minutes}분  ·  {LimitedEvent.EndsAt:M월 d일 HH:mm} 종료 · 남은 코인은 사라집니다";

            _summary.text = $"<color=#FFD36B>축제 코인</color>  <b>{ev.Coins:N0}</b>      " +
                            $"<color=#9AA3B5>누적</color> {ev.Earned:N0}      " +
                            $"<color=#9AA3B5>오늘 도전</color>  <color={(ev.AttemptsLeft > 0 ? "#7CFF9A" : "#FF8A8A")}><b>{ev.AttemptsLeft}</b></color> / {LimitedEvent.AttemptsPerDay}";

            Clear(_difficulties);
            for (int i = 0; i < LimitedEvent.Difficulties.Length; i++) DifficultyRow(i);
            if (!_diffScrolled && _diffScroll != null)
            {
                // 처음 한 번: 열린 가장 높은 단계 쪽으로
                _diffScrolled = true;
                int top = Mathf.Clamp(ev.BestCleared + 1, 0, LimitedEvent.Difficulties.Length - 1);
                Canvas.ForceUpdateCanvases();
                _diffScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((top - 1) / (float)(LimitedEvent.Difficulties.Length - 4));
            }

            SetTab(_tabRewards, !_shopTab);
            SetTab(_tabShop, _shopTab);
            Clear(_list);
            if (_shopTab) { var shop = LimitedEvent.CurrentShop; for (int i = 0; i < shop.Count; i++) ShopRow(i, shop[i]); }
            else for (int i = 0; i < LimitedEvent.Milestones.Count; i++) MilestoneRow(i);
        }

        private static void SetTab(Button b, bool on)
        {
            if (b == null) return;
            b.image.color = on ? new Color(0.78f, 0.58f, 0.22f, 0.95f) : new Color(1, 1, 1, 0.12f);
        }

        private void DifficultyRow(int i)
        {
            var d = LimitedEvent.Difficulties[i];
            var ev = LimitedEvent.Instance;
            bool open = ev.TierOpen(i), cleared = ev.BestCleared >= i;
            var row = Panel(_difficulties, open ? new Color(0.06f, 0.07f, 0.11f, 0.88f) : new Color(0.04f, 0.04f, 0.06f, 0.8f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 76;
            string enemies = d.guardian ? $"정령 {d.minions} · 수호자 1" : $"정령 {d.minions}";
            var label = Text(row.transform,
                $"<b>{d.name}</b>  <color=#FFD36B>Lv.{d.level}</color>" + (cleared ? "  <size=70%><color=#7CFF9A>클리어</color></size>" : "") +
                $"   <size=72%><color=#9AA3B5>{enemies}" + (d.statMul > 1f ? $" · 강화 ×{d.statMul:0.##}" : "") + "</color></size>\n" +
                $"<size=78%><color=#FFD36B>축제 코인 {d.coins}</color> <color=#9AA3B5>(보너스 속성으로 최대 {Mathf.RoundToInt(d.coins * (1f + LimitedEvent.BonusPerMember * 4))})</color></size>",
                26, open ? Color.white : new Color(0.55f, 0.58f, 0.64f), FontStyles.Normal, TextAlignmentOptions.Left);
            label.lineSpacing = 2f;
            label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = 26;
            UIFactory.Stretch(label.rectTransform, 22, 6, 220, 6);

            if (!open)
            {
                var lockT = Text(row.transform, $"<color=#8A8FA0>{LimitedEvent.Difficulties[i - 1].name} 클리어 시 개방</color>", 19, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
                UIFactory.Anchor(lockT.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
                lockT.rectTransform.sizeDelta = new Vector2(200, 60); lockT.rectTransform.anchoredPosition = new Vector2(-10, 0);
                return;
            }
            var go = UIFactory.AddButton("Fight", row.transform, "도전", 28, font, new Color(0.85f, 0.45f, 0.3f, 0.96f), Color.white);
            UIFactory.Anchor((RectTransform)go.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            ((RectTransform)go.transform).sizeDelta = new Vector2(180, 58); ((RectTransform)go.transform).anchoredPosition = new Vector2(-14, 0);
            int idx = i;
            go.onClick.AddListener(() =>
            {
                if (LimitedEvent.Instance.AttemptsLeft <= 0) { Toast("오늘 도전 횟수를 모두 썼습니다 (매일 05:00 충전)"); return; }
                OnChallenge?.Invoke(idx);
            });
        }

        private void MilestoneRow(int i)
        {
            var ev = LimitedEvent.Instance;
            var m = LimitedEvent.Milestones[i];
            bool claimed = ev.IsClaimed(i), can = ev.CanClaim(i);
            var row = Panel(_list, claimed ? new Color(1, 1, 1, 0.04f) : can ? new Color(0.3f, 0.55f, 0.4f, 0.4f) : new Color(1, 1, 1, 0.07f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 72;
            var label = Text(row.transform, $"<color=#FFD36B><b>{m.coins:N0}</b></color> <size=80%><color=#9AA3B5>코인 누적</color></size>   {m.reward.Describe()}",
                22, claimed ? new Color(0.55f, 0.58f, 0.64f) : Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            UIFactory.Stretch(label.rectTransform, 20, 24, 200, 6);

            var bg = UIFactory.AddImage("Bar", row.transform, new Color(0, 0, 0, 0.4f));
            UIFactory.Anchor(bg.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0));
            bg.rectTransform.sizeDelta = new Vector2(-220, 8); bg.rectTransform.anchoredPosition = new Vector2(20, 12);
            var fill = UIFactory.AddImage("Fill", bg.transform, can || claimed ? new Color(0.45f, 0.95f, 0.55f) : new Color(1f, 0.8f, 0.4f));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ev.Earned / (float)m.coins), 1f);
            fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;

            var btn = UIFactory.AddButton("Claim", row.transform, claimed ? "수령 완료" : can ? "받기" : "진행 중", 20, font,
                can ? new Color(0.25f, 0.6f, 0.95f, 0.96f) : new Color(1, 1, 1, 0.1f), Color.white);
            UIFactory.Anchor((RectTransform)btn.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            ((RectTransform)btn.transform).sizeDelta = new Vector2(160, 50); ((RectTransform)btn.transform).anchoredPosition = new Vector2(-14, 0);
            btn.interactable = can;
            int idx = i;
            btn.onClick.AddListener(() => { if (LimitedEvent.Instance.Claim(idx)) Toast($"{m.reward.Describe()} 획득!"); });
        }

        private void ShopRow(int i, LimitedEvent.ShopEntry e)
        {
            if (e.IsSkin) { SkinRow(i, e); return; }
            var ev = LimitedEvent.Instance;
            int left = ev.ShopLeft(i);
            bool afford = ev.Coins >= e.price;
            var row = Panel(_list, left > 0 ? new Color(1, 1, 1, 0.07f) : new Color(1, 1, 1, 0.03f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 76;

            var icon = e.reward.Icon();
            if (icon != null)
            {
                var im = UIFactory.AddImage("Icon", row.transform, Color.white, icon);
                im.preserveAspect = true;
                UIFactory.Anchor(im.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                im.rectTransform.sizeDelta = new Vector2(60, 60); im.rectTransform.anchoredPosition = new Vector2(14, 0);
            }
            var label = Text(row.transform, $"{e.reward.Describe()}\n<size=75%><color=#9AA3B5>이번 이벤트 남은 구매 {left} / {e.limit}</color></size>",
                22, left > 0 ? Color.white : new Color(0.55f, 0.58f, 0.64f), FontStyles.Normal, TextAlignmentOptions.Left);
            UIFactory.Stretch(label.rectTransform, 90, 6, 230, 6);

            var btn = UIFactory.AddButton("Buy", row.transform, left <= 0 ? "품절" : $"코인 {e.price:N0}", 20, font,
                left > 0 && afford ? new Color(0.85f, 0.6f, 0.2f, 0.96f) : new Color(1, 1, 1, 0.1f), Color.white);
            UIFactory.Anchor((RectTransform)btn.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            ((RectTransform)btn.transform).sizeDelta = new Vector2(190, 54); ((RectTransform)btn.transform).anchoredPosition = new Vector2(-14, 0);
            btn.interactable = left > 0 && afford;
            int idx = i;
            btn.onClick.AddListener(() =>
            {
                string err = LimitedEvent.Instance.Buy(idx);
                Toast(err ?? $"{e.reward.Describe()} 획득!");
            });
        }

        /// <summary>한정 축제 스킨 1벌: 썸네일 · 캐릭터 · 미리보기 · 교환(보유하면 '보유 중').</summary>
        private void SkinRow(int i, LimitedEvent.ShopEntry e)
        {
            var ev = LimitedEvent.Instance;
            var roster = FindAnyObjectByType<PlayerRoster>();
            var c = roster != null && roster.Database != null ? roster.Database.GetCharacter(e.skinChar) : null;
            string name = c != null ? c.displayName : e.skinChar;
            int star = c != null ? (int)c.rarity : 0;
            bool owned = CharacterOutfits.Owns(e.skinChar, CharacterOutfits.FestivalId);
            bool haveChar = roster != null && c != null && roster.IsOwned(c);
            bool afford = ev.Coins >= e.price;
            var theme = LimitedEvent.CurrentTheme;
            var row = Panel(_list, new Color(theme.color.r * 0.35f, theme.color.g * 0.25f, theme.color.b * 0.25f, 0.85f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 104;

            var thumb = GameArt.Load("Thumbs/" + e.skinChar + "_fest") ?? GameArt.Load("Thumbs/" + e.skinChar);
            var frame = UIFactory.AddImage("Frame", row.transform, theme.color);
            UIFactory.Anchor(frame.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            frame.rectTransform.sizeDelta = new Vector2(88, 88); frame.rectTransform.anchoredPosition = new Vector2(12, 0);
            if (thumb != null)
            {
                var im = UIFactory.AddImage("Thumb", frame.transform, Color.white, thumb);
                im.preserveAspect = true;
                UIFactory.Stretch(im.rectTransform, 3, 3, 3, 3);
            }
            string stars = star > 0 ? $"<color=#{ColorUtility.ToHtmlStringRGB(StarText.ColorOf(star))}>★{star}</color> " : "";
            var label = Text(row.transform,
                $"<color=#FFD36B><b>한정 축제 스킨</b></color>  {stars}<b>{name}</b>\n" +
                $"<size=75%><color=#9AA3B5>{theme.name} 상점 한정 · 영구 보유 · 옷장에서 입기(일러스트 · 전투 그림 모두)</color></size>" +
                (!haveChar && !owned ? "\n<size=72%><color=#FFB38A>캐릭터를 아직 보유하지 않았습니다 (교환은 가능)</color></size>" : ""),
                22, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            label.enableAutoSizing = true; label.fontSizeMin = 14; label.fontSizeMax = 22;
            UIFactory.Stretch(label.rectTransform, 114, 6, 350, 6);

            var prev = UIFactory.AddButton("Preview", row.transform, "미리보기", 18, font, new Color(1, 1, 1, 0.14f), Color.white);
            UIFactory.Anchor((RectTransform)prev.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            ((RectTransform)prev.transform).sizeDelta = new Vector2(120, 54); ((RectTransform)prev.transform).anchoredPosition = new Vector2(-218, 0);
            prev.onClick.AddListener(() => PreviewSkin(e.skinChar, name));

            var btn = UIFactory.AddButton("Buy", row.transform, owned ? "보유 중" : $"코인 {e.price:N0}", 20, font,
                !owned && afford ? new Color(0.85f, 0.6f, 0.2f, 0.96f) : new Color(1, 1, 1, 0.1f), Color.white);
            UIFactory.Anchor((RectTransform)btn.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            ((RectTransform)btn.transform).sizeDelta = new Vector2(190, 54); ((RectTransform)btn.transform).anchoredPosition = new Vector2(-14, 0);
            btn.interactable = !owned && afford;
            int idx = i;
            btn.onClick.AddListener(() =>
            {
                string err = LimitedEvent.Instance.Buy(idx);
                if (err != null) { Toast(err); return; }
                Popups.Show("축제 스킨 획득", $"<b>{name}</b>의 <color=#FFD36B>{theme.name} 스킨</color>을 얻었습니다.\n\n육성 화면 → 캐릭터 상세의 <b>옷장</b>에서 입을 수 있고,\n입으면 일러스트 · 썸네일 · 전투 그림이 모두 바뀝니다.");
            });
        }

        private void PreviewSkin(string charId, string name)
        {
            var w = OverlayWindow.Open($"축제 스킨 · {name}", 660, 900, font);
            var sp = GameArt.Load("Characters/" + charId + "_fest");
            if (sp == null) { w.Text(w.Panel, "<color=#9AA3B5>일러스트 준비 중</color>", 26, Color.white, TextAlignmentOptions.Center, 0, -400, 660, 60); return; }
            var im = UIFactory.AddImage("Art", w.Panel, Color.white, sp);
            im.preserveAspect = true;
            OverlayWindow.Place(im.rectTransform, 30, -86, 600, 790);
        }
    }
}
