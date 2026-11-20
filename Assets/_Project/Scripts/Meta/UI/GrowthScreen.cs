using System.Collections.Generic;
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
    /// 캐릭터 육성 화면. 재료로 레벨업/돌파/스킬 레벨업 (가챠 크리스탈과 별개).
    /// 코드 자동 구성 — roster/database/inventory 연결(비우면 자동 탐색).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Growth Screen")]
    public class GrowthScreen : MonoBehaviour
    {
        [SerializeField] private PlayerRoster roster;
        [SerializeField] private GameDatabase database;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private TMP_FontAsset font;

        private GameObject _root, _detPanel;
        private RectTransform _grid;
        private Image _detPortrait, _expFill;
        private TextMeshProUGUI _matText, _detName, _detLevel, _detStats, _detExp, _detSkill, _toast;
        private TextMeshProUGUI _potionLbl, _crystalLbl, _ascendLbl, _skillLbl;
        private Button _potionBtn, _crystalBtn, _ascendBtn, _skillBtn;

        private CharacterData _selected;
        private readonly Dictionary<CharacterData, Image> _cardHL = new();
        private readonly Vector2 _cell = new(130, 160);

        private void Awake()
        {
            if (roster == null) roster = FindAnyObjectByType<PlayerRoster>();
            if (inventory == null) inventory = FindAnyObjectByType<PlayerInventory>();
            BuildUI();
        }

        private void OnEnable()
        {
            if (roster != null) roster.OnRosterChanged += OnRosterChanged;
            if (inventory != null) inventory.OnChanged += Refresh;
        }

        private void OnDisable()
        {
            if (roster != null) roster.OnRosterChanged -= OnRosterChanged;
            if (inventory != null) inventory.OnChanged -= Refresh;
        }

        private void Start()
        {
            RebuildGrid();
            SelectFirst();
            Refresh();
        }

        public void Show(bool on) { if (_root != null) _root.SetActive(on); }

        private void OnRosterChanged()
        {
            RebuildGrid();
            if (_selected == null) SelectFirst();
            RefreshHighlights();
            Refresh();
        }

        private void SelectFirst()
        {
            if (_selected != null || roster == null || database == null) return;
            foreach (var o in roster.Owned)
            {
                var c = database.GetCharacter(o.id);
                if (c != null) { Select(c); return; }
            }
        }

        // ---------------- 목록 ----------------

        private void RebuildGrid()
        {
            for (int i = _grid.childCount - 1; i >= 0; i--)
            {
                var ch = _grid.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
            _cardHL.Clear();

            if (roster == null || database == null) return;
            foreach (var o in roster.Owned)
            {
                var c = database.GetCharacter(o.id);
                if (c != null) BuildCard(c, o.level, o.ascension);
            }
        }

        private void BuildCard(CharacterData c, int level, int ascension)
        {
            var card = UIFactory.NewRect("Card", _grid);

            var hl = UIFactory.AddImage("HL", card, new Color(0.3f, 0.8f, 1f, 0f));
            UIFactory.Stretch(hl.rectTransform, -3, -3, -3, -3);
            _cardHL[c] = hl;

            var frame = UIFactory.AddImage("Frame", card, RarityColor(c.rarity), null, raycast: true);
            UIFactory.Stretch(frame.rectTransform);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            var cc = c;
            btn.onClick.AddListener(() => Select(cc));

            var inner = UIFactory.AddImage("Inner", frame.transform, new Color(0.10f, 0.11f, 0.14f, 1f));
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);

            var portrait = UIFactory.AddImage("Portrait", inner.transform, new Color(1, 1, 1, 0.12f));
            UIFactory.Stretch(portrait.rectTransform, 5, 44, 5, 5);
            var sprite = c.thumbnail != null ? c.thumbnail : c.portrait;
            portrait.sprite = sprite; portrait.enabled = sprite != null; portrait.preserveAspect = true;

            var name = UIFactory.AddText("Name", inner.transform, c.displayName, 13, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var nrt = name.rectTransform;
            UIFactory.Anchor(nrt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            nrt.sizeDelta = new Vector2(0, 22); nrt.anchoredPosition = new Vector2(0, 22);

            string star = ascension > 0 ? $"Lv.{level} <color=#FFD36B>★{ascension}</color>" : $"Lv.{level}";
            var lv = UIFactory.AddText("Lv", inner.transform, star, 13, font, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, FontStyles.Bold);
            var lrt = lv.rectTransform;
            UIFactory.Anchor(lrt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            lrt.sizeDelta = new Vector2(0, 20); lrt.anchoredPosition = new Vector2(0, 2);
        }

        private void Select(CharacterData c)
        {
            _selected = c;
            RefreshHighlights();
            RefreshDetail();
        }

        private void RefreshHighlights()
        {
            foreach (var kv in _cardHL)
                kv.Value.color = new Color(0.3f, 0.8f, 1f, kv.Key == _selected ? 0.9f : 0f);
        }

        // ---------------- 갱신 ----------------

        private void Refresh()
        {
            if (_matText != null)
                _matText.text = inventory == null ? "" :
                    $"물약 {inventory.Count(MaterialType.ExpPotion)}    결정 {inventory.Count(MaterialType.ExpCrystal)}    돌파석 {inventory.Count(MaterialType.AscensionStone)}    교본 {inventory.Count(MaterialType.SkillBook)}";
            RefreshDetail();
        }

        private void RefreshDetail()
        {
            bool has = _selected != null && roster != null;
            if (_detPanel != null) _detPanel.SetActive(has);
            if (!has) return;

            var o = roster.GetOwned(_selected);
            if (o == null) { _detPanel.SetActive(false); return; }

            var sprite = _selected.thumbnail != null ? _selected.thumbnail : _selected.portrait;
            _detPortrait.sprite = sprite; _detPortrait.enabled = sprite != null;

            _detName.text = $"{new string('★', (int)_selected.rarity)}  {_selected.displayName}";

            int cap = Leveling.LevelCap(o.ascension);
            string asc = o.ascension > 0 ? $"  <color=#FFD36B>돌파 {o.ascension}</color>" : "";
            _detLevel.text = $"Lv. {o.level} / {cap}{asc}";

            var (hp, atk, def, spd) = StatsAt(_selected, o.level, o.skillLevel);
            _detStats.text = $"HP {hp}    ATK {atk}\nDEF {def}    SPD {spd}";

            bool atCap = o.level >= cap;
            int need = o.level >= Leveling.MaxLevel ? 0 : Leveling.ExpForNext(o.level);
            _detExp.text = atCap ? (o.ascension >= Leveling.MaxAscension ? "MAX" : "상한 — 돌파 필요") : $"EXP {o.exp} / {need}";
            SetExpFill(atCap ? 1f : (need > 0 ? (float)o.exp / need : 0f));

            _detSkill.text = $"스킬 레벨  {o.skillLevel} / {Leveling.MaxSkillLevel}";

            // 버튼 라벨/활성
            int potion = Mat(MaterialType.ExpPotion), crystal = Mat(MaterialType.ExpCrystal);
            _potionLbl.text = $"물약 +500\n<size=60%>보유 {potion}</size>";
            _crystalLbl.text = $"결정 +2500\n<size=60%>보유 {crystal}</size>";
            _potionBtn.interactable = !atCap && potion > 0;
            _crystalBtn.interactable = !atCap && crystal > 0;

            if (o.ascension >= Leveling.MaxAscension)
            {
                _ascendLbl.text = "돌파 완료";
                _ascendBtn.interactable = false;
            }
            else
            {
                int cost = Leveling.AscensionCost(o.ascension);
                _ascendLbl.text = $"돌파 → Lv.{Leveling.LevelCap(o.ascension + 1)}\n<size=60%>돌파석 {Mat(MaterialType.AscensionStone)} / {cost}</size>";
                _ascendBtn.interactable = roster.CanAscend(_selected) && Mat(MaterialType.AscensionStone) >= cost;
            }

            if (o.skillLevel >= Leveling.MaxSkillLevel)
            {
                _skillLbl.text = "스킬 MAX";
                _skillBtn.interactable = false;
            }
            else
            {
                int cost = Leveling.SkillUpgradeCost(o.skillLevel);
                _skillLbl.text = $"스킬 Lv.{o.skillLevel} → {o.skillLevel + 1}\n<size=60%>교본 {Mat(MaterialType.SkillBook)} / {cost}</size>";
                _skillBtn.interactable = Mat(MaterialType.SkillBook) >= cost;
            }
        }

        private int Mat(MaterialType t) => inventory != null ? inventory.Count(t) : 0;

        // ---------------- 액션 ----------------

        private void UseExp(MaterialType mt)
        {
            if (_selected == null || roster == null) return;
            var o = roster.GetOwned(_selected);
            if (o == null) return;
            if (o.level >= Leveling.LevelCap(o.ascension)) { Toast("레벨 상한 — 돌파가 필요합니다"); return; }
            if (inventory != null && !inventory.Spend(mt, 1)) { Toast($"{Materials.Name(mt)} 부족"); return; }
            roster.AddExp(_selected, Materials.ExpValue(mt));
        }

        private void Ascend()
        {
            if (_selected == null || roster == null) return;
            if (!roster.CanAscend(_selected)) { Toast("상한까지 레벨을 올리세요"); return; }
            int cost = Leveling.AscensionCost(roster.AscensionOf(_selected));
            if (inventory != null && !inventory.Spend(MaterialType.AscensionStone, cost)) { Toast("돌파석 부족"); return; }
            roster.Ascend(_selected);
        }

        private void SkillUp()
        {
            if (_selected == null || roster == null) return;
            if (!roster.CanUpgradeSkill(_selected)) { Toast("이미 최대 스킬 레벨"); return; }
            int cost = Leveling.SkillUpgradeCost(roster.SkillLevelOf(_selected));
            if (inventory != null && !inventory.Spend(MaterialType.SkillBook, cost)) { Toast("스킬 교본 부족"); return; }
            roster.UpgradeSkill(_selected);
        }

        private (int hp, int atk, int def, int spd) StatsAt(CharacterData c, int level, int skillLevel)
        {
            System.Func<CharacterData, BattleUnitData> lookup = null;
            if (database != null) lookup = database.GetUnit;
            var u = RuntimeUnits.FromCharacter(c, level, lookup, skillLevel);
            var s = u.baseStats;
            return (s.MaxHPAt(level), s.AtkAt(level), s.DefAt(level), s.spd);
        }

        private void SetExpFill(float ratio)
        {
            var rt = _expFill.rectTransform;
            var m = rt.anchorMax; m.x = Mathf.Clamp01(ratio); rt.anchorMax = m;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private void Toast(string msg)
        {
            if (_toast == null) return;
            _toast.text = msg;
            CancelInvoke(nameof(ClearToast));
            Invoke(nameof(ClearToast), 1.6f);
        }
        private void ClearToast() { if (_toast != null) _toast.text = ""; }

        // ---------------- UI 구성 ----------------

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

            var title = UIFactory.AddText("Title", _root.transform, "캐릭터 육성", 36, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = title.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            trt.sizeDelta = new Vector2(600, 48); trt.anchoredPosition = new Vector2(0, -30);

            _matText = UIFactory.AddText("Materials", _root.transform, "", 20, font, new Color(0.8f, 0.9f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
            var mrt = _matText.rectTransform;
            UIFactory.Anchor(mrt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            mrt.sizeDelta = new Vector2(1100, 30); mrt.anchoredPosition = new Vector2(0, -76);

            // 좌: 보유 그리드
            _grid = UIFactory.NewRect("Grid", _root.transform);
            UIFactory.Anchor(_grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _grid.anchoredPosition = new Vector2(-430, -20);
            var gl = _grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = _cell; gl.spacing = new Vector2(12, 12);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 3;
            gl.childAlignment = TextAnchor.MiddleCenter;
            _grid.sizeDelta = new Vector2(3 * _cell.x + 2 * 12, 620);

            BuildDetail();

            // 테스트 재료 충전
            var charge = UIFactory.AddButton("Charge", _root.transform, "재료 충전(테스트)", 18, font, new Color(1, 1, 1, 0.14f), Color.white);
            var crt = (RectTransform)charge.transform;
            UIFactory.Anchor(crt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            crt.sizeDelta = new Vector2(240, 48); crt.anchoredPosition = new Vector2(30, 30);
            charge.onClick.AddListener(() => { if (inventory != null) inventory.GrantTest(); });

            _toast = UIFactory.AddText("Toast", _root.transform, "", 22, font, new Color(1f, 0.6f, 0.5f), TextAlignmentOptions.Center, FontStyles.Bold);
            var tort = _toast.rectTransform;
            UIFactory.Anchor(tort, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            tort.sizeDelta = new Vector2(600, 30); tort.anchoredPosition = new Vector2(0, 40);
        }

        private void BuildDetail()
        {
            var panel = UIFactory.AddImage("Detail", _root.transform, new Color(0.10f, 0.10f, 0.14f, 0.95f));
            _detPanel = panel.gameObject;
            var prt = panel.rectTransform;
            UIFactory.Anchor(prt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            prt.sizeDelta = new Vector2(540, 780); prt.anchoredPosition = new Vector2(440, -20);

            _detPortrait = TopImg(panel.transform, "Portrait", new Vector2(200, 250), -16);
            _detPortrait.color = new Color(1, 1, 1, 0.1f); _detPortrait.preserveAspect = true;

            _detName = TopText(panel.transform, "Name", 26, FontStyles.Bold, new Vector2(500, 36), -278);
            _detLevel = TopText(panel.transform, "Level", 23, FontStyles.Bold, new Vector2(500, 30), -316);
            _detLevel.color = new Color(1f, 0.85f, 0.4f);
            _detStats = TopText(panel.transform, "Stats", 21, FontStyles.Normal, new Vector2(500, 70), -352);
            _detStats.lineSpacing = 6f;

            // EXP 바
            var expBar = UIFactory.NewRect("ExpBar", panel.transform);
            UIFactory.Anchor(expBar, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            expBar.sizeDelta = new Vector2(480, 20); expBar.anchoredPosition = new Vector2(0, -452);
            var expBg = UIFactory.AddImage("BG", expBar, new Color(0, 0, 0, 0.5f));
            UIFactory.Stretch(expBg.rectTransform);
            _expFill = UIFactory.AddImage("Fill", expBar, new Color(0.4f, 0.85f, 1f, 1f));
            UIFactory.Stretch(_expFill.rectTransform);
            _detExp = UIFactory.AddText("ExpText", expBar.transform, "", 13, font, Color.white, TextAlignmentOptions.Center);
            UIFactory.Stretch(_detExp.rectTransform);

            _detSkill = TopText(panel.transform, "Skill", 20, FontStyles.Normal, new Vector2(500, 26), -486);
            _detSkill.color = new Color(0.8f, 0.85f, 1f);

            // 레벨업(물약/결정)
            _potionBtn = DetailButton(panel.transform, new Color(0.35f, 0.55f, 0.35f, 0.95f), new Vector2(238, 54), new Vector2(-126, -548), UseExpPotion, out _potionLbl);
            _crystalBtn = DetailButton(panel.transform, new Color(0.3f, 0.5f, 0.6f, 0.95f), new Vector2(238, 54), new Vector2(126, -548), UseExpCrystal, out _crystalLbl);
            // 돌파
            _ascendBtn = DetailButton(panel.transform, new Color(0.8f, 0.6f, 0.2f, 0.95f), new Vector2(492, 54), new Vector2(0, -610), Ascend, out _ascendLbl);
            // 스킬업
            _skillBtn = DetailButton(panel.transform, new Color(0.5f, 0.35f, 0.75f, 0.95f), new Vector2(492, 54), new Vector2(0, -672), SkillUp, out _skillLbl);

            _detPanel.SetActive(false);
        }

        private void UseExpPotion() => UseExp(MaterialType.ExpPotion);
        private void UseExpCrystal() => UseExp(MaterialType.ExpCrystal);

        private Image TopImg(Transform parent, string name, Vector2 size, float y)
        {
            var img = UIFactory.AddImage(name, parent, Color.white);
            var rt = img.rectTransform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            rt.sizeDelta = size; rt.anchoredPosition = new Vector2(0, y);
            return img;
        }

        private TextMeshProUGUI TopText(Transform parent, string name, float size, FontStyles style, Vector2 dim, float y)
        {
            var t = UIFactory.AddText(name, parent, "", size, font, Color.white, TextAlignmentOptions.Center, style);
            var rt = t.rectTransform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            rt.sizeDelta = dim; rt.anchoredPosition = new Vector2(0, y);
            return t;
        }

        private Button DetailButton(Transform parent, Color bg, Vector2 size, Vector2 pos, UnityEngine.Events.UnityAction onClick, out TextMeshProUGUI label)
        {
            var btn = UIFactory.AddButton("Btn", parent, "", 20, font, bg, Color.white);
            var rt = (RectTransform)btn.transform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            rt.sizeDelta = size; rt.anchoredPosition = pos;
            btn.onClick.AddListener(onClick);
            label = btn.GetComponentInChildren<TextMeshProUGUI>();
            return btn;
        }

        private static Color RarityColor(Rarity r) => r switch
        {
            Rarity.R6 => new Color(1f, 0.78f, 0.25f),
            Rarity.R5 => new Color(0.72f, 0.42f, 1f),
            Rarity.R4 => new Color(0.33f, 0.62f, 1f),
            _ => new Color(0.62f, 0.67f, 0.75f),
        };

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
