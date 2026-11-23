using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;

namespace SubcultureRPG.Battle.UI
{
    /// <summary>
    /// 전투 유닛 1명의 상태 패널(이름·HP·에너지·현재 턴 하이라이트). 코드로 구성.
    /// 클릭 시 onClick(유닛) 호출 → HUD가 대상 선택 처리.
    /// </summary>
    public class UnitPanel : MonoBehaviour
    {
        private BattleUnit _unit;
        private TMP_FontAsset _font;
        private Action<BattleUnit> _onClick;

        private Image _bg, _highlight, _portrait, _hpFill, _energyFill, _toughFill, _deadOverlay;
        private GameObject _toughBarGO;
        private TextMeshProUGUI _nameText, _hpText, _statusText;
        private Button _button;
        private Coroutine _hpAnim;
        private float _hpShown = 1f;

        public BattleUnit Unit => _unit;

        public static UnitPanel Create(Transform parent, TMP_FontAsset font, Action<BattleUnit> onClick)
        {
            var rt = UIFactory.NewRect("UnitPanel", parent);
            var p = rt.gameObject.AddComponent<UnitPanel>();
            p._font = font;
            p._onClick = onClick;
            p.Build();
            return p;
        }

        private void Build()
        {
            var le = gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 210;
            le.preferredHeight = 92;

            // 현재 턴 하이라이트(패널보다 살짝 크게, 뒤에)
            _highlight = UIFactory.AddImage("Highlight", transform, new Color(1f, 0.85f, 0.35f, 0f));
            UIFactory.Stretch(_highlight.rectTransform, -3, -3, -3, -3);

            _bg = UIFactory.AddImage("BG", transform, new Color(0.12f, 0.13f, 0.17f, 0.96f), null, raycast: true);
            UIFactory.Stretch(_bg.rectTransform);
            _button = _bg.gameObject.AddComponent<Button>();
            _button.targetGraphic = _bg;
            _button.onClick.AddListener(() => _onClick?.Invoke(_unit));

            // 포트레이트(좌측)
            _portrait = UIFactory.AddImage("Portrait", transform, new Color(1, 1, 1, 0.12f));
            var prt = _portrait.rectTransform;
            UIFactory.Anchor(prt, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f));
            prt.sizeDelta = new Vector2(64, -16);
            prt.anchoredPosition = new Vector2(8, 0);
            _portrait.preserveAspect = true;

            // 우측 영역(이름/HP/에너지)
            var right = UIFactory.NewRect("Right", transform);
            right.anchorMin = Vector2.zero; right.anchorMax = Vector2.one; right.pivot = new Vector2(0.5f, 0.5f);
            right.offsetMin = new Vector2(80, 8); right.offsetMax = new Vector2(-8, -8);

            _nameText = UIFactory.AddText("Name", right, "", 16, _font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            var nrt = _nameText.rectTransform;
            UIFactory.Anchor(nrt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            nrt.sizeDelta = new Vector2(0, 22); nrt.anchoredPosition = Vector2.zero;

            // HP 바
            var hpBar = UIFactory.NewRect("HPBar", right);
            UIFactory.Anchor(hpBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            hpBar.sizeDelta = new Vector2(0, 18); hpBar.anchoredPosition = new Vector2(0, -26);
            var hpBg = UIFactory.AddImage("BG", hpBar, new Color(0, 0, 0, 0.5f));
            UIFactory.Stretch(hpBg.rectTransform);
            _hpFill = UIFactory.AddImage("Fill", hpBar, new Color(0.3f, 0.85f, 0.4f, 1f));
            UIFactory.Stretch(_hpFill.rectTransform);
            _hpText = UIFactory.AddText("T", hpBar, "", 12, _font, Color.white, TextAlignmentOptions.Center);
            UIFactory.Stretch(_hpText.rectTransform);

            // 에너지 바
            var enBar = UIFactory.NewRect("EnergyBar", right);
            UIFactory.Anchor(enBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            enBar.sizeDelta = new Vector2(0, 6); enBar.anchoredPosition = new Vector2(0, -48);
            var enBg = UIFactory.AddImage("BG", enBar, new Color(0, 0, 0, 0.5f));
            UIFactory.Stretch(enBg.rectTransform);
            _energyFill = UIFactory.AddImage("Fill", enBar, new Color(0.35f, 0.75f, 1f, 1f));
            UIFactory.Stretch(_energyFill.rectTransform);

            // 내구도 바(브레이크) — HasBreakSystem일 때만 표시
            var tgBar = UIFactory.NewRect("ToughBar", right);
            UIFactory.Anchor(tgBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            tgBar.sizeDelta = new Vector2(0, 5); tgBar.anchoredPosition = new Vector2(0, -56);
            var tgBg = UIFactory.AddImage("BG", tgBar, new Color(0, 0, 0, 0.5f));
            UIFactory.Stretch(tgBg.rectTransform);
            _toughFill = UIFactory.AddImage("Fill", tgBar, new Color(1f, 0.7f, 0.25f, 1f));
            UIFactory.Stretch(_toughFill.rectTransform);
            _toughBarGO = tgBar.gameObject;

            // 상태(버프/디버프) 표시 — 우상단
            _statusText = UIFactory.AddText("Status", transform, "", 13, _font, Color.white, TextAlignmentOptions.TopRight);
            var sttr = _statusText.rectTransform;
            UIFactory.Anchor(sttr, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            sttr.sizeDelta = new Vector2(90, 20); sttr.anchoredPosition = new Vector2(-6, -6);

            // 격파 오버레이
            _deadOverlay = UIFactory.AddImage("Dead", transform, new Color(0, 0, 0, 0.55f));
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
            _portrait.preserveAspect = true;

            _hpShown = unit.HPRatio;
            SetHPImmediate(unit.HPRatio);
            SetEnergy(unit.EnergyRatio, unit.UltimateReady);
            _toughBarGO.SetActive(unit.HasBreakSystem);
            SetToughness(unit.ToughnessRatio);
            UpdateStatus();
            _hpText.text = $"{unit.currentHP}/{unit.maxHP}";
            _deadOverlay.gameObject.SetActive(!unit.IsAlive);
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

        public void SetCurrent(bool on)
        {
            _highlight.color = new Color(1f, 0.85f, 0.35f, on ? 0.9f : 0f);
        }

        /// <summary>대상 선택 중 '고를 수 있음' 표시(붉은 하이라이트).</summary>
        public void SetTargetable(bool on)
        {
            _highlight.color = new Color(1f, 0.4f, 0.4f, on ? 0.9f : 0f);
        }

        // ---- 내부 ----

        private void SetHPImmediate(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            var m = _hpFill.rectTransform.anchorMax; m.x = ratio; _hpFill.rectTransform.anchorMax = m;
            _hpFill.rectTransform.offsetMin = Vector2.zero; _hpFill.rectTransform.offsetMax = Vector2.zero;
            _hpFill.color = HPColor(ratio);
            _hpShown = ratio;
        }

        private void AnimateHP(float target)
        {
            if (_hpAnim != null) StopCoroutine(_hpAnim);
            if (!gameObject.activeInHierarchy) { SetHPImmediate(target); return; }
            _hpAnim = StartCoroutine(HPRoutine(Mathf.Clamp01(target)));
        }

        private IEnumerator HPRoutine(float target)
        {
            float from = _hpShown, t = 0f, dur = 0.3f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                SetHPImmediate(Mathf.Lerp(from, target, t / dur));
                yield return null;
            }
            SetHPImmediate(target);
            _hpAnim = null;
        }

        private void SetEnergy(float ratio, bool full)
        {
            ratio = Mathf.Clamp01(ratio);
            var rt = _energyFill.rectTransform;
            var m = rt.anchorMax; m.x = ratio; rt.anchorMax = m;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _energyFill.color = full ? new Color(1f, 0.85f, 0.35f, 1f) : new Color(0.35f, 0.75f, 1f, 1f);
        }

        private void SetToughness(float ratio)
        {
            var rt = _toughFill.rectTransform;
            var m = rt.anchorMax; m.x = Mathf.Clamp01(ratio); rt.anchorMax = m;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private void UpdateStatus()
        {
            if (_unit == null) { _statusText.text = ""; return; }
            int b = _unit.BuffCount, d = _unit.DebuffCount;
            if (b == 0 && d == 0) { _statusText.text = ""; return; }
            string s = "";
            if (b > 0) s += $"<color=#6FE08F>+{b}</color>";
            if (d > 0) s += (b > 0 ? " " : "") + $"<color=#FF7A7A>-{d}</color>";
            _statusText.text = s;
        }

        private static Color HPColor(float ratio) =>
            ratio > 0.5f ? new Color(0.3f, 0.85f, 0.4f) :
            ratio > 0.25f ? new Color(0.95f, 0.8f, 0.3f) :
            new Color(0.95f, 0.35f, 0.35f);
    }
}
