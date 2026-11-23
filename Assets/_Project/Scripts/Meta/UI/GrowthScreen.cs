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
        [SerializeField] private PlayerWallet wallet;
        [SerializeField] private TMP_FontAsset font;

        private GameObject _root, _detPanel;
        private RectTransform _grid;
        private Image _detPortrait, _expFill;
        private TextMeshProUGUI _matText, _detName, _detLevel, _detInfo, _detStats, _detExp, _detSkill, _detPassives, _toast;
        private TextMeshProUGUI _potionLbl, _crystalLbl, _ascendLbl, _skillLbl, _mergeLbl;
        private Button _potionBtn, _crystalBtn, _ascendBtn, _skillBtn, _mergeBtn, _backBtn;
        private System.Action _onBack;
        private RosterFilterBar _filterBar;
        private StarRow _detStars;

        private CharacterData _selected;
        private readonly Dictionary<CharacterData, Image> _cardHL = new();
        private readonly Vector2 _cell = new(130, 160);

        private void Awake()
        {
            if (roster == null) roster = FindAnyObjectByType<PlayerRoster>();
            if (inventory == null) inventory = FindAnyObjectByType<PlayerInventory>();
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            BuildUI();
        }

        private void OnEnable()
        {
            if (roster != null) roster.OnRosterChanged += OnRosterChanged;
            if (inventory != null) inventory.OnChanged += Refresh;
            if (wallet != null) wallet.OnChanged += Refresh;
        }

        private void OnDisable()
        {
            if (roster != null) roster.OnRosterChanged -= OnRosterChanged;
            if (inventory != null) inventory.OnChanged -= Refresh;
            if (wallet != null) wallet.OnChanged -= Refresh;
        }

        private void Start()
        {
            RebuildGrid();
            SelectFirst();
            Refresh();
        }

        public void Show(bool on)
        {
            if (_root != null) _root.SetActive(on);
            if (!on) return;
            RebuildGrid();
            if (_selected == null) SelectFirst();
            RefreshHighlights();
            Refresh();
        }

        /// <summary>'← 홈' 버튼 동작 지정(null이면 버튼 숨김).</summary>
        public void SetBackAction(System.Action onBack)
        {
            _onBack = onBack;
            if (_backBtn != null) _backBtn.gameObject.SetActive(onBack != null);
        }

        private void OnRosterChanged()
        {
            RebuildGrid();
            if (_selected == null) SelectFirst();
            RefreshHighlights();
            Refresh();
        }

        /// <summary>정렬·필터된 목록의 맨 앞 캐릭터를 선택.</summary>
        private void SelectFirst()
        {
            if (_selected != null || roster == null || database == null) return;
            if (_filterBar != null)
            {
                var entries = _filterBar.filter.Apply(roster, database);
                if (entries.Count > 0) { Select(entries[0].character); return; }
            }
            foreach (var o in roster.Owned)
            {
                var c = database.GetCharacter(o.id);
                if (c != null) { Select(c); return; }
            }
        }

        private void OnFilterChanged()
        {
            RebuildGrid();
            RefreshHighlights();
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

            var entries = _filterBar != null ? _filterBar.filter.Apply(roster, database) : new List<RosterEntry>();
            foreach (var e in entries) BuildCard(e.character, e.owned);
            if (_filterBar != null) _filterBar.SetCount(entries.Count, roster != null ? roster.Owned.Count : 0);
        }

        private void BuildCard(CharacterData c, OwnedCharacter o)
        {
            var card = UIFactory.NewRect("Card", _grid);
            int starN = Transcendence.CurrentStar(c.rarity, o.star);

            var hl = UIFactory.AddImage("HL", card, new Color(0.3f, 0.8f, 1f, 0f));
            UIFactory.Stretch(hl.rectTransform, -3, -3, -3, -3);
            _cardHL[c] = hl;

            var frame = UIFactory.AddImage("Frame", card, StarText.FrameColor(starN, o.transcend), null, raycast: true);
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

            string lvText = o.ascension > 0 ? $"Lv.{o.level} <color=#FFD36B>돌파{o.ascension}</color>" : $"Lv.{o.level}";
            var lv = UIFactory.AddText("Lv", inner.transform, lvText, 13, font, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, FontStyles.Bold);
            var lrt = lv.rectTransform;
            UIFactory.Anchor(lrt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            lrt.sizeDelta = new Vector2(0, 20); lrt.anchoredPosition = new Vector2(0, 2);

            // 속성(색) · 역할 (상단)
            var traits = UIFactory.AddText("Traits", inner.transform,
                $"<color={Traits.Hex(c.element)}>{Traits.Name(c.element)}</color>·{Traits.Name(c.role)}",
                11, font, new Color(0.88f, 0.88f, 0.92f), TextAlignmentOptions.Center, FontStyles.Bold);
            var trrt = traits.rectTransform;
            UIFactory.Anchor(trrt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            trrt.sizeDelta = new Vector2(0, 16); trrt.anchoredPosition = new Vector2(0, -3);

            // 별(초월 = 무지개) — 초상 하단
            var stars = StarRow.Create(inner.transform, 19, TextAnchor.MiddleCenter, font);
            var srt = stars.Rect;
            UIFactory.Anchor(srt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            srt.sizeDelta = new Vector2(0, 19); srt.anchoredPosition = new Vector2(0, 42);
            stars.Set(starN, o.transcend);

            // 합성/초월 가능 표시
            if (roster != null && (roster.CanPromote(c) || roster.CanTranscend(c)))
            {
                var up = UIFactory.AddText("Up", inner.transform, roster.CanPromote(c) ? "합성!" : "초월!", 11, font, new Color(0.45f, 1f, 0.55f), TextAlignmentOptions.TopRight, FontStyles.Bold);
                var urt = up.rectTransform;
                UIFactory.Anchor(urt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
                urt.sizeDelta = new Vector2(48, 16); urt.anchoredPosition = new Vector2(-3, -18);
            }
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
                    $"<color=#FFD36B>금화 {Gold:N0}</color>    물약 {inventory.Count(MaterialType.ExpPotion)}    결정 {inventory.Count(MaterialType.ExpCrystal)}    교본 {inventory.Count(MaterialType.SkillBook)}";
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

            int starN = roster.StarOf(_selected);
            int tr = roster.TranscendOf(_selected);
            _detStars.Set(starN, tr);
            _detName.text = $"{_selected.displayName}" +
                $"  <size=70%><color={Traits.Hex(_selected.element)}>{Traits.Name(_selected.element)}</color> · {Traits.Name(_selected.role)}</size>";
            string promoted = starN > (int)_selected.rarity ? $" → 현재 {starN}★" : "";
            _detInfo.text = $"태생 {(int)_selected.rarity}★{promoted}    중복 {o.Duplicates}장    " +
                $"<color=#9AA3B5>풀 초월까지 {Transcendence.RemainingCost(_selected.rarity, starN, tr)} / {Transcendence.TotalCost(_selected.rarity)}장</color>";

            int cap = Leveling.LevelCap(o.ascension);
            string asc = o.ascension > 0 ? $"  <color=#FFD36B>돌파 {o.ascension}</color>" : "";
            _detLevel.text = $"Lv. {o.level} / {cap}{asc}";

            var (hp, atk, def, spd) = StatsAt(_selected, o.level, o.skillLevel, starN, tr);
            _detStats.text = $"HP {hp}    ATK {atk}\nDEF {def}    SPD {spd}";

            bool atCap = o.level >= cap;
            int need = o.level >= Leveling.MaxLevel ? 0 : Leveling.ExpForNext(o.level);
            _detExp.text = atCap ? (o.ascension >= Leveling.MaxAscension ? "MAX" : "상한 — 돌파 필요") : $"EXP {o.exp} / {need}";
            SetExpFill(atCap ? 1f : (need > 0 ? (float)o.exp / need : 0f));

            float sk = Transcendence.SkillMultiplier(tr);
            _detSkill.text = $"스킬 레벨  {o.skillLevel} / {Leveling.MaxSkillLevel}" +
                (tr > 0 ? $"    <color=#B47CFF>초월 스킬 계수 +{Mathf.RoundToInt((sk - 1f) * 100)}%</color>" : "");
            _detPassives.text = PassiveText(_selected.role, starN, tr);

            // 버튼 라벨/활성
            int potion = Mat(MaterialType.ExpPotion), crystal = Mat(MaterialType.ExpCrystal);
            int potionG = Economy.LevelUpGold(MaterialType.ExpPotion), crystalG = Economy.LevelUpGold(MaterialType.ExpCrystal);
            _potionLbl.text = $"물약 +500\n<size=60%>보유 {potion} · {GoldTag(potionG)}</size>";
            _crystalLbl.text = $"결정 +2500\n<size=60%>보유 {crystal} · {GoldTag(crystalG)}</size>";
            _potionBtn.interactable = !atCap && potion > 0 && Gold >= potionG;
            _crystalBtn.interactable = !atCap && crystal > 0 && Gold >= crystalG;

            if (o.ascension >= Leveling.MaxAscension)
            {
                _ascendLbl.text = "돌파 완료";
                _ascendBtn.interactable = false;
            }
            else
            {
                var am = Economy.AscensionMaterial(_selected.element, o.ascension);
                int cost = Economy.AscensionMaterialCount(o.ascension);
                int gold = Economy.AscensionGold(o.ascension);
                int have = Mat(am);
                string haveStr = have >= cost ? $"{have}" : $"<color=#FF8A8A>{have}</color>";
                _ascendLbl.text = $"돌파 → Lv.{Leveling.LevelCap(o.ascension + 1)}\n" +
                    $"<size=55%>{Materials.RichName(am)} {haveStr}/{cost} · {GoldTag(gold)}</size>";
                _ascendBtn.interactable = roster.CanAscend(_selected) && have >= cost && Gold >= gold;
            }

            if (o.skillLevel >= Leveling.MaxSkillLevel)
            {
                _skillLbl.text = "스킬 MAX";
                _skillBtn.interactable = false;
            }
            else
            {
                int cost = Leveling.SkillUpgradeCost(o.skillLevel);
                int gold = Economy.SkillUpGold(o.skillLevel);
                _skillLbl.text = $"스킬 Lv.{o.skillLevel} → {o.skillLevel + 1}\n<size=60%>교본 {Mat(MaterialType.SkillBook)}/{cost} · {GoldTag(gold)}</size>";
                _skillBtn.interactable = Mat(MaterialType.SkillBook) >= cost && Gold >= gold;
            }

            // 합성 / 초월
            int mcost = roster.MergeCost(_selected);
            int mgold = Economy.MergeGold(starN, tr);
            if (starN < Transcendence.MaxStar)
            {
                _mergeLbl.text = $"합성 → {starN + 1}★\n<size=60%>중복 {o.Duplicates}/{mcost} · {GoldTag(mgold)}</size>";
                _mergeBtn.interactable = roster.CanPromote(_selected) && Gold >= mgold;
            }
            else if (tr < Transcendence.MaxTranscend)
            {
                _mergeLbl.text = $"초월 → {StarText.Plus(tr + 1)}\n<size=60%>중복 {o.Duplicates}/{mcost} · {GoldTag(mgold)}</size>";
                _mergeBtn.interactable = roster.CanTranscend(_selected) && Gold >= mgold;
            }
            else
            {
                _mergeLbl.text = "초월 완료 " + StarText.Plus(tr);
                _mergeBtn.interactable = false;
            }
        }

        /// <summary>초월 고유 패시브 목록(해금 = 밝게, 미해금 = 회색).</summary>
        private static string PassiveText(Role role, int star, int transcend)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("<b>초월 고유 패시브</b>");
            if (star < Transcendence.MaxStar) sb.Append("  <color=#9AA3B5><size=85%>(6★ 달성 후 초월)</size></color>");
            foreach (var p in Transcendence.Passives(role))
            {
                bool on = transcend >= p.level;
                string lvl = $"<color={StarText.Rainbow[p.level - 1]}>+{p.level}</color>";
                sb.Append('\n');
                if (on) sb.Append($"{lvl} <b>{p.name}</b> — {p.desc}");
                else sb.Append($"<color=#6B7280>+{p.level} {p.name} — {p.desc}</color>");
            }
            return sb.ToString();
        }

        private int Mat(MaterialType t) => inventory != null ? inventory.Count(t) : 0;

        private int Gold => wallet != null ? wallet.Gold : int.MaxValue;

        /// <summary>금화 표시(부족하면 빨강).</summary>
        private string GoldTag(int cost) =>
            Gold >= cost ? $"<color=#FFD36B>금화 {cost:N0}</color>" : $"<color=#FF8A8A>금화 {cost:N0}</color>";

        /// <summary>금화 차감(지갑이 없으면 무료). 부족하면 토스트 후 false.</summary>
        private bool PayGold(int cost)
        {
            if (wallet == null || cost <= 0) return true;
            if (wallet.SpendGold(cost)) return true;
            Toast($"금화가 부족합니다 (필요 {cost:N0})");
            return false;
        }

        // ---------------- 액션 ----------------

        private void UseExp(MaterialType mt)
        {
            if (_selected == null || roster == null) return;
            var o = roster.GetOwned(_selected);
            if (o == null) return;
            if (o.level >= Leveling.LevelCap(o.ascension)) { Toast("레벨 상한 — 돌파가 필요합니다"); return; }
            if (Mat(mt) < 1) { Toast($"{Materials.Name(mt)} 부족"); return; }
            int gold = Economy.LevelUpGold(mt);
            if (Gold < gold) { Toast($"금화가 부족합니다 (필요 {gold:N0})"); return; }
            if (inventory != null && !inventory.Spend(mt, 1)) { Toast($"{Materials.Name(mt)} 부족"); return; }
            PayGold(gold);
            roster.AddExp(_selected, Materials.ExpValue(mt));
        }

        private void Ascend()
        {
            if (_selected == null || roster == null) return;
            if (!roster.CanAscend(_selected)) { Toast("상한까지 레벨을 올리세요"); return; }
            int asc = roster.AscensionOf(_selected);
            var am = Economy.AscensionMaterial(_selected.element, asc);
            int cost = Economy.AscensionMaterialCount(asc);
            int gold = Economy.AscensionGold(asc);
            if (Mat(am) < cost)
            {
                Toast($"{Materials.Name(am)} 부족 ({Mat(am)}/{cost}) — 돌파 던전 {Materials.TierName(Materials.TierOf(am))}에서 획득");
                return;
            }
            if (Gold < gold) { Toast($"금화가 부족합니다 (필요 {gold:N0})"); return; }
            if (inventory != null && !inventory.Spend(am, cost)) return;
            PayGold(gold);
            roster.Ascend(_selected);
            Toast($"돌파 완료! 레벨 상한 {Leveling.LevelCap(asc + 1)}");
        }

        private void SkillUp()
        {
            if (_selected == null || roster == null) return;
            if (!roster.CanUpgradeSkill(_selected)) { Toast("이미 최대 스킬 레벨"); return; }
            int lv = roster.SkillLevelOf(_selected);
            int cost = Leveling.SkillUpgradeCost(lv);
            int gold = Economy.SkillUpGold(lv);
            if (Mat(MaterialType.SkillBook) < cost) { Toast("스킬 교본 부족"); return; }
            if (Gold < gold) { Toast($"금화가 부족합니다 (필요 {gold:N0})"); return; }
            if (inventory != null && !inventory.Spend(MaterialType.SkillBook, cost)) return;
            PayGold(gold);
            roster.UpgradeSkill(_selected);
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
                string unlocked = "";
                foreach (var p in Transcendence.Passives(_selected.role))
                    if (p.level == t) unlocked = $" — 패시브 해금: {p.name}";
                Toast($"초월 +{t} 달성!{unlocked}");
            }
        }

        private (int hp, int atk, int def, int spd) StatsAt(CharacterData c, int level, int skillLevel, int star, int transcend)
        {
            System.Func<CharacterData, BattleUnitData> lookup = null;
            if (database != null) lookup = database.GetUnit;
            var u = RuntimeUnits.FromCharacter(c, level, lookup, skillLevel, star, transcend);
            var s = u.baseStats;
            var result = (s.MaxHPAt(level), s.AtkAt(level), s.DefAt(level), s.spd);

            // 미리보기용으로 새로 만든 유닛(생성/초월 복제본)은 바로 정리 — 저작 에셋은 건드리지 않음
            if (lookup == null || lookup(c) != u)
            {
                if (u.skills != null) foreach (var sk in u.skills) if (sk != null) Destroy(sk);
                Destroy(u);
            }
            return result;
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
            Invoke(nameof(ClearToast), 2.2f);
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
            float gridW = 4 * _cell.x + 3 * 12;
            _grid.anchoredPosition = new Vector2(-420, -70);
            var gl = _grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = _cell; gl.spacing = new Vector2(12, 12);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 4;
            gl.childAlignment = TextAnchor.UpperCenter;
            _grid.sizeDelta = new Vector2(gridW, 520);

            // 정렬/필터 바(그리드 위)
            _filterBar = RosterFilterBar.Build(_root.transform, new Vector2(-420, -116), gridW, font);
            _filterBar.OnChanged += OnFilterChanged;

            BuildDetail();

            // 테스트 재료 충전
            var charge = UIFactory.AddButton("Charge", _root.transform, "재료·금화 충전(테스트)", 18, font, new Color(1, 1, 1, 0.14f), Color.white);
            var crt = (RectTransform)charge.transform;
            UIFactory.Anchor(crt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            crt.sizeDelta = new Vector2(240, 48); crt.anchoredPosition = new Vector2(30, 30);
            charge.onClick.AddListener(() =>
            {
                if (inventory != null) inventory.GrantTest();
                if (wallet != null) wallet.AddGold(100000);
            });

            // 테스트: 선택 캐릭터 중복 +6 (합성/초월 확인용)
            var dup = UIFactory.AddButton("DupTest", _root.transform, "선택 캐릭 중복 +6(테스트)", 18, font, new Color(1, 1, 1, 0.14f), Color.white);
            var drt = (RectTransform)dup.transform;
            UIFactory.Anchor(drt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0));
            drt.sizeDelta = new Vector2(280, 48); drt.anchoredPosition = new Vector2(290, 30);
            dup.onClick.AddListener(() => { if (roster != null && _selected != null) roster.GrantTestDuplicates(_selected, 6); });

            // 홈 버튼(좌상단) — 내비게이터가 연결할 때만 표시
            _backBtn = UIFactory.AddButton("BackBtn", _root.transform, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            var bkrt = (RectTransform)_backBtn.transform;
            UIFactory.Anchor(bkrt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            bkrt.sizeDelta = new Vector2(150, 44); bkrt.anchoredPosition = new Vector2(30, -30);
            _backBtn.onClick.AddListener(() => _onBack?.Invoke());
            _backBtn.gameObject.SetActive(false);

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
            prt.sizeDelta = new Vector2(560, 880); prt.anchoredPosition = new Vector2(440, -20);

            _detPortrait = TopImg(panel.transform, "Portrait", new Vector2(150, 186), -12);
            _detPortrait.color = new Color(1, 1, 1, 0.1f); _detPortrait.preserveAspect = true;

            _detStars = StarRow.Create(panel.transform, 36, TextAnchor.MiddleCenter, font);
            var dsrt = _detStars.Rect;
            UIFactory.Anchor(dsrt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            dsrt.sizeDelta = new Vector2(520, 36); dsrt.anchoredPosition = new Vector2(0, -200);
            _detName = TopText(panel.transform, "Name", 26, FontStyles.Bold, new Vector2(520, 36), -236);
            _detLevel = TopText(panel.transform, "Level", 22, FontStyles.Bold, new Vector2(520, 30), -272);
            _detLevel.color = new Color(1f, 0.85f, 0.4f);
            _detInfo = TopText(panel.transform, "Info", 17, FontStyles.Normal, new Vector2(520, 26), -302);
            _detInfo.color = new Color(0.85f, 0.88f, 0.95f);
            _detStats = TopText(panel.transform, "Stats", 21, FontStyles.Normal, new Vector2(520, 64), -330);
            _detStats.lineSpacing = 6f;

            // EXP 바
            var expBar = UIFactory.NewRect("ExpBar", panel.transform);
            UIFactory.Anchor(expBar, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            expBar.sizeDelta = new Vector2(480, 20); expBar.anchoredPosition = new Vector2(0, -400);
            var expBg = UIFactory.AddImage("BG", expBar, new Color(0, 0, 0, 0.5f));
            UIFactory.Stretch(expBg.rectTransform);
            _expFill = UIFactory.AddImage("Fill", expBar, new Color(0.4f, 0.85f, 1f, 1f));
            UIFactory.Stretch(_expFill.rectTransform);
            _detExp = UIFactory.AddText("ExpText", expBar.transform, "", 13, font, Color.white, TextAlignmentOptions.Center);
            UIFactory.Stretch(_detExp.rectTransform);

            _detSkill = TopText(panel.transform, "Skill", 19, FontStyles.Normal, new Vector2(520, 26), -428);
            _detSkill.color = new Color(0.8f, 0.85f, 1f);

            // 초월 고유 패시브
            _detPassives = TopText(panel.transform, "Passives", 16, FontStyles.Normal, new Vector2(500, 118), -460);
            _detPassives.alignment = TextAlignmentOptions.TopLeft;
            _detPassives.lineSpacing = 4f;
            _detPassives.enableAutoSizing = true;
            _detPassives.fontSizeMin = 12; _detPassives.fontSizeMax = 16;
            _detPassives.color = new Color(0.92f, 0.93f, 0.97f);

            // 레벨업(물약/결정)
            _potionBtn = DetailButton(panel.transform, new Color(0.35f, 0.55f, 0.35f, 0.95f), new Vector2(238, 52), new Vector2(-126, -590), UseExpPotion, out _potionLbl);
            _crystalBtn = DetailButton(panel.transform, new Color(0.3f, 0.5f, 0.6f, 0.95f), new Vector2(238, 52), new Vector2(126, -590), UseExpCrystal, out _crystalLbl);
            // 돌파
            _ascendBtn = DetailButton(panel.transform, new Color(0.8f, 0.6f, 0.2f, 0.95f), new Vector2(492, 52), new Vector2(0, -650), Ascend, out _ascendLbl);
            // 스킬업
            _skillBtn = DetailButton(panel.transform, new Color(0.5f, 0.35f, 0.75f, 0.95f), new Vector2(492, 52), new Vector2(0, -710), SkillUp, out _skillLbl);
            // 합성 / 초월 (중복 캐릭터 소모)
            _mergeBtn = DetailButton(panel.transform, new Color(0.85f, 0.35f, 0.55f, 0.95f), new Vector2(492, 56), new Vector2(0, -774), MergeOrTranscend, out _mergeLbl);

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

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
