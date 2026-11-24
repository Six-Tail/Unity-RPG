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
        private TextMeshProUGUI _missionLabel, _rosterText, _assistName, _bubbleText;
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
        }

        private void OnDisable()
        {
            if (wallet != null) wallet.OnChanged -= Refresh;
            if (inventory != null) inventory.OnChanged -= Refresh;
            if (roster != null) roster.OnRosterChanged -= Refresh;
        }

        private void Start()
        {
            // 임무 시스템은 내비게이터가 실행 중에 만들 수도 있어 Start에서 찾음
            _missions = FindAnyObjectByType<PlayerMissions>();
            if (_missions != null) _missions.OnChanged += RefreshMissionBadge;
            _codex = FindAnyObjectByType<PlayerCodex>();
            if (_codex != null) _codex.OnChanged += RefreshCodexButton;
            PlayerBond.Instance.OnChanged += RefreshBond;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_missions != null) _missions.OnChanged -= RefreshMissionBadge;
            if (_codex != null) _codex.OnChanged -= RefreshCodexButton;
            if (FindAnyObjectByType<PlayerBond>() is PlayerBond bond) bond.OnChanged -= RefreshBond;
            var pf = FindAnyObjectByType<PlayerProfile>();
            if (pf != null) pf.OnChanged -= RefreshProfileBadge;
        }

        public void Show(bool on)
        {
            if (_root != null) _root.SetActive(on);
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
            string id = PlayerPrefs.GetString(AssistantKey, "");
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
                PlayerPrefs.SetString(AssistantKey, !string.IsNullOrEmpty(c.id) ? c.id : c.name);
                PlayerPrefs.Save();
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
            if (_rosterText == null) return;
            int owned = roster != null ? roster.Owned.Count : 0;
            string count = _codex != null && _codex.Total > 0 ? $"{_codex.OwnedCount} / {_codex.Total}" : $"{owned}명";
            int n = _codex != null ? _codex.ClaimableCount() : 0;
            _rosterText.text = "도감" + (n > 0 ? $"  <color=#FFE08A><size=70%>●{n}</size></color>" : "") + $"\n<size=55%>수집 {count}</size>";
        }

        /// <summary>임무 타일: 받을 보상 수 + 빨간 느낌표.</summary>
        private void RefreshMissionBadge()
        {
            if (_missionLabel == null) return;
            int n = _missions != null ? _missions.ClaimableCount() : 0;
            _missionLabel.text = n > 0 ? $"임무\n<size=55%>받을 보상 {n}개</size>" : "임무\n<size=55%>일일 · 주간</size>";
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

            // 재화(오른쪽 위): 크리스탈 · 금화 알약
            CurrencyBar.Create(root, font, wallet, new Vector2(-50, -36), Currency.Crystal, Currency.Gold);

            // 메뉴(오른쪽): 스테이지(크게) → 가챠 → 2열 타일
            const float cx = 590f, w = 580f;
            var stage = _stageTile = Tile(root, "스테이지\n<size=50%>출격하고 보상 획득</size>", new Color(0.85f, 0.42f, 0.25f, 0.96f), new Vector2(cx, 268), new Vector2(w, 170), 52, () => Nav(HubScreen.Stage));
            var gachaBtn = Tile(root, "가챠\n<size=50%>캐릭터 소환</size>", new Color(0.78f, 0.56f, 0.2f, 0.96f), new Vector2(cx, 118), new Vector2(w, 110), 42, () => Nav(HubScreen.Gacha));

            // 2열 타일: 육성 · 임무 / 결투장 · 주간 보스 (상점 · 가방은 오른쪽 위 아이콘, 도감 · 대표 변경은 프로필에서)
            float tw = (w - 16f) / 2f, lx = cx - (tw + 16f) / 2f, rx = cx + (tw + 16f) / 2f;
            var growthTile = Tile(root, "육성\n<size=55%>레벨 · 스킬 · 초월</size>", new Color(0.33f, 0.55f, 0.4f, 0.95f), new Vector2(lx, -10), new Vector2(tw, 112), 34, () => Nav(HubScreen.Growth));
            var mission = Tile(root, "", new Color(0.3f, 0.45f, 0.8f, 0.95f), new Vector2(rx, -10), new Vector2(tw, 112), 34, () => Nav(HubScreen.Mission));
            _missionLabel = mission.GetComponentInChildren<TextMeshProUGUI>();
            _arenaColor = new Color(0.72f, 0.3f, 0.3f, 0.95f); _raidColor = new Color(0.45f, 0.3f, 0.7f, 0.95f);
            var arenaBtn = Tile(root, "", new Color(0.72f, 0.3f, 0.3f, 0.95f), new Vector2(lx, -136), new Vector2(tw, 112), 34, () => Nav(HubScreen.Arena));
            _arenaLabel = arenaBtn.GetComponentInChildren<TextMeshProUGUI>();
            _arenaTile = arenaBtn;
            TileIcon(arenaBtn, "Items/ArenaCoin");
            var raidBtn = Tile(root, "", new Color(0.45f, 0.3f, 0.7f, 0.95f), new Vector2(rx, -136), new Vector2(tw, 112), 34, () => Nav(HubScreen.Raid));
            _raidLabel = raidBtn.GetComponentInChildren<TextMeshProUGUI>();
            _raidTile = raidBtn;
            TileIcon(raidBtn, "Items/RaidIcon");
            var guildBtn = Tile(root, "", new Color(0.25f, 0.42f, 0.62f, 0.95f), new Vector2(lx, -262), new Vector2(tw, 112), 34, () => Nav(HubScreen.Guild));
            _guildLabel = guildBtn.GetComponentInChildren<TextMeshProUGUI>();
            TileIcon(guildBtn, "Items/GuildIcon");
            var friendBtn = Tile(root, "", new Color(0.62f, 0.38f, 0.52f, 0.95f), new Vector2(rx, -262), new Vector2(tw, 112), 34, () => Nav(HubScreen.Friends));
            _friendLabel = friendBtn.GetComponentInChildren<TextMeshProUGUI>();
            TileIcon(friendBtn, "Items/FriendIcon");

            // 상점 · 가방: 재화 알약 왼쪽 아이콘
            IconButton(root, "Items/UiShop", "상점", new Vector2(-50 - 2 * CurrencyBar.PillW - CurrencyBar.Gap - 14 - 78, -30), () => Nav(HubScreen.Shop));
            IconButton(root, "Items/UiBag", "가방", new Vector2(-50 - 2 * CurrencyBar.PillW - CurrencyBar.Gap - 14, -30), () => Nav(HubScreen.Bag));
            // 출석부 · 우편함(받을 것이 있으면 빨간 느낌표)
            float iconX = -50 - 2 * CurrencyBar.PillW - CurrencyBar.Gap - 14 - 78;
            var attend = IconButton(root, "Items/UiAttendance", "출석", new Vector2(iconX - 78, -30), () => AttendanceWindow.Open(font));
            var mailBtn = IconButton(root, "Items/UiMail", "우편", new Vector2(iconX - 156, -30), () => MailWindow.Open(font));

            // 빨간 느낌표: 받지 않은 보상 · 할 일이 있는 곳
            _attendBadge = NotifyBadge.Attach(attend, font, 8, 8, 28);
            _mailBadge = NotifyBadge.Attach(mailBtn, font, 8, 8, 28);
            _stageBadge = NotifyBadge.Attach(stage, font);
            _missionBadge = NotifyBadge.Attach(mission, font);
            _raidBadge = NotifyBadge.Attach(raidBtn, font);
            _guildBadge = NotifyBadge.Attach(guildBtn, font);
            _friendBadge = NotifyBadge.Attach(friendBtn, font);

            // 튜토리얼이 가리킬 곳
            TutorialTargets.Set("home.stage", stage);
            TutorialTargets.Set("home.growth", growthTile);

            BuildPicker();
        }

        private TextMeshProUGUI _arenaLabel, _raidLabel, _guildLabel, _friendLabel, _eventLabel;
        private Image _eventBg;
        private BackgroundArt _eventArt;
        private NotifyBadge _eventBadge;

        /// <summary>기간 한정 이벤트 배너(왼쪽, 대표 캐릭터 이름 위).</summary>
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
            _eventBadge.Set(open && (ev.ClaimableCount > 0 || ev.AttemptsLeft == LimitedEvent.AttemptsPerDay));
        }

        private NotifyBadge _profileBadge, _stageBadge, _missionBadge, _raidBadge, _guildBadge, _friendBadge, _mailBadge, _attendBadge;
        private Button _stageTile, _arenaTile, _raidTile;
        private Color _arenaColor, _raidColor;
        private StageScreen _stageScreen;
        private StageProgress _stageProgress;

        /// <summary>빨간 느낌표 갱신: 받지 않은 보상이 있으면 켜고, 받으면 끔.</summary>
        private void RefreshBadges()
        {
            if (_mailBadge == null) return;
            _mailBadge.Set(PlayerMail.Instance.UnclaimedCount > 0);
            _attendBadge.Set(PlayerAttendance.Instance.CanClaimAny);
            _missionBadge.Set(_missions != null && _missions.ClaimableCount() > 0);
            _raidBadge.Set(FeatureGate.RaidOpen && WorldRaid.Instance.ClaimableCount > 0);
            _guildBadge.Set(PlayerGuild.Instance.BadgeCount > 0);
            _friendBadge.Set(PlayerSocial.Instance.BadgeCount > 0);
            if (_stageScreen == null) _stageScreen = FindAnyObjectByType<StageScreen>();
            if (_stageProgress == null) _stageProgress = FindAnyObjectByType<StageProgress>();
            _stageBadge.Set(_stageScreen != null && ChapterStarRewards.ClaimableTotal(_stageScreen.Database, _stageProgress) > 0);
        }

        /// <summary>잠긴 타일은 어둡게.</summary>
        private static void TileLocked(Button tile, bool locked, Color normal)
        {
            if (tile == null) return;
            tile.image.color = locked ? new Color(0.26f, 0.27f, 0.32f, 0.9f) : normal;
        }

        /// <summary>결투장 · 주간 보스 · 길드 · 친구 타일 문구 + 잠금 표시 + 빨간 느낌표.</summary>
        private void RefreshModeTiles()
        {
            int cleared = FeatureGate.ClearedChapters();
            if (_arenaLabel != null)
            {
                bool open = cleared >= FeatureGate.ArenaChapter;
                TileLocked(_arenaTile, !open, _arenaColor);
                if (!open) _arenaLabel.text = $"결투장\n<size=55%><color=#FFB3B3>{FeatureGate.LockText(FeatureGate.ArenaChapter)}</color></size>";
                else
                {
                    var a = PlayerArena.Instance;
                    var (tier, _) = PlayerArena.Tier(a.Points);
                    _arenaLabel.text = $"결투장\n<size=55%>{tier} · {a.MyRank}위 · 도전권 {a.Tickets}/{PlayerArena.MaxTickets}</size>";
                }
            }
            if (_raidLabel != null)
            {
                bool open = cleared >= FeatureGate.RaidChapter;
                TileLocked(_raidTile, !open, _raidColor);
                if (!open) _raidLabel.text = $"주간 보스\n<size=55%><color=#FFB3B3>{FeatureGate.LockText(FeatureGate.RaidChapter)}</color></size>";
                else
                {
                    var r = WorldRaid.Instance;
                    _raidLabel.text = $"주간 보스\n<size=55%>남은 도전 {r.AttemptsLeft}/{WorldRaid.AttemptsPerDay}</size>";
                }
            }
            if (_guildLabel != null)
            {
                var g = PlayerGuild.Instance;
                _guildLabel.text = $"길드\n<size=55%>{(g.Joined ? $"{g.Name} Lv.{g.Level}" : "가입 · 창설")}</size>";
            }
            if (_friendLabel != null)
            {
                var so = PlayerSocial.Instance;
                _friendLabel.text = $"친구\n<size=55%>{so.Friends.Count}/{PlayerSocial.MaxFriends}명</size>";
            }
            RefreshEventBanner();
            RefreshBadges();
        }

        /// <summary>타일 왼쪽에 아이콘(글자는 오른쪽으로 밀림).</summary>
        private static void TileIcon(Button tile, string icon)
        {
            var sp = GameArt.Load(icon);
            if (sp == null) return;
            var img = UIFactory.AddImage("Icon", tile.transform, Color.white, sp);
            img.preserveAspect = true;
            UIFactory.Anchor(img.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            img.rectTransform.sizeDelta = new Vector2(88, 88); img.rectTransform.anchoredPosition = new Vector2(16, 0);
            var lbl = tile.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) lbl.rectTransform.offsetMin = new Vector2(96, lbl.rectTransform.offsetMin.y);
        }

        /// <summary>오른쪽 위 작은 아이콘 버튼(아이콘 + 아래 이름).</summary>
        private RectTransform IconButton(Transform parent, string icon, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var bg = UIFactory.AddImage(label, parent, new Color(0.04f, 0.05f, 0.09f, 0.72f), ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            var rt = bg.rectTransform;
            UIFactory.Anchor(rt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            rt.sizeDelta = new Vector2(68, 68); rt.anchoredPosition = pos;
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(onClick);
            bg.gameObject.AddComponent<ButtonFx>();
            var sp = GameArt.Load(icon);
            var img = UIFactory.AddImage("Icon", rt, Color.white, sp);
            img.preserveAspect = true;
            UIFactory.Stretch(img.rectTransform, 8, 14, 8, 4);
            img.enabled = sp != null;
            var t = UIFactory.AddText("Label", rt, label, 15, font, Color.white, TextAlignmentOptions.Bottom, FontStyles.Bold);
            UIFactory.Stretch(t.rectTransform, 0, 2, 0, 0);
            return rt;
        }

        private Button Tile(Transform parent, string label, Color bg, Vector2 pos, Vector2 size, float fontSize, UnityEngine.Events.UnityAction onClick)
        {
            var btn = UIFactory.AddButton("Menu", parent, label, fontSize, font, bg, Color.white);
            var rt = (RectTransform)btn.transform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            var lbl = btn.GetComponentInChildren<TextMeshProUGUI>();
            lbl.richText = true;
            lbl.lineSpacing = -6f;
            btn.onClick.AddListener(onClick);
            return btn;
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
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
