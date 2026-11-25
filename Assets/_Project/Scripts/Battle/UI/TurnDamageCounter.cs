using System.Collections.Generic;
using UnityEngine;
using TMPro;
using SubcultureRPG.UI;

namespace SubcultureRPG.Battle.UI
{
    /// <summary>
    /// 화면 오른쪽 '총 피해량': 아군 1명의 턴 동안 그 캐릭터가 적에게 준 피해 합계(다단 히트 · 연쇄 시전 · 추가 공격 포함).
    /// 피해가 들어올 때마다 숫자가 빠르게 올라가고 살짝 튀며, 다음 턴이 시작되면 잠시 뒤 사라짐.
    /// 개별 피해 숫자는 지금처럼 적 위에 뜨고, 여기에는 합계만 표시합니다.
    /// </summary>
    public class TurnDamageCounter : MonoBehaviour
    {
        private RectTransform _rt;
        private CanvasGroup _cg;
        private TextMeshProUGUI _label, _value;
        private BattleUnit _actor;
        private long _total;
        private float _shown, _punch, _hideAt = -1f, _alpha;

        public static TurnDamageCounter Create(Transform parent, TMP_FontAsset font)
        {
            var rt = UIFactory.NewRect("TurnDamage", parent);
            UIFactory.Anchor(rt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            rt.sizeDelta = new Vector2(420, 110); rt.anchoredPosition = new Vector2(-36, -96);
            var c = rt.gameObject.AddComponent<TurnDamageCounter>();
            c._rt = rt;
            c._cg = rt.gameObject.AddComponent<CanvasGroup>();
            c._cg.alpha = 0f; c._cg.blocksRaycasts = false; c._cg.interactable = false;

            // 오른쪽에서 왼쪽으로 옅어지는 띠
            var band = UIFactory.AddImage("Band", rt, new Color(0.03f, 0.03f, 0.06f, 0.55f), ProceduralSprites.Beam);
            band.preserveAspect = false;
            UIFactory.Stretch(band.rectTransform, 40, 6, -36, 6);
            var line = UIFactory.AddImage("Line", rt, new Color(1f, 0.8f, 0.35f, 0.9f));
            UIFactory.Anchor(line.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
            line.rectTransform.sizeDelta = new Vector2(300, 2); line.rectTransform.anchoredPosition = new Vector2(0, 10);

            c._label = UIFactory.AddText("Label", rt, "총 피해량", 22, font, new Color(1f, 0.92f, 0.75f), TextAlignmentOptions.Right, FontStyles.Bold);
            UIFactory.Anchor(c._label.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            c._label.rectTransform.sizeDelta = new Vector2(400, 30); c._label.rectTransform.anchoredPosition = new Vector2(0, -4);

            var nf = GameFonts.Number(font);
            c._value = UIFactory.AddText("Value", rt, "0", 58, nf, new Color(1f, 0.84f, 0.4f), TextAlignmentOptions.Right, nf == font ? FontStyles.Bold | FontStyles.Italic : FontStyles.Italic);
            UIFactory.Anchor(c._value.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 0.5f));
            c._value.rectTransform.sizeDelta = new Vector2(420, 70); c._value.rectTransform.anchoredPosition = new Vector2(0, -66);
            TextOutline.Apply(c._value, 0.16f, new Color32(30, 16, 4, 255));
            return c;
        }

        /// <summary>마지막 피해 뒤 이만큼(초) 계속 보여 줌 — 다음 턴이 빨리 와도 숫자를 읽을 수 있게.</summary>
        public const float HoldSeconds = 1.8f;

        /// <summary>
        /// 새 턴 시작: 아군이면 그 캐릭터 기준으로 새로 셀 준비만 함(숫자는 그 캐릭터의 첫 피해가 들어올 때 바꿈).
        /// 턴이 바뀌어도 앞 캐릭터의 숫자를 바로 0으로 만들거나 숨기지 않고, 마지막 피해 뒤 HoldSeconds 동안 보여 준 뒤 사라짐.
        /// </summary>
        public void BeginTurn(BattleUnit unit)
        {
            _actor = unit != null && unit.team == Team.Ally ? unit : null;
            _pendingReset = true;
        }

        private bool _pendingReset;

        /// <summary>행동 결과 반영(이번 턴 캐릭터가 적에게 준 피해만).</summary>
        public void Add(BattleUnit actor, List<ActionResult.TargetHit> hits)
        {
            if (_actor == null || hits == null) return;
            long add = 0;
            foreach (var h in hits)
            {
                var src = h.extra && h.source != null ? h.source : actor;
                if (src != _actor || h.target == null || h.target.team == _actor.team) continue;
                add += Mathf.Max(0, h.totalDamage) + Mathf.Max(0, h.breakBonusDamage);
            }
            if (add <= 0) return;
            if (_pendingReset)
            {
                // 새 캐릭터의 첫 피해: 이 순간에 새로 셈(앞 숫자 → 바로 새 숫자로 굴러감, 0이 보이지 않게 첫 피해의 절반에서 시작)
                _pendingReset = false;
                _total = 0;
                _shown = add * 0.5f;
            }
            _total += add;
            _hideAt = Time.unscaledTime + HoldSeconds;
            _punch = 1f;
        }

        public void Clear()
        {
            _actor = null; _total = 0; _shown = 0f; _hideAt = 0f; _alpha = 0f; _pendingReset = false;
            if (_cg != null) _cg.alpha = 0f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool visible = _total > 0 && Time.unscaledTime < _hideAt;
            _alpha = Mathf.MoveTowards(_alpha, visible ? 1f : 0f, dt * (visible ? 8f : 3f));
            _cg.alpha = _alpha;
            if (_alpha <= 0f && !visible) return;

            // 숫자 굴리기(남은 차이의 비율만큼 → 큰 피해도 0.4초쯤에 따라잡음)
            if (_shown < _total) _shown = Mathf.Min(_total, _shown + Mathf.Max((_total - _shown) * dt * 9f, dt * 400f));
            _value.text = Mathf.RoundToInt(_shown).ToString("N0");

            _punch = Mathf.MoveTowards(_punch, 0f, dt * 5f);
            float s = 1f + 0.14f * _punch * _punch;
            _value.rectTransform.localScale = new Vector3(s, s, 1f);
            _rt.anchoredPosition = new Vector2(-36 + (1f - _alpha) * 40f, -96);
        }
    }
}
