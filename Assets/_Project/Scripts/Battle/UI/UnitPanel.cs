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
    /// 전투 유닛 1명의 상태 패널(초상·이름·HP·에너지·내구도·버프/디버프·현재 턴 하이라이트). 코드로 구성.
    /// 그림은 안쪽 Body에 그려서, 줄(레이아웃) 위치와 상관없이 돌진·피격 흔들림을 줄 수 있습니다.
    /// 클릭 시 onClick(유닛) 호출 → HUD가 대상 선택 처리.
    /// </summary>
    public class UnitPanel : MonoBehaviour
    {
        private BattleUnit _unit;
        private TMP_FontAsset _font;
        private Action<BattleUnit> _onClick;
        private Action<UnitPanel, StatusEffect, RectTransform> _onStatus;   // 상태 아이콘 클릭(효과 null = 브레이크)
        private RectTransform _statusRow;
        private string _statusKey = "";

        private RectTransform _body;
        private CanvasGroup _group;
        private Image _bg, _highlight, _portrait, _hpFill, _hpLag, _energyFill, _toughFill, _deadOverlay, _flash;
        private GameObject _toughBarGO;
        private TextMeshProUGUI _toughText;
        private RectTransform _elementSlot, _weakRow, _weakHint, _weakHintIcon;
        private Image _weakHintBg;
        private readonly System.Collections.Generic.Dictionary<Element, RectTransform> _weakIcons = new();
        private bool _hintOn;
        private Element _hintElement;
        private TextMeshProUGUI _nameText, _hpText;
        private Button _button;
        private Coroutine _hpAnim, _moveAnim, _flashAnim;
        private float _hpShown = 1f;
        private bool _current, _targetable;

        public BattleUnit Unit => _unit;
        public RectTransform Body => _body;

        public static UnitPanel Create(Transform parent, TMP_FontAsset font, Action<BattleUnit> onClick,
            Action<UnitPanel, StatusEffect, RectTransform> onStatus = null)
        {
            var rt = UIFactory.NewRect("UnitPanel", parent);
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
            var le = gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 220;
            le.preferredHeight = 96;

            _body = UIFactory.NewRect("Body", transform);
            UIFactory.Stretch(_body);
            _group = _body.gameObject.AddComponent<CanvasGroup>();

            // 현재 턴 / 대상 / 필살기 준비 하이라이트(패널보다 살짝 크게, 뒤에)
            _highlight = Rounded("Highlight", _body, new Color(1f, 0.85f, 0.35f, 0f));
            UIFactory.Stretch(_highlight.rectTransform, -4, -4, -4, -4);

            _bg = Rounded("BG", _body, new Color(0.11f, 0.12f, 0.17f, 0.94f), raycast: true);
            UIFactory.Stretch(_bg.rectTransform);
            _button = _bg.gameObject.AddComponent<Button>();
            _button.targetGraphic = _bg;
            _button.onClick.AddListener(() => _onClick?.Invoke(_unit));

            // 포트레이트(좌측)
            var frame = Rounded("PortraitFrame", _body, new Color(1, 1, 1, 0.08f));
            var frt = frame.rectTransform;
            UIFactory.Anchor(frt, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f));
            frt.sizeDelta = new Vector2(70, -12); frt.anchoredPosition = new Vector2(6, 0);
            _portrait = UIFactory.AddImage("Portrait", frame.transform, Color.white);
            UIFactory.Stretch(_portrait.rectTransform, 2, 2, 2, 2);
            _portrait.preserveAspect = true;

            // 우측 영역(이름/HP/에너지)
            var right = UIFactory.NewRect("Right", _body);
            right.anchorMin = Vector2.zero; right.anchorMax = Vector2.one; right.pivot = new Vector2(0.5f, 0.5f);
            right.offsetMin = new Vector2(84, 8); right.offsetMax = new Vector2(-8, -8);

            _nameText = UIFactory.AddText("Name", right, "", 16, _font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            var nrt = _nameText.rectTransform;
            UIFactory.Anchor(nrt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            nrt.sizeDelta = new Vector2(0, 22); nrt.anchoredPosition = Vector2.zero;

            // HP 바(줄어든 만큼 잠시 남는 붉은 잔상 포함)
            var hpBar = UIFactory.NewRect("HPBar", right);
            UIFactory.Anchor(hpBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            hpBar.sizeDelta = new Vector2(0, 18); hpBar.anchoredPosition = new Vector2(0, -26);
            var hpBg = UIFactory.AddImage("BG", hpBar, new Color(0, 0, 0, 0.55f));
            UIFactory.Stretch(hpBg.rectTransform);
            _hpLag = UIFactory.AddImage("Lag", hpBar, new Color(1f, 0.35f, 0.3f, 0.85f));
            UIFactory.Stretch(_hpLag.rectTransform);
            _hpFill = UIFactory.AddImage("Fill", hpBar, new Color(0.3f, 0.85f, 0.4f, 1f));
            UIFactory.Stretch(_hpFill.rectTransform);
            _hpText = UIFactory.AddText("T", hpBar, "", 12, _font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_hpText.rectTransform);

            // 에너지 바
            var enBar = UIFactory.NewRect("EnergyBar", right);
            UIFactory.Anchor(enBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            enBar.sizeDelta = new Vector2(0, 7); enBar.anchoredPosition = new Vector2(0, -48);
            var enBg = UIFactory.AddImage("BG", enBar, new Color(0, 0, 0, 0.55f));
            UIFactory.Stretch(enBg.rectTransform);
            _energyFill = UIFactory.AddImage("Fill", enBar, new Color(0.35f, 0.75f, 1f, 1f));
            UIFactory.Stretch(_energyFill.rectTransform);

            // 내구도 바(브레이크) — HasBreakSystem일 때만 표시
            var tgBar = UIFactory.NewRect("ToughBar", right);
            UIFactory.Anchor(tgBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            tgBar.sizeDelta = new Vector2(0, 13); tgBar.anchoredPosition = new Vector2(0, -59);
            var tgBg = UIFactory.AddImage("BG", tgBar, new Color(0, 0, 0, 0.7f));
            UIFactory.Stretch(tgBg.rectTransform);
            _toughFill = UIFactory.AddImage("Fill", tgBar, new Color(0.92f, 0.94f, 1f, 1f));
            UIFactory.Stretch(_toughFill.rectTransform);
            _toughText = UIFactory.AddText("T", tgBar, "", 10, _font, new Color(0.1f, 0.11f, 0.16f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(_toughText.rectTransform);
            _toughBarGO = tgBar.gameObject;

            // 속성 아이콘 자리(초상 왼쪽 위 모서리)
            _elementSlot = UIFactory.NewRect("ElementSlot", _body);
            UIFactory.Anchor(_elementSlot, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            _elementSlot.sizeDelta = new Vector2(28, 28); _elementSlot.anchoredPosition = new Vector2(-6, 6);

            // 약점 속성 줄(적만) — 패널 바로 위 왼쪽: '약점' + 속성 아이콘들
            _weakRow = UIFactory.NewRect("WeakRow", _body);
            UIFactory.Anchor(_weakRow, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0));
            _weakRow.sizeDelta = new Vector2(0, 24); _weakRow.anchoredPosition = new Vector2(26, 4);
            var wrl = _weakRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            wrl.spacing = 4; wrl.childAlignment = TextAnchor.MiddleLeft;
            wrl.childControlWidth = true; wrl.childControlHeight = true;
            wrl.childForceExpandWidth = false; wrl.childForceExpandHeight = false;

            // 약점 어시스트 표시(패널 위 오른쪽): 지금 행동하는 아군 속성이 이 적의 약점일 때 '속성 아이콘 + 약점!'
            _weakHintBg = Rounded("WeakHint", _body, Color.white);
            _weakHintBg.pixelsPerUnitMultiplier = 3f;
            _weakHint = _weakHintBg.rectTransform;
            UIFactory.Anchor(_weakHint, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0));
            _weakHint.sizeDelta = new Vector2(84, 28); _weakHint.anchoredPosition = new Vector2(2, 3);
            _weakHintIcon = UIFactory.NewRect("Icon", _weakHint);
            UIFactory.Anchor(_weakHintIcon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            _weakHintIcon.sizeDelta = new Vector2(24, 24); _weakHintIcon.anchoredPosition = new Vector2(2, 0);
            var hintTxt = UIFactory.AddText("T", _weakHint, "약점!", 15, _font, new Color(0.08f, 0.09f, 0.13f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(hintTxt.rectTransform, 26, 0, 4, 0);
            _weakHint.gameObject.SetActive(false);

            // 상태(버프/디버프) 아이콘 줄 — 패널 바로 아래. 아이콘마다 남은 턴 숫자, 누르면 설명
            _statusRow = UIFactory.NewRect("StatusRow", _body);
            UIFactory.Anchor(_statusRow, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1));
            _statusRow.sizeDelta = new Vector2(0, 32); _statusRow.anchoredPosition = new Vector2(2, -5);
            var srl = _statusRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            srl.spacing = 5; srl.childAlignment = TextAnchor.UpperLeft;
            srl.childControlWidth = true; srl.childControlHeight = true;
            srl.childForceExpandWidth = false; srl.childForceExpandHeight = false;

            // 피격 번쩍임(맨 위, 클릭 통과)
            _flash = Rounded("Flash", _body, new Color(1, 1, 1, 0));
            UIFactory.Stretch(_flash.rectTransform);

            // 격파 오버레이
            _deadOverlay = Rounded("Dead", _body, new Color(0, 0, 0, 0.62f));
            UIFactory.Stretch(_deadOverlay.rectTransform);
            var deadTxt = UIFactory.AddText("격파", _deadOverlay.transform, "격파", 20, _font, new Color(1f, 0.5f, 0.5f), TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(deadTxt.rectTransform);
            _deadOverlay.gameObject.SetActive(false);
        }

        public void Bind(BattleUnit unit)
        {
            _unit = unit;
            _nameText.text = unit.Name;

            Sprite sprite = unit.character != null ? GameArt.Thumb(unit.character) : GameArt.Enemy(unit.Name);
            _portrait.sprite = sprite;
            _portrait.enabled = sprite != null;
            _portrait.color = Color.white;

            _hpShown = unit.HPRatio;
            SetHPImmediate(unit.HPRatio);
            SetBar(_hpLag, unit.HPRatio);
            SetEnergy(unit.EnergyRatio, unit.UltimateReady);
            _toughBarGO.SetActive(unit.HasBreakSystem);
            SetToughness(unit.ToughnessRatio);
            BuildElements();
            SetWeakHint(false, unit.element);
            _hpText.text = $"{unit.currentHP}/{unit.maxHP}";
            _deadOverlay.gameObject.SetActive(!unit.IsAlive);
            _body.anchoredPosition = Vector2.zero;
            _body.localScale = Vector3.one;
            _group.alpha = 1f;
            _statusKey = "?";   // 새 유닛 → 아이콘 다시 만들기
            UpdateStatus();
            SetCurrent(false);
        }

        /// <summary>바/격파 상태 갱신(HP는 부드럽게).</summary>
        public void Refresh()
        {
            if (_unit == null) return;
            _hpText.text = $"{_unit.currentHP}/{_unit.maxHP}";
            SetEnergy(_unit.EnergyRatio, _unit.UltimateReady);
            if (_toughBarGO.activeSelf) SetToughness(_unit.ToughnessRatio);
            UpdateStatus();
            AnimateHP(_unit.HPRatio);
            _deadOverlay.gameObject.SetActive(!_unit.IsAlive);
            _button.interactable = _unit.IsAlive;
            if (!_unit.IsAlive) SetCurrent(false);
        }

        public void SetCurrent(bool on) { _current = on; _targetable = false; }

        /// <summary>대상 선택 중 '고를 수 있음' 표시(붉은 하이라이트).</summary>
        public void SetTargetable(bool on) => _targetable = on;

        private void Update()
        {
            if (_highlight == null) return;
            // 우선순위: 대상 선택(빨강) > 현재 턴(금색) > 필살기 준비(금빛 맥동) > 없음
            Color c;
            if (_targetable) c = new Color(1f, 0.4f, 0.4f, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f));
            else if (_current) c = new Color(1f, 0.85f, 0.35f, 0.95f);
            else if (_unit != null && _unit.IsAlive && _unit.team == Team.Ally && _unit.UltimateReady)
                c = new Color(1f, 0.75f, 0.25f, 0.35f + 0.3f * Mathf.Sin(Time.unscaledTime * 4f));
            else c = new Color(1f, 0.85f, 0.35f, 0f);
            _highlight.color = c;

            if (_hintOn)
            {
                // 약점 표시는 살짝 커졌다 작아지며 눈에 띄게
                float wave = Mathf.Sin(Time.unscaledTime * 7f);
                _weakHint.localScale = Vector3.one * (1f + 0.08f * wave);
                if (_weakIcons.TryGetValue(_hintElement, out var wi) && wi != null) wi.localScale = Vector3.one * (1.12f + 0.12f * wave);
                if (_unit == null || !_unit.IsAlive) SetWeakHint(false, _hintElement);
            }
        }

        // ---------------- 연출 ----------------

        /// <summary>행동 돌진: dir 방향으로 튀어나갔다 돌아옴.</summary>
        public void PlayLunge(Vector2 dir, float dur)
        {
            if (!gameObject.activeInHierarchy) return;
            if (_moveAnim != null) StopCoroutine(_moveAnim);
            _moveAnim = StartCoroutine(LungeRoutine(dir, dur));
        }

        private IEnumerator LungeRoutine(Vector2 dir, float dur)
        {
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                float s = k < 0.45f ? Mathf.SmoothStep(0, 1, k / 0.45f) : Mathf.SmoothStep(1, 0, (k - 0.45f) / 0.55f);
                _body.anchoredPosition = dir * s;
                _body.localScale = Vector3.one * (1f + 0.06f * s);
                yield return null;
            }
            _body.anchoredPosition = Vector2.zero;
            _body.localScale = Vector3.one;
            _moveAnim = null;
        }

        /// <summary>전투 종료 시 진 쪽 패널: 아래로 가라앉으며 작아지고 흐려짐.</summary>
        public void PlayDefeat(float dur)
        {
            if (!gameObject.activeInHierarchy) return;
            if (_moveAnim != null) StopCoroutine(_moveAnim);
            _moveAnim = StartCoroutine(DefeatRoutine(dur));
        }

        private IEnumerator DefeatRoutine(float dur)
        {
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float e = Mathf.SmoothStep(0, 1, t / dur);
                _body.anchoredPosition = new Vector2(0, -18f * e);
                _body.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, e);
                _group.alpha = Mathf.Lerp(1f, 0.35f, e);
                yield return null;
            }
            _body.anchoredPosition = new Vector2(0, -18f);
            _body.localScale = Vector3.one * 0.9f;
            _group.alpha = 0.35f;
            _moveAnim = null;
        }

        /// <summary>피격: 번쩍임(색) + 좌우 흔들림.</summary>
        public void PlayHit(Color flash, float amplitude, float dur)
        {
            if (!gameObject.activeInHierarchy) return;
            if (_moveAnim != null) StopCoroutine(_moveAnim);
            _moveAnim = StartCoroutine(ShakeRoutine(amplitude, dur));
            if (_flashAnim != null) StopCoroutine(_flashAnim);
            _flashAnim = StartCoroutine(FlashRoutine(flash, dur));
        }

        private IEnumerator ShakeRoutine(float amp, float dur)
        {
            _body.localScale = Vector3.one;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / dur;
                _body.anchoredPosition = new Vector2(Mathf.Sin(t * 70f) * amp * k, Mathf.Cos(t * 55f) * amp * 0.3f * k);
                yield return null;
            }
            _body.anchoredPosition = Vector2.zero;
            _moveAnim = null;
        }

        private IEnumerator FlashRoutine(Color c, float dur)
        {
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                _flash.color = new Color(c.r, c.g, c.b, 0.65f * (1f - t / dur));
                yield return null;
            }
            _flash.color = new Color(1, 1, 1, 0);
            _flashAnim = null;
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
            _hpFill.color = HPColor(ratio);
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
                // 그림(어두운 바탕의 정사각 아이콘): 속성 색 테두리의 둥근 칸 안에
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
            FillElementIcon(_elementSlot, _unit.element, 28);

            ClearChildren(_weakRow);
            _weakIcons.Clear();
            bool show = _unit.team == Team.Enemy && _unit.weaknesses != null && _unit.weaknesses.Count > 0;
            _weakRow.gameObject.SetActive(show);
            if (!show) return;

            var label = UIFactory.AddText("L", _weakRow, "약점", 13, _font, new Color(1f, 0.9f, 0.7f), TextAlignmentOptions.Center, FontStyles.Bold);
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
            // 버프 먼저, 그다음 디버프
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
            le.preferredWidth = 32; le.preferredHeight = 32;

            // 아이콘 그림이 있으면(Resources/Art/Status/{종류}.png) 그걸 사용
            Sprite art = isBreak ? GameArt.Load("Status/Break") : GameArt.Load("Status/" + effect.type);
            if (art != null)
            {
                // 그림은 어두운 바탕 위에(색 화살표가 잘 보이게), 종류 색은 테두리로 남김
                var tile = Rounded("Tile", icon.transform, new Color(0.08f, 0.09f, 0.13f, 1f));
                tile.pixelsPerUnitMultiplier = 3f;
                UIFactory.Stretch(tile.rectTransform, 2, 2, 2, 2);
                var img = UIFactory.AddImage("Art", icon.transform, Color.white, art);
                UIFactory.Stretch(img.rectTransform, 3, 3, 3, 3);
            }
            else
            {
                var glyph = UIFactory.AddText("Glyph", icon.transform, isBreak ? "B" : StatusText.Glyph(effect.type), 17, _font,
                    Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Stretch(glyph.rectTransform, 0, 2, 6, 0);
                if (!isBreak && StatusText.Arrow(effect.type) != 0)
                {
                    bool up = StatusText.Arrow(effect.type) > 0;
                    var arrow = UIFactory.AddImage("Arrow", icon.transform, up ? new Color(0.75f, 1f, 0.8f) : new Color(1f, 0.85f, 0.8f), ProceduralSprites.Triangle);
                    var art2 = arrow.rectTransform;
                    UIFactory.Anchor(art2, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
                    art2.sizeDelta = new Vector2(12, 12); art2.anchoredPosition = new Vector2(-1, -2);
                    if (!up) art2.localEulerAngles = new Vector3(0, 0, 180f);
                    if (!up) art2.anchoredPosition = new Vector2(-13, -14);   // 180도 회전 보정(피벗이 오른쪽 위)
                }
            }

            // 남은 턴(오른쪽 아래)
            if (!isBreak)
            {
                var badge = UIFactory.AddImage("Turns", icon.transform, new Color(0.05f, 0.06f, 0.09f, 0.95f), ProceduralSprites.Circle);
                var brt = badge.rectTransform;
                UIFactory.Anchor(brt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
                brt.sizeDelta = new Vector2(17, 17); brt.anchoredPosition = new Vector2(4, -4);
                var num = UIFactory.AddText("N", badge.transform, effect.remaining.ToString(), 12, _font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
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
