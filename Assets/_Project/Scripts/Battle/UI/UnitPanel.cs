using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.Characters;
using SubcultureRPG.UI;

namespace SubcultureRPG.Battle.UI
{
    /// <summary>
    /// 전투 필드에 서 있는 유닛 1명: 전신 그림(아군 = Figures → Cutouts → 초상, 적 = Enemies) + 발밑 그림자 · 고리,
    /// 머리 위 정보(이름 · 속성 · 체력 · 에너지/내구도 · 약점 · 상태 아이콘).
    /// 이 RectTransform은 피벗이 발밑(아래 가운데)이고 크기 = 그림 상자. HUD가 위치 · 크기를 정합니다(SetFigure).
    /// 피해 숫자 · 이펙트는 이 상자의 가운데(가슴 높이)에 뜹니다.
    /// 연출: 대기(숨쉬기) · 차례(앞으로 한 발) · 돌진(대상 앞까지 갔다 돌아옴) · 피격(번쩍임 + 밀려남) · 쓰러짐.
    /// 클릭 시 onClick(유닛) 호출 → HUD가 대상 선택 처리.
    /// </summary>
    public class UnitPanel : MonoBehaviour
    {
        /// <summary>히트스톱: 이 시각까지 모든 유닛의 움직임이 멈춤(강한 타격의 손맛).</summary>
        public static float HitStopUntil;
        private static bool Frozen => Time.unscaledTime < HitStopUntil;

        private static Shader _flashShader;
        private static bool _shaderLooked;

        private BattleUnit _unit;
        private TMP_FontAsset _font;
        private Action<BattleUnit> _onClick;
        private Action<UnitPanel, StatusEffect, RectTransform> _onStatus;   // 상태 아이콘 클릭(효과 null = 브레이크)
        private RectTransform _statusRow;
        private string _statusKey = "";

        private RectTransform _rt, _body, _figureHolder, _figureRT, _info, _ring;
        private RectMask2D _fade;
        private CanvasGroup _group, _infoGroup;
        private Image _backGlow;
        private RectTransform _glowAnchor;   // 역광이 따라갈 자리(몸 안) — 역광 자체는 모든 유닛 뒤의 층으로 옮김

        /// <summary>
        /// HUD: 역광을 유닛들 뒤의 공용 층으로 옮김. 유닛 안에 두면 앞줄 유닛의 역광이 뒷줄 유닛 위에 덮여 색이 바래 보임.
        /// 위치는 매 프레임 몸을 따라감(돌진 · 피격 포함).
        /// </summary>
        public void MoveGlowTo(RectTransform layer)
        {
            if (_backGlow == null || layer == null) return;
            _backGlow.rectTransform.SetParent(layer, true);
        }

        private void LateUpdate()
        {
            if (_backGlow == null || _glowAnchor == null || _backGlow.transform.parent == _body) return;
            _backGlow.rectTransform.position = _glowAnchor.position;
            _backGlow.rectTransform.localScale = _body.localScale;
        }
        private Image _figure, _shadow, _ringImg, _ringGlow, _hpFill, _hpLag, _energyFill, _toughFill;
        private Material _flashMat;
        private GameObject _toughBarGO, _energyBarGO;
        private TextMeshProUGUI _toughText;
        private RectTransform _elementSlot, _weakRow, _weakHint, _weakHintIcon;
        private Image _weakHintBg;
        private readonly System.Collections.Generic.Dictionary<Element, RectTransform> _weakIcons = new();
        private bool _hintOn;
        private Element _hintElement;
        private TextMeshProUGUI _nameText, _hpText;
        private Button _button;
        private Coroutine _hpAnim, _moveAnim, _flashAnim, _deathAnim;
        private float _hpShown = 1f;
        private bool _current, _targetable, _wasAlive = true, _boss;
        private float _phase, _step, _flashAmount;
        private Vector2 _moveOffset;
        private Color _flashColor = Color.white;

        public BattleUnit Unit => _unit;
        public RectTransform Body => _body;
        /// <summary>앞쪽(적을 향하는 쪽) x 부호: 아군 +1, 적 -1.</summary>
        public float Forward => _unit != null && _unit.team == Team.Enemy ? -1f : 1f;
        public bool IsBoss => _boss;

        public static UnitPanel Create(Transform parent, TMP_FontAsset font, Action<BattleUnit> onClick,
            Action<UnitPanel, StatusEffect, RectTransform> onStatus = null)
        {
            var rt = UIFactory.NewRect("Unit", parent);
            var p = rt.gameObject.AddComponent<UnitPanel>();
            p._font = font;
            p._onClick = onClick;
            p._onStatus = onStatus;
            p.Build();
            return p;
        }

        private Image Rounded(string name, Transform parent, Color c, bool raycast = false)
        {
            var img = UIFactory.AddImage(name, parent, c, ProceduralSprites.Rounded, raycast);
            img.type = Image.Type.Sliced; img.preserveAspect = false; img.pixelsPerUnitMultiplier = 2.2f;
            return img;
        }

        private void Build()
        {
            _rt = (RectTransform)transform;
            UIFactory.Anchor(_rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f));
            _rt.sizeDelta = new Vector2(260, 400);
            _phase = UnityEngine.Random.Range(0f, 10f);

            _body = UIFactory.NewRect("Body", transform);
            UIFactory.Stretch(_body);
            _body.pivot = new Vector2(0.5f, 0f);
            _group = _body.gameObject.AddComponent<CanvasGroup>();

            // 발밑: 그림자 · 고리(차례 금색 / 대상 선택 빨강 / 궁극기 준비 금빛 맥동)
            _shadow = UIFactory.AddImage("Shadow", _body, new Color(0, 0, 0, 0.55f), ProceduralSprites.Glow);
            _shadow.preserveAspect = false;
            UIFactory.Anchor(_shadow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
            _ringGlow = UIFactory.AddImage("RingGlow", _body, new Color(1f, 0.8f, 0.3f, 0f), ProceduralSprites.Glow);
            _ringGlow.preserveAspect = false;
            UIFactory.Anchor(_ringGlow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
            _ringImg = UIFactory.AddImage("Ring", _body, new Color(1f, 0.85f, 0.35f, 0f), ProceduralSprites.Ring);
            _ringImg.preserveAspect = false;
            _ring = _ringImg.rectTransform;
            UIFactory.Anchor(_ring, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));

            // 역광: 인물 뒤 은은한 빛(어두운 그림이 어두운 배경에 묻히지 않게)
            _backGlow = UIFactory.AddImage("BackGlow", _body, new Color(1f, 1f, 1f, 0f), ProceduralSprites.Glow);
            _backGlow.preserveAspect = false;
            UIFactory.Anchor(_backGlow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
            _glowAnchor = UIFactory.NewRect("GlowAnchor", _body);
            UIFactory.Anchor(_glowAnchor, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
            _glowAnchor.sizeDelta = Vector2.zero;

            // 그림: 바깥 상자(RectMask2D — 허벅지까지만 있는 그림은 아래를 부드럽게 지움) 안에 인물
            _figureHolder = UIFactory.NewRect("FigureHolder", _body);
            _fade = _figureHolder.gameObject.AddComponent<RectMask2D>();
            _figure = UIFactory.AddImage("Figure", _figureHolder, Color.white);
            _figure.preserveAspect = true;
            _figureRT = _figure.rectTransform;
            _figureRT.pivot = new Vector2(0.5f, 0f);
            if (!_shaderLooked) { _shaderLooked = true; _flashShader = Shader.Find("SubcultureRPG/UIFlash"); }
            if (_flashShader != null)
            {
                _flashMat = new Material(_flashShader);
                _figure.material = _flashMat;
            }

            // 누르는 영역(인물 가운데 — 겹친 이웃을 덜 가리게)
            var hit = UIFactory.AddImage("Hit", _body, new Color(0, 0, 0, 0), null, raycast: true);
            UIFactory.Stretch(hit.rectTransform);
            hit.rectTransform.anchorMin = new Vector2(0.2f, 0.05f); hit.rectTransform.anchorMax = new Vector2(0.8f, 0.85f);
            hit.rectTransform.offsetMin = hit.rectTransform.offsetMax = Vector2.zero;
            _button = hit.gameObject.AddComponent<Button>();
            _button.transition = Selectable.Transition.None;
            _button.targetGraphic = hit;
            _button.onClick.AddListener(() => _onClick?.Invoke(_unit));

            BuildInfo();
        }

        /// <summary>머리 위 정보(이름 · 바 · 약점 · 상태).</summary>
        private void BuildInfo()
        {
            _info = UIFactory.NewRect("Info", _body);
            UIFactory.Anchor(_info, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f));
            _info.sizeDelta = new Vector2(200, 52);
            _infoGroup = _info.gameObject.AddComponent<CanvasGroup>();
            _infoGroup.blocksRaycasts = true;

            var plate = Rounded("Plate", _info, new Color(0.04f, 0.05f, 0.08f, 0.62f));
            UIFactory.Stretch(plate.rectTransform, -6, -4, -6, -2);

            _nameText = UIFactory.AddText("Name", _info, "", 17, _font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            UIFactory.Anchor(_nameText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            _nameText.rectTransform.sizeDelta = new Vector2(-30, 22); _nameText.rectTransform.anchoredPosition = new Vector2(15, 0);
            _nameText.enableAutoSizing = true; _nameText.fontSizeMin = 11; _nameText.fontSizeMax = 17;
            SubcultureRPG.UI.TextOutline.Apply(_nameText, 0.2f, new Color32(8, 8, 14, 255));

            _elementSlot = UIFactory.NewRect("ElementSlot", _info);
            UIFactory.Anchor(_elementSlot, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            _elementSlot.sizeDelta = new Vector2(24, 24); _elementSlot.anchoredPosition = new Vector2(-6, 2);

            // HP 바(줄어든 만큼 잠시 남는 붉은 잔상 포함)
            var hpBar = UIFactory.NewRect("HPBar", _info);
            UIFactory.Anchor(hpBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            hpBar.sizeDelta = new Vector2(0, 14); hpBar.anchoredPosition = new Vector2(0, -24);
            var hpBg = UIFactory.AddImage("BG", hpBar, new Color(0, 0, 0, 0.7f));
            UIFactory.Stretch(hpBg.rectTransform, -1, -1, -1, -1);
            _hpLag = UIFactory.AddImage("Lag", hpBar, new Color(1f, 0.35f, 0.3f, 0.85f));
            UIFactory.Stretch(_hpLag.rectTransform);
            _hpFill = UIFactory.AddImage("Fill", hpBar, new Color(0.3f, 0.85f, 0.4f, 1f));
            UIFactory.Stretch(_hpFill.rectTransform);
            _hpText = UIFactory.AddText("T", hpBar, "", 11, _font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_hpText.rectTransform);
            SubcultureRPG.UI.TextOutline.Apply(_hpText, 0.25f, new Color32(0, 0, 0, 255));

            // 에너지 바(아군)
            var enBar = UIFactory.NewRect("EnergyBar", _info);
            UIFactory.Anchor(enBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            enBar.sizeDelta = new Vector2(0, 5); enBar.anchoredPosition = new Vector2(0, -41);
            var enBg = UIFactory.AddImage("BG", enBar, new Color(0, 0, 0, 0.7f));
            UIFactory.Stretch(enBg.rectTransform);
            _energyFill = UIFactory.AddImage("Fill", enBar, new Color(0.35f, 0.75f, 1f, 1f));
            UIFactory.Stretch(_energyFill.rectTransform);
            _energyBarGO = enBar.gameObject;

            // 내구도 바(적, 브레이크)
            var tgBar = UIFactory.NewRect("ToughBar", _info);
            UIFactory.Anchor(tgBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            tgBar.sizeDelta = new Vector2(0, 11); tgBar.anchoredPosition = new Vector2(0, -40);
            var tgBg = UIFactory.AddImage("BG", tgBar, new Color(0, 0, 0, 0.75f));
            UIFactory.Stretch(tgBg.rectTransform);
            _toughFill = UIFactory.AddImage("Fill", tgBar, new Color(0.92f, 0.94f, 1f, 1f));
            UIFactory.Stretch(_toughFill.rectTransform);
            _toughText = UIFactory.AddText("T", tgBar, "", 9, _font, new Color(0.1f, 0.11f, 0.16f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_toughText.rectTransform);
            _toughBarGO = tgBar.gameObject;

            // 약점 속성 줄(적만) — 정보 판 위
            _weakRow = UIFactory.NewRect("WeakRow", _info);
            UIFactory.Anchor(_weakRow, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0));
            _weakRow.sizeDelta = new Vector2(0, 24); _weakRow.anchoredPosition = new Vector2(-4, 6);
            var wrl = _weakRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            wrl.spacing = 4; wrl.childAlignment = TextAnchor.MiddleLeft;
            wrl.childControlWidth = true; wrl.childControlHeight = true;
            wrl.childForceExpandWidth = false; wrl.childForceExpandHeight = false;

            // 약점 어시스트(정보 판 위 오른쪽): 지금 행동하는 아군 속성이 이 적의 약점일 때 '속성 아이콘 + 약점!'
            _weakHintBg = Rounded("WeakHint", _info, Color.white);
            _weakHintBg.pixelsPerUnitMultiplier = 3f;
            _weakHint = _weakHintBg.rectTransform;
            UIFactory.Anchor(_weakHint, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0));
            _weakHint.sizeDelta = new Vector2(84, 28); _weakHint.anchoredPosition = new Vector2(8, 6);
            _weakHintIcon = UIFactory.NewRect("Icon", _weakHint);
            UIFactory.Anchor(_weakHintIcon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            _weakHintIcon.sizeDelta = new Vector2(24, 24); _weakHintIcon.anchoredPosition = new Vector2(2, 0);
            var hintTxt = UIFactory.AddText("T", _weakHint, "약점!", 15, _font, new Color(0.08f, 0.09f, 0.13f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(hintTxt.rectTransform, 26, 0, 4, 0);
            _weakHint.gameObject.SetActive(false);

            // 상태(버프/디버프) 아이콘 줄 — 정보 판 바로 아래(머리 쪽). 아이콘마다 남은 턴 숫자, 누르면 설명
            _statusRow = UIFactory.NewRect("StatusRow", _info);
            UIFactory.Anchor(_statusRow, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1));
            _statusRow.sizeDelta = new Vector2(0, 28); _statusRow.anchoredPosition = new Vector2(0, -6);
            var srl = _statusRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            srl.spacing = 4; srl.childAlignment = TextAnchor.UpperLeft;
            srl.childControlWidth = true; srl.childControlHeight = true;
            srl.childForceExpandWidth = false; srl.childForceExpandHeight = false;
        }

        // ---------------- 그림 ----------------

        /// <summary>유닛 그림: 아군 = Figures(전신) → Cutouts(허벅지까지, 아래를 흐리게) → 초상, 적 = Enemies.</summary>
        private Sprite FigureSprite(out bool fadeBottom)
        {
            fadeBottom = false;
            if (_unit.character != null)
            {
                string key = !string.IsNullOrEmpty(_unit.character.id) ? _unit.character.id : _unit.character.name;
                // 입고 있는 의상(개화 등) → 기본. 적 팀(결투장 상대)의 캐릭터는 내 옷장 선택이 아니라 기본 의상
                string sfx = GameArt.SuffixFor(_unit.character, _unit.team == Team.Ally);
                if (sfx != "")
                {
                    // 스킨을 입었으면 그 스킨의 그림을 먼저(스킨 전신 → 스킨 배경 없는 그림) — 기본 의상 전신 그림으로 돌아가지 않게
                    var skinFull = GameArt.Load("Figures/" + key + sfx);
                    if (skinFull != null) return skinFull;
                    var skinCut = GameArt.Load("Cutouts/" + key + sfx);
                    if (skinCut != null) { fadeBottom = true; return skinCut; }
                }
                var full = GameArt.Outfitted("Figures/", key, sfx);
                if (full != null) return full;
                var cut = GameArt.Outfitted("Cutouts/", key, sfx);
                if (cut != null) { fadeBottom = true; return cut; }
                return GameArt.Portrait(_unit.character, sfx) ?? GameArt.Thumb(_unit.character, sfx);
            }
            return GameArt.Enemy(_unit.Name);
        }

        /// <summary>HUD: 그림 상자 크기(키). 폭은 그림 비율로 정함. boss = 크게 그린 적(정보 판도 넓게).</summary>
        public void SetFigure(float height, bool boss)
        {
            _boss = boss;
            var sp = _figure.sprite;
            float aspect = sp != null ? sp.rect.width / Mathf.Max(1f, sp.rect.height) : 0.7f;
            float w = Mathf.Clamp(height * aspect, height * 0.55f, height * 1.05f);
            _rt.sizeDelta = new Vector2(w, height);

            bool fade = _fadeBottom;
            // 그림 상자: 흐리게 할 땐 위 · 양옆을 넉넉히 넓혀(가장자리 흐림이 인물에 닿지 않게) 아래만 흐려지게
            float padX = fade ? 300f : 0f, padTop = fade ? 300f : 0f;
            _figureHolder.anchorMin = Vector2.zero; _figureHolder.anchorMax = Vector2.one;
            _figureHolder.pivot = new Vector2(0.5f, 0f);
            _figureHolder.offsetMin = new Vector2(-padX, fade ? height * 0.02f : -200f);
            _figureHolder.offsetMax = new Vector2(padX, padTop + (fade ? 0f : 200f));
            _fade.softness = fade ? new Vector2Int(0, Mathf.RoundToInt(height * 0.22f)) : Vector2Int.zero;
            _fade.enabled = fade;
            // 인물은 원래 상자 크기로(그림 상자가 넓어진 만큼 안쪽으로)
            _figureRT.anchorMin = new Vector2(0.5f, 0f); _figureRT.anchorMax = new Vector2(0.5f, 0f);
            _figureRT.sizeDelta = new Vector2(w, height);
            _figureRT.anchoredPosition = new Vector2(0, fade ? -height * 0.02f : 200f);

            float sw = w * 0.85f;
            _shadow.rectTransform.sizeDelta = new Vector2(sw * 1.15f, sw * 0.3f);
            _shadow.rectTransform.anchoredPosition = new Vector2(0, 4);
            _ring.sizeDelta = new Vector2(sw, sw * 0.26f);
            _ring.anchoredPosition = new Vector2(0, 4);
            _ringGlow.rectTransform.sizeDelta = new Vector2(sw * 1.5f, sw * 0.5f);
            _ringGlow.rectTransform.anchoredPosition = new Vector2(0, 4);

            _backGlow.rectTransform.sizeDelta = new Vector2(w * 1.5f, height * 1.05f);
            if (boss && _unit != null && _unit.team == Team.Enemy) _backGlow.color = new Color(0.85f, 0.4f, 1f, 0.42f);
            _backGlow.rectTransform.anchoredPosition = new Vector2(0, height * 0.52f);
            _glowAnchor.anchoredPosition = new Vector2(0, height * 0.52f);

            _info.sizeDelta = new Vector2(boss ? 260 : 196, 52);
            _info.anchoredPosition = new Vector2(0, boss ? -height * 0.04f : -height * 0.02f);
        }

        private bool _fadeBottom;

        public void Bind(BattleUnit unit)
        {
            _unit = unit;
            _nameText.text = unit.Name;

            var sprite = FigureSprite(out _fadeBottom);
            _figure.sprite = sprite;
            _figure.enabled = sprite != null;
            _figure.color = Color.white;
            // 적 그림은 왼쪽(아군)을 보게 — 그림은 대부분 정면/오른쪽이라 뒤집지 않음. 아군 전신 그림은 오른쪽을 봄
            _figureRT.localScale = Vector3.one;

            _hpShown = unit.HPRatio;
            SetHPImmediate(unit.HPRatio);
            SetBar(_hpLag, unit.HPRatio);
            SetEnergy(unit.EnergyRatio, unit.UltimateReady);
            _energyBarGO.SetActive(unit.team == Team.Ally);
            _toughBarGO.SetActive(unit.HasBreakSystem);
            SetToughness(unit.ToughnessRatio);
            BuildElements();
            SetWeakHint(false, unit.element);
            _hpText.text = $"{unit.currentHP:N0}";
            _wasAlive = unit.IsAlive;
            _moveOffset = Vector2.zero; _step = 0f;
            _body.anchoredPosition = Vector2.zero;
            _body.localScale = Vector3.one;
            _group.alpha = unit.IsAlive ? 1f : 0f;
            _infoGroup.alpha = 1f;
            SetFlash(Color.white, 0f);
            SetTint(Color.black, 0f);
            // 역광 색: 적은 붉은 기운(보스는 더 진하게), 아군은 아주 옅은 흰빛
            _backGlow.color = unit.team == Team.Enemy ? new Color(1f, 0.45f, 0.4f, 0.3f) : new Color(1f, 0.95f, 0.85f, 0.1f);
            _statusKey = "?";   // 새 유닛 → 아이콘 다시 만들기
            UpdateStatus();
            SetCurrent(false);
            SetFigure(_rt.sizeDelta.y, _boss);
        }

        /// <summary>바/쓰러짐 상태 갱신(HP는 부드럽게).</summary>
        public void Refresh()
        {
            if (_unit == null) return;
            _hpText.text = $"{_unit.currentHP:N0}";
            SetEnergy(_unit.EnergyRatio, _unit.UltimateReady);
            if (_toughBarGO.activeSelf) SetToughness(_unit.ToughnessRatio);
            UpdateStatus();
            AnimateHP(_unit.HPRatio);
            _button.interactable = _unit.IsAlive;
            if (!_unit.IsAlive) SetCurrent(false);
            if (_wasAlive && !_unit.IsAlive) PlayDeath();
            else if (!_wasAlive && _unit.IsAlive) { if (_deathAnim != null) StopCoroutine(_deathAnim); _group.alpha = 1f; _infoGroup.alpha = 1f; _figure.color = Color.white; }
            _wasAlive = _unit.IsAlive;
        }

        public void SetCurrent(bool on) { _current = on; _targetable = false; }

        /// <summary>대상 선택 중 '고를 수 있음' 표시(발밑 빨간 고리).</summary>
        public void SetTargetable(bool on) => _targetable = on;

        private void Update()
        {
            if (_unit == null) return;
            float t = Time.unscaledTime;
            bool frozen = Frozen;

            // 발밑 고리: 대상 선택(빨강) > 차례(금색) > 궁극기 준비(금빛 맥동) > 없음
            Color c;
            if (_targetable) c = new Color(1f, 0.35f, 0.35f, 0.75f + 0.25f * Mathf.Sin(t * 8f));
            else if (_current) c = new Color(1f, 0.85f, 0.35f, 0.95f);
            else if (_unit.IsAlive && _unit.team == Team.Ally && _unit.UltimateReady) c = new Color(1f, 0.75f, 0.25f, 0.35f + 0.3f * Mathf.Sin(t * 4f));
            else c = new Color(1f, 0.85f, 0.35f, 0f);
            _ringImg.color = c;
            _ringGlow.color = new Color(c.r, c.g, c.b, c.a * 0.45f);
            _ring.localScale = Vector3.one * (1f + (_targetable || _current ? 0.05f * Mathf.Sin(t * 5f) : 0f));

            if (!frozen)
            {
                // 차례인 유닛은 앞으로 한 발
                _step = Mathf.MoveTowards(_step, _current && _unit.IsAlive ? 1f : 0f, Time.unscaledDeltaTime * 5f);
                // 대기 동작: 숨쉬기(세로로 살짝 늘었다 줄었다) + 무게 중심 옮기기(발을 축으로 아주 살짝 기울기 · 좌우).
                // 보스는 몸이 커서 기울이지 않고 천천히 위아래로 떠오름
                bool alive = _unit.IsAlive;
                float breath = alive ? Mathf.Sin(t * 1.7f + _phase) : 0f;
                float sway = alive ? Mathf.Sin(t * 0.85f + _phase * 1.7f) : 0f;
                float baseY = _fadeBottom ? -_rt.sizeDelta.y * 0.02f : 200f;
                if (_boss)
                {
                    float hover = alive ? Mathf.Sin(t * 1.1f + _phase) : 0f;
                    _figureRT.localScale = new Vector3(1f + 0.006f * breath, 1f + 0.014f * breath, 1f);
                    _figureRT.anchoredPosition = new Vector2(0, baseY + 10f * hover);
                    _figureRT.localEulerAngles = Vector3.zero;
                }
                else
                {
                    _figureRT.localScale = new Vector3(1f - 0.004f * breath, 1f + 0.02f * breath, 1f);
                    _figureRT.anchoredPosition = new Vector2(3f * sway, baseY + 3f * breath);
                    _figureRT.localEulerAngles = new Vector3(0, 0, 0.7f * sway * -Forward);
                }
            }
            _body.anchoredPosition = _moveOffset + new Vector2(Forward * 46f * Mathf.SmoothStep(0, 1, _step), 0f);

            if (_hintOn)
            {
                float wave = Mathf.Sin(t * 7f);
                _weakHint.localScale = Vector3.one * (1f + 0.08f * wave);
                if (_weakIcons.TryGetValue(_hintElement, out var wi) && wi != null) wi.localScale = Vector3.one * (1.12f + 0.12f * wave);
                if (!_unit.IsAlive) SetWeakHint(false, _hintElement);
            }
        }

        // ---------------- 연출 ----------------

        private float Dt => Frozen ? 0f : Time.unscaledDeltaTime;

        /// <summary>행동 돌진(작게): dir 크기만큼 앞으로 튀어나갔다 돌아옴(방향은 팀 앞쪽).</summary>
        public void PlayLunge(Vector2 dir, float dur)
        {
            float d = Mathf.Max(20f, dir.magnitude) * 1.8f;
            PlayDash(new Vector2(Forward * d, 0f), dur * 0.4f, dur * 0.1f, dur * 0.5f);
        }

        /// <summary>
        /// 돌진: delta(필드 좌표)만큼 out초에 걸쳐 나가고, hold초 머문 뒤 back초에 걸쳐 돌아옴.
        /// 나가는 동안 앞으로 살짝 기울고 커짐(다가오는 느낌).
        /// </summary>
        public void PlayDash(Vector2 delta, float outDur, float hold, float backDur)
        {
            if (!gameObject.activeInHierarchy) return;
            if (_moveAnim != null) StopCoroutine(_moveAnim);
            _moveAnim = StartCoroutine(DashRoutine(delta, outDur, hold, backDur));
        }

        private IEnumerator DashRoutine(Vector2 delta, float outDur, float hold, float backDur)
        {
            var from = _moveOffset;
            for (float t = 0; t < outDur; t += Dt)
            {
                float e = 1f - Mathf.Pow(1f - t / outDur, 3f);
                _moveOffset = Vector2.Lerp(from, delta, e);
                _body.localScale = Vector3.one * (1f + 0.05f * e);
                _body.localEulerAngles = new Vector3(0, 0, -Forward * 4f * Mathf.Sin(e * Mathf.PI));
                yield return null;
            }
            _moveOffset = delta;
            _body.localEulerAngles = Vector3.zero;
            for (float t = 0; t < hold; t += Dt) yield return null;
            for (float t = 0; t < backDur; t += Dt)
            {
                float e = Mathf.SmoothStep(0, 1, t / backDur);
                _moveOffset = Vector2.Lerp(delta, Vector2.zero, e);
                _body.localScale = Vector3.one * (1f + 0.05f * (1f - e));
                yield return null;
            }
            _moveOffset = Vector2.zero;
            _body.localScale = Vector3.one;
            _moveAnim = null;
        }

        /// <summary>전투 종료 시 진 쪽: 뒤로 물러나 가라앉으며 흐려짐.</summary>
        public void PlayDefeat(float dur)
        {
            if (!gameObject.activeInHierarchy || _group.alpha <= 0.01f) return;
            if (_moveAnim != null) StopCoroutine(_moveAnim);
            _moveAnim = StartCoroutine(DefeatRoutine(dur));
        }

        private IEnumerator DefeatRoutine(float dur)
        {
            float a0 = _group.alpha;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float e = Mathf.SmoothStep(0, 1, t / dur);
                _moveOffset = new Vector2(-Forward * 30f * e, -14f * e);
                _figure.color = Color.Lerp(Color.white, new Color(0.45f, 0.45f, 0.5f), e);
                _group.alpha = Mathf.Lerp(a0, 0.35f, e);
                yield return null;
            }
            _moveAnim = null;
        }

        /// <summary>전투 중 쓰러짐: 적은 번쩍인 뒤 사라지고, 아군은 어둡게 남음.</summary>
        private void PlayDeath()
        {
            if (!gameObject.activeInHierarchy) { _group.alpha = _unit.team == Team.Enemy ? 0f : 0.35f; return; }
            if (_deathAnim != null) StopCoroutine(_deathAnim);
            _deathAnim = StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            bool enemy = _unit.team == Team.Enemy;
            for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime) yield return null;   // 마지막 타격이 보이게
            float dur = 0.6f;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float e = t / dur;
                // 적: 하얗게 빛나며 사라짐(번쩍임) · 아군: 어둡게 가라앉음(물들이기)
                if (enemy) SetFlash(new Color(1f, 0.95f, 0.9f), Mathf.Sin(e * Mathf.PI) * 0.8f);
                else SetTint(new Color(0.2f, 0.2f, 0.25f), e * 0.5f);
                _group.alpha = enemy ? 1f - e : Mathf.Lerp(1f, 0.4f, e);
                _infoGroup.alpha = 1f - e;
                _body.localScale = new Vector3(1f + (enemy ? 0.08f * e : 0f), 1f - (enemy ? 0.06f * e : 0f), 1f);
                yield return null;
            }
            _group.alpha = enemy ? 0f : 0.4f;
            _infoGroup.alpha = 0f;
            if (_backGlow != null) _backGlow.color = new Color(1f, 1f, 1f, 0f);
            _body.localScale = Vector3.one;
            _deathAnim = null;
        }

        /// <summary>피격: 그림이 번쩍이고(flash 색), 공격 반대쪽으로 밀렸다 돌아옴.</summary>
        public void PlayHit(Color flash, float amplitude, float dur)
        {
            if (!gameObject.activeInHierarchy) return;
            if (amplitude > 0f)
            {
                if (_moveAnim != null) StopCoroutine(_moveAnim);
                _moveAnim = StartCoroutine(KnockRoutine(amplitude, dur));
            }
            if (_flashAnim != null) StopCoroutine(_flashAnim);
            _flashAnim = StartCoroutine(FlashRoutine(flash, dur));
        }

        private IEnumerator KnockRoutine(float amp, float dur)
        {
            _body.localScale = Vector3.one;
            float push = -Forward * amp * 2.2f;   // 뒤로
            for (float t = 0; t < dur; t += Dt)
            {
                float k = t / dur;
                float back = k < 0.2f ? k / 0.2f : 1f - Mathf.SmoothStep(0, 1, (k - 0.2f) / 0.8f);
                _moveOffset = new Vector2(push * back + Mathf.Sin(t * 70f) * amp * 0.3f * (1f - k), 0f);
                _body.localEulerAngles = new Vector3(0, 0, Forward * 3f * back);
                yield return null;
            }
            _moveOffset = Vector2.zero;
            _body.localEulerAngles = Vector3.zero;
            _moveAnim = null;
        }

        private IEnumerator FlashRoutine(Color c, float dur)
        {
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                SetFlash(c, 0.85f * (1f - t / dur));
                yield return null;
            }
            SetFlash(c, 0f);
            _flashAnim = null;
        }

        /// <summary>물들이기(쓰러질 때 어둡게 등) — 번쩍임과 따로.</summary>
        private void SetTint(Color c, float amount)
        {
            if (_flashMat != null)
            {
                _flashMat.SetColor("_TintColor", c);
                _flashMat.SetFloat("_TintAmount", amount);
            }
            else if (_figure != null && amount > 0f)
                _figure.color = Color.Lerp(Color.white, c, amount);
        }

        /// <summary>피격 번쩍임: 몸은 그 색으로 밝아지고(무늬 유지) 테두리가 빛남(UIFlash 셰이더).</summary>
        private void SetFlash(Color c, float amount)
        {
            _flashColor = c; _flashAmount = amount;
            if (_flashMat != null)
            {
                _flashMat.SetColor("_FlashColor", c);
                _flashMat.SetFloat("_FlashAmount", amount);
            }
            else if (_figure != null)
                _figure.color = Color.Lerp(Color.white, c * 1.2f, amount * 0.6f);   // 셰이더가 없으면 색만 살짝
        }

        private void OnDestroy()
        {
            if (_backGlow != null && _backGlow.transform.parent != _body) Destroy(_backGlow.gameObject);
            if (_flashMat != null) Destroy(_flashMat);
        }

        // ---------------- 내부 ----------------

        private static void SetBar(Image fill, float ratio)
        {
            var rt = fill.rectTransform;
            var m = rt.anchorMax; m.x = Mathf.Clamp01(ratio); rt.anchorMax = m;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private void SetHPImmediate(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            SetBar(_hpFill, ratio);
            _hpFill.color = _unit != null && _unit.team == Team.Enemy ? new Color(0.92f, 0.3f, 0.3f) : HPColor(ratio);
            _hpShown = ratio;
        }

        private void AnimateHP(float target)
        {
            if (_hpAnim != null) StopCoroutine(_hpAnim);
            if (!gameObject.activeInHierarchy) { SetHPImmediate(target); SetBar(_hpLag, target); return; }
            _hpAnim = StartCoroutine(HPRoutine(Mathf.Clamp01(target)));
        }

        /// <summary>HP 바는 바로 줄고, 붉은 잔상이 잠시 뒤 따라 줄어듦(회복은 바로).</summary>
        private IEnumerator HPRoutine(float target)
        {
            float from = _hpShown;
            float lagFrom = _hpLag.rectTransform.anchorMax.x;
            for (float t = 0; t < 0.2f; t += Time.unscaledDeltaTime) { SetHPImmediate(Mathf.Lerp(from, target, t / 0.2f)); yield return null; }
            SetHPImmediate(target);
            if (target >= lagFrom) { SetBar(_hpLag, target); _hpAnim = null; yield break; }
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime) { SetBar(_hpLag, Mathf.Lerp(lagFrom, target, t / 0.4f)); yield return null; }
            SetBar(_hpLag, target);
            _hpAnim = null;
        }

        private void SetEnergy(float ratio, bool full)
        {
            SetBar(_energyFill, ratio);
            _energyFill.color = full ? new Color(1f, 0.85f, 0.35f, 1f) : new Color(0.35f, 0.75f, 1f, 1f);
        }

        /// <summary>브레이크 게이지: 남은 양 표시. 브레이크 중에는 비어 있고 붉게 'BREAK'.</summary>
        private void SetToughness(float ratio)
        {
            SetBar(_toughFill, ratio);
            if (_unit == null || _toughText == null) return;
            bool broken = _unit.isBroken;
            _toughText.text = broken ? "BREAK" : $"{_unit.toughness}/{_unit.maxToughness}";
            _toughText.color = broken ? new Color(1f, 0.55f, 0.4f) : (ratio > 0.45f ? new Color(0.1f, 0.11f, 0.16f) : Color.white);
        }

        // ---------------- 속성 / 약점 ----------------

        private static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var ch = t.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
        }

        /// <summary>
        /// 속성 아이콘 1개(부모를 꽉 채움). 그림이 있으면(Resources/Art/Elements/{속성}.png) 그걸 쓰고,
        /// 없으면 속성 색 원 + 이름 첫 글자.
        /// </summary>
        private void FillElementIcon(RectTransform holder, Element e, float size)
        {
            ClearChildren(holder);
            Sprite art = GameArt.Load("Elements/" + e);
            if (art != null)
            {
                var border = Rounded("Border", holder, Traits.ColorOf(e));
                border.pixelsPerUnitMultiplier = 3f;
                UIFactory.Stretch(border.rectTransform);
                var tile = Rounded("Tile", holder, new Color(0.06f, 0.07f, 0.11f, 1f));
                tile.pixelsPerUnitMultiplier = 3f;
                UIFactory.Stretch(tile.rectTransform, 2, 2, 2, 2);
                var img = UIFactory.AddImage("Art", holder, Color.white, art);
                UIFactory.Stretch(img.rectTransform, 3, 3, 3, 3);
                return;
            }
            var rim = UIFactory.AddImage("Rim", holder, new Color(0.05f, 0.06f, 0.09f, 1f), ProceduralSprites.Circle);
            UIFactory.Stretch(rim.rectTransform);
            var fill = UIFactory.AddImage("Fill", holder, Traits.ColorOf(e), ProceduralSprites.Circle);
            UIFactory.Stretch(fill.rectTransform, 2, 2, 2, 2);
            var g = UIFactory.AddText("G", holder, Traits.Glyph(e), Mathf.Max(10, Mathf.RoundToInt(size * 0.52f)), _font,
                new Color(0.08f, 0.09f, 0.13f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(g.rectTransform);
        }

        /// <summary>자기 속성 아이콘 + (적이면) 약점 속성 줄을 만듦.</summary>
        private void BuildElements()
        {
            FillElementIcon(_elementSlot, _unit.element, 24);

            ClearChildren(_weakRow);
            _weakIcons.Clear();
            bool show = _unit.team == Team.Enemy && _unit.weaknesses != null && _unit.weaknesses.Count > 0;
            _weakRow.gameObject.SetActive(show);
            if (!show) return;

            var label = UIFactory.AddText("L", _weakRow, "약점", 13, _font, new Color(1f, 0.9f, 0.7f), TextAlignmentOptions.Center, FontStyles.Bold);
            SubcultureRPG.UI.TextOutline.Apply(label, 0.25f, new Color32(0, 0, 0, 255));
            var lle = label.gameObject.AddComponent<LayoutElement>();
            lle.preferredWidth = 30; lle.preferredHeight = 22;
            foreach (var w in _unit.weaknesses)
            {
                if (_weakIcons.ContainsKey(w)) continue;
                var slot = UIFactory.NewRect("Weak", _weakRow);
                var le = slot.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 22; le.preferredHeight = 22;
                FillElementIcon(slot, w, 22);
                _weakIcons[w] = slot;
            }
        }

        /// <summary>
        /// 약점 어시스트: 지금 행동을 고르는 아군의 속성(element)이 이 적의 약점이면 '속성 아이콘 + 약점!'을 띄움.
        /// </summary>
        public void SetWeakHint(bool on, Element element)
        {
            foreach (var kv in _weakIcons) if (kv.Value != null) kv.Value.localScale = Vector3.one;
            _hintOn = on && _unit != null && _unit.IsAlive && _unit.team == Team.Enemy && _unit.IsWeakTo(element);
            _hintElement = element;
            if (_weakHint == null) return;
            _weakHint.gameObject.SetActive(_hintOn);
            if (!_hintOn) return;
            _weakHintBg.color = Traits.ColorOf(element);
            FillElementIcon(_weakHintIcon, element, 24);
        }

        /// <summary>걸린 효과 중 같은 종류(설명 창 갱신용). 없으면 null.</summary>
        public StatusEffect FindEffect(StatusType t) => _unit != null ? _unit.effects.Find(e => e.type == t) : null;

        /// <summary>상태 아이콘 줄 갱신(내용이 바뀔 때만 다시 만듦).</summary>
        private void UpdateStatus()
        {
            if (_statusRow == null) return;
            var sb = new System.Text.StringBuilder();
            if (_unit != null && _unit.IsAlive)
            {
                if (_unit.isBroken) sb.Append("B;");
                foreach (var e in _unit.effects) sb.Append((int)e.type).Append(':').Append(e.remaining).Append(';');
            }
            string key = sb.ToString();
            if (key == _statusKey) return;
            _statusKey = key;

            for (int i = _statusRow.childCount - 1; i >= 0; i--)
            {
                var ch = _statusRow.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
            if (_unit == null || !_unit.IsAlive) return;

            if (_unit.isBroken) BuildStatusIcon(null);
            foreach (var e in _unit.effects) if (e.IsBuff) BuildStatusIcon(e);
            foreach (var e in _unit.effects) if (!e.IsBuff) BuildStatusIcon(e);
        }

        /// <summary>아이콘 1개: 색 바탕 + 글자(공/방/독) + ↑↓ 화살표 + 남은 턴 숫자. effect == null이면 브레이크.</summary>
        private void BuildStatusIcon(StatusEffect effect)
        {
            bool isBreak = effect == null;
            Color bg = isBreak ? new Color(0.85f, 0.58f, 0.18f) : StatusText.ColorOf(effect.type);

            var icon = Rounded("StatusIcon", _statusRow, bg, raycast: true);
            icon.pixelsPerUnitMultiplier = 3f;
            var le = icon.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 28; le.preferredHeight = 28;

            Sprite art = isBreak ? GameArt.Load("Status/Break") : GameArt.Load("Status/" + effect.type);
            if (art != null)
            {
                var tile = Rounded("Tile", icon.transform, new Color(0.08f, 0.09f, 0.13f, 1f));
                tile.pixelsPerUnitMultiplier = 3f;
                UIFactory.Stretch(tile.rectTransform, 2, 2, 2, 2);
                var img = UIFactory.AddImage("Art", icon.transform, Color.white, art);
                UIFactory.Stretch(img.rectTransform, 3, 3, 3, 3);
            }
            else
            {
                var glyph = UIFactory.AddText("Glyph", icon.transform, isBreak ? "B" : StatusText.Glyph(effect.type), 15, _font,
                    Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Stretch(glyph.rectTransform, 0, 2, 6, 0);
                if (!isBreak && StatusText.Arrow(effect.type) != 0)
                {
                    bool up = StatusText.Arrow(effect.type) > 0;
                    var arrow = UIFactory.AddImage("Arrow", icon.transform, up ? new Color(0.75f, 1f, 0.8f) : new Color(1f, 0.85f, 0.8f), ProceduralSprites.Triangle);
                    var art2 = arrow.rectTransform;
                    UIFactory.Anchor(art2, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
                    art2.sizeDelta = new Vector2(11, 11); art2.anchoredPosition = new Vector2(-1, -2);
                    if (!up) art2.localEulerAngles = new Vector3(0, 0, 180f);
                    if (!up) art2.anchoredPosition = new Vector2(-12, -13);
                }
            }

            if (!isBreak)
            {
                var badge = UIFactory.AddImage("Turns", icon.transform, new Color(0.05f, 0.06f, 0.09f, 0.95f), ProceduralSprites.Circle);
                var brt = badge.rectTransform;
                UIFactory.Anchor(brt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
                brt.sizeDelta = new Vector2(16, 16); brt.anchoredPosition = new Vector2(4, -4);
                var num = UIFactory.AddText("N", badge.transform, effect.remaining.ToString(), 11, _font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Stretch(num.rectTransform);
            }

            var btn = icon.gameObject.AddComponent<Button>();
            btn.targetGraphic = icon;
            var rt = icon.rectTransform;
            btn.onClick.AddListener(() => _onStatus?.Invoke(this, effect, rt));
        }

        private static Color HPColor(float ratio) =>
            ratio > 0.5f ? new Color(0.3f, 0.85f, 0.4f) :
            ratio > 0.25f ? new Color(0.95f, 0.8f, 0.3f) :
            new Color(0.95f, 0.35f, 0.35f);
    }
}
