using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 캐릭터 육성 화면.
    /// 목록에서 캐릭터를 고르면 카드가 회전하며 앞으로 확대된 뒤 왼쪽으로 이동하고,
    /// 오른쪽에 상세 능력치와 [레벨 · 장비 · 돌파 · 스킬 · 초월] 탭이 나타납니다.
    /// 코드 자동 구성 — roster/database/inventory/wallet/equipment 연결(비우면 자동 탐색).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Growth Screen")]
    public class GrowthScreen : MonoBehaviour
    {
        [SerializeField] private PlayerRoster roster;
        [SerializeField] private GameDatabase database;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private TMP_FontAsset font;

        private enum GrowTab { Level, Equip, Ascend, Skill, Transcend }
        private static readonly string[] TabNames = { "레벨", "장비", "돌파", "스킬", "초월" };

        // 공통
        private GameObject _root;
        private TextMeshProUGUI _goldText, _toast, _backLabel;
        private Button _backBtn;
        private System.Action _onBack;
        private EquipmentPanel _equipPanel;

        // 목록
        private CanvasGroup _listGroup;
        private RectTransform _grid;
        private RosterFilterBar _filterBar;
        private readonly Dictionary<CharacterData, RectTransform> _cardRT = new();
        private readonly Vector2 _cell = new(130, 160);

        // 상세
        private CanvasGroup _detailGroup;
        private RectTransform _detailPanel, _heroLayer, _hero;
        private TextMeshProUGUI _hName, _hLine, _statsL, _statsR;
        private readonly Button[] _tabBtns = new Button[5];
        private readonly GameObject[] _tabPages = new GameObject[5];
        private GrowTab _tab = GrowTab.Level;
        private bool _inDetail, _animating;

        private CharacterData _selected;
        private BattleConfig _cfg;

        private static readonly Vector2 HeroPos = new(-560, -40);
        private const float HeroScale = 2.6f;

        private void Awake()
        {
            if (roster == null) roster = FindAnyObjectByType<PlayerRoster>();
            if (inventory == null) inventory = FindAnyObjectByType<PlayerInventory>();
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            if (equipment == null) equipment = FindAnyObjectByType<PlayerEquipment>();
            _cfg = BattleConfig.CreateDefault();
            BuildUI();
        }

        private void OnDestroy() { if (_cfg != null) Destroy(_cfg); }

        private void OnEnable()
        {
            if (roster != null) roster.OnRosterChanged += OnDataChanged;
            if (inventory != null) inventory.OnChanged += Refresh;
            if (wallet != null) wallet.OnChanged += Refresh;
            if (equipment != null) equipment.OnChanged += OnDataChanged;
        }

        private void OnDisable()
        {
            if (roster != null) roster.OnRosterChanged -= OnDataChanged;
            if (inventory != null) inventory.OnChanged -= Refresh;
            if (wallet != null) wallet.OnChanged -= Refresh;
            if (equipment != null) equipment.OnChanged -= OnDataChanged;
        }

        private void Start()
        {
            RebuildGrid();
            Refresh();
        }

        public void Show(bool on)
        {
            if (!on)
            {
                if (_equipPanel != null && _equipPanel.IsOpen) _equipPanel.Close();
                ExitDetailImmediate();
            }
            if (_root != null) _root.SetActive(on);
            if (!on) return;
            RebuildGrid();
            Refresh();
        }

        /// <summary>'← 홈' 버튼 동작(목록 화면). 상세 화면에서는 '← 목록'.</summary>
        public void SetBackAction(System.Action onBack)
        {
            _onBack = onBack;
            RefreshBackButton();
        }

        private void RefreshBackButton()
        {
            if (_backBtn == null) return;
            _backBtn.gameObject.SetActive(_inDetail || _onBack != null);
            _backLabel.text = _inDetail ? "← 목록" : "← 홈";
        }

        private void OnBack()
        {
            if (_animating) return;
            if (_inDetail) StartCoroutine(CloseDetailRoutine());
            else _onBack?.Invoke();
        }

        private void OnDataChanged()
        {
            if (!_inDetail) RebuildGrid();
            Refresh();
            if (_inDetail) RebuildHeroVisual();
        }

        // ================================================================
        //  목록
        // ================================================================

        private void RebuildGrid()
        {
            for (int i = _grid.childCount - 1; i >= 0; i--)
            {
                var ch = _grid.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
            _cardRT.Clear();

            var entries = _filterBar != null ? _filterBar.filter.Apply(roster, database) : new List<RosterEntry>();
            foreach (var e in entries)
            {
                var card = UIFactory.NewRect("Card", _grid);
                BuildCardVisual(card, e.character, e.owned);
                var frame = card.GetChild(0).GetComponent<Image>();
                var btn = frame.gameObject.AddComponent<Button>();
                btn.targetGraphic = frame;
                var c = e.character;
                btn.onClick.AddListener(() => OnCardClicked(c));
                _cardRT[c] = card;
            }
            if (_filterBar != null) _filterBar.SetCount(entries.Count, roster != null ? roster.Owned.Count : 0);
        }

        /// <summary>카드 그림(목록 칸과 확대 카드 공용). parent 크기 = 130×160 기준.</summary>
        private void BuildCardVisual(RectTransform parent, CharacterData c, OwnedCharacter o)
        {
            int starN = Transcendence.CurrentStar(c.rarity, o.star);
            var frame = UIFactory.AddImage("Frame", parent, StarText.FrameColor(starN, o.transcend), null, raycast: true);
            UIFactory.Stretch(frame.rectTransform);

            var inner = UIFactory.AddImage("Inner", frame.transform, new Color(0.10f, 0.11f, 0.14f, 1f));
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);

            var portrait = UIFactory.AddImage("Portrait", inner.transform, new Color(1, 1, 1, 0.12f));
            UIFactory.Stretch(portrait.rectTransform, 5, 44, 5, 5);
            var sprite = GameArt.Thumb(c);
            portrait.sprite = sprite; portrait.enabled = sprite != null; portrait.preserveAspect = true;
            if (sprite != null)
            {
                portrait.color = Color.white;
                // 그림 위에 겹치는 글자(속성·역할 / 별)가 읽히도록 어두운 띠
                var top = UIFactory.AddImage("TopShade", inner.transform, new Color(0f, 0f, 0f, 0.6f));
                UIFactory.Anchor(top.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
                top.rectTransform.sizeDelta = new Vector2(0, 21); top.rectTransform.anchoredPosition = Vector2.zero;
                var low = UIFactory.AddImage("StarShade", inner.transform, new Color(0f, 0f, 0f, 0.6f));
                Bottom(low.rectTransform, 22, 41);
            }

            var name = UIFactory.AddText("Name", inner.transform, c.displayName, 13, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Bottom(name.rectTransform, 22, 22);

            string lvText = o.ascension > 0 ? $"Lv.{o.level} <color=#FFD36B>돌파{o.ascension}</color>" : $"Lv.{o.level}";
            var lv = UIFactory.AddText("Lv", inner.transform, lvText, 13, font, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, FontStyles.Bold);
            Bottom(lv.rectTransform, 20, 2);

            var traits = UIFactory.AddText("Traits", inner.transform,
                $"<color={Traits.Hex(c.element)}>{Traits.Name(c.element)}</color>·{Traits.Name(c.role)}",
                11, font, new Color(0.88f, 0.88f, 0.92f), TextAlignmentOptions.Center, FontStyles.Bold);
            var trrt = traits.rectTransform;
            UIFactory.Anchor(trrt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            trrt.sizeDelta = new Vector2(0, 16); trrt.anchoredPosition = new Vector2(0, -3);

            var stars = StarRow.Create(inner.transform, 19, TextAnchor.MiddleCenter, font);
            Bottom(stars.Rect, 19, 42);
            stars.Set(starN, o.transcend);

            if (roster != null && (roster.CanPromote(c) || roster.CanTranscend(c)))
            {
                var up = UIFactory.AddText("Up", inner.transform, roster.CanPromote(c) ? "합성!" : "초월!", 11, font, new Color(0.45f, 1f, 0.55f), TextAlignmentOptions.TopRight, FontStyles.Bold);
                var urt = up.rectTransform;
                UIFactory.Anchor(urt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
                urt.sizeDelta = new Vector2(48, 16); urt.anchoredPosition = new Vector2(-3, -18);
            }
        }

        private static void Bottom(RectTransform rt, float h, float y)
        {
            UIFactory.Anchor(rt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            rt.sizeDelta = new Vector2(0, h); rt.anchoredPosition = new Vector2(0, y);
        }

        private void OnCardClicked(CharacterData c)
        {
            if (_animating || _inDetail || c == null) return;
            _selected = c;
            _tab = GrowTab.Level;
            ResetLevelSelection();
            StartCoroutine(OpenDetailRoutine(c));
        }

        // ================================================================
        //  카드 연출
        // ================================================================

        private static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        private static float EaseInOut(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        private Vector2 CardCenterInHeroLayer(CharacterData c)
        {
            if (c != null && _cardRT.TryGetValue(c, out var rt) && rt != null)
            {
                Vector3 world = rt.TransformPoint(rt.rect.center);
                return _heroLayer.InverseTransformPoint(world);
            }
            return Vector2.zero;
        }

        private IEnumerator OpenDetailRoutine(CharacterData c)
        {
            _animating = true;
            var o = roster != null ? roster.GetOwned(c) : null;
            if (o == null) { _animating = false; yield break; }

            // 확대 카드(목록 카드와 같은 위치에서 시작)
            if (_hero != null) Destroy(_hero.gameObject);
            _hero = UIFactory.NewRect("HeroCard", _heroLayer);
            UIFactory.Anchor(_hero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _hero.sizeDelta = _cell;
            BuildCardVisual(_hero, c, o);
            Vector2 start = CardCenterInHeroLayer(c);
            _hero.anchoredPosition = start;

            // 상세 내용 미리 채우기(안 보이는 상태)
            _inDetail = true;
            RefreshBackButton();
            Refresh();
            ShowTab(_tab);
            _detailGroup.gameObject.SetActive(true);
            _detailGroup.alpha = 0f;
            _detailGroup.blocksRaycasts = false;
            _listGroup.blocksRaycasts = false;

            // 1) 목록이 사라지며 카드가 회전 · 앞으로 확대
            Vector2 mid = new(0, 10);
            const float spin = 0.55f;
            for (float t = 0; t < spin; t += Time.unscaledDeltaTime)
            {
                float k = EaseOut(t / spin);
                _listGroup.alpha = 1f - Mathf.Clamp01(t / 0.25f);
                _hero.anchoredPosition = Vector2.Lerp(start, mid, k);
                _hero.localScale = Vector3.one * Mathf.Lerp(1f, 3.2f, k);
                _hero.localEulerAngles = new Vector3(0, 360f * k, 0);
                yield return null;
            }
            _hero.localEulerAngles = Vector3.zero;
            _listGroup.alpha = 0f;
            _listGroup.gameObject.SetActive(false);

            // 2) 왼쪽으로 이동 · 상세가 오른쪽에서 나타남
            Vector2 panelEnd = _detailPanel.anchoredPosition, panelStart = panelEnd + new Vector2(140, 0);
            const float slide = 0.4f;
            for (float t = 0; t < slide; t += Time.unscaledDeltaTime)
            {
                float k = EaseInOut(t / slide);
                _hero.anchoredPosition = Vector2.Lerp(mid, HeroPos, k);
                _hero.localScale = Vector3.one * Mathf.Lerp(3.2f, HeroScale, k);
                _detailGroup.alpha = k;
                _detailPanel.anchoredPosition = Vector2.Lerp(panelStart, panelEnd, k);
                yield return null;
            }
            _hero.anchoredPosition = HeroPos;
            _hero.localScale = Vector3.one * HeroScale;
            _detailPanel.anchoredPosition = panelEnd;
            _detailGroup.alpha = 1f;
            _detailGroup.blocksRaycasts = true;
            _animating = false;
        }

        private IEnumerator CloseDetailRoutine()
        {
            _animating = true;
            if (_equipPanel != null && _equipPanel.IsOpen) _equipPanel.Close();
            _detailGroup.blocksRaycasts = false;

            RebuildGrid();   // 육성 결과(레벨·별) 반영
            _listGroup.gameObject.SetActive(true);
            _listGroup.alpha = 0f;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_grid);
            Vector2 end = CardCenterInHeroLayer(_selected);
            Vector2 from = _hero != null ? _hero.anchoredPosition : end;
            float fromScale = _hero != null ? _hero.localScale.x : 1f;

            const float dur = 0.45f;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = EaseInOut(t / dur);
                _detailGroup.alpha = 1f - Mathf.Clamp01(t / 0.2f);
                _listGroup.alpha = Mathf.Clamp01((t - 0.15f) / 0.3f);
                if (_hero != null)
                {
                    _hero.anchoredPosition = Vector2.Lerp(from, end, k);
                    _hero.localScale = Vector3.one * Mathf.Lerp(fromScale, 1f, k);
                    _hero.localEulerAngles = new Vector3(0, -360f * k, 0);
                }
                yield return null;
            }
            ExitDetailImmediate();
            _animating = false;
        }

        /// <summary>연출 없이 목록 상태로.</summary>
        private void ExitDetailImmediate()
        {
            if (_hero != null) { Destroy(_hero.gameObject); _hero = null; }
            _inDetail = false;
            if (_detailGroup != null) { _detailGroup.alpha = 0f; _detailGroup.blocksRaycasts = false; _detailGroup.gameObject.SetActive(false); }
            if (_listGroup != null) { _listGroup.gameObject.SetActive(true); _listGroup.alpha = 1f; _listGroup.blocksRaycasts = true; }
            RefreshBackButton();
        }

        /// <summary>육성 결과를 확대 카드에 반영(연출 없이).</summary>
        private void RebuildHeroVisual()
        {
            if (_hero == null || _selected == null || roster == null) return;
            var o = roster.GetOwned(_selected);
            if (o == null) return;
            for (int i = _hero.childCount - 1; i >= 0; i--) Destroy(_hero.GetChild(i).gameObject);
            BuildCardVisual(_hero, _selected, o);
        }

        // ================================================================
        //  갱신
        // ================================================================

        private int Mat(MaterialType t) => inventory != null ? inventory.Count(t) : 0;
        private int Gold => wallet != null ? wallet.Gold : int.MaxValue;

        private string GoldTag(int cost) =>
            Gold >= cost ? $"<color=#FFD36B>금화 {cost:N0}</color>" : $"<color=#FF8A8A>금화 {cost:N0}</color>";

        private string Need(MaterialType t, int n)
        {
            int have = Mat(t);
            return $"{Materials.RichName(t)} {(have >= n ? $"{have}" : $"<color=#FF8A8A>{have}</color>")}/{n}";
        }

        private bool PayGold(int cost)
        {
            if (wallet == null || cost <= 0) return true;
            if (wallet.SpendGold(cost)) return true;
            Toast($"금화가 부족합니다 (필요 {cost:N0})");
            return false;
        }

        private void Refresh()
        {
            if (_goldText != null) _goldText.text = $"<color=#FFD36B>금화 {Gold:N0}</color>";
            if (!_inDetail || _selected == null || roster == null) return;
            var o = roster.GetOwned(_selected);
            if (o == null) return;

            RefreshHeader(o);
            switch (_tab)
            {
                case GrowTab.Level: RefreshLevelTab(o); break;
                case GrowTab.Equip: RefreshEquipTab(); break;
                case GrowTab.Ascend: RefreshAscendTab(o); break;
                case GrowTab.Skill: RefreshSkillTab(); break;
                case GrowTab.Transcend: RefreshTranscendTab(o); break;
            }
        }

        // ---------------- 미리보기 유닛(능력치·스킬) ----------------

        /// <summary>올린 스킬 레벨에 초월 보너스(+2 · +4 · +5에서 +3)를 더한 실제 적용 레벨.</summary>
        private int[] WithBonus(CharacterData c, int[] levels)
        {
            var lv = (int[])levels.Clone();
            int tr = roster.TranscendOf(c);
            for (int i = 0; i < lv.Length && i < 3; i++) lv[i] += Leveling.SkillBonus(tr, (SkillSlot)i);
            return lv;
        }

        private struct SkillInfo
        {
            public string name;
            public SkillCategory category;
            public Element element;
            public TargetType target;
            public float power, heal;
            public int hits, breakDamage, energyGain;
            public StatusType effect;
            public float effectMag, effectChance;
            public int effectTurns;
            public float splash, lifesteal, advance;
            public bool cleanse;
            public StatusType selfEffect;
            public float selfMag;
            public int selfTurns;
        }

        /// <summary>
        /// 현재 육성 상태(레벨·스킬·성급·초월·장비) 그대로의 전투 유닛 미리보기.
        /// skillLevels를 주면 스킬 레벨만 그 값으로 가정(다음 레벨 수치 비교용).
        /// </summary>
        private (BattleUnit unit, SkillInfo[] skills) Preview(CharacterData c, int[] skillLevels = null)
        {
            var o = roster.GetOwned(c);
            System.Func<CharacterData, BattleUnitData> lookup = null;
            if (database != null) lookup = database.GetUnit;
            var data = RuntimeUnits.FromCharacter(c, o.level, lookup, o.skillLevel, roster.StarOf(c), roster.TranscendOf(c), WithBonus(c, skillLevels ?? roster.SkillLevels(c)));
            bool authored = lookup != null && lookup(c) == data;
            if (authored) data = Instantiate(data);
            if (equipment != null) data.bonus = equipment.BonusFor(c);
            if (authored) data.level = o.level;

            var unit = new BattleUnit(data, Team.Ally, _cfg);
            var infos = new SkillInfo[3];
            foreach (var sk in unit.skills)
            {
                int i = sk.category == SkillCategory.Basic ? 0 : sk.category == SkillCategory.Skill ? 1 : 2;
                infos[i] = new SkillInfo
                {
                    name = sk.skillName, category = sk.category, element = sk.element, target = sk.target,
                    power = sk.power, heal = sk.heal, hits = Mathf.Max(1, sk.hits),
                    breakDamage = sk.breakDamage, energyGain = sk.energyGain,
                    effect = sk.applyEffect, effectMag = sk.effectMagnitude, effectChance = sk.effectChance, effectTurns = sk.effectDuration,
                    splash = sk.splash, lifesteal = sk.lifesteal, advance = sk.advance, cleanse = sk.cleanse,
                    selfEffect = sk.selfEffect, selfMag = sk.selfEffectMagnitude, selfTurns = sk.selfEffectDuration,
                };
            }

            // 미리보기용 생성물 정리(저작 에셋은 건드리지 않음)
            if (data.skills != null && !authored) foreach (var sk in data.skills) if (sk != null) Destroy(sk);
            Destroy(data);
            return (unit, infos);
        }

        private void RefreshHeader(OwnedCharacter o)
        {
            int cap = Leveling.LevelCap(o.ascension);
            _hName.text = $"{_selected.displayName}  <size=60%><color={Traits.Hex(_selected.element)}>{Traits.Name(_selected.element)}</color> · {Traits.Name(_selected.role)}</size>";
            string exp = o.level >= cap ? (o.ascension >= Leveling.MaxAscension ? "MAX" : "상한 — 돌파 필요") : $"EXP {o.exp:N0} / {Leveling.ExpForNext(o.level):N0}";
            _hLine.text = $"<color=#FFD36B>Lv. {o.level}</color> / {cap}" + (o.ascension > 0 ? $"   <color=#FFD36B>돌파 {o.ascension}</color>" : "") +
                          $"   <size=85%><color=#9AA3B5>{exp}</color></size>";

            var (u, _) = Preview(_selected);
            string P(float v) => $"{v * 100f:0.#}%";
            var l = new StringBuilder();
            l.Append($"체력  <b>{u.maxHP:N0}</b>\n공격력  <b>{u.atk:N0}</b>\n방어력  <b>{u.def:N0}</b>\n속도  <b>{u.spd}</b>\n");
            l.Append($"치명타 확률  <b>{P(u.critRate)}</b>\n치명타 피해  <b>{P(u.critDmg)}</b>");
            _statsL.text = l.ToString();

            var r = new StringBuilder();
            r.Append($"방어 관통  <b>{P(u.penetration)}</b>\n브레이크 효율  <b>{P(u.breakEffect)}</b>\n에너지 회복  <b>{P(u.energyRegen)}</b>\n");
            r.Append($"치유량  <b>{P(u.healBonus)}</b>\n효과 명중  <b>{P(u.effectHit)}</b>   효과 저항  <b>{P(u.effectRes)}</b>\n");
            float el = u.ElementDmg(_selected.element) + u.allDmg;
            r.Append($"<color={Traits.Hex(_selected.element)}>{Traits.Name(_selected.element)} 피해</color>  <b>{P(el)}</b>");
            _statsR.text = r.ToString();
        }

        // ================================================================
        //  탭
        // ================================================================

        private void ShowTab(GrowTab tab)
        {
            _tab = tab;
            for (int i = 0; i < 5; i++)
            {
                bool on = i == (int)tab;
                _tabPages[i].SetActive(on);
                _tabBtns[i].GetComponent<Image>().color = on ? new Color(0.78f, 0.58f, 0.22f, 0.95f) : new Color(1, 1, 1, 0.12f);
            }
            Refresh();
        }

        // ---------------- 레벨 탭 (경험치 재료 선택) ----------------
        // 재료를 담고(+/−, 자동 선택) '레벨업'을 눌러야 재료·금화가 함께 소모됩니다.

        private TextMeshProUGUI _lvPreview, _lvCost;
        private readonly TextMeshProUGUI[] _lvCount = new TextMeshProUGUI[4], _lvOwned = new TextMeshProUGUI[4];
        private readonly Button[] _lvPlus = new Button[4], _lvMinus = new Button[4];
        private readonly Button[] _lvSlotBtn = new Button[4];
        private Button _lvConfirm, _lvAuto, _lvClear;
        private readonly int[] _lvSel = new int[4];

        private void ResetLevelSelection() => System.Array.Clear(_lvSel, 0, _lvSel.Length);

        private int SelectedExp()
        {
            int n = 0;
            for (int i = 0; i < _lvSel.Length; i++) n += _lvSel[i] * Materials.ExpValue(Materials.ExpMaterials[i]);
            return n;
        }

        private int ExpOwnedTotal()
        {
            long n = 0;
            foreach (var m in Materials.ExpMaterials) n += (long)Mat(m) * Materials.ExpValue(m);
            return (int)System.Math.Min(int.MaxValue, n);
        }

        private static Leveling.LevelPreview PreviewFor(OwnedCharacter o, int addExp) =>
            Leveling.Preview(o.level, o.exp, Leveling.LevelCap(o.ascension), addExp);

        private OwnedCharacter Sel => _selected != null && roster != null ? roster.GetOwned(_selected) : null;

        private void AdjustSel(int i, int delta)
        {
            var o = Sel;
            if (o == null) return;
            if (delta > 0)
            {
                if (PreviewFor(o, SelectedExp()).atCap) { Toast("이미 레벨 상한까지 선택했습니다"); return; }
                if (_lvSel[i] >= Mat(Materials.ExpMaterials[i])) { Toast($"{Materials.Name(Materials.ExpMaterials[i])} 부족"); return; }
            }
            _lvSel[i] = Mathf.Clamp(_lvSel[i] + delta, 0, Mat(Materials.ExpMaterials[i]));
            Refresh();
        }

        /// <summary>자동 선택: 상한까지 큰 재료부터 넘치지 않게 → 남는 양은 덮는 최소 재료 1개 → 금화가 모자라면 작은 재료부터 덜어냄.</summary>
        private void AutoSelect()
        {
            var o = Sel;
            if (o == null) return;
            var mats = Materials.ExpMaterials;
            ResetLevelSelection();
            int remaining = Leveling.ExpToCap(o.level, o.exp, Leveling.LevelCap(o.ascension));
            for (int i = mats.Length - 1; i >= 0; i--)
            {
                int v = Materials.ExpValue(mats[i]);
                int n = Mathf.Min(Mat(mats[i]), remaining / v);
                _lvSel[i] = n;
                remaining -= n * v;
            }
            while (remaining > 0)
            {
                int pick = -1;
                for (int i = 0; i < mats.Length && pick < 0; i++)
                    if (_lvSel[i] < Mat(mats[i]) && Materials.ExpValue(mats[i]) >= remaining) pick = i;
                for (int i = mats.Length - 1; i >= 0 && pick < 0; i--)
                    if (_lvSel[i] < Mat(mats[i])) pick = i;
                if (pick < 0) break;
                _lvSel[pick]++;
                remaining -= Materials.ExpValue(mats[pick]);
            }
            while (SelectedExp() > 0 && PreviewFor(o, SelectedExp()).gold > Gold)
                for (int i = 0; i < mats.Length; i++)
                    if (_lvSel[i] > 0) { _lvSel[i]--; break; }
            if (SelectedExp() == 0) Toast(ExpOwnedTotal() == 0 ? "경험치 재료가 없습니다" : "금화가 부족합니다");
            Refresh();
        }

        private void ConfirmLevelUp()
        {
            var o = Sel;
            if (o == null) return;
            var mats = Materials.ExpMaterials;
            var sel = (int[])_lvSel.Clone();
            int add = SelectedExp();
            if (add <= 0) { Toast("경험치 재료를 선택하세요"); return; }
            for (int i = 0; i < mats.Length; i++)
                if (sel[i] > Mat(mats[i])) { Toast($"{Materials.Name(mats[i])} 부족"); return; }
            var pv = PreviewFor(o, add);
            if (Gold < pv.gold) { Toast($"금화가 부족합니다 (필요 {pv.gold:N0})"); return; }

            int before = o.level;
            var target = _selected;
            ResetLevelSelection();
            if (inventory != null)
                for (int i = 0; i < mats.Length; i++)
                    if (sel[i] > 0) inventory.Spend(mats[i], sel[i]);
            PayGold(pv.gold);
            roster.AddExp(target, pv.usedExp);
            MissionEvents.Report(MissionGoal.Growth);
            var after = roster.GetOwned(target);
            int lv = after != null ? after.level : before;
            Toast(lv > before ? $"레벨업! Lv.{before} → Lv.{lv}  (금화 -{pv.gold:N0})" : $"경험치 +{pv.usedExp:N0}  (금화 -{pv.gold:N0})");
        }

        private void RefreshLevelTab(OwnedCharacter o)
        {
            var mats = Materials.ExpMaterials;
            for (int i = 0; i < mats.Length; i++) _lvSel[i] = Mathf.Clamp(_lvSel[i], 0, Mat(mats[i]));
            int cap = Leveling.LevelCap(o.ascension);
            bool atCap = o.level >= cap;
            int add = SelectedExp();
            var pv = PreviewFor(o, add);

            if (atCap)
                _lvPreview.text = o.ascension >= Leveling.MaxAscension
                    ? "<color=#FFD36B>최대 레벨</color>"
                    : $"<color=#FFD36B>레벨 상한 Lv.{cap}</color>\n<size=70%><color=#9AA3B5>'돌파' 탭에서 돌파하면 상한이 올라갑니다</color></size>";
            else
            {
                string to = pv.level > o.level ? $"<color=#7CFF9A>Lv.{pv.level}</color>" : $"Lv.{pv.level}";
                string sub = add <= 0 ? "<color=#9AA3B5>재료를 눌러 담으세요</color>"
                    : $"EXP +{pv.usedExp:N0}" + (pv.atCap ? "  ·  <color=#FFD36B>상한 도달</color>"
                        : $"  ·  다음 레벨까지 {Leveling.ExpForNext(pv.level) - pv.exp:N0}")
                      + (pv.wastedExp > 0 ? $"  ·  <color=#FF8A8A>초과 {pv.wastedExp:N0} EXP는 버려짐</color>" : "");
                _lvPreview.text = $"Lv.{o.level} → {to}  <size=70%><color=#9AA3B5>/ 상한 {cap}</color></size>\n<size=72%>{sub}</size>";
            }

            for (int i = 0; i < mats.Length; i++)
            {
                int owned = Mat(mats[i]);
                _lvCount[i].text = _lvSel[i] > 0 ? $"×{_lvSel[i]}" : "<color=#6B7280>0</color>";
                _lvOwned[i].text = $"보유 {owned:N0}";
                _lvPlus[i].interactable = !atCap && _lvSel[i] < owned && !pv.atCap;
                _lvSlotBtn[i].interactable = _lvPlus[i].interactable;
                _lvMinus[i].interactable = _lvSel[i] > 0;
            }
            _lvCost.text = atCap ? "" : $"필요 {GoldTag(pv.gold)}   <size=80%><color=#9AA3B5>보유 {Gold:N0}</color></size>";
            _lvConfirm.interactable = !atCap && add > 0 && Gold >= pv.gold;
            _lvAuto.interactable = !atCap;
            _lvClear.interactable = add > 0;
        }

        // ---------------- 장비 탭 ----------------

        private readonly TextMeshProUGUI[] _eqSlotText = new TextMeshProUGUI[6];
        private readonly Image[] _eqSlotFrame = new Image[6];
        private TextMeshProUGUI _eqSets;

        private void RefreshEquipTab()
        {
            for (int i = 0; i < EquipStats.AllSlots.Length; i++)
            {
                var slot = EquipStats.AllSlots[i];
                var it = equipment != null ? equipment.EquippedIn(_selected, slot) : null;
                if (it == null)
                {
                    _eqSlotFrame[i].color = new Color(1, 1, 1, 0.1f);
                    _eqSlotText[i].text = $"<color=#9AA3B5>{EquipStats.SlotName(slot)}</color>\n<color=#6B7280>비어 있음</color>";
                }
                else
                {
                    var col = EquipSets.Get(it.set)?.color ?? Color.white;
                    _eqSlotFrame[i].color = new Color(col.r, col.g, col.b, 0.85f);
                    _eqSlotText[i].text = $"<color=#9AA3B5>{EquipStats.SlotName(slot)}</color>  {EquipSets.Rich(it.set)} <size=75%>{EquipStats.Stars(it.Rarity, it.transcend)}</size>" +
                                          $"  <color=#FFD36B>+{it.level}</color>\n{EquipStats.Format(it.main, it.MainValue)}";
                }
            }
            var sb = new StringBuilder("<color=#FFD36B><b>세트 효과</b></color>   ");
            bool any = false;
            if (equipment != null)
                foreach (var kv in EquipRules.SetCounts(equipment.EquippedOn(_selected)))
                {
                    var set = EquipSets.Get(kv.Key);
                    if (set == null || kv.Value < 2) continue;
                    any = true;
                    sb.Append($"{EquipSets.Rich(kv.Key)} {kv.Value}세트  <size=85%>{set.desc2}{(kv.Value >= 4 ? " · " + set.desc4 : "")}</size>   ");
                }
            if (!any) sb.Append("<color=#6B7280>같은 세트 2개 / 4개 장착 시 발동</color>");
            _eqSets.text = sb.ToString();
        }

        private void OpenEquipment()
        {
            if (_selected == null || _equipPanel == null) return;
            _equipPanel.Open(_selected);
        }

        // ---------------- 돌파 탭 ----------------

        private TextMeshProUGUI _ascInfo, _ascNeed, _ascBtnLbl;
        private Button _ascBtn;

        private void RefreshAscendTab(OwnedCharacter o)
        {
            var sb = new StringBuilder();
            for (int a = 0; a < Leveling.MaxAscension; a++)
            {
                int target = Leveling.LevelCap(a + 1);
                var am = Economy.AscensionMaterial(_selected.element, a);
                string line = $"{Materials.TierName(a + 1)}  <b>{target} 돌파</b>  <size=85%>(Lv.{Leveling.LevelCap(a)} → {target})  " +
                              $"{Materials.RichName(am)} ×{Economy.AscensionMaterialCount(a)} · 금화 {Economy.AscensionGold(a):N0}</size>";
                if (o.ascension > a) sb.Append($"<color=#7CFF9A>●</color> <color=#9AA3B5>{line}</color>  <color=#7CFF9A>완료</color>\n");
                else if (o.ascension == a) sb.Append($"<color=#FFD36B>→</color> {line}\n");
                else sb.Append($"<color=#6B7280>   {line}</color>\n");
            }
            _ascInfo.text = sb.ToString();

            if (o.ascension >= Leveling.MaxAscension)
            {
                _ascNeed.text = "<color=#FFD36B>모든 돌파 완료 (Lv.80)</color>";
                _ascBtnLbl.text = "돌파 완료";
                _ascBtn.interactable = false;
                return;
            }
            var mat = Economy.AscensionMaterial(_selected.element, o.ascension);
            int cost = Economy.AscensionMaterialCount(o.ascension);
            int gold = Economy.AscensionGold(o.ascension);
            int cap = Leveling.LevelCap(o.ascension);
            bool lvOk = o.level >= cap;
            _ascNeed.text = (lvOk ? $"<color=#7CFF9A>Lv.{cap} 달성</color>" : $"<color=#FF8A8A>Lv.{cap} 필요 (현재 Lv.{o.level})</color>") +
                            $"     {Need(mat, cost)}     {GoldTag(gold)}\n" +
                            $"<size=80%><color=#9AA3B5>재료 획득: 특수 던전 · 돌파 던전 {Materials.TierName(Materials.TierOf(mat))}</color></size>";
            _ascBtnLbl.text = $"돌파 → Lv.{Leveling.LevelCap(o.ascension + 1)}";
            _ascBtn.interactable = roster.CanAscend(_selected) && Mat(mat) >= cost && Gold >= gold;
        }

        private void Ascend()
        {
            if (_selected == null || roster == null) return;
            if (!roster.CanAscend(_selected)) { Toast("상한까지 레벨을 올리세요"); return; }
            int asc = roster.AscensionOf(_selected);
            var am = Economy.AscensionMaterial(_selected.element, asc);
            int cost = Economy.AscensionMaterialCount(asc);
            int gold = Economy.AscensionGold(asc);
            if (Mat(am) < cost) { Toast($"{Materials.Name(am)} 부족 ({Mat(am)}/{cost})"); return; }
            if (Gold < gold) { Toast($"금화가 부족합니다 (필요 {gold:N0})"); return; }
            if (inventory != null && !inventory.Spend(am, cost)) return;
            PayGold(gold);
            roster.Ascend(_selected);
            MissionEvents.Report(MissionGoal.Growth);
            Toast($"돌파 완료! 레벨 상한 {Leveling.LevelCap(asc + 1)}");
        }

        // ---------------- 스킬 탭 ----------------

        private readonly Button[] _skSelBtn = new Button[4];
        private readonly TextMeshProUGUI[] _skSelLbl = new TextMeshProUGUI[4];
        private TextMeshProUGUI _skTitle, _skDesc, _skStats, _skTable, _skCostText, _skUpLbl;
        private Button _skUpBtn;
        private int _skillSel;

        private static string EffectName(StatusType t) => SkillText.Effect(t);
        private static string TargetName(TargetType t) => SkillText.Target(t);

        private static string Pct(float v) => $"{v * 100f:0.#}%";

        /// <summary>스킬 설명 문장(현재 수치 기준).</summary>
        private static string Sentence(SkillInfo s)
        {
            string el = $"<color={Traits.Hex(s.element)}>{Traits.Name(s.element)}</color>";
            var parts = new List<string>();
            bool ally = s.target == TargetType.SingleAlly || s.target == TargetType.AllAllies || s.target == TargetType.Self;
            if (s.heal > 0f && ally)
                parts.Add($"{TargetName(s.target)}의 체력을 공격력의 <b>{Pct(s.heal)}</b>만큼 회복합니다.");
            if (s.power > 0f)
            {
                string hits = s.hits > 1 ? $" × {s.hits}회" : "";
                if (s.target == TargetType.RandomEnemies)
                    parts.Add($"무작위 적을 <b>{s.hits}회</b> 공격해 타격마다 공격력의 <b>{Pct(s.power)}</b>만큼 {el} 피해를 줍니다.");
                else if (s.target == TargetType.Blast)
                    parts.Add($"적 1명에게 공격력의 <b>{Pct(s.power)}</b>{hits}, 양옆의 적에게는 그 <b>{Pct(s.splash)}</b>만큼 {el} 피해를 줍니다.");
                else
                    parts.Add($"{TargetName(s.target)}에게 공격력의 <b>{Pct(s.power)}</b>{hits}만큼 {el} 피해를 줍니다.");
            }
            if (s.effect != StatusType.None)
            {
                string chance = s.effectChance < 0.999f ? $"{Pct(s.effectChance)} 확률로 " : "";
                string who = s.power > 0f || s.heal > 0f ? "대상" : TargetName(s.target);
                parts.Add(s.effect == StatusType.Taunt
                    ? $"{s.effectTurns}턴 동안 적의 단일 대상 공격을 자신에게 끌어옵니다(도발)."
                    : s.effect == StatusType.Stun
                        ? $"{chance}대상을 기절시켜 다음 턴에 행동하지 못하게 합니다."
                        : $"{chance}{who}에게 <b>{StatusText.Short(s.effect, s.effectMag, s.effectTurns)}</b> 효과를 겁니다.");
            }
            if (s.selfEffect != StatusType.None)
                parts.Add($"사용 후 자신에게 <b>{StatusText.Short(s.selfEffect, s.selfMag, s.selfTurns)}</b> 효과를 겁니다.");
            if (s.advance > 0f) parts.Add($"대상의 행동 게이지를 <b>{Pct(s.advance)}</b> 당깁니다.");
            if (s.lifesteal > 0f) parts.Add($"준 피해의 <b>{Pct(s.lifesteal)}</b>만큼 체력을 회복합니다.");
            if (s.cleanse) parts.Add("대상의 디버프를 모두 해제합니다.");
            if (parts.Count == 0) parts.Add("효과 없음");
            return string.Join(" ", parts);
        }

        /// <summary>'현재 → 다음' (다음 레벨에서 달라질 때만 화살표).</summary>
        private static string Pair(float now, float next, bool max) =>
            max || Mathf.Abs(next - now) < 0.0005f ? $"<b>{Pct(now)}</b>" : $"<b>{Pct(now)}</b> → <color=#7CFF9A>{Pct(next)}</color>";

        private void SelectSkill(int i)
        {
            _skillSel = Mathf.Clamp(i, 0, 3);   // 3 = 패시브
            Refresh();
        }

        /// <summary>초월로 재료 없이 더해진 스킬 레벨(+2 / +4 / +5에서 기본 공격 / 스킬 / 궁극기 +3).</summary>
        private int SkillBonus(int slot) => roster.SkillBonusOf(_selected, (SkillSlot)slot);

        /// <summary>'Lv. 7' 또는 초월 보너스가 있으면 'Lv. 7 <보라>+3</보라>'.</summary>
        private string LevelText(int lv, int bonus) =>
            bonus > 0 ? $"<color=#FFD36B>Lv. {lv}</color> <color=#B47CFF>+{bonus}</color>" : $"<color=#FFD36B>Lv. {lv}</color>";

        /// <summary>패시브 보기: 고유 패시브(지금 초월 단계의 효과) + 초월 고유 능력(+1 / +3 / +6).</summary>
        private void RefreshPassiveView()
        {
            var kit = CharacterKits.Get(_selected);
            int tr = roster.TranscendOf(_selected);
            bool upgraded = false;
            foreach (var a in kit.TranscendAbilities()) upgraded |= tr >= a.unlock && a.upgrade.Count > 0;
            _skTitle.text = $"<size=62%><color=#C9B8FF>패시브</color></size>  {kit.passive.name}" +
                            (upgraded ? "  <size=58%><color=#B47CFF>초월 강화 적용 중</color></size>" : "");
            _skDesc.text = CharacterKits.Describe(CharacterKits.CurrentPassive(_selected, tr));
            var sb = new StringBuilder("<color=#9AA3B5>초월 고유 능력</color>");
            foreach (var a in kit.TranscendAbilities())
            {
                bool on = tr >= a.unlock;
                sb.Append('\n');
                if (on) sb.Append($"<color={StarText.Rainbow[a.unlock - 1]}>+{a.unlock}</color> <b>{a.name}</b> — {a.Desc}");
                else sb.Append($"<color=#6B7280>+{a.unlock} {a.name} — {a.Desc.Replace("<b>", "").Replace("</b>", "")}</color>");
            }
            _skStats.text = sb.ToString();
            _skTable.text = "<color=#9AA3B5>패시브는 레벨이 없고 항상 적용됩니다. 초월 +1 · +3 · +6에서 패시브가 강화되고, +2 · +4 · +5에서는 기본 공격 · 스킬 · 궁극기 레벨이 재료 없이 +3 됩니다.</color>";
            _skCostText.text = "";
            _skUpBtn.gameObject.SetActive(false);
        }

        private void RefreshSkillTab()
        {
            var levels = roster.SkillLevels(_selected);
            var (unit, cur) = Preview(_selected);

            // 비교용: 스킬마다 다음 레벨(최대면 이전 레벨)로 가정한 미리보기 → 레벨당 변화량
            var altLevels = new int[3];
            var dir = new int[3];
            for (int i = 0; i < 3; i++)
            {
                dir[i] = levels[i] < Leveling.MaxSkillLevel ? +1 : -1;
                altLevels[i] = Mathf.Clamp(levels[i] + dir[i], 1, Leveling.MaxSkillLevel);
            }
            var (_, alt) = Preview(_selected, altLevels);

            // 왼쪽: 스킬 3개 선택
            for (int i = 0; i < 3; i++)
            {
                bool on = i == _skillSel;
                _skSelBtn[i].GetComponent<Image>().color = on ? new Color(0.5f, 0.35f, 0.75f, 0.98f) : new Color(1, 1, 1, 0.1f);
                bool canUp = CanAffordSkill(levels[i]);
                _skSelLbl[i].text = $"<size=70%><color=#C9B8FF>{Leveling.SkillSlotName((SkillSlot)i)}</color></size>\n<b>{cur[i].name}</b>\n" +
                                    $"<size=80%>{LevelText(levels[i], SkillBonus(i))} / {Leveling.MaxSkillLevel}" +
                                    (canUp ? "  <color=#7CFF9A>레벨업 가능</color>" : "") + "</size>";
            }
            {
                // 4번째: 패시브
                var kit = CharacterKits.Get(_selected);
                int tr = roster.TranscendOf(_selected);
                int open = (tr >= 1 ? 1 : 0) + (tr >= 3 ? 1 : 0) + (tr >= 6 ? 1 : 0);
                _skSelBtn[3].GetComponent<Image>().color = _skillSel == 3 ? new Color(0.5f, 0.35f, 0.75f, 0.98f) : new Color(1, 1, 1, 0.1f);
                _skSelLbl[3].text = $"<size=70%><color=#C9B8FF>패시브</color></size>\n<b>{kit.passive.name}</b>\n" +
                                    $"<size=80%><color=#9AA3B5>초월 고유 능력 {open} / 3</color></size>";
            }

            if (_skillSel == 3) { RefreshPassiveView(); return; }
            _skUpBtn.gameObject.SetActive(true);

            // 오른쪽: 선택한 스킬 상세
            int k = _skillSel;
            var s = cur[k];
            int lv = levels[k];
            int bonus = SkillBonus(k);
            bool max = lv >= Leveling.MaxSkillLevel;
            // 레벨당 변화량(선형) — 다음 레벨 값 = 현재 + step
            float stepPower = (alt[k].power - s.power) * dir[k];
            float stepHeal = (alt[k].heal - s.heal) * dir[k];
            float stepMag = (alt[k].effectMag - s.effectMag) * dir[k];

            string elHex = Traits.Hex(s.element);
            _skTitle.text = $"<size=62%><color=#C9B8FF>{Leveling.SkillSlotName((SkillSlot)k)}</color></size>  {s.name}  " +
                            $"<size=62%>{LevelText(lv, bonus)} / {Leveling.MaxSkillLevel}   <color={elHex}>{Traits.Name(s.element)}</color> · {TargetName(s.target)}</size>";
            _skDesc.text = Sentence(s);

            var sb = new StringBuilder();
            if (s.power > 0f)
            {
                sb.Append($"<color=#9AA3B5>피해 계수</color>   공격력 × {Pair(s.power, s.power + stepPower, max)}" + (s.hits > 1 ? $"  × {s.hits}회" : ""));
                int est = Mathf.RoundToInt(unit.atk * s.power * s.hits * (1f + unit.allDmg + unit.ElementDmg(s.element)));
                sb.Append($"\n<color=#9AA3B5>예상 피해</color>   약 <b>{est:N0}</b>  <size=82%><color=#9AA3B5>(현재 공격력 {unit.atk:N0} · 속성 피해 포함 · 적 방어·치명타 적용 전)</color></size>\n");
            }
            if (s.heal > 0f)
            {
                sb.Append($"<color=#9AA3B5>회복 계수</color>   공격력 × {Pair(s.heal, s.heal + stepHeal, max)}");
                int est = Mathf.RoundToInt(unit.atk * s.heal * (1f + unit.healBonus));
                sb.Append($"\n<color=#9AA3B5>예상 회복</color>   약 <b>{est:N0}</b>  <size=82%><color=#9AA3B5>(현재 공격력 {unit.atk:N0} · 치유량 보너스 포함)</color></size>\n");
            }
            if (s.effect != StatusType.None)
            {
                string chance = s.effectChance < 0.999f ? $"  · 확률 {Pct(s.effectChance)}" : "";
                string unitLabel = s.effect == StatusType.Dot || s.effect == StatusType.Regen ? "공격력 × " : "";
                string magText = SkillText.NoMagnitude(s.effect) ? "" : $" {unitLabel}{Pair(s.effectMag, s.effectMag + stepMag, max)}";
                sb.Append($"<color=#9AA3B5>부여 효과</color>   {EffectName(s.effect)}{magText}  · {s.effectTurns}턴{chance}\n");
            }
            if (s.selfEffect != StatusType.None)
                sb.Append($"<color=#9AA3B5>자신 효과</color>   {StatusText.Short(s.selfEffect, s.selfMag, s.selfTurns)}\n");
            {
                var extra = new List<string>();
                if (s.target == TargetType.Blast && s.splash > 0f) extra.Add($"양옆 피해 {Pct(s.splash)}");
                if (s.advance > 0f) extra.Add($"행동 게이지 당김 {Pct(s.advance)}");
                if (s.lifesteal > 0f) extra.Add($"흡혈 {Pct(s.lifesteal)}");
                if (s.cleanse) extra.Add("디버프 해제");
                if (extra.Count > 0) sb.Append($"<color=#9AA3B5>추가 효과</color>   {string.Join("  ·  ", extra)}\n");
            }
            if (s.breakDamage > 0) sb.Append($"<color=#9AA3B5>브레이크</color>   브레이크 게이지 -{s.breakDamage}" + (s.hits > 1 ? $" × {s.hits}회" : "") + " (약점 속성일 때 · 약점이 아니면 약 1/3)\n");
            string energy = s.category == SkillCategory.Ultimate ? "에너지 가득 필요 · 사용 시 전부 소모" : $"사용 시 에너지 +{s.energyGain}";
            string sp = s.category == SkillCategory.Ultimate ? "SP 변화 없음" : s.category == SkillCategory.Skill ? "SP -1" : "SP +1";
            sb.Append($"<color=#9AA3B5>에너지 · SP</color>   {energy}  ·  {sp}");
            _skStats.text = sb.ToString();

            // 레벨별 계수 표(주 수치 = 피해 > 회복 > 부여 효과)
            float baseVal, step; string label;
            if (s.power > 0f) { baseVal = s.power; step = stepPower; label = "레벨별 피해 계수"; }
            else if (s.heal > 0f) { baseVal = s.heal; step = stepHeal; label = "레벨별 회복 계수"; }
            else { baseVal = s.effectMag; step = stepMag; label = "레벨별 효과 수치"; }
            var tb = new StringBuilder($"<color=#9AA3B5>{label}</color>\n");
            for (int L = 1; L <= Leveling.MaxSkillLevel; L++)
            {
                float v = baseVal + (L - lv) * step;
                string cell = $"<size=70%><color=#9AA3B5>Lv{L}</color></size> {v * 100f:0}";
                tb.Append(L == lv ? $"<color=#FFD36B><b>{cell}</b></color>" : L == lv + 1 ? $"<color=#7CFF9A>{cell}</color>" : cell);
                if (L < Leveling.MaxSkillLevel) tb.Append("   ");
            }
            tb.Append(bonus > 0
                ? $"\n<size=80%><color=#B47CFF>초월 보너스 +{bonus} 레벨이 포함된 수치입니다 (실제 적용 Lv. {lv + bonus})</color></size>"
                : $"\n<size=80%><color=#B47CFF>초월 +{Transcendence.SkillLevelUnlock(k)}에서 재료 없이 레벨 +{Transcendence.SkillLevelStep}</color></size>");
            if (Mathf.Abs(step) < 0.0005f) tb.Append("  <size=80%><color=#9AA3B5>(레벨로 변하지 않음)</color></size>");
            _skTable.text = tb.ToString();

            // 비용 · 버튼
            if (max)
            {
                _skCostText.text = bonus > 0
                    ? $"<color=#FFD36B>최대 레벨</color>  <color=#B47CFF>(초월 보너스 +{bonus} 적용 중)</color>"
                    : $"<color=#FFD36B>최대 레벨</color>  <color=#B47CFF>(초월 +{Transcendence.SkillLevelUnlock(k)}에서 재료 없이 +{Transcendence.SkillLevelStep})</color>";
                _skUpLbl.text = "MAX";
                _skUpBtn.interactable = false;
            }
            else
            {
                var c = Leveling.SkillUpgrade(lv);
                var cb = new StringBuilder("<color=#9AA3B5>필요</color>   ").Append(Need(c.book, c.books));
                if (c.legend > 0) cb.Append("   ").Append(Need(MaterialType.SkillLegend, c.legend));
                cb.Append("   ").Append(GoldTag(c.gold));
                _skCostText.text = cb.ToString();
                _skUpLbl.text = $"레벨업  →  Lv.{lv + 1}";
                _skUpBtn.interactable = CanAffordSkill(lv);
            }
        }

        private bool CanAffordSkill(int lv)
        {
            if (lv >= Leveling.MaxSkillLevel) return false;
            var c = Leveling.SkillUpgrade(lv);
            return Mat(c.book) >= c.books && Mat(MaterialType.SkillLegend) >= c.legend && Gold >= c.gold;
        }

        private void SkillUp(SkillSlot slot)
        {
            if (_selected == null || roster == null) return;
            if ((int)slot > 2) return;
            if (!roster.CanUpgradeSkill(_selected, slot))
            {
                Toast(roster.SkillBonusOf(_selected, slot) == 0
                    ? $"최대 레벨 — 초월 +{Transcendence.SkillLevelUnlock((int)slot)}에서 재료 없이 +{Transcendence.SkillLevelStep} 레벨" : "이미 최대 레벨");
                return;
            }
            int lv = roster.SkillLevelOf(_selected, slot);
            var c = Leveling.SkillUpgrade(lv);
            if (Mat(c.book) < c.books) { Toast($"{Materials.Name(c.book)} 부족 ({Mat(c.book)}/{c.books})"); return; }
            if (Mat(MaterialType.SkillLegend) < c.legend) { Toast($"전설의 비전서 부족 ({Mat(MaterialType.SkillLegend)}/{c.legend}) — 이벤트·상점 교환으로 획득"); return; }
            if (Gold < c.gold) { Toast($"금화가 부족합니다 (필요 {c.gold:N0})"); return; }
            if (inventory != null)
            {
                inventory.Spend(c.book, c.books);
                if (c.legend > 0) inventory.Spend(MaterialType.SkillLegend, c.legend);
            }
            PayGold(c.gold);
            roster.UpgradeSkill(_selected, slot);
            MissionEvents.Report(MissionGoal.Growth);
            Toast($"{Leveling.SkillSlotName(slot)} Lv.{lv + 1}!");
        }

        // ---------------- 초월 탭 ----------------

        private TextMeshProUGUI _trInfo, _trPassives, _trBtnLbl;
        private Button _trBtn;
        private StarRow _trStars;

        private void RefreshTranscendTab(OwnedCharacter o)
        {
            int starN = roster.StarOf(_selected);
            int tr = roster.TranscendOf(_selected);
            _trStars.Set(starN, tr);
            string promoted = starN > (int)_selected.rarity ? $" → 현재 {starN}★" : "";
            float statMul = Transcendence.StatMultiplier(_selected.rarity, starN, tr);
            float skMul = Transcendence.SkillMultiplier(tr);
            _trInfo.text = $"태생 {(int)_selected.rarity}★{promoted}    중복 <b>{o.Duplicates}</b>장    " +
                           $"<color=#9AA3B5>풀 초월까지 {Transcendence.RemainingCost(_selected.rarity, starN, tr)} / {Transcendence.TotalCost(_selected.rarity)}장</color>\n" +
                           $"능력치 배율 <b>×{statMul:0.00}</b>    " + (tr > 0 ? $"<color=#B47CFF>초월 스킬 계수 +{Mathf.RoundToInt((skMul - 1f) * 100)}%</color>" : "<color=#6B7280>초월 스킬 계수 +0%</color>");
            _trPassives.text = TranscendRewardText(_selected, starN, tr);

            int mcost = roster.MergeCost(_selected);
            int mgold = Economy.MergeGold(starN, tr);
            if (starN < Transcendence.MaxStar)
            {
                _trBtnLbl.text = $"합성 → {starN + 1}★  <size=70%>중복 {o.Duplicates}/{mcost} · {GoldTag(mgold)}</size>";
                _trBtn.interactable = roster.CanPromote(_selected) && Gold >= mgold;
            }
            else if (tr < Transcendence.MaxTranscend)
            {
                _trBtnLbl.text = $"초월 → {StarText.Plus(tr + 1)}  <size=70%>중복 {o.Duplicates}/{mcost} · {GoldTag(mgold)}</size>";
                _trBtn.interactable = roster.CanTranscend(_selected) && Gold >= mgold;
            }
            else
            {
                _trBtnLbl.text = "초월 완료 " + StarText.Plus(tr);
                _trBtn.interactable = false;
            }
        }

        /// <summary>초월 단계별 보상: +1 · +3 · +6 고유 능력(패시브 강화), +2 · +4 · +5 스킬 레벨 +3(재료 없이).</summary>
        private static string TranscendRewardText(CharacterData c, int star, int transcend)
        {
            var kit = CharacterKits.Get(c);
            var sb = new StringBuilder("<b>초월 보상</b>");
            if (star < Transcendence.MaxStar) sb.Append("  <color=#9AA3B5><size=85%>(6★ 달성 후 초월)</size></color>");
            for (int t = 1; t <= Transcendence.MaxTranscend; t++)
            {
                string body = TranscendRewardLine(kit, t);
                sb.Append('\n');
                if (transcend >= t) sb.Append($"<color={StarText.Rainbow[t - 1]}>+{t}</color> {body}");
                else sb.Append($"<color=#6B7280>+{t} {body.Replace("<b>", "").Replace("</b>", "")}</color>");
            }
            return sb.ToString();
        }

        private static string TranscendRewardLine(CharacterKit kit, int t)
        {
            KitAbility a = t == 1 ? kit.t1 : t == 3 ? kit.t3 : t == 6 ? kit.t6 : null;
            if (a != null) return $"<b>{a.name}</b> — {a.Desc}";
            int slot = t == 2 ? 0 : t == 4 ? 1 : 2;
            return $"<b>{Leveling.SkillSlotName((SkillSlot)slot)} 레벨 +{Transcendence.SkillLevelStep}</b> (재료 없이 바로 적용 · 최대 Lv. {Leveling.MaxSkillLevelAbsolute})";
        }

        private void MergeOrTranscend()
        {
            if (_selected == null || roster == null) return;
            int cost = roster.MergeCost(_selected);
            int gold = Economy.MergeGold(roster.StarOf(_selected), roster.TranscendOf(_selected));
            bool canMerge = roster.StarOf(_selected) < Transcendence.MaxStar ? roster.CanPromote(_selected) : roster.CanTranscend(_selected);
            if (canMerge && Gold < gold) { Toast($"금화가 부족합니다 (필요 {gold:N0})"); return; }
            if (canMerge) PayGold(gold);
            if (roster.StarOf(_selected) < Transcendence.MaxStar)
            {
                if (!roster.Promote(_selected)) { Toast($"중복 캐릭터가 부족합니다 (필요 {cost})"); return; }
                Toast($"합성 완료! {roster.StarOf(_selected)}★ 달성");
            }
            else
            {
                if (roster.TranscendOf(_selected) >= Transcendence.MaxTranscend) { Toast("이미 최대 초월(+6)"); return; }
                if (!roster.Transcend(_selected)) { Toast($"중복 캐릭터가 부족합니다 (필요 {cost})"); return; }
                int t = roster.TranscendOf(_selected);
                var kit = CharacterKits.Get(_selected);
                KitAbility ability = t == 1 ? kit.t1 : t == 3 ? kit.t3 : t == 6 ? kit.t6 : null;
                string unlocked = ability != null
                    ? $" — 고유 능력 해금: {ability.name}"
                    : $" — {Leveling.SkillSlotName((SkillSlot)(t == 2 ? 0 : t == 4 ? 1 : 2))} 레벨 +{Transcendence.SkillLevelStep}";
                Toast($"초월 +{t} 달성!{unlocked}");
            }
        }

        private void Toast(string msg)
        {
            if (_toast == null) return;
            _toast.text = msg;
            CancelInvoke(nameof(ClearToast));
            Invoke(nameof(ClearToast), 2.4f);
        }
        private void ClearToast() { if (_toast != null) _toast.text = ""; }

        // ================================================================
        //  UI 구성
        // ================================================================

        private void BuildUI()
        {
            var cgo = UIFactory.NewRect("GrowthCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 85;
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.05f, 0.05f, 0.08f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            BackgroundArt.Create(_root.transform, 0.72f).Set("Backgrounds/growth");
            var root = _root.transform;

            var title = UIFactory.AddText("Title", root, "캐릭터 육성", 36, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(600, 48), new Vector2(0, -30));
            _goldText = UIFactory.AddText("Gold", root, "", 24, font, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
            Place(_goldText.rectTransform, new Vector2(1, 1), new Vector2(500, 36), new Vector2(-50, -36), new Vector2(1, 1));

            BuildList(root);

            _heroLayer = UIFactory.NewRect("HeroLayer", root);
            UIFactory.Stretch(_heroLayer);

            BuildDetail(root);

            // 테스트 버튼(좌하단)
            var charge = UIFactory.AddButton("Charge", root, "재료·금화·장비 충전(테스트)", 18, font, new Color(1, 1, 1, 0.14f), Color.white);
            Place((RectTransform)charge.transform, new Vector2(0, 0), new Vector2(240, 48), new Vector2(30, 30), new Vector2(0, 0));
            charge.onClick.AddListener(() =>
            {
                if (inventory != null) inventory.GrantTest();
                if (equipment != null) equipment.GrantTest(12);
                if (wallet != null) wallet.AddGold(100000);
            });
            var dup = UIFactory.AddButton("DupTest", root, "선택 캐릭 중복 +6(테스트)", 18, font, new Color(1, 1, 1, 0.14f), Color.white);
            Place((RectTransform)dup.transform, new Vector2(0, 0), new Vector2(280, 48), new Vector2(290, 30), new Vector2(0, 0));
            dup.onClick.AddListener(() =>
            {
                if (roster == null) return;
                if (_selected != null && _inDetail) roster.GrantTestDuplicates(_selected, 6);
                else Toast("캐릭터를 선택한 상태에서 누르세요");
            });

            _equipPanel = EquipmentPanel.Create(root, font, equipment, inventory, wallet,
                key => database != null ? database.GetCharacter(key)?.displayName : null);
            _equipPanel.OnClosed += Refresh;

            _backBtn = UIFactory.AddButton("BackBtn", root, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            Place((RectTransform)_backBtn.transform, new Vector2(0, 1), new Vector2(150, 44), new Vector2(30, -30), new Vector2(0, 1));
            _backBtn.onClick.AddListener(OnBack);
            _backLabel = _backBtn.GetComponentInChildren<TextMeshProUGUI>();
            _backBtn.gameObject.SetActive(false);

            _toast = UIFactory.AddText("Toast", root, "", 24, font, new Color(1f, 0.7f, 0.5f), TextAlignmentOptions.Center, FontStyles.Bold);
            Place(_toast.rectTransform, new Vector2(0.5f, 1), new Vector2(1200, 34), new Vector2(0, -84));
            _toast.raycastTarget = false;
        }

        private void BuildList(Transform root)
        {
            var list = UIFactory.NewRect("List", root);
            UIFactory.Stretch(list);
            _listGroup = list.gameObject.AddComponent<CanvasGroup>();

            const int cols = 7;
            float gridW = cols * _cell.x + (cols - 1) * 12;
            _grid = UIFactory.NewRect("Grid", list);
            UIFactory.Anchor(_grid, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            _grid.sizeDelta = new Vector2(gridW, 700); _grid.anchoredPosition = new Vector2(0, -246);   // 필터 바(3줄, -120 ~ -228) 아래
            var gl = _grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = _cell; gl.spacing = new Vector2(12, 14);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = cols;
            gl.childAlignment = TextAnchor.UpperCenter;

            _filterBar = RosterFilterBar.Build(list, new Vector2(0, -120), gridW, font);
            _filterBar.OnChanged += () => RebuildGrid();
        }

        private void BuildDetail(Transform root)
        {
            var group = UIFactory.NewRect("Detail", root);
            UIFactory.Stretch(group);
            _detailGroup = group.gameObject.AddComponent<CanvasGroup>();

            var panel = UIFactory.AddImage("Panel", group, new Color(0.10f, 0.10f, 0.14f, 0.96f), null, raycast: true);
            _detailPanel = panel.rectTransform;
            UIFactory.Anchor(_detailPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _detailPanel.sizeDelta = new Vector2(1040, 900); _detailPanel.anchoredPosition = new Vector2(380, -50);
            var p = panel.transform;

            _hName = Txt(p, "", 34, FontStyles.Bold, TextAlignmentOptions.TopLeft, 40, -22, 960, 46);
            _hLine = Txt(p, "", 22, FontStyles.Normal, TextAlignmentOptions.TopLeft, 40, -72, 960, 32);

            var statsBg = UIFactory.AddImage("StatsBg", p, new Color(0, 0, 0, 0.25f));
            TopLeft(statsBg.rectTransform, 40, -112, 960, 196);
            _statsL = Txt(p, "", 19, FontStyles.Normal, TextAlignmentOptions.TopLeft, 60, -122, 440, 180);
            _statsR = Txt(p, "", 19, FontStyles.Normal, TextAlignmentOptions.TopLeft, 520, -122, 470, 180);
            _statsL.lineSpacing = 4f; _statsR.lineSpacing = 4f;

            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                var b = UIFactory.AddButton("Tab", p, TabNames[i], 22, font, new Color(1, 1, 1, 0.12f), Color.white);
                TopLeft((RectTransform)b.transform, 40 + i * 194, -324, 184, 52);
                b.onClick.AddListener(() => ShowTab((GrowTab)idx));
                _tabBtns[i] = b;
            }

            var content = UIFactory.NewRect("Content", p);
            TopLeft(content, 40, -392, 960, 488);
            for (int i = 0; i < 5; i++)
            {
                var page = UIFactory.NewRect(TabNames[i], content);
                UIFactory.Stretch(page);
                _tabPages[i] = page.gameObject;
            }
            BuildLevelPage(_tabPages[0].transform);
            BuildEquipPage(_tabPages[1].transform);
            BuildAscendPage(_tabPages[2].transform);
            BuildSkillPage(_tabPages[3].transform);
            BuildTranscendPage(_tabPages[4].transform);

            group.gameObject.SetActive(false);
        }

        private void BuildLevelPage(Transform page)
        {
            _lvPreview = Txt(page, "", 28, FontStyles.Bold, TextAlignmentOptions.Top, 0, -4, 960, 74);
            _lvPreview.lineSpacing = 4f;
            var tint = new[]
            {
                Grades.ColorOf(3), Grades.ColorOf(4), Grades.ColorOf(5), Grades.ColorOf(6),
            };
            var mats = Materials.ExpMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                int idx = i;
                float x = 150 + i * 174;
                var slot = UIFactory.AddImage("Slot", page, tint[i], null, raycast: true);
                TopLeft(slot.rectTransform, x, -88, 156, 150);
                var sbtn = slot.gameObject.AddComponent<Button>();
                sbtn.targetGraphic = slot;
                sbtn.onClick.AddListener(() => AdjustSel(idx, +1));
                _lvSlotBtn[i] = sbtn;
                var inner = UIFactory.AddImage("Inner", slot.transform, new Color(0.08f, 0.09f, 0.12f, 0.94f));
                UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);
                Txt(inner.transform, $"<color={Grades.Hex(Materials.Grade(mats[i]))}>{Materials.ShortName(mats[i])}</color>", 19, FontStyles.Bold, TextAlignmentOptions.Top, 0, -6, 150, 26);
                Txt(inner.transform, $"+{Materials.ExpValue(mats[i]):N0} EXP", 15, FontStyles.Normal, TextAlignmentOptions.Top, 0, -32, 150, 20).color = new Color(0.8f, 0.83f, 0.9f);
                _lvCount[i] = Txt(inner.transform, "0", 34, FontStyles.Bold, TextAlignmentOptions.Top, 0, -56, 150, 44);
                _lvOwned[i] = Txt(inner.transform, "", 14, FontStyles.Normal, TextAlignmentOptions.Top, 0, -114, 150, 22);
                _lvOwned[i].color = new Color(0.75f, 0.78f, 0.85f);
                _lvMinus[i] = SmallBtn(page, "-", x + 4, -246, () => AdjustSel(idx, -1));
                _lvPlus[i] = SmallBtn(page, "+", x + 82, -246, () => AdjustSel(idx, +1));
            }
            _lvCost = Txt(page, "", 22, FontStyles.Bold, TextAlignmentOptions.Top, 0, -304, 960, 34);
            _lvClear = Btn(page, "초기화", new Color(1, 1, 1, 0.14f), 150, -416, 170, 64, ResetAndRefresh);
            _lvAuto = Btn(page, "자동 선택", new Color(0.28f, 0.5f, 0.75f, 0.95f), 340, -416, 210, 64, AutoSelect);
            _lvConfirm = Btn(page, "레벨업", new Color(0.3f, 0.62f, 0.4f, 0.98f), 570, -416, 240, 64, ConfirmLevelUp);
        }

        private void ResetAndRefresh() { ResetLevelSelection(); Refresh(); }

        private void BuildEquipPage(Transform page)
        {
            for (int i = 0; i < 6; i++)
            {
                float x = (i % 2) * 486, y = -(i / 2) * 104;
                var frame = UIFactory.AddImage("Slot", page, new Color(1, 1, 1, 0.1f));
                TopLeft(frame.rectTransform, x, y, 474, 94);
                var inner = UIFactory.AddImage("Inner", frame.transform, new Color(0.08f, 0.09f, 0.12f, 0.95f));
                UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);
                _eqSlotFrame[i] = frame;
                _eqSlotText[i] = Txt(inner.transform, "", 18, FontStyles.Normal, TextAlignmentOptions.TopLeft, 14, -10, 446, 76);
                _eqSlotText[i].lineSpacing = 6f;
            }
            _eqSets = Txt(page, "", 17, FontStyles.Normal, TextAlignmentOptions.TopLeft, 0, -326, 960, 70);
            _eqSets.enableAutoSizing = true; _eqSets.fontSizeMin = 12; _eqSets.fontSizeMax = 17;
            Btn(page, "장비 관리  <size=70%>(장착 · 강화 · 초월 · 분해)</size>", new Color(0.22f, 0.55f, 0.55f, 0.95f), 280, -416, 400, 64, OpenEquipment);
        }

        private void BuildAscendPage(Transform page)
        {
            _ascInfo = Txt(page, "", 21, FontStyles.Normal, TextAlignmentOptions.TopLeft, 0, -6, 960, 150);
            _ascInfo.lineSpacing = 14f;
            _ascNeed = Txt(page, "", 21, FontStyles.Normal, TextAlignmentOptions.Top, 0, -230, 960, 90);
            _ascNeed.lineSpacing = 8f;
            _ascBtn = Btn(page, "", new Color(0.8f, 0.6f, 0.2f, 0.95f), 280, -416, 400, 64, Ascend);
            _ascBtnLbl = _ascBtn.GetComponentInChildren<TextMeshProUGUI>();
        }

        private void BuildSkillPage(Transform page)
        {
            // 왼쪽: 스킬 3개 + 패시브 선택
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                _skSelBtn[i] = Btn(page, "", new Color(1, 1, 1, 0.1f), 0, -i * 123, 262, 115, () => SelectSkill(idx));
                _skSelLbl[i] = _skSelBtn[i].GetComponentInChildren<TextMeshProUGUI>();
                _skSelLbl[i].alignment = TextAlignmentOptions.Left;
                _skSelLbl[i].lineSpacing = 4f;
                _skSelLbl[i].margin = new Vector4(10, 0, 4, 0);
                _skSelLbl[i].fontSizeMax = 21;
            }
            // 오른쪽: 상세
            var bg = UIFactory.AddImage("SkillDetail", page, new Color(0, 0, 0, 0.28f), ProceduralSprites.Rounded);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false; bg.pixelsPerUnitMultiplier = 2f;
            TopLeft(bg.rectTransform, 276, 0, 684, 488);
            var d = bg.transform;
            _skTitle = Txt(d, "", 28, FontStyles.Bold, TextAlignmentOptions.TopLeft, 20, -12, 644, 40);
            _skTitle.enableAutoSizing = true; _skTitle.fontSizeMin = 16; _skTitle.fontSizeMax = 28;
            _skDesc = Txt(d, "", 18, FontStyles.Normal, TextAlignmentOptions.TopLeft, 20, -58, 644, 62);
            _skDesc.enableAutoSizing = true; _skDesc.fontSizeMin = 13; _skDesc.fontSizeMax = 18;
            _skDesc.lineSpacing = 4f;
            _skStats = Txt(d, "", 17, FontStyles.Normal, TextAlignmentOptions.TopLeft, 20, -128, 644, 168);
            _skStats.enableAutoSizing = true; _skStats.fontSizeMin = 12; _skStats.fontSizeMax = 17;
            _skStats.lineSpacing = 8f;
            _skTable = Txt(d, "", 17, FontStyles.Normal, TextAlignmentOptions.TopLeft, 20, -304, 644, 62);
            _skTable.enableAutoSizing = true; _skTable.fontSizeMin = 11; _skTable.fontSizeMax = 17;
            _skTable.lineSpacing = 6f;
            _skCostText = Txt(d, "", 17, FontStyles.Normal, TextAlignmentOptions.TopLeft, 20, -378, 644, 28);
            _skCostText.enableAutoSizing = true; _skCostText.fontSizeMin = 12; _skCostText.fontSizeMax = 17;
            _skUpBtn = Btn(d, "", new Color(0.5f, 0.35f, 0.75f, 0.98f), 192, -416, 300, 60, () => SkillUp((SkillSlot)_skillSel));
            _skUpLbl = _skUpBtn.GetComponentInChildren<TextMeshProUGUI>();
        }

        private void BuildTranscendPage(Transform page)
        {
            _trStars = StarRow.Create(page, 34, TextAnchor.MiddleLeft, font);
            TopLeft(_trStars.Rect, 0, -2, 600, 36);
            _trInfo = Txt(page, "", 19, FontStyles.Normal, TextAlignmentOptions.TopLeft, 0, -48, 960, 64);
            _trInfo.lineSpacing = 6f;
            _trPassives = Txt(page, "", 17, FontStyles.Normal, TextAlignmentOptions.TopLeft, 0, -124, 960, 270);
            _trPassives.lineSpacing = 6f;
            _trPassives.enableAutoSizing = true; _trPassives.fontSizeMin = 12; _trPassives.fontSizeMax = 17;
            _trBtn = Btn(page, "", new Color(0.85f, 0.35f, 0.55f, 0.95f), 230, -416, 500, 64, MergeOrTranscend);
            _trBtnLbl = _trBtn.GetComponentInChildren<TextMeshProUGUI>();
        }

        // ---------------- UI 헬퍼 ----------------

        private static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            UIFactory.Anchor(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, y);
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos, Vector2? pivot = null)
        {
            UIFactory.Anchor(rt, anchor, anchor, pivot ?? new Vector2(0.5f, 1));
            rt.sizeDelta = size; rt.anchoredPosition = pos;
        }

        private TextMeshProUGUI Txt(Transform parent, string text, float size, FontStyles style, TextAlignmentOptions align, float x, float y, float w, float h)
        {
            var t = UIFactory.AddText("T", parent, text, size, font, Color.white, align, style);
            TopLeft(t.rectTransform, x, y, w, h);
            t.richText = true;
            t.raycastTarget = false;
            return t;
        }

        private Button Btn(Transform parent, string label, Color bg, float x, float y, float w, float h, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIFactory.AddButton("Btn", parent, label, 22, font, bg, Color.white);
            TopLeft((RectTransform)b.transform, x, y, w, h);
            b.onClick.AddListener(onClick);
            var l = b.GetComponentInChildren<TextMeshProUGUI>();
            l.richText = true; l.enableAutoSizing = true; l.fontSizeMin = 12; l.fontSizeMax = 22;
            return b;
        }

        private Button SmallBtn(Transform parent, string label, float x, float y, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIFactory.AddButton("Adj", parent, label, 28, font, new Color(1, 1, 1, 0.14f), Color.white);
            TopLeft((RectTransform)b.transform, x, y, 70, 44);
            b.onClick.AddListener(onClick);
            return b;
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
