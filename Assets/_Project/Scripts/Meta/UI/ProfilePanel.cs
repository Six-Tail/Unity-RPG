using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 프로필 아바타: 동그란 캐릭터 얼굴 위에 테두리 그림(Resources/Art/Frames/{id}, 가운데가 뚫린 고리).
    /// 테두리 그림이 없으면(기본 테두리 등) 테두리 색의 원을 대신 깔아 줌.
    /// 홈 왼쪽 위 배지 · 프로필 창 · 테두리 고르기에서 같이 씀.
    /// </summary>
    public class ProfileAvatar
    {
        private readonly Image _frame, _ring, _face;
        public RectTransform Rect { get; }

        public ProfileAvatar(Transform parent, float size)
        {
            Rect = UIFactory.NewRect("Avatar", parent);
            Rect.sizeDelta = new Vector2(size, size);

            _ring = UIFactory.AddImage("Ring", Rect, Color.white, ProceduralSprites.Circle);
            Fit(_ring.rectTransform, size * 0.80f);

            // 얼굴: 원형으로 잘라서(테두리 그림의 가운데 구멍보다 살짝 크게 → 테두리가 가장자리를 덮음)
            var mask = UIFactory.AddImage("Mask", Rect, Color.white, ProceduralSprites.Circle);
            Fit(mask.rectTransform, size * 0.70f);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var bg = UIFactory.AddImage("Bg", mask.transform, new Color(0.1f, 0.11f, 0.16f, 1f));
            UIFactory.Stretch(bg.rectTransform);
            _face = UIFactory.AddImage("Face", mask.transform, Color.white);
            _face.preserveAspect = true;
            UIFactory.Stretch(_face.rectTransform);

            // 테두리 그림은 얼굴 위에(가운데가 뚫린 고리)
            _frame = UIFactory.AddImage("Frame", Rect, Color.white);
            _frame.preserveAspect = true;
            UIFactory.Stretch(_frame.rectTransform);
        }

        private static void Fit(RectTransform rt, float size)
        {
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = new Vector2(size, size); rt.anchoredPosition = Vector2.zero;
        }

        public void Set(CharacterData character, string frameId)
        {
            var frame = ProfileRewards.Frame(frameId);
            var art = GameArt.Load("Frames/" + frame.id);
            _frame.sprite = art;
            _frame.enabled = art != null;
            _ring.enabled = art == null;
            _ring.color = frame.color;

            var th = character != null ? GameArt.Thumb(character) : null;
            _face.sprite = th;
            _face.enabled = th != null;
        }

        /// <summary>프로필 아바타 캐릭터: 고른 캐릭터(보유 중) → 없으면 보유 중 가장 높은 성급.</summary>
        public static CharacterData CharacterOf(PlayerProfile profile, PlayerRoster roster)
        {
            if (roster == null || roster.Database == null) return null;
            CharacterData best = null;
            int bestGrade = -1;
            foreach (var o in roster.Owned)
            {
                var c = roster.Database.GetCharacter(o.id);
                if (c == null) continue;
                if (!string.IsNullOrEmpty(profile.AvatarId) && (c.id == profile.AvatarId || c.name == profile.AvatarId)) return c;
                int g = Transcendence.GradeValue(roster.StarOf(c), roster.TranscendOf(c));
                if (g > bestGrade) { best = c; bestGrade = g; }
            }
            return best;
        }
    }

    /// <summary>
    /// 프로필 창(홈 왼쪽 위 배지를 누르면 열림): 아바타 · 테두리 · 이름 · 칭호 · 레벨/경험치 · 총 전투력 ·
    /// UID · 스토리 진행 · 보유 캐릭터 · 인사말 · 주력 캐릭터 5명. 이름/칭호/테두리/아바타를 여기서 바꿈.
    /// 처음 시작할 때 이름을 정하는 창(AskName)도 이 클래스가 띄움.
    /// </summary>
    public class ProfilePanel : MonoBehaviour
    {
        private TMP_FontAsset _font;
        private PlayerRoster _roster;
        private PlayerEquipment _equipment;
        private GameObject _root, _main;
        private RectTransform _subLayer, _top5;
        private ProfileAvatar _avatar;
        private TextMeshProUGUI _name, _title, _level, _expText, _power, _info;
        private RectTransform _expFill;
        private TMP_InputField _greeting;
        private System.Action _onLobby;

        private static ProfilePanel Ensure(TMP_FontAsset font)
        {
            var p = FindAnyObjectByType<ProfilePanel>();
            if (p == null) p = new GameObject("ProfilePanel").AddComponent<ProfilePanel>();
            if (p._font == null) p._font = font;
            p.Build();
            return p;
        }

        /// <summary>프로필 창 열기. onLobby = '로비 설정' 버튼(대표 캐릭터 고르기).</summary>
        public static void Open(TMP_FontAsset font, System.Action onLobby)
        {
            var p = Ensure(font);
            p._onLobby = onLobby;
            p._root.SetActive(true);
            p._main.SetActive(true);
            p.CloseSub();
            p.Refresh();
        }

        /// <summary>이름 정하기 창만 띄움(처음 시작할 때). 이름이 정해지면 onDone.</summary>
        public static void AskName(TMP_FontAsset font, System.Action onDone)
        {
            var p = Ensure(font);
            p._root.SetActive(true);
            p._main.SetActive(false);
            p.OpenNameDialog(false, () => { p._root.SetActive(false); onDone?.Invoke(); });
        }

        private PlayerProfile Profile => PlayerProfile.Instance;

        // ---------------- 갱신 ----------------

        private void Refresh()
        {
            var pf = Profile;
            _avatar.Set(ProfileAvatar.CharacterOf(pf, _roster), pf.FrameId);
            _name.text = pf.DisplayName;
            var title = ProfileRewards.Title(pf.TitleId);
            _title.text = $"<color=#{ColorUtility.ToHtmlStringRGB(title.color)}>{title.name}</color>";

            bool max = pf.Level >= PlayerProfile.MaxLevel;
            int need = PlayerProfile.ExpForNext(pf.Level);
            _level.text = $"Lv.{pf.Level}";
            _expText.text = max ? "MAX" : $"{pf.Exp:N0} / {need:N0}";
            _expFill.anchorMax = new Vector2(max ? 1f : Mathf.Clamp01(pf.Exp / (float)need), 1f);

            var powers = CombatPower.All(_roster, _equipment);
            long total = 0;
            foreach (var kv in powers) total += kv.Value;
            _power.text = $"{total:N0}";

            var ctx = ProfileContext.Capture();
            int totalChars = _roster != null && _roster.Database != null ? _roster.Database.Characters.Count : 0;
            string story = ctx.chaptersCleared >= 15 ? "15장 클리어" : ctx.chaptersCleared > 0 ? $"{ctx.chaptersCleared}장 클리어 · {ctx.chaptersCleared + 1}장 진행 중" : "1장 진행 중";
            _info.text = $"<color=#9AA3B5>UID</color>   {pf.Uid}\n" +
                         $"<color=#9AA3B5>스토리</color>   {story}\n" +
                         $"<color=#9AA3B5>보유 캐릭터</color>   {ctx.owned} / {totalChars}명\n" +
                         $"<color=#9AA3B5>지휘관 경험치</color>   스테이지 클리어 시 쓴 행동력 × {PlayerProfile.ExpPerStamina}";

            _greeting.SetTextWithoutNotify(pf.Greeting);

            Clear(_top5);
            for (int i = 0; i < Mathf.Min(5, powers.Count); i++) TopCell(powers[i].Key, powers[i].Value, i);
        }

        private void TopCell(CharacterData c, int power, int index)
        {
            int star = _roster.StarOf(c);
            var col = StarText.ColorOf(star);
            var cell = UIFactory.AddImage("Top", _top5, new Color(col.r, col.g, col.b, 0.9f));
            Place(cell.rectTransform, index * 124, 0, 112, 112);
            var inner = UIFactory.AddImage("Inner", cell.transform, new Color(0.1f, 0.1f, 0.15f, 1f));
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);
            var th = GameArt.Thumb(c);
            if (th != null)
            {
                var img = UIFactory.AddImage("Thumb", inner.transform, Color.white, th);
                img.preserveAspect = true;
                UIFactory.Stretch(img.rectTransform, 2, 2, 2, 2);
            }
            var lv = Text(inner.transform, $"Lv.{_roster.LevelOf(c)}", 15, Color.white, FontStyles.Bold, TextAlignmentOptions.BottomRight);
            UIFactory.Stretch(lv.rectTransform, 2, 2, 6, 2);
            var pw = Text(_top5, $"{power:N0}", 17, new Color(1f, 0.86f, 0.5f), FontStyles.Bold, TextAlignmentOptions.Center);
            Place(pw.rectTransform, index * 124 - 6, -114, 124, 24);
        }

        // ---------------- 하위 창 ----------------

        private void CloseSub()
        {
            Clear(_subLayer);
            _subLayer.gameObject.SetActive(false);
        }

        /// <summary>하위 창 틀: 어두운 바탕 + 가운데 패널 + 제목. 반환 = 패널.</summary>
        private RectTransform Sub(string title, float w, float h, bool closable)
        {
            Clear(_subLayer);
            _subLayer.gameObject.SetActive(true);
            _subLayer.SetAsLastSibling();
            var dim = UIFactory.AddImage("Dim", _subLayer, new Color(0, 0, 0, 0.7f), null, raycast: true);
            UIFactory.Stretch(dim.rectTransform);
            if (closable)
            {
                var b = dim.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(CloseSub);
            }
            var panel = UIFactory.AddImage("Panel", _subLayer, new Color(0.1f, 0.11f, 0.17f, 0.99f), ProceduralSprites.Rounded, raycast: true);
            panel.type = Image.Type.Sliced; panel.preserveAspect = false;
            var rt = panel.rectTransform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = new Vector2(w, h);
            var t = Text(rt, title, 32, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            Place(t.rectTransform, 36, -24, w - 220, 46);
            if (closable)
            {
                var x = UIFactory.AddButton("Close", rt, "닫기", 20, _font, new Color(1, 1, 1, 0.14f), Color.white);
                var xr = (RectTransform)x.transform;
                UIFactory.Anchor(xr, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
                xr.sizeDelta = new Vector2(110, 44); xr.anchoredPosition = new Vector2(-24, -24);
                x.onClick.AddListener(CloseSub);
            }
            return rt;
        }

        private void OpenNameDialog(bool closable, System.Action onDone)
        {
            var rt = Sub(closable ? "이름 변경" : "지휘관의 이름을 정해 주세요", 760, 380, closable);
            var hint = Text(rt, $"2 ~ {PlayerProfile.MaxNameLength}자 · 나중에 프로필에서 바꿀 수 있습니다", 20, new Color(0.7f, 0.74f, 0.82f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(hint.rectTransform, 38, -80, 680, 30);
            var input = Input(rt, "이름 입력", PlayerProfile.MaxNameLength, 30);
            Place((RectTransform)input.transform, 36, -130, 688, 70);
            input.text = Profile.HasName ? Profile.Name : "";
            var warn = Text(rt, "", 20, new Color(1f, 0.55f, 0.5f), FontStyles.Bold, TextAlignmentOptions.Left);
            Place(warn.rectTransform, 38, -210, 680, 30);

            var ok = UIFactory.AddButton("Ok", rt, "확인", 26, _font, new Color(0.25f, 0.55f, 0.85f, 0.96f), Color.white);
            var okr = (RectTransform)ok.transform;
            UIFactory.Anchor(okr, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            okr.sizeDelta = new Vector2(300, 64); okr.anchoredPosition = new Vector2(0, 30);
            void Submit()
            {
                if (!Profile.SetName(input.text)) { warn.text = "이름은 2자 이상이어야 합니다"; return; }
                CloseSub();
                onDone?.Invoke();
            }
            ok.onClick.AddListener(Submit);
            input.onSubmit.AddListener(_ => Submit());
            input.Select();
            input.ActivateInputField();
        }

        private void OpenTitles()
        {
            var rt = Sub("칭호 변경", 820, 760, true);
            var content = ScrollArea(rt, 24, 24, 24, 90);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 8; vl.padding = new RectOffset(8, 8, 8, 8);
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandHeight = false;
            var ctx = ProfileContext.Capture();
            foreach (var t in ProfileRewards.Titles)
            {
                bool open = t.unlocked(ctx), cur = t.id == Profile.TitleId;
                var row = UIFactory.AddImage("Row", content, cur ? new Color(0.3f, 0.5f, 0.8f, 0.4f) : new Color(1, 1, 1, 0.06f), ProceduralSprites.Rounded, raycast: true);
                row.type = Image.Type.Sliced; row.preserveAspect = false;
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 66;
                string hex = ColorUtility.ToHtmlStringRGB(open ? t.color : new Color(0.45f, 0.47f, 0.53f));
                var name = Text(row.transform, $"<color=#{hex}><b>{t.name}</b></color>   <size=72%><color=#9AA3B5>{t.condition}</color></size>", 24, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                name.richText = true;
                UIFactory.Stretch(name.rectTransform, 22, 0, 150, 0);
                var state = Text(row.transform, cur ? "사용 중" : open ? "선택" : "잠김", 20,
                    cur ? new Color(0.6f, 0.85f, 1f) : open ? Color.white : new Color(0.5f, 0.52f, 0.58f), FontStyles.Bold, TextAlignmentOptions.Right);
                UIFactory.Stretch(state.rectTransform, 0, 0, 22, 0);
                if (open && !cur)
                {
                    var id = t.id;
                    var b = row.gameObject.AddComponent<Button>();
                    b.targetGraphic = row;
                    b.onClick.AddListener(() => { Profile.SetTitle(id); CloseSub(); Refresh(); });
                }
            }
        }

        private void OpenFrames()
        {
            var rt = Sub("테두리 변경", 980, 760, true);
            var content = ScrollArea(rt, 24, 24, 24, 90);
            var gl = content.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(214, 250); gl.spacing = new Vector2(14, 14); gl.padding = new RectOffset(10, 10, 10, 10);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 4;
            var ctx = ProfileContext.Capture();
            var who = ProfileAvatar.CharacterOf(Profile, _roster);
            foreach (var f in ProfileRewards.Frames)
            {
                bool open = f.unlocked(ctx), cur = f.id == Profile.FrameId;
                var cell = UIFactory.AddImage("Cell", content, cur ? new Color(0.3f, 0.5f, 0.8f, 0.45f) : new Color(1, 1, 1, 0.06f), ProceduralSprites.Rounded, raycast: true);
                cell.type = Image.Type.Sliced; cell.preserveAspect = false;
                var av = new ProfileAvatar(cell.transform, 150);
                UIFactory.Anchor(av.Rect, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
                av.Rect.anchoredPosition = new Vector2(0, -12);
                av.Set(who, f.id);
                var cg = av.Rect.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = open ? 1f : 0.35f; cg.blocksRaycasts = false;
                var name = Text(cell.transform, $"<b>{f.name}</b>\n<size=78%><color={(cur ? "#9AD4FF" : open ? "#9AA3B5" : "#FF9A9A")}>{(cur ? "사용 중" : open ? f.condition : "잠김 · " + f.condition)}</color></size>",
                    20, open ? Color.white : new Color(0.6f, 0.62f, 0.68f), FontStyles.Normal, TextAlignmentOptions.Bottom);
                name.richText = true;
                UIFactory.Stretch(name.rectTransform, 6, 10, 6, 0);
                if (open && !cur)
                {
                    var id = f.id;
                    var b = cell.gameObject.AddComponent<Button>();
                    b.targetGraphic = cell;
                    b.onClick.AddListener(() => { Profile.SetFrame(id); CloseSub(); Refresh(); });
                }
            }
        }

        private void OpenAvatars()
        {
            var rt = Sub("아바타 변경", 1100, 760, true);
            var content = ScrollArea(rt, 24, 24, 24, 90);
            var gl = content.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(134, 160); gl.spacing = new Vector2(12, 12); gl.padding = new RectOffset(10, 10, 10, 10);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 7;
            var cur = ProfileAvatar.CharacterOf(Profile, _roster);
            foreach (var kv in CombatPower.All(_roster, _equipment))
            {
                var c = kv.Key;
                bool on = c == cur;
                var col = StarText.ColorOf(_roster.StarOf(c));
                var cell = UIFactory.AddImage("Cell", content, on ? new Color(1f, 0.9f, 0.5f, 1f) : new Color(col.r, col.g, col.b, 0.85f), null, raycast: true);
                var inner = UIFactory.AddImage("Inner", cell.transform, new Color(0.1f, 0.1f, 0.15f, 1f));
                UIFactory.Stretch(inner.rectTransform, on ? 5 : 3, on ? 5 : 3, on ? 5 : 3, on ? 5 : 3);
                var th = GameArt.Thumb(c);
                if (th != null)
                {
                    var img = UIFactory.AddImage("Thumb", inner.transform, Color.white, th);
                    img.preserveAspect = true;
                    UIFactory.Stretch(img.rectTransform, 4, 30, 4, 4);
                }
                var name = Text(inner.transform, c.displayName, 17, Color.white, FontStyles.Bold, TextAlignmentOptions.Bottom);
                UIFactory.Stretch(name.rectTransform, 2, 4, 2, 0);
                var b = cell.gameObject.AddComponent<Button>();
                b.targetGraphic = cell;
                b.onClick.AddListener(() => { Profile.SetAvatar(c); CloseSub(); Refresh(); });
            }
        }

        // ---------------- UI 구성 ----------------

        private void Build()
        {
            if (_root != null) return;
            _roster = FindAnyObjectByType<PlayerRoster>();
            _equipment = FindAnyObjectByType<PlayerEquipment>();

            var cgo = UIFactory.NewRect("ProfileCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600;   // 타이틀(500) 위 — 처음 시작할 때 이름 정하기
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<StandaloneInputModule>();
            }

            _root = UIFactory.NewRect("Root", canvas.transform).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);

            // ---- 본 창 ----
            _main = UIFactory.NewRect("Main", _root.transform).gameObject;
            UIFactory.Stretch((RectTransform)_main.transform);
            var dim = UIFactory.AddImage("Dim", _main.transform, new Color(0, 0, 0, 0.72f), null, raycast: true);
            UIFactory.Stretch(dim.rectTransform);
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Close);

            var panel = UIFactory.AddImage("Panel", _main.transform, new Color(0.09f, 0.1f, 0.15f, 0.98f), ProceduralSprites.Rounded, raycast: true);
            panel.type = Image.Type.Sliced; panel.preserveAspect = false;
            var p = panel.rectTransform;
            UIFactory.Anchor(p, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            p.sizeDelta = new Vector2(1180, 900);

            // 왼쪽 위: 아바타 + 변경 버튼
            _avatar = new ProfileAvatar(p, 190);
            Place(_avatar.Rect, 36, -30, 190, 190);
            SmallButton(p, "아바타 변경", 46, -228, 170, OpenAvatars);
            SmallButton(p, "테두리 변경", 46, -276, 170, OpenFrames);

            // 이름 · 칭호 · 레벨
            _name = Text(p, "", 44, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            Place(_name.rectTransform, 252, -36, 430, 58);
            SmallButton(p, "이름 변경", 690, -46, 130, () => OpenNameDialog(true, Refresh));

            var titleBg = UIFactory.AddImage("TitleBg", p, new Color(1, 1, 1, 0.08f), ProceduralSprites.Rounded);
            titleBg.type = Image.Type.Sliced; titleBg.preserveAspect = false;
            Place(titleBg.rectTransform, 252, -108, 430, 46);
            _title = Text(titleBg.transform, "", 24, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            _title.richText = true;
            UIFactory.Stretch(_title.rectTransform, 18, 0, 10, 0);
            SmallButton(p, "칭호 변경", 690, -110, 130, OpenTitles);

            _level = Text(p, "", 30, new Color(1f, 0.86f, 0.5f), FontStyles.Bold, TextAlignmentOptions.Left);
            Place(_level.rectTransform, 252, -172, 120, 40);
            var barBg = UIFactory.AddImage("ExpBg", p, new Color(1, 1, 1, 0.14f), ProceduralSprites.Rounded);
            barBg.type = Image.Type.Sliced; barBg.preserveAspect = false; barBg.pixelsPerUnitMultiplier = 3f;
            Place(barBg.rectTransform, 372, -182, 448, 22);
            var fill = UIFactory.AddImage("Fill", barBg.transform, new Color(1f, 0.82f, 0.4f), ProceduralSprites.Rounded);
            fill.type = Image.Type.Sliced; fill.preserveAspect = false; fill.pixelsPerUnitMultiplier = 3f;
            _expFill = fill.rectTransform;
            _expFill.anchorMin = Vector2.zero; _expFill.anchorMax = new Vector2(0f, 1f);
            _expFill.offsetMin = Vector2.zero; _expFill.offsetMax = Vector2.zero;
            _expText = Text(barBg.transform, "", 16, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            UIFactory.Stretch(_expText.rectTransform);

            // 총 전투력(오른쪽 위)
            var powerBg = UIFactory.AddImage("PowerBg", p, new Color(0.16f, 0.14f, 0.08f, 0.9f), ProceduralSprites.Rounded);
            powerBg.type = Image.Type.Sliced; powerBg.preserveAspect = false;
            Place(powerBg.rectTransform, 852, -36, 292, 112);
            var pl = Text(powerBg.transform, "총 전투력  <size=75%><color=#9AA3B5>보유 캐릭터 합산</color></size>", 20, new Color(1f, 0.86f, 0.5f), FontStyles.Bold, TextAlignmentOptions.Top);
            pl.richText = true;
            UIFactory.Stretch(pl.rectTransform, 8, 0, 8, 12);
            _power = Text(powerBg.transform, "", 42, Color.white, FontStyles.Bold, TextAlignmentOptions.Bottom);
            UIFactory.Stretch(_power.rectTransform, 8, 12, 8, 0);

            // 정보
            _info = UIFactory.AddText("Info", p, "", 22, _font, Color.white, TextAlignmentOptions.TopLeft);
            _info.richText = true; _info.lineSpacing = 16f;
            Place(_info.rectTransform, 252, -228, 880, 170);

            // 인사말
            _greeting = Input(p, $"인사말을 입력해 주세요 (최대 {PlayerProfile.MaxGreetingLength}자)", PlayerProfile.MaxGreetingLength, 24);
            Place((RectTransform)_greeting.transform, 36, -416, 1108, 62);
            _greeting.onEndEdit.AddListener(s => Profile.SetGreeting(s));

            // 주력 캐릭터(전투력 상위 5명)
            var tl = Text(p, "주력 캐릭터  <size=75%><color=#9AA3B5>전투력 상위 5명</color></size>", 24, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            tl.richText = true;
            Place(tl.rectTransform, 36, -500, 700, 36);
            _top5 = UIFactory.NewRect("Top5", p);
            Place(_top5, 40, -548, 700, 150);

            // 아래 버튼
            var close = UIFactory.AddButton("Close", p, "닫기", 26, _font, new Color(1, 1, 1, 0.16f), Color.white);
            var cr = (RectTransform)close.transform;
            UIFactory.Anchor(cr, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            cr.sizeDelta = new Vector2(260, 66); cr.anchoredPosition = new Vector2(-150, 30);
            close.onClick.AddListener(Close);
            var lobby = UIFactory.AddButton("Lobby", p, "로비 설정", 26, _font, new Color(0.25f, 0.55f, 0.85f, 0.96f), Color.white);
            var lr = (RectTransform)lobby.transform;
            UIFactory.Anchor(lr, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            lr.sizeDelta = new Vector2(300, 66); lr.anchoredPosition = new Vector2(150, 30);
            lobby.onClick.AddListener(() => { var a = _onLobby; Close(); a?.Invoke(); });

            // ---- 하위 창 레이어 ----
            _subLayer = UIFactory.NewRect("Sub", _root.transform);
            UIFactory.Stretch(_subLayer);
            _subLayer.gameObject.SetActive(false);

            _root.SetActive(false);
        }

        private void Close()
        {
            CloseSub();
            _root.SetActive(false);
        }

        // ---------------- 도우미 ----------------

        private void SmallButton(RectTransform parent, string label, float x, float y, float w, System.Action onClick)
        {
            var b = UIFactory.AddButton("Btn", parent, label, 19, _font, new Color(1, 1, 1, 0.14f), Color.white);
            Place((RectTransform)b.transform, x, y, w, 40);
            b.onClick.AddListener(() => onClick());
        }

        private TextMeshProUGUI Text(Transform parent, string s, float size, Color c, FontStyles style, TextAlignmentOptions align)
        {
            var t = UIFactory.AddText("T", parent, s, size, _font, c, align, style);
            t.enableAutoSizing = true; t.fontSizeMin = Mathf.Max(11, size * 0.6f); t.fontSizeMax = size;
            return t;
        }

        /// <summary>입력 칸(TMP_InputField)을 코드로 구성.</summary>
        private TMP_InputField Input(Transform parent, string placeholder, int limit, float size)
        {
            var bg = UIFactory.AddImage("Input", parent, new Color(1, 1, 1, 0.1f), ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            var area = UIFactory.NewRect("Text Area", bg.transform);
            UIFactory.Stretch(area, 20, 6, 20, 6);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = UIFactory.AddText("Placeholder", area, placeholder, size, _font, new Color(0.6f, 0.63f, 0.7f), TextAlignmentOptions.Left, FontStyles.Italic);
            UIFactory.Stretch(ph.rectTransform);
            var text = UIFactory.AddText("Text", area, "", size, _font, Color.white, TextAlignmentOptions.Left);
            UIFactory.Stretch(text.rectTransform);
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;

            var field = bg.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = ph;
            field.characterLimit = limit;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.richText = false;
            if (_font != null) field.fontAsset = _font;
            field.pointSize = size;
            field.caretColor = Color.white;
            field.customCaretColor = true;
            field.selectionColor = new Color(0.4f, 0.6f, 1f, 0.5f);
            return field;
        }

        /// <summary>패널 안 스크롤 영역(여백 = 왼 · 아래 · 오른 · 위). 반환 = 내용 칸.</summary>
        private RectTransform ScrollArea(RectTransform panel, float l, float b, float r, float t)
        {
            var view = UIFactory.NewRect("View", panel);
            UIFactory.Stretch(view, l, b, r, t);
            var content = UIFactory.NewRect("Content", view);
            UIFactory.MakeVerticalScroll(view, content);
            return content;
        }

        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            UIFactory.Anchor(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, y);
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
    }
}
