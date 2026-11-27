using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;
using SubcultureRPG.Gacha;
using SubcultureRPG.Meta.Missions;
using SubcultureRPG.Meta.Codex;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 홈(로비) 화면.
    ///  · 왼쪽: 플레이어가 고른 <b>대표 캐릭터</b>가 크게 서 있음(움직이는 일러스트가 있으면 그것) — 누르면 대사 말풍선
    ///  · 오른쪽: 스테이지(크게) · 가챠 · 육성 · 임무 · 상점 · 가방 · 도감 · 대표 변경
    ///  · 오른쪽 위: 크리스탈 · 금화
    /// 대표 캐릭터는 PlayerPrefs(home.assistant)에 기억, 없으면 보유 중 가장 높은 성급.
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

        private const string AssistantKey = "home.assistant";

        /// <summary>홈 화면 폰트(코드로 추가하는 화면이 같은 폰트를 쓰도록).</summary>
        public TMP_FontAsset Font => font;

        private GameObject _root, _picker;
        private PlayerMissions _missions;
        private PlayerCodex _codex;
        private TextMeshProUGUI _rosterText, _assistName, _bubbleText;
        private Image _assistant;
        private RectTransform _assistRt, _bubble, _pickerGrid;
        private StarRow _assistStars;

        // 왼쪽 위 프로필 배지(아바타 · 칭호 · 이름 · 레벨 · 경험치) — 누르면 프로필 창
        private ProfileAvatar _badgeAvatar;
        private TextMeshProUGUI _badgeName, _badgeTitle, _badgeLevel;
        private RectTransform _badgeExp;
        private RectTransform _assistMask;
        private TextMeshProUGUI _assistBond;
        private NotifyBadge _assistBondBadge;
        private RectMask2D _assistSoft;
        private CanvasGroup _bubbleGroup;
        private Coroutine _bubbleRoutine, _bounceRoutine;
        private CharacterData _shown;

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
            CharacterOutfits.Changed += OnOutfitChanged;
        }

        /// <summary>대표 캐릭터의 의상이 바뀌면 그림 · 영상을 다시 띄움.</summary>
        private void OnOutfitChanged(CharacterData c)
        {
            if (c != _shown) return;
            _shown = null;
            RefreshAssistant();
            RefreshProfileBadge();
        }

        private void OnDisable()
        {
            if (wallet != null) wallet.OnChanged -= Refresh;
            if (inventory != null) inventory.OnChanged -= Refresh;
            if (roster != null) roster.OnRosterChanged -= Refresh;
            CharacterOutfits.Changed -= OnOutfitChanged;
        }

        private void Start()
        {
            // 임무 시스템은 내비게이터가 실행 중에 만들 수도 있어 Start에서 찾음
            _missions = FindAnyObjectByType<PlayerMissions>();
            if (_missions != null) _missions.OnChanged += RefreshMissionBadge;
            PlayerAchievements.Instance.OnChanged += RefreshMissionBadge;
            SeasonPass.Instance.OnChanged += RefreshBadges;
            _codex = FindAnyObjectByType<PlayerCodex>();
            if (_codex != null) _codex.OnChanged += RefreshCodexButton;
            PlayerBond.Instance.OnChanged += RefreshBond;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_missions != null) _missions.OnChanged -= RefreshMissionBadge;
            if (FindAnyObjectByType<ChatService>() is ChatService chat) chat.OnChanged -= RefreshMiniChat;
            if (FindAnyObjectByType<PlayerAchievements>() is PlayerAchievements ach) ach.OnChanged -= RefreshMissionBadge;
            if (FindAnyObjectByType<SeasonPass>() is SeasonPass sp) sp.OnChanged -= RefreshBadges;
            if (_codex != null) _codex.OnChanged -= RefreshCodexButton;
            if (FindAnyObjectByType<PlayerBond>() is PlayerBond bond) bond.OnChanged -= RefreshBond;
            var pf = FindAnyObjectByType<PlayerProfile>();
            if (pf != null) pf.OnChanged -= RefreshProfileBadge;
        }

        public void Show(bool on)
        {
            if (_root != null) _root.SetActive(on);
            if (_menuPop != null) _menuPop.SetActive(false);
            if (on && _hub != null && _hub.activeSelf && !Tutorial.Finished) OpenModeHub(false);
            if (on) { _shown = null; Refresh(); }
            else if (_picker != null) _picker.SetActive(false);
        }

        public void Refresh()
        {
            RefreshMissionBadge();
            RefreshCodexButton();
            RefreshAssistant();
            RefreshBond();
            RefreshProfileBadge();
            RefreshModeTiles();
        }

        private void RefreshProfileBadge()
        {
            if (_badgeAvatar == null) return;
            var pf = PlayerProfile.Instance;
            _badgeAvatar.Set(ProfileAvatar.CharacterOf(pf, roster), pf.FrameId);
            var title = ProfileRewards.Title(pf.TitleId);
            _badgeTitle.text = title.name;
            _badgeTitle.color = title.color;
            _badgeName.text = pf.DisplayName;
            _badgeLevel.text = $"Lv.{pf.Level}";
            bool max = pf.Level >= PlayerProfile.MaxLevel;
            _badgeExp.anchorMax = new Vector2(max ? 1f : Mathf.Clamp01(pf.Exp / (float)PlayerProfile.ExpForNext(pf.Level)), 1f);
        }

        /// <summary>대표 캐릭터 고르기 창(프로필의 '로비 설정'에서도 엶).</summary>
        public void OpenAssistantPicker() => OpenPicker();

        private void BuildProfileBadge(Transform root)
        {
            var hit = UIFactory.AddImage("ProfileBadge", root, new Color(0.04f, 0.05f, 0.09f, 0.62f), ProceduralSprites.Rounded, raycast: true);
            hit.type = Image.Type.Sliced; hit.preserveAspect = false;
            var rt = hit.rectTransform;
            UIFactory.Anchor(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            rt.sizeDelta = new Vector2(430, 124); rt.anchoredPosition = new Vector2(36, -22);
            var btn = hit.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.onClick.AddListener(() => ProfilePanel.Open(font, OpenAssistantPicker));
            hit.gameObject.AddComponent<ButtonFx>();

            _badgeAvatar = new ProfileAvatar(rt, 128);
            UIFactory.Anchor(_badgeAvatar.Rect, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            _badgeAvatar.Rect.anchoredPosition = new Vector2(-2, 0);

            _badgeTitle = UIFactory.AddText("Title", rt, "", 18, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            UIFactory.Anchor(_badgeTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            _badgeTitle.rectTransform.sizeDelta = new Vector2(290, 24); _badgeTitle.rectTransform.anchoredPosition = new Vector2(130, -12);
            _badgeName = UIFactory.AddText("Name", rt, "", 28, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            UIFactory.Anchor(_badgeName.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            _badgeName.rectTransform.sizeDelta = new Vector2(290, 38); _badgeName.rectTransform.anchoredPosition = new Vector2(130, -38);
            _badgeName.enableAutoSizing = true; _badgeName.fontSizeMin = 16; _badgeName.fontSizeMax = 28;
            _badgeLevel = UIFactory.AddText("Level", rt, "", 20, font, new Color(1f, 0.86f, 0.5f), TextAlignmentOptions.Left, FontStyles.Bold);
            UIFactory.Anchor(_badgeLevel.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            _badgeLevel.rectTransform.sizeDelta = new Vector2(80, 26); _badgeLevel.rectTransform.anchoredPosition = new Vector2(130, 14);

            var barBg = UIFactory.AddImage("ExpBg", rt, new Color(1, 1, 1, 0.16f), ProceduralSprites.Rounded);
            barBg.type = Image.Type.Sliced; barBg.preserveAspect = false; barBg.pixelsPerUnitMultiplier = 4f;
            UIFactory.Anchor(barBg.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            barBg.rectTransform.sizeDelta = new Vector2(200, 12); barBg.rectTransform.anchoredPosition = new Vector2(208, 21);
            var fill = UIFactory.AddImage("Fill", barBg.transform, new Color(1f, 0.82f, 0.4f), ProceduralSprites.Rounded);
            fill.type = Image.Type.Sliced; fill.preserveAspect = false; fill.pixelsPerUnitMultiplier = 4f;
            _badgeExp = fill.rectTransform;
            _badgeExp.anchorMin = Vector2.zero; _badgeExp.anchorMax = new Vector2(0f, 1f);
            _badgeExp.offsetMin = Vector2.zero; _badgeExp.offsetMax = Vector2.zero;

            PlayerProfile.Instance.OnChanged += RefreshProfileBadge;

            _profileBadge = NotifyBadge.Attach(rt, font);   // 도감 수집 보상

            PlayerArena.Instance.OnChanged += RefreshModeTiles;
            WorldRaid.Instance.OnChanged += RefreshModeTiles;
            LimitedEvent.Instance.OnChanged += RefreshModeTiles;
            PlayerGuild.Instance.OnChanged += RefreshModeTiles;
            PlayerSocial.Instance.OnChanged += RefreshModeTiles;
            PlayerMail.Instance.OnChanged += RefreshBadges;
            PlayerAttendance.Instance.OnChanged += RefreshBadges;
            ChapterStarRewards.OnChanged += RefreshBadges;
        }

        // ---------------- 대표 캐릭터 ----------------

        private List<CharacterData> OwnedCharacters()
        {
            var list = new List<CharacterData>();
            if (roster == null || roster.Database == null) return list;
            foreach (var o in roster.Owned)
            {
                var c = roster.Database.GetCharacter(o.id);
                if (c != null) list.Add(c);
            }
            // 성급(현재) 높은 순 → 초월 → 이름
            list.Sort((a, b) =>
            {
                int ga = Transcendence.GradeValue(roster.StarOf(a), roster.TranscendOf(a));
                int gb = Transcendence.GradeValue(roster.StarOf(b), roster.TranscendOf(b));
                if (ga != gb) return gb.CompareTo(ga);
                return string.CompareOrdinal(a.displayName, b.displayName);
            });
            return list;
        }

        /// <summary>대표 캐릭터: 고른 캐릭터(보유 중일 때) → 없으면 보유 중 가장 높은 성급.</summary>
        private CharacterData Assistant()
        {
            var owned = OwnedCharacters();
            if (owned.Count == 0) return null;
            string id = SaveStore.GetString(AssistantKey, "");
            foreach (var c in owned)
                if (!string.IsNullOrEmpty(id) && (c.id == id || c.name == id)) return c;
            return owned[0];
        }

        private void RefreshAssistant()
        {
            if (_assistant == null) return;
            var c = Assistant();
            if (c == _shown && c != null) return;
            _shown = c;
            RefreshBond();   // 이름표의 호감도 단계
            if (c == null)
            {
                Live2DUI.Hide(_assistant);
                _assistant.enabled = false;
                _assistName.text = "";
                return;
            }
            // 움직이는 일러스트(태생 6성 — 배경 포함 반복 영상 · Live2D)가 있으면 그대로 움직이게 두고,
            // 없는 캐릭터는 배경 없는 그림(Cutouts)을 세움(없으면 원래 일러스트)
            bool live = Live2DUI.Show(_assistant, c);
            var cutout = live ? null : GameArt.Cutout(c);
            var sprite = cutout != null ? cutout : GameArt.Portrait(c);
            // 배경이 있는 그림 · 영상은 가장자리를 흐려 로비에 녹임. 배경 없는 그림은 좌우 끝만 살짝
            if (_assistSoft != null) _assistSoft.softness = cutout != null ? new Vector2Int(40, 0) : new Vector2Int(170, 150);
            _assistant.sprite = sprite;
            _assistant.enabled = !live && sprite != null;
            _assistant.color = Color.white;
            int star = roster != null ? roster.StarOf(c) : (int)c.rarity;
            int tr = roster != null ? roster.TranscendOf(c) : 0;
            _assistStars.Set(star, tr);
            _assistName.text = $"<b>{c.displayName}</b>  <size=70%><color={Traits.Hex(c.element)}>{Traits.Name(c.element)}</color> · {Traits.Name(c.role)}</size>";
            if (_root.activeInHierarchy) StartCoroutine(Appear());
        }

        private IEnumerator Appear()
        {
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / 0.4f, 3f);
                _assistMask.anchoredPosition = new Vector2(Mathf.Lerp(-420f, -380f, k), 0f);
                SetAssistantAlpha(k);
                yield return null;
            }
            _assistMask.anchoredPosition = new Vector2(-380f, 0f);
            SetAssistantAlpha(1f);
        }

        private void SetAssistantAlpha(float a)
        {
            _assistant.color = new Color(1f, 1f, 1f, a);
            Live2DUI.SetAlpha(_assistant, a);
        }

        /// <summary>대표 캐릭터를 누르면: 톡 튀고 대표 대사 말풍선. 하루 3번까지 호감도 +10.</summary>
        private void PokeAssistant()
        {
            if (_shown == null) return;
            if (_bounceRoutine != null) StopCoroutine(_bounceRoutine);
            _bounceRoutine = StartCoroutine(Bounce());
            if (_bubbleRoutine != null) StopCoroutine(_bubbleRoutine);
            string line = CharacterLines.Random(_shown);   // 캐릭터별 로비 대사 중 무작위
            int got = PlayerBond.Instance.Touch(_shown);
            if (got > 0) line += $"\n<size=72%><color=#E0457F>호감도 +{got}</color>  <color=#8A8FA0>(오늘 {PlayerBond.Instance.TouchesLeft}번 남음)</color></size>";
            _bubbleRoutine = StartCoroutine(Bubble(line));
        }

        /// <summary>이름표의 호감도 단계 · 알림.</summary>
        private void RefreshBond()
        {
            if (_assistBond == null) return;
            var bond = PlayerBond.Instance;
            _assistBond.text = _shown != null ? $"호감도 <b>Lv.{bond.Level(_shown)}</b>" : "";
            if (_assistBondBadge != null) _assistBondBadge.Set(_shown != null && bond.HasNotice(_shown));
            RefreshCodexButton();
        }

        private IEnumerator Bounce()
        {
            for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.3f;
                _assistRt.localScale = new Vector3(1f + 0.02f * Mathf.Sin(k * Mathf.PI), 1f - 0.025f * Mathf.Sin(k * Mathf.PI * 2f), 1f);
                yield return null;
            }
            _assistRt.localScale = Vector3.one;
            _bounceRoutine = null;
        }

        private IEnumerator Bubble(string line)
        {
            _bubbleText.text = line;
            for (float t = 0; t < 0.2f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.2f;
                _bubbleGroup.alpha = k;
                _bubble.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, k);
                yield return null;
            }
            _bubbleGroup.alpha = 1f; _bubble.localScale = Vector3.one;
            for (float t = 0; t < 3.2f; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime) { _bubbleGroup.alpha = 1f - t / 0.35f; yield return null; }
            _bubbleGroup.alpha = 0f;
            _bubbleRoutine = null;
        }

        // ---------------- 대표 캐릭터 고르기 ----------------

        private void OpenPicker()
        {
            for (int i = _pickerGrid.childCount - 1; i >= 0; i--)
            {
                var ch = _pickerGrid.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
            var cur = Assistant();
            foreach (var c in OwnedCharacters()) PickerCell(c, c == cur);
            _picker.SetActive(true);
            _picker.transform.SetAsLastSibling();
        }

        private void PickerCell(CharacterData c, bool on)
        {
            int star = roster != null ? roster.StarOf(c) : (int)c.rarity;
            var col = StarText.ColorOf(star);
            var cell = UIFactory.AddImage("Cell", _pickerGrid, on ? new Color(1f, 0.9f, 0.5f, 1f) : new Color(col.r, col.g, col.b, 0.85f), null, raycast: true);
            var btn = cell.gameObject.AddComponent<Button>();
            btn.targetGraphic = cell;
            btn.onClick.AddListener(() =>
            {
                SaveStore.SetString(AssistantKey, !string.IsNullOrEmpty(c.id) ? c.id : c.name);
                SaveStore.Save();
                _picker.SetActive(false);
                _shown = null;
                RefreshAssistant();
                PokeAssistant();
            });
            cell.gameObject.AddComponent<ButtonFx>();
            var inner = UIFactory.AddImage("Inner", cell.transform, new Color(0.1f, 0.1f, 0.15f, 1f));
            UIFactory.Stretch(inner.rectTransform, on ? 5 : 3, on ? 5 : 3, on ? 5 : 3, on ? 5 : 3);
            var th = GameArt.Thumb(c);
            if (th != null)
            {
                var img = UIFactory.AddImage("Thumb", inner.transform, Color.white, th);
                img.preserveAspect = true;
                UIFactory.Stretch(img.rectTransform, 4, 46, 4, 4);
            }
            var stars = StarRow.Create(inner.transform, 17, TextAnchor.MiddleCenter, font);
            UIFactory.Anchor(stars.Rect, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            stars.Rect.sizeDelta = new Vector2(0, 18); stars.Rect.anchoredPosition = new Vector2(0, 24);
            stars.Set(star, roster != null ? roster.TranscendOf(c) : 0);
            var name = UIFactory.AddText("Name", inner.transform, c.displayName, 17, font, Color.white, TextAlignmentOptions.Bottom, FontStyles.Bold);
            name.enableAutoSizing = true; name.fontSizeMin = 11; name.fontSizeMax = 17;
            UIFactory.Anchor(name.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            name.rectTransform.sizeDelta = new Vector2(-6, 22); name.rectTransform.anchoredPosition = new Vector2(0, 2);
            if (on)
            {
                var tag = UIFactory.AddText("Now", inner.transform, "대표", 16, font, new Color(0.2f, 0.12f, 0.02f), TextAlignmentOptions.Center, FontStyles.Bold);
                var bg = UIFactory.AddImage("TagBg", inner.transform, new Color(1f, 0.85f, 0.4f, 1f), ProceduralSprites.Rounded);
                bg.type = Image.Type.Sliced; bg.preserveAspect = false;
                UIFactory.Anchor(bg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
                bg.rectTransform.sizeDelta = new Vector2(56, 26); bg.rectTransform.anchoredPosition = new Vector2(6, -6);
                tag.transform.SetParent(bg.transform, false);
                UIFactory.Stretch(tag.rectTransform);
            }
        }

        // ---------------- 메뉴 ----------------

        /// <summary>도감 버튼: 수집 현황 + 받을 수 있는 수집 보상 수.</summary>
        private void RefreshCodexButton()
        {
            // 도감은 프로필에서 — 받을 수집 보상 · 호감도 보상(새 개인 스토리)이 있으면 프로필 배지에 점
            if (_profileBadge != null)
                _profileBadge.Set((_codex != null && _codex.ClaimableCount() > 0) || PlayerBond.Instance.NoticeCount(roster) > 0);
            if (_menuCodexBadge != null) RefreshBadges();
            if (_rosterText == null) return;
            int owned = roster != null ? roster.Owned.Count : 0;
            string count = _codex != null && _codex.Total > 0 ? $"{_codex.OwnedCount} / {_codex.Total}" : $"{owned}명";
            int n = _codex != null ? _codex.ClaimableCount() : 0;
            _rosterText.text = "도감" + (n > 0 ? $"  <color=#FFE08A><size=70%>●{n}</size></color>" : "") + $"\n<size=55%>수집 {count}</size>";
        }

        /// <summary>임무 타일: 받을 보상 수 + 빨간 느낌표.</summary>
        private void RefreshMissionBadge()
        {
            int n = (_missions != null ? _missions.ClaimableCount() : 0) + PlayerAchievements.Instance.ClaimableCount();
            if (_missionBadge != null) _missionBadge.Set(n > 0);
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
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 어떤 화면비에서도 1920×1080 설계 영역이 전부 보이게(남는 쪽은 여백)
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.07f, 0.06f, 0.11f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            BackgroundArt.Create(_root.transform, 0.25f).Set("Backgrounds/home");

            // 오른쪽 메뉴 뒤를 살짝 어둡게(그림 위 글씨가 잘 보이게) — 화면 오른쪽 끝까지
            var shade = UIFactory.AddImage("RightShade", _root.transform, new Color(0, 0, 0, 0.38f), ProceduralSprites.Glow);
            shade.preserveAspect = false;
            UIFactory.Anchor(shade.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f));
            shade.rectTransform.sizeDelta = new Vector2(1700, 400); shade.rectTransform.anchoredPosition = new Vector2(500, 0);

            var root = UIFactory.Frame(_root.transform);

            // 대표 캐릭터(왼쪽, 아래에 붙여 크게) — 누르면 대사
            // 배경 없는 일러스트(Cutouts)를 세움. 없어서 배경 있는 직사각형 일러스트를 쓸 때는 가장자리를 부드럽게 지워 로비 배경에 녹임(RectMask2D softness)
            _assistMask = UIFactory.NewRect("AssistantMask", root);
            UIFactory.Anchor(_assistMask, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f));
            _assistMask.sizeDelta = new Vector2(820, 0); _assistMask.anchoredPosition = new Vector2(-380, 0);
            _assistSoft = _assistMask.gameObject.AddComponent<RectMask2D>();
            _assistSoft.softness = new Vector2Int(170, 150);
            _assistMask.SetAsFirstSibling();

            // 뒤쪽 은은한 빛(캐릭터가 배경에서 떠 보이게)
            var halo = UIFactory.AddImage("Halo", root, new Color(1f, 0.92f, 0.78f, 0.22f), ProceduralSprites.Glow);
            halo.preserveAspect = false;
            UIFactory.Anchor(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            halo.rectTransform.sizeDelta = new Vector2(1100, 1100); halo.rectTransform.anchoredPosition = new Vector2(-380, 40);
            halo.transform.SetAsFirstSibling();

            _assistant = UIFactory.AddImage("Assistant", _assistMask, Color.white);
            _assistant.preserveAspect = true;
            _assistRt = _assistant.rectTransform;
            UIFactory.Stretch(_assistRt, 0, -40, 0, 30);   // 아래는 화면 밖까지(발끝이 잘려도 자연스럽게)
            // 누르는 영역은 따로(움직이는 일러스트를 띄우면 정적 Image가 꺼져 클릭을 못 받으므로)
            var hit = UIFactory.AddImage("Hit", _assistRt, new Color(0, 0, 0, 0), null, raycast: true);
            UIFactory.Stretch(hit.rectTransform, 160, 0, 160, 80);
            var poke = hit.gameObject.AddComponent<Button>();
            poke.transition = Selectable.Transition.None;
            poke.onClick.AddListener(PokeAssistant);

            // 말풍선(캐릭터 오른쪽 위)
            var bubble = UIFactory.AddImage("Bubble", root, new Color(1f, 1f, 1f, 0.95f), ProceduralSprites.Rounded);
            bubble.type = Image.Type.Sliced; bubble.preserveAspect = false; bubble.pixelsPerUnitMultiplier = 1.6f;
            _bubble = bubble.rectTransform;
            UIFactory.Anchor(_bubble, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f));
            _bubble.sizeDelta = new Vector2(520, 130); _bubble.anchoredPosition = new Vector2(-250, 250);
            _bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();
            _bubbleGroup.alpha = 0f; _bubbleGroup.blocksRaycasts = false;
            _bubbleText = UIFactory.AddText("T", _bubble, "", 24, font, new Color(0.15f, 0.13f, 0.2f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_bubbleText.rectTransform, 24, 14, 24, 14);
            _bubbleText.enableAutoSizing = true; _bubbleText.fontSizeMin = 15; _bubbleText.fontSizeMax = 24;

            // 프로필 배지(왼쪽 위) — 게임 이름은 타이틀 화면(TitleScreen)에서만 보여 줌
            BuildProfileBadge(root);

            // 대표 캐릭터 이름(왼쪽 아래)

            var nameBg = UIFactory.AddImage("NameBg", root, new Color(0.04f, 0.05f, 0.09f, 0.72f), ProceduralSprites.Rounded);
            nameBg.type = Image.Type.Sliced; nameBg.preserveAspect = false;
            UIFactory.Anchor(nameBg.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            nameBg.rectTransform.sizeDelta = new Vector2(440, 104); nameBg.rectTransform.anchoredPosition = new Vector2(50, 40);
            // 이름표를 누르면 호감도 창
            nameBg.raycastTarget = true;
            var nameBtn = nameBg.gameObject.AddComponent<Button>();
            nameBtn.transition = Selectable.Transition.None;
            nameBtn.onClick.AddListener(() => { if (_shown != null) BondWindow.Open(_shown, font); });
            nameBg.gameObject.AddComponent<ButtonFx>();
            _assistBond = UIFactory.AddText("Bond", nameBg.transform, "", 20, font, new Color(1f, 0.62f, 0.75f), TextAlignmentOptions.Right);
            _assistBond.richText = true;
            UIFactory.Anchor(_assistBond.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            _assistBond.rectTransform.sizeDelta = new Vector2(200, 32); _assistBond.rectTransform.anchoredPosition = new Vector2(-20, -12);
            _assistBondBadge = NotifyBadge.Attach(nameBg, font, 6, 6, 28);
            _assistStars = StarRow.Create(nameBg.transform, 26, TextAnchor.MiddleLeft, font);
            UIFactory.Anchor(_assistStars.Rect, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1));
            _assistStars.Rect.sizeDelta = new Vector2(-40, 32); _assistStars.Rect.anchoredPosition = new Vector2(20, -12);
            _assistName = UIFactory.AddText("Name", nameBg.transform, "", 30, font, Color.white, TextAlignmentOptions.Left);
            _assistName.richText = true;
            UIFactory.Anchor(_assistName.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0));
            _assistName.rectTransform.sizeDelta = new Vector2(-40, 50); _assistName.rectTransform.anchoredPosition = new Vector2(22, 8);
            _assistName.enableAutoSizing = true; _assistName.fontSizeMin = 16; _assistName.fontSizeMax = 30;

            BuildEventBanner(root);
            BuildMiniChat(root);

            // 재화(오른쪽 위): 크리스탈 · 금화 알약
            CurrencyBar.Create(root, font, wallet, new Vector2(-50, -36), Currency.Crystal, Currency.Gold);

            // 위쪽 아이콘(재화 왼쪽): 메뉴(시즌 패스 · 도감 · 프로필 · 설정) · 우편 · 출석
            float iconX = -50 - 2 * CurrencyBar.PillW - CurrencyBar.Gap - 14;
            var menuBtn = IconButton(root, "Items/UiMenu", "메뉴", new Vector2(iconX, -30), ToggleMenu);
            var mailBtn = IconButton(root, "Items/UiMail", "우편", new Vector2(iconX - 78, -30), () => MailWindow.Open(font));
            var attend = IconButton(root, "Items/UiAttendance", "출석", new Vector2(iconX - 156, -30), () => AttendanceWindow.Open(font));
            _menuBadge = NotifyBadge.Attach(menuBtn, font, 8, 8, 28);
            _mailBadge = NotifyBadge.Attach(mailBtn, font, 8, 8, 28);
            _attendBadge = NotifyBadge.Attach(attend, font, 8, 8, 28);

            // 오른쪽: 출격(크게) · 소환 — 콘텐츠는 출격 화면에 모아 둠
            _sortieBtn = BigButton(root, "Sortie", "Backgrounds/stage", "Items/UiSortie", new Color(0.95f, 0.62f, 0.3f, 1f),
                new Vector2(620, 180), new Vector2(600, 400), 76, out _sortieTitle, out _sortieSub, () => OpenModeHub(true));
            _sortieTitle.text = "출격";
            _sortieBadge = NotifyBadge.Attach(_sortieBtn, font, 10, 10, 40);
            _summonBtn = BigButton(root, "Summon", "Backgrounds/gacha", "Items/UiSummon", new Color(1f, 0.82f, 0.4f, 1f),
                new Vector2(620, -155), new Vector2(600, 230), 58, out var summonTitle, out _summonSub, OpenSummon);
            summonTitle.text = "소환";
            BuildSummonArt();
            BuildSummonPager();

            BuildDock(root);
            BuildMenuPopup(iconX);
            BuildModeHub();

            PlayerDispatch.Instance.OnChanged += RefreshModeTiles;
            PlayerTower.Instance.OnChanged += RefreshModeTiles;

            // 튜토리얼이 가리킬 곳(출격 화면이 열려 있으면 스테이지 카드)
            TutorialTargets.Set("home.stage", _sortieBtn);
            TutorialTargets.Set("home.growth", _growthDock);

            BuildPicker();
        }

        private TextMeshProUGUI _eventLabel;
        private Image _eventBg;
        private BackgroundArt _eventArt;
        private NotifyBadge _eventBadge;

        /// <summary>기간 한정 이벤트 배너(왼쪽, 대표 캐릭터 이름 위).</summary>
        private TextMeshProUGUI _miniChat;
        private NotifyBadge _chatBadge;

        /// <summary>미니 채팅(왼쪽 아래 이벤트 배너 위): 최근 전체 채팅 한 줄 · 안 읽은 1:1이 있으면 빨간 점 · 누르면 채팅 창.</summary>
        private void BuildMiniChat(Transform root)
        {
            var bg = UIFactory.AddImage("MiniChat", root, new Color(0.06f, 0.08f, 0.14f, 0.82f), ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            var rt = bg.rectTransform;
            UIFactory.Anchor(rt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            rt.sizeDelta = new Vector2(440, 60); rt.anchoredPosition = new Vector2(50, 272);
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(() => ChatWindow.Open(font));
            bg.gameObject.AddComponent<ButtonFx>();
            var tag = UIFactory.AddText("Tag", rt, "<b>채팅</b>", 20, font, new Color(0.6f, 0.85f, 1f), TextAlignmentOptions.Center);
            tag.richText = true;
            UIFactory.Anchor(tag.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f));
            tag.rectTransform.sizeDelta = new Vector2(70, 0); tag.rectTransform.anchoredPosition = new Vector2(8, 0);
            _miniChat = UIFactory.AddText("Last", rt, "", 18, font, new Color(0.88f, 0.9f, 0.96f), TextAlignmentOptions.Left);
            _miniChat.richText = true;
            _miniChat.overflowMode = TextOverflowModes.Ellipsis;
            _miniChat.textWrappingMode = TextWrappingModes.NoWrap;
            UIFactory.Stretch(_miniChat.rectTransform, 82, 6, 16, 6);
            _chatBadge = NotifyBadge.Attach(b, font, 4, 4, 18);
            ChatService.Instance.OnChanged += RefreshMiniChat;
            RefreshMiniChat();
        }

        private void RefreshMiniChat()
        {
            if (_miniChat == null) return;
            var chat = ChatService.Instance;
            string why = chat.CannotSend();
            if (chat.World.Count > 0)
            {
                var m = chat.World[chat.World.Count - 1];
                _miniChat.text = $"<color=#FFD98A>{m.name}</color>  {m.text.Replace("<", "‹")}";
            }
            else _miniChat.text = $"<color=#9AA3B5>{why ?? "전체 채팅에 인사를 남겨 보세요"}</color>";
            if (_chatBadge != null) _chatBadge.Set(chat.UnreadDirect > 0);
        }

        private void BuildEventBanner(Transform root)
        {
            _eventBg = UIFactory.AddImage("EventBanner", root, new Color(0.3f, 0.15f, 0.1f, 0.88f), ProceduralSprites.Rounded, raycast: true);
            _eventBg.type = Image.Type.Sliced; _eventBg.preserveAspect = false;
            var rt = _eventBg.rectTransform;
            UIFactory.Anchor(rt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            rt.sizeDelta = new Vector2(440, 104); rt.anchoredPosition = new Vector2(50, 156);
            var b = _eventBg.gameObject.AddComponent<Button>();
            b.targetGraphic = _eventBg;
            b.onClick.AddListener(() => Nav(HubScreen.Event));
            _eventBg.gameObject.AddComponent<ButtonFx>();
            // 이벤트 배경 그림을 배너에 맞춰 잘라 깖(없으면 단색)
            var theme = LimitedEvent.CurrentTheme;
            _eventArt = BackgroundArt.Create(rt, 0.42f);
            _eventArt.Set(theme.ArtBackground);
            var artClip = rt.Find("BackgroundArt") as RectTransform;
            if (artClip != null) UIFactory.Stretch(artClip, 3, 3, 3, 3);   // 테마 색 테두리가 보이게 살짝 안쪽으로
            var icon = GameArt.Load(theme.ArtEmblem);
            if (icon == null) icon = GameArt.Load(LimitedEvent.CoinIcon);
            float textLeft = 22;
            if (icon != null)
            {
                var im = UIFactory.AddImage("Icon", rt, Color.white, icon);
                im.preserveAspect = true;
                UIFactory.Anchor(im.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                im.rectTransform.sizeDelta = new Vector2(76, 76); im.rectTransform.anchoredPosition = new Vector2(14, 0);
                textLeft = 100;
            }
            _eventLabel = UIFactory.AddText("Label", rt, "", 28, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            _eventLabel.richText = true;
            _eventLabel.lineSpacing = -4f;
            UIFactory.Stretch(_eventLabel.rectTransform, textLeft, 8, 16, 8);
            _eventLabel.enableAutoSizing = true; _eventLabel.fontSizeMin = 16; _eventLabel.fontSizeMax = 28;
            _eventBadge = NotifyBadge.Attach(rt, font);
        }

        private void RefreshEventBanner()
        {
            if (_eventLabel == null) return;
            var theme = LimitedEvent.CurrentTheme;
            bool open = FeatureGate.EventOpen;
            var c = theme.color;
            bool art = _eventArt != null && _eventArt.HasImage;
            // 그림이 깔려 있으면 바탕은 테두리 색 역할만(잠겨 있으면 어둡게)
            _eventBg.color = !open ? new Color(0.2f, 0.21f, 0.26f, art ? 1f : 0.88f)
                : art ? new Color(c.r, c.g, c.b, 1f) : new Color(c.r * 0.45f, c.g * 0.3f, c.b * 0.3f, 0.9f);
            var t = LimitedEvent.Remaining;
            string hex = "#" + ColorUtility.ToHtmlStringRGB(c);
            _eventLabel.text = $"<size=60%><color=#FFE08A>기간 한정 이벤트</color></size>\n<color={hex}>{theme.name}</color>  " +
                               (open ? $"<size=55%><color=#E8E8F0>남은 {(t.Days > 0 ? $"{t.Days}일" : $"{t.Hours}시간")}</color></size>"
                                     : $"<size=55%><color=#FFB3B3>{FeatureGate.LockText(FeatureGate.EventChapter)}</color></size>");
            var ev = LimitedEvent.Instance;
            _eventHas = open && (ev.ClaimableCount > 0 || ev.AttemptsLeft == LimitedEvent.AttemptsPerDay);
            _eventBadge.Set(_eventHas);
            if (_eventCard != null)
            {
                _eventCard.SetLocked(!open, FeatureGate.LockText(FeatureGate.EventChapter));
                if (open) _eventCard.Status.text = $"남은 <color=#FFE08A>{(t.Days > 0 ? $"{t.Days}일" : $"{t.Hours}시간")}</color> · 오늘 {ev.AttemptsLeft}/{LimitedEvent.AttemptsPerDay}회";
            }
        }

        private NotifyBadge _profileBadge, _missionBadge, _guildBadge, _friendBadge, _mailBadge, _attendBadge;
        private float _dispatchTick;
        private int _dispatchDone = -1;

        private void Update()
        {
            // 파견은 시간이 지나면 끝나므로 홈에 있는 동안 가끔 확인(끝나면 느낌표)
            if (_root == null || !_root.activeSelf) return;
            // 소환 버튼: 가만히 두면 7초마다 다음 배너(끄는 중 · 출격 화면이 덮고 있을 때는 멈춤)
            if (_summonSwipe != null && !_summonSwipe.Dragging && (_hub == null || !_hub.activeSelf))
            {
                _summonAuto -= Time.unscaledDeltaTime;
                if (_summonAuto <= 0f) { if (SummonBanners().Count > 1) ShowSummon(_summonIndex + 1); else _summonAuto = 7f; }
            }
            _dispatchTick -= Time.unscaledDeltaTime;
            if (_dispatchTick > 0f) return;
            _dispatchTick = 5f;
            int done = PlayerDispatch.Instance.DoneCount;
            if (done != _dispatchDone) { _dispatchDone = done; RefreshModeTiles(); }
        }
        private StageScreen _stageScreen;
        private StageProgress _stageProgress;

        /// <summary>빨간 느낌표 갱신: 받지 않은 보상이 있으면 켜고, 받으면 끔.</summary>
        private void RefreshBadges()
        {
            if (_mailBadge == null) return;
            _mailBadge.Set(PlayerMail.Instance.UnclaimedCount > 0);
            _attendBadge.Set(PlayerAttendance.Instance.CanClaimAny);
            bool pass = SeasonPass.Instance.ClaimableCount() > 0;
            bool codex = _codex != null && _codex.ClaimableCount() > 0;
            if (_menuPassBadge != null) _menuPassBadge.Set(pass);
            if (_menuCodexBadge != null) _menuCodexBadge.Set(codex);
            if (_menuBadge != null) _menuBadge.Set(pass || codex);
            _missionBadge.Set((_missions != null && _missions.ClaimableCount() > 0) || PlayerAchievements.Instance.ClaimableCount() > 0);
            _guildBadge.Set(PlayerGuild.Instance.BadgeCount > 0);
            _friendBadge.Set(PlayerSocial.Instance.BadgeCount > 0);

            // 출격 화면 카드 → 홈의 출격 버튼 하나로 묶어서
            if (_stageScreen == null) _stageScreen = FindAnyObjectByType<StageScreen>();
            if (_stageProgress == null) _stageProgress = FindAnyObjectByType<StageProgress>();
            int stars = _stageScreen != null ? ChapterStarRewards.ClaimableTotal(_stageScreen.Database, _stageProgress) : 0;
            bool raid = FeatureGate.RaidOpen && (WorldRaid.Instance.ClaimableCount > 0 || ServerRaid.Instance.ClaimableCount > 0);
            if (_stageCard != null) _stageCard.Badge.Set(stars > 0);
            if (_raidCard != null) _raidCard.Badge.Set(raid);
            if (_dispatchCard != null) _dispatchCard.Badge.Set(_dispatchHas);
            if (_eventCard != null) _eventCard.Badge.Set(_eventHas);
            int todo = (stars > 0 ? 1 : 0) + (raid ? 1 : 0) + (_dispatchHas ? 1 : 0) + (_eventHas ? 1 : 0);
            if (_sortieBadge != null) _sortieBadge.Set(todo > 0);
            if (_sortieSub != null)
            {
                int cleared = FeatureGate.ClearedChapters();
                string story = cleared > 0 ? $"스토리 {cleared}장 클리어" : "스토리 · 결투장 · 레이드 · 탑 · 파견";
                _sortieSub.text = todo > 0 ? $"{story}  <color=#FFD36B>· 받을 보상 {todo}곳</color>" : story;
            }
        }

        /// <summary>출격 화면 카드 문구 · 잠금 · 빨간 느낌표 + 홈의 출격 버튼 요약.</summary>
        private void RefreshModeTiles()
        {
            int cleared = FeatureGate.ClearedChapters();
            if (_stageCard != null)
                _stageCard.Status.text = cleared > 0 ? $"<color=#FFE08A>{cleared}장</color> 클리어 · 특수 던전" : "첫 전투를 시작해 보세요";
            if (_arenaCard != null)
            {
                bool open = cleared >= FeatureGate.ArenaChapter;
                _arenaCard.SetLocked(!open, FeatureGate.LockText(FeatureGate.ArenaChapter));
                if (open)
                {
                    var a = PlayerArena.Instance;
                    var (tier, _) = a.MyTier;
                    _arenaCard.Status.text = $"{tier} · {a.MyRank}위 · 도전권 <color=#FFE08A>{a.Tickets}/{PlayerArena.MaxTickets}</color>";
                }
            }
            if (_raidCard != null)
            {
                bool open = cleared >= FeatureGate.RaidChapter;
                _raidCard.SetLocked(!open, FeatureGate.LockText(FeatureGate.RaidChapter));
                if (open)
                {
                    var r = WorldRaid.Instance;
                    _raidCard.Status.text = $"주간 <color=#FFE08A>{r.AttemptsLeft}/{WorldRaid.AttemptsPerDay}</color> · 월드 <color=#FFE08A>{ServerRaid.Instance.AttemptsLeft}/{ServerRaid.AttemptsPerDay}</color>";
                }
            }
            _dispatchHas = false;
            if (_dispatchCard != null)
            {
                bool open = cleared >= FeatureGate.DispatchChapter;
                _dispatchCard.SetLocked(!open, FeatureGate.LockText(FeatureGate.DispatchChapter));
                var dp = PlayerDispatch.Instance;
                int done = dp.DoneCount;
                _dispatchHas = open && done > 0;
                if (open) _dispatchCard.Status.text = done > 0 ? $"<color=#7CFF9A>완료 {done}팀 · 받기</color>" : $"진행 {dp.Actives.Count}/{PlayerDispatch.MaxActive}팀";
            }
            if (_towerCard != null)
            {
                bool open = cleared >= FeatureGate.TowerChapter;
                _towerCard.SetLocked(!open, FeatureGate.LockText(FeatureGate.TowerChapter));
                if (open) _towerCard.Status.text = $"최고 <color=#FFE08A>{PlayerTower.Instance.Cleared}층</color>";
            }
            RefreshEventBanner();
            RefreshBadges();
            RefreshSummon();
        }

        /// <summary>홈 소환 버튼: 대표 픽업 캐릭터 그림 + 이름.</summary>
        private void RefreshSummon()
        {
            if (_summonSub == null) return;
            var list = SummonBanners();
            if (list.Count > 0) _summonIndex = ((_summonIndex % list.Count) + list.Count) % list.Count;
            var b = list.Count > 0 ? list[_summonIndex] : null;
            CharacterData c = null;
            if (b != null) c = b.IsPickup ? b.featured6 : (b.pool6 != null ? b.pool6.Find(x => x != null) : null);
            _summonSub.text = b == null ? "캐릭터 소환"
                : b.IsPickup ? $"<color=#FFE08A>픽업</color>  {(c != null ? c.displayName : b.bannerName)}"
                : "<color=#9AD4FF>상시</color>  언제나 만날 수 있는 동료";
            bool many = list.Count > 1;
            if (_summonPrev != null) { _summonPrev.gameObject.SetActive(many); _summonNext.gameObject.SetActive(many); _summonDots.gameObject.SetActive(many); }
            if (many && _summonDots != null)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < list.Count; i++) sb.Append(i == _summonIndex ? "<color=#FFD36B>●</color> " : "<color=#8A90A0>●</color> ");
                _summonDots.text = sb.ToString().TrimEnd();
            }
            if (_summonArt == null) return;
            if (c == _summonShown && _summonArt.sprite != null) return;
            _summonShown = c;
            // 소환(뽑기) 미리보기는 항상 기본 의상(개화 · 스킨을 미리 보여 주지 않게)
            var sp = c != null ? (GameArt.Outfitted("Cutouts/", !string.IsNullOrEmpty(c.id) ? c.id : c.name, "") ?? GameArt.Portrait(c, "")) : null;
            _summonArt.sprite = sp;
            _summonArt.enabled = sp != null;
            if (sp != null)
            {
                _summonFit.aspectRatio = sp.rect.width / Mathf.Max(1f, sp.rect.height);
                // 그림마다 머리 위 빈 공간이 달라(세라핀 18% · 시엘 1%) 투명한 윗부분만큼 끌어올려 머리 높이를 맞춤
                float h = 330f / _summonFit.aspectRatio;
                _summonY = 24f + TopGap(sp) * h;
                _summonArt.rectTransform.anchoredPosition = new Vector2(0, _summonY);
            }
        }

        /// <summary>오른쪽 위 작은 아이콘 버튼(아이콘 + 아래 이름).</summary>
        private RectTransform IconButton(Transform parent, string icon, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            // 바탕 없이 아이콘 + 글자만(누르는 영역은 투명 이미지). 배경 그림 위에서도 보이게 그림자
            var bg = UIFactory.AddImage(label, parent, new Color(0, 0, 0, 0), null, raycast: true);
            var rt = bg.rectTransform;
            UIFactory.Anchor(rt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            rt.sizeDelta = new Vector2(68, 72); rt.anchoredPosition = pos;
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(onClick);
            bg.gameObject.AddComponent<ButtonFx>();
            var sp = GameArt.Load(icon);
            var img = UIFactory.AddImage("Icon", rt, Color.white, sp);
            img.preserveAspect = true;
            UIFactory.Stretch(img.rectTransform, 4, 18, 4, -2);
            img.enabled = sp != null;
            var ish = img.gameObject.AddComponent<Shadow>();
            ish.effectColor = new Color(0, 0, 0, 0.55f); ish.effectDistance = new Vector2(0, -2);
            var t = UIFactory.AddText("Label", rt, label, 16, font, Color.white, TextAlignmentOptions.Bottom, FontStyles.Bold);
            UIFactory.Stretch(t.rectTransform, -6, -2, -6, 0);
            SubcultureRPG.UI.TextOutline.Apply(t, 0.22f, new Color32(10, 12, 20, 230));
            return rt;
        }

        // ---------------- 출격 · 소환 · 아래 메뉴 바 · 메뉴 ----------------

        private Button _sortieBtn, _summonBtn;
        private TextMeshProUGUI _sortieTitle, _sortieSub, _summonSub;
        private NotifyBadge _sortieBadge, _menuBadge, _menuPassBadge, _menuCodexBadge;
        private Image _summonArt;
        private AspectRatioFitter _summonFit;
        private CharacterData _summonShown;
        private RectTransform _growthDock;
        private GameObject _menuPop, _hub;
        private CanvasGroup _hubGroup;
        private bool _dispatchHas, _eventHas;
        private ModeCard _stageCard, _arenaCard, _raidCard, _towerCard, _dispatchCard, _eventCard;

        /// <summary>출격 화면의 콘텐츠 카드 1장.</summary>
        private sealed class ModeCard
        {
            public Button Btn;
            public TextMeshProUGUI Title, Status, LockText;
            public NotifyBadge Badge;
            public GameObject Lock;
            public BackgroundArt Art;

            /// <summary>잠기면 어둡게 덮고 여는 조건만 보여 줌(눌러도 내비게이터가 안내 창을 띄움).</summary>
            public void SetLocked(bool locked, string why)
            {
                if (Lock.activeSelf != locked) Lock.SetActive(locked);
                if (locked) { LockText.text = $"<size=120%><b>잠김</b></size>\n<color=#FFB3B3>{why}</color>"; Status.text = ""; Badge.Set(false); }
            }
        }

        /// <summary>아래에서 위로 옅어지는 검은 막(카드 위 글씨가 그림에 묻히지 않게).</summary>
        private static Sprite s_fade;
        private static Sprite Fade()
        {
            if (s_fade != null) return s_fade;
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++) { float k = 1f - y / 63f; tex.SetPixel(0, y, new Color(1f, 1f, 1f, k * k * (3f - 2f * k))); }
            tex.Apply();
            return s_fade = Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
        }

        private void AddFade(RectTransform rt, float height, float alpha)
        {
            var shade = UIFactory.AddImage("Shade", rt, new Color(0f, 0f, 0f, alpha), Fade());
            shade.preserveAspect = false; shade.raycastTarget = false;
            UIFactory.Anchor(shade.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            shade.rectTransform.sizeDelta = new Vector2(-8, height); shade.rectTransform.anchoredPosition = new Vector2(0, 4);
        }

        private TextMeshProUGUI CardText(RectTransform rt, string name, float size, Color color, float left, float bottom, float height, bool bold)
        {
            var t = UIFactory.AddText(name, rt, "", size, font, color, TextAlignmentOptions.BottomLeft, bold ? FontStyles.Bold : FontStyles.Normal);
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = size * 0.55f;
            UIFactory.Anchor(t.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0));
            t.rectTransform.sizeDelta = new Vector2(-left - 24, height); t.rectTransform.anchoredPosition = new Vector2(left, bottom);
            SubcultureRPG.UI.TextOutline.Apply(t, bold ? 0.2f : 0.15f, new Color32(8, 10, 18, 235));
            return t;
        }

        /// <summary>그림이 깔린 큰 버튼(출격 · 소환): 왼쪽 아래 아이콘 + 이름 + 한 줄 상태.</summary>
        private Button BigButton(Transform root, string name, string art, string icon, Color accent, Vector2 pos, Vector2 size, float titleSize,
            out TextMeshProUGUI title, out TextMeshProUGUI sub, UnityEngine.Events.UnityAction onClick)
        {
            var bg = UIFactory.AddImage(name, root, accent, ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            var rt = bg.rectTransform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(onClick);
            bg.gameObject.AddComponent<ButtonFx>();
            BackgroundArt.Create(rt, 0.12f).Set(art);
            if (rt.Find("BackgroundArt") is RectTransform clip) UIFactory.Stretch(clip, 4, 4, 4, 4);   // 테두리 색이 보이게 살짝 안쪽
            AddFade(rt, size.y * 0.66f, 0.88f);
            float iconSize = Mathf.Min(140f, size.y * 0.5f);
            var sp = GameArt.Load(icon);
            float left = 30f;
            if (sp != null)
            {
                var im = UIFactory.AddImage("Icon", rt, Color.white, sp);
                im.preserveAspect = true; im.raycastTarget = false;
                UIFactory.Anchor(im.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
                im.rectTransform.sizeDelta = new Vector2(iconSize, iconSize); im.rectTransform.anchoredPosition = new Vector2(20, 18);
                var sh = im.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.6f); sh.effectDistance = new Vector2(0, -3);
                left = iconSize + 32f;
            }
            title = CardText(rt, "Title", titleSize, Color.white, left, 56, titleSize * 1.15f, true);
            sub = CardText(rt, "Sub", 26, new Color(0.9f, 0.92f, 0.97f), left + 2, 20, 36, false);
            return btn;
        }

        // 소환 버튼은 배너를 넘겨 봄: 픽업(여러 개) → 상시. 누르면 지금 보이는 배너의 뽑기 화면으로
        private int _summonIndex;
        private float _summonAuto;
        private Button _summonPrev, _summonNext;
        private TextMeshProUGUI _summonDots;
        private SwipeArea _summonSwipe;

        private List<GachaBanner> SummonBanners()
        {
            var list = new List<GachaBanner>();
            if (gacha == null) return list;
            foreach (var b in gacha.Banners) if (b != null && b.IsPickup) list.Add(b);
            foreach (var b in gacha.Banners) if (b != null && !b.IsPickup) list.Add(b);
            return list;
        }

        private void ShowSummon(int index)
        {
            _summonIndex = index;
            _summonAuto = 7f;
            RefreshSummon();
            if (_summonArt != null && _summonArt.enabled && _root.activeInHierarchy)
            {
                if (_summonFade != null) StopCoroutine(_summonFade);
                _summonFade = StartCoroutine(SummonFade());
            }
        }

        private Coroutine _summonFade;
        private float _summonY = 24f;

        /// <summary>그림 위쪽 투명한 부분의 비율(0 ~ 1). 'Tight' 스프라이트는 꼭짓점이 보이는 부분만 감싸므로 그 맨 위로 계산(픽셀을 읽지 않음).</summary>
        private static float TopGap(Sprite sp)
        {
            if (sp == null) return 0f;
            var v = sp.vertices;
            if (v == null || v.Length == 0) return 0f;
            float maxY = float.MinValue;
            foreach (var p in v) if (p.y > maxY) maxY = p.y;
            float ppu = sp.pixelsPerUnit, fullH = sp.rect.height / ppu;
            float top = (sp.rect.height - sp.pivot.y) / ppu;   // 피벗 기준 그림 맨 위
            return Mathf.Clamp((top - maxY) / Mathf.Max(0.0001f, fullH), 0f, 0.4f);
        }
        private IEnumerator SummonFade()
        {
            var rt = _summonArt.rectTransform;
            for (float t = 0; t < 0.22f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.22f;
                _summonArt.color = new Color(1, 1, 1, k);
                rt.anchoredPosition = new Vector2(Mathf.Lerp(26f, 0f, k), _summonY);
                yield return null;
            }
            _summonArt.color = Color.white; rt.anchoredPosition = new Vector2(0, _summonY);
            _summonFade = null;
        }

        /// <summary>지금 보이는 배너를 골라 둔 채로 뽑기 화면으로.</summary>
        private void OpenSummon()
        {
            if (_summonSwipe != null && _summonSwipe.Dragging) return;   // 넘기려고 끈 것은 누른 것이 아님
            var list = SummonBanners();
            if (gacha != null && list.Count > 0) gacha.Select(list[Mathf.Clamp(_summonIndex, 0, list.Count - 1)]);
            Nav(HubScreen.Gacha);
        }

        private void BuildSummonPager()
        {
            var rt = (RectTransform)_summonBtn.transform;
            _summonSwipe = _summonBtn.gameObject.AddComponent<SwipeArea>();
            _summonSwipe.OnSwipe = dir => ShowSummon(_summonIndex + dir);
            _summonPrev = UIFactory.AddButton("PrevBanner", rt, "<", 30, font, new Color(0f, 0f, 0f, 0.5f), Color.white);
            var prt = (RectTransform)_summonPrev.transform;
            UIFactory.Anchor(prt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            prt.sizeDelta = new Vector2(48, 56); prt.anchoredPosition = new Vector2(12, -12);
            _summonPrev.onClick.AddListener(() => ShowSummon(_summonIndex - 1));
            _summonNext = UIFactory.AddButton("NextBanner", rt, ">", 30, font, new Color(0f, 0f, 0f, 0.5f), Color.white);
            var nrt = (RectTransform)_summonNext.transform;
            UIFactory.Anchor(nrt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            nrt.sizeDelta = new Vector2(48, 56); nrt.anchoredPosition = new Vector2(-12, -12);
            _summonNext.onClick.AddListener(() => ShowSummon(_summonIndex + 1));
            _summonDots = UIFactory.AddText("Dots", rt, "", 18, font, Color.white, TextAlignmentOptions.Left);
            _summonDots.richText = true; _summonDots.raycastTarget = false;
            UIFactory.Anchor(_summonDots.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            _summonDots.rectTransform.sizeDelta = new Vector2(200, 24); _summonDots.rectTransform.anchoredPosition = new Vector2(72, -28);
            _summonAuto = 7f;
        }

        /// <summary>소환 버튼 오른쪽: 대표 픽업 캐릭터 그림(가장자리를 흐려 배경에 녹임).</summary>
        private void BuildSummonArt()
        {
            var rt = (RectTransform)_summonBtn.transform;
            var mask = UIFactory.NewRect("PickupArt", rt);
            UIFactory.Anchor(mask, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f));
            mask.sizeDelta = new Vector2(330, -8); mask.anchoredPosition = new Vector2(-4, 0);
            mask.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(80, 0);
            mask.SetSiblingIndex(1);   // 배경 그림 바로 위, 어두운 막 · 글씨 아래
            _summonArt = UIFactory.AddImage("Art", mask, Color.white);
            _summonArt.preserveAspect = false; _summonArt.raycastTarget = false;
            var art = _summonArt.rectTransform;
            UIFactory.Anchor(art, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            art.sizeDelta = Vector2.zero; art.anchoredPosition = new Vector2(0, 24);
            _summonFit = _summonArt.gameObject.AddComponent<AspectRatioFitter>();
            _summonFit.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            _summonArt.enabled = false;
        }

        /// <summary>아래 메뉴 바: 캐릭터 · 편성 · 임무 · 길드 · 친구 · 상점 · 가방.</summary>
        private void BuildDock(Transform root)
        {
            var items = new (string icon, string label, UnityEngine.Events.UnityAction act)[]
            {
                ("Items/UiCharacter", "캐릭터", () => Nav(HubScreen.Growth)),
                ("Items/UiParty", "편성", OpenPartyEdit),
                ("Items/UiMission", "임무", () => Nav(HubScreen.Mission)),
                ("Items/GuildIcon", "길드", () => Nav(HubScreen.Guild)),
                ("Items/FriendIcon", "친구", () => Nav(HubScreen.Friends)),
                ("Items/UiShop", "상점", () => Nav(HubScreen.Shop)),
                ("Items/UiBag", "가방", () => Nav(HubScreen.Bag)),
            };
            const float itemW = 138f, gap = 6f, h = 128f, pad = 12f;
            float w = items.Length * itemW + (items.Length - 1) * gap + pad * 2;
            var bar = UIFactory.AddImage("Dock", root, new Color(0.04f, 0.05f, 0.09f, 0.82f), ProceduralSprites.Rounded, raycast: true);
            bar.type = Image.Type.Sliced; bar.preserveAspect = false;
            var brt = bar.rectTransform;
            UIFactory.Anchor(brt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
            brt.sizeDelta = new Vector2(w, h); brt.anchoredPosition = new Vector2(-40, 22);
            var line = UIFactory.AddImage("Line", brt, new Color(1f, 0.85f, 0.5f, 0.45f));
            UIFactory.Anchor(line.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            line.rectTransform.sizeDelta = new Vector2(-48, 2); line.rectTransform.anchoredPosition = new Vector2(0, -1);
            var rts = new RectTransform[items.Length];
            for (int i = 0; i < items.Length; i++)
                rts[i] = DockItem(brt, items[i].icon, items[i].label, pad + i * (itemW + gap), itemW, items[i].act);
            _growthDock = rts[0];
            _missionBadge = NotifyBadge.Attach(rts[2], font, -20, 0, 28);
            _guildBadge = NotifyBadge.Attach(rts[3], font, -20, 0, 28);
            _friendBadge = NotifyBadge.Attach(rts[4], font, -20, 0, 28);
        }

        private RectTransform DockItem(RectTransform bar, string icon, string label, float x, float w, UnityEngine.Events.UnityAction onClick)
        {
            var bg = UIFactory.AddImage(label, bar, Color.white, ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            var rt = bg.rectTransform;
            UIFactory.Anchor(rt, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f));
            rt.sizeDelta = new Vector2(w, -12); rt.anchoredPosition = new Vector2(x, 0);
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            var cb = b.colors;
            cb.normalColor = new Color(1, 1, 1, 0f); cb.selectedColor = cb.normalColor;
            cb.highlightedColor = new Color(1, 1, 1, 0.08f); cb.pressedColor = new Color(1, 1, 1, 0.16f);
            b.colors = cb;
            b.onClick.AddListener(onClick);
            bg.gameObject.AddComponent<ButtonFx>();
            var sp = GameArt.Load(icon);
            var img = UIFactory.AddImage("Icon", rt, Color.white, sp);
            img.preserveAspect = true; img.raycastTarget = false;
            UIFactory.Stretch(img.rectTransform, 26, 34, 26, 4);
            img.enabled = sp != null;
            var ish = img.gameObject.AddComponent<Shadow>();
            ish.effectColor = new Color(0, 0, 0, 0.55f); ish.effectDistance = new Vector2(0, -2);
            var t = UIFactory.AddText("Label", rt, label, 22, font, Color.white, TextAlignmentOptions.Bottom, FontStyles.Bold);
            UIFactory.Stretch(t.rectTransform, 0, 4, 0, 0);
            SubcultureRPG.UI.TextOutline.Apply(t, 0.2f, new Color32(10, 12, 20, 230));
            return rt;
        }

        /// <summary>편성: 출격 없이 파티만 짜 두기(다음 출격 때 이 파티가 미리 골라짐).</summary>
        private void OpenPartyEdit()
        {
            var party = FindAnyObjectByType<PartyScreen>();
            if (party != null) party.SetEditMode();
            Nav(HubScreen.Party);
        }

        /// <summary>오른쪽 위 '메뉴': 시즌 패스 · 도감 · 프로필 · 설정.</summary>
        private void BuildMenuPopup(float iconX)
        {
            var blocker = UIFactory.AddImage("Close", _root.transform, new Color(0, 0, 0, 0.35f), null, raycast: true);   // 뒤로 키로도 닫힘
            UIFactory.Stretch(blocker.rectTransform);
            _menuPop = blocker.gameObject;
            var bb = blocker.gameObject.AddComponent<Button>();
            bb.transition = Selectable.Transition.None;
            bb.onClick.AddListener(() => _menuPop.SetActive(false));
            var frame = UIFactory.Frame(blocker.transform);
            var rows = new (string icon, string label, UnityEngine.Events.UnityAction act)[]
            {
                ("Items/UiPass", "시즌 패스", () => SeasonPassWindow.Open(font)),
                ("Items/UiCodex", "도감", () => Nav(HubScreen.Codex)),
                ("Items/UiCharacter", "프로필 · 로비 설정", () => ProfilePanel.Open(font, OpenAssistantPicker)),
                ("Items/UiSettings", "설정", () => SettingsWindow.Open(font)),
            };
            const float rowH = 86f, pad = 14f, w = 380f;
            var panel = UIFactory.AddImage("Panel", frame, new Color(0.08f, 0.09f, 0.14f, 0.98f), ProceduralSprites.Rounded, raycast: true);
            panel.type = Image.Type.Sliced; panel.preserveAspect = false;
            var prt = panel.rectTransform;
            UIFactory.Anchor(prt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            prt.sizeDelta = new Vector2(w, rows.Length * rowH + pad * 2); prt.anchoredPosition = new Vector2(iconX + 20, -108);
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                var btn = UIFactory.AddButton("Row", prt, r.label, 26, font, new Color(1, 1, 1, 0.06f), Color.white);
                var rrt = (RectTransform)btn.transform;
                UIFactory.Anchor(rrt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
                rrt.sizeDelta = new Vector2(-pad * 2, rowH - 8); rrt.anchoredPosition = new Vector2(0, -pad - i * rowH);
                var act = r.act;
                btn.onClick.AddListener(() => { _menuPop.SetActive(false); act(); });
                var sp = GameArt.Load(r.icon);
                if (sp != null)
                {
                    var im = UIFactory.AddImage("Icon", rrt, Color.white, sp);
                    im.preserveAspect = true; im.raycastTarget = false;
                    UIFactory.Anchor(im.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                    im.rectTransform.sizeDelta = new Vector2(62, 62); im.rectTransform.anchoredPosition = new Vector2(12, 0);
                }
                var lbl = btn.GetComponentInChildren<TextMeshProUGUI>();
                lbl.alignment = TextAlignmentOptions.Left;
                UIFactory.Stretch(lbl.rectTransform, 90, 0, 12, 0);
                if (i == 0) _menuPassBadge = NotifyBadge.Attach(rrt, font, -24, -22, 28);
                if (i == 1) _menuCodexBadge = NotifyBadge.Attach(rrt, font, -24, -22, 28);
            }
            _menuPop.SetActive(false);
        }

        private void ToggleMenu()
        {
            if (_menuPop == null) return;
            bool on = !_menuPop.activeSelf;
            _menuPop.SetActive(on);
            if (on) _menuPop.transform.SetAsLastSibling();
        }

        /// <summary>출격 화면: 스토리 · 결투장 · 레이드 / 무한의 탑 · 파견 · 기간 한정 이벤트 카드.</summary>
        private void BuildModeHub()
        {
            var hub = UIFactory.AddImage("ModeHub", _root.transform, new Color(0.05f, 0.06f, 0.1f, 1f), null, raycast: true);
            UIFactory.Stretch(hub.rectTransform);
            _hub = hub.gameObject;
            _hubGroup = _hub.AddComponent<CanvasGroup>();
            BackgroundArt.Create(hub.transform, 0.66f).Set("Backgrounds/stage");
            var root = UIFactory.Frame(hub.transform);

            var back = UIFactory.AddButton("BackBtn", root, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            var brt = (RectTransform)back.transform;
            UIFactory.Anchor(brt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            brt.sizeDelta = new Vector2(150, 44); brt.anchoredPosition = new Vector2(30, -34);
            back.onClick.AddListener(() => OpenModeHub(false));
            var title = UIFactory.AddText("Title", root, "출격  <size=50%><color=#9AA3B5>도전할 콘텐츠를 고르세요</color></size>", 44, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            title.richText = true;
            UIFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            title.rectTransform.sizeDelta = new Vector2(900, 60); title.rectTransform.anchoredPosition = new Vector2(204, -26);
            CurrencyBar.Create(root, font, wallet, new Vector2(-50, -36), Currency.Stamina, Currency.Crystal);

            var size = new Vector2(560, 388);
            float dx = size.x + 28f, dy = (size.y + 28f) * 0.5f, cy = -26f;   // 위쪽 행동력(회복 시간 글) 아래로 여유
            var theme = LimitedEvent.CurrentTheme;
            string emblem = GameArt.Load(theme.ArtEmblem) != null ? theme.ArtEmblem : LimitedEvent.CoinIcon;
            _stageCard = Card(root, "스토리 · 스테이지", "메인 스토리 · 하드 · 극악 · 특수 던전", "Backgrounds/stage", "Items/UiSortie",
                new Color(1f, 0.8f, 0.4f, 1f), new Vector2(-dx, cy + dy), size, () => Nav(HubScreen.Stage));
            _arenaCard = Card(root, "결투장", "다른 지휘관과 순위 경쟁", "Backgrounds/arena", "Items/ArenaCoin",
                new Color(0.85f, 0.35f, 0.35f, 1f), new Vector2(0, cy + dy), size, () => Nav(HubScreen.Arena));
            _raidCard = Card(root, "레이드", "주간 보스 · 월드 보스 협동 토벌", "Backgrounds/raid", "Items/RaidIcon",
                new Color(0.6f, 0.4f, 0.9f, 1f), new Vector2(dx, cy + dy), size, () => Nav(HubScreen.Raid));
            _towerCard = Card(root, "무한의 탑", "한 층씩 오르는 고난도 도전", "Backgrounds/tower", "Items/TowerIcon",
                new Color(0.75f, 0.58f, 0.3f, 1f), new Vector2(-dx, cy - dy), size, () => Nav(HubScreen.Tower));
            _dispatchCard = Card(root, "파견", "캐릭터를 보내 시간이 지나면 보상", "Backgrounds/dispatch", "Items/DispatchIcon",
                new Color(0.35f, 0.7f, 0.58f, 1f), new Vector2(0, cy - dy), size, () => Nav(HubScreen.Dispatch));
            _eventCard = Card(root, "기간 한정 이벤트", theme.name, theme.ArtBackground, emblem,
                theme.color, new Vector2(dx, cy - dy), size, () => Nav(HubScreen.Event));
            _hub.SetActive(false);
        }

        private ModeCard Card(RectTransform root, string title, string desc, string art, string icon, Color accent, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var bg = UIFactory.AddImage("Card", root, new Color(accent.r, accent.g, accent.b, 1f), ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            var rt = bg.rectTransform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(onClick);
            bg.gameObject.AddComponent<ButtonFx>();
            var c = new ModeCard { Btn = btn };
            c.Art = BackgroundArt.Create(rt, 0.08f);
            c.Art.Set(art);
            if (rt.Find("BackgroundArt") is RectTransform clip) UIFactory.Stretch(clip, 4, 4, 4, 4);
            AddFade(rt, size.y * 0.62f, 0.92f);
            float left = 26f;
            var sp = GameArt.Load(icon);
            if (sp != null)
            {
                var im = UIFactory.AddImage("Icon", rt, Color.white, sp);
                im.preserveAspect = true; im.raycastTarget = false;
                UIFactory.Anchor(im.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
                im.rectTransform.sizeDelta = new Vector2(100, 100); im.rectTransform.anchoredPosition = new Vector2(18, 20);
                var sh = im.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.6f); sh.effectDistance = new Vector2(0, -3);
                left = 128f;
            }
            c.Title = CardText(rt, "Title", 42, Color.white, left, 92, 52, true);
            c.Title.text = title;
            var d = CardText(rt, "Desc", 20, new Color(0.8f, 0.83f, 0.9f), left + 2, 62, 28, false);
            d.text = desc;
            c.Status = CardText(rt, "Status", 25, Color.white, left + 2, 22, 36, false);

            var lockImg = UIFactory.AddImage("Lock", rt, new Color(0.02f, 0.03f, 0.06f, 0.68f), ProceduralSprites.Rounded);
            lockImg.type = Image.Type.Sliced; lockImg.preserveAspect = false; lockImg.raycastTarget = false;
            UIFactory.Stretch(lockImg.rectTransform, 4, 4, 4, 4);
            c.Lock = lockImg.gameObject;
            c.LockText = UIFactory.AddText("T", lockImg.rectTransform, "", 28, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            c.LockText.richText = true;
            UIFactory.Stretch(c.LockText.rectTransform, 20, 20, 20, 140);
            c.Lock.SetActive(false);
            c.Badge = NotifyBadge.Attach(rt, font, 10, 10, 40);
            return c;
        }

        /// <summary>출격 화면 열기 · 닫기(열면 살짝 떠오르며 나타남).</summary>
        private void OpenModeHub(bool on)
        {
            if (_hub == null) return;
            if (_menuPop != null) _menuPop.SetActive(false);
            _hub.SetActive(on);
            TutorialTargets.Set("home.stage", on ? (Component)_stageCard.Btn : _sortieBtn);
            if (!on) return;
            _hub.transform.SetAsLastSibling();
            RefreshModeTiles();
            if (_hubRoutine != null) StopCoroutine(_hubRoutine);
            if (_root.activeInHierarchy) _hubRoutine = StartCoroutine(HubIn());
            else _hubGroup.alpha = 1f;
        }

        private Coroutine _hubRoutine;
        private IEnumerator HubIn()
        {
            var rt = (RectTransform)_hub.transform;
            for (float t = 0; t < 0.18f; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / 0.18f, 3f);
                _hubGroup.alpha = Mathf.Max(0.05f, k);
                rt.localScale = Vector3.one * Mathf.Lerp(1.03f, 1f, k);
                yield return null;
            }
            _hubGroup.alpha = 1f; rt.localScale = Vector3.one;
            _hubRoutine = null;
        }

        private void BuildPicker()
        {
            var dim = UIFactory.AddImage("Picker", _root.transform, new Color(0, 0, 0, 0.75f), null, raycast: true);
            _picker = dim.gameObject;
            UIFactory.Stretch(dim.rectTransform);
            var close = dim.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(() => _picker.SetActive(false));

            var frame = UIFactory.Frame(dim.transform);
            var panel = UIFactory.AddImage("Panel", frame, new Color(0.09f, 0.1f, 0.15f, 0.98f), ProceduralSprites.Rounded, raycast: true);
            panel.type = Image.Type.Sliced; panel.preserveAspect = false;
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panel.rectTransform.sizeDelta = new Vector2(1280, 800);

            var title = UIFactory.AddText("Title", panel.transform, "대표 캐릭터 선택", 38, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            UIFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            title.rectTransform.sizeDelta = new Vector2(700, 56); title.rectTransform.anchoredPosition = new Vector2(40, -26);
            var sub = UIFactory.AddText("Sub", panel.transform, "<color=#9AA3B5>홈 화면에 서 있을 캐릭터를 고르세요 (보유 캐릭터)</color>", 20, font, Color.white, TextAlignmentOptions.Left);
            UIFactory.Anchor(sub.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            sub.rectTransform.sizeDelta = new Vector2(900, 30); sub.rectTransform.anchoredPosition = new Vector2(42, -84);

            var x = UIFactory.AddButton("Close", panel.transform, "닫기", 22, font, new Color(1, 1, 1, 0.14f), Color.white);
            var xr = (RectTransform)x.transform;
            UIFactory.Anchor(xr, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            xr.sizeDelta = new Vector2(130, 50); xr.anchoredPosition = new Vector2(-28, -28);
            x.onClick.AddListener(() => _picker.SetActive(false));

            var view = UIFactory.NewRect("View", panel.transform);
            UIFactory.Stretch(view, 30, 30, 30, 130);
            _pickerGrid = UIFactory.NewRect("Grid", view);
            var gl = _pickerGrid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(150, 196); gl.spacing = new Vector2(14, 14);
            gl.padding = new RectOffset(10, 10, 10, 10);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 7;
            gl.childAlignment = TextAnchor.UpperCenter;
            UIFactory.MakeVerticalScroll(view, _pickerGrid);

            _picker.SetActive(false);
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            SubcultureRPG.UI.UIInput.AddModule(es);
        }
    }
}
