using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;
using SubcultureRPG.Battle;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 캐릭터 장비 패널(육성 화면에서 열림).
    /// 왼쪽: 6부위 장착 칸 + 장비 능력치·세트 효과 / 가운데: 선택 부위 보유 장비 목록 / 오른쪽: 장비 상세·장착·강화·잠금·분해.
    /// </summary>
    public class EquipmentPanel : MonoBehaviour
    {
        private PlayerEquipment _equip;
        private PlayerInventory _inventory;
        private PlayerWallet _wallet;
        private TMP_FontAsset _font;
        private System.Func<string, string> _nameOf;   // 캐릭터 키 → 이름

        private GameObject _root;
        private TextMeshProUGUI _title, _summary, _listHeader, _detSet, _detLevel, _detMain, _detSubs, _detSetBonus, _detCost, _detNote;
        private TextMeshProUGUI _equipLbl, _lockLbl, _plusLbl, _mileLbl, _dismantleLbl, _transLbl;
        private Button _equipBtn, _lockBtn, _plusBtn, _mileBtn, _dismantleBtn, _transBtn;
        private RectTransform _listContent;
        private GameObject _detailGroup, _emptyDetail;

        private class SlotCard { public Image hl, frame; public TextMeshProUGUI set, main, level; }
        private readonly Dictionary<EquipSlot, SlotCard> _slotCards = new();
        private readonly Dictionary<EquipmentItem, Image> _listHL = new();

        private CharacterData _char;
        private EquipSlot _slot = EquipSlot.Head;
        private EquipmentItem _item;
        private bool _confirmDismantle;

        public bool IsOpen => _root != null && _root.activeSelf;
        public event System.Action OnClosed;

        /// <summary>parent(화면 루트) 아래에 패널 생성.</summary>
        public static EquipmentPanel Create(Transform parent, TMP_FontAsset font, PlayerEquipment equip,
            PlayerInventory inventory, PlayerWallet wallet, System.Func<string, string> nameOf)
        {
            var go = new GameObject("EquipmentPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<EquipmentPanel>();
            p._font = font; p._equip = equip; p._inventory = inventory; p._wallet = wallet; p._nameOf = nameOf;
            UIFactory.Stretch((RectTransform)go.transform);
            p.Build();
            if (equip != null) equip.OnChanged += p.Refresh;
            if (inventory != null) inventory.OnChanged += p.Refresh;
            if (wallet != null) wallet.OnChanged += p.Refresh;
            return p;
        }

        private void OnDestroy()
        {
            if (_equip != null) _equip.OnChanged -= Refresh;
            if (_inventory != null) _inventory.OnChanged -= Refresh;
            if (_wallet != null) _wallet.OnChanged -= Refresh;
        }

        public void Open(CharacterData c)
        {
            if (c == null || _equip == null) return;
            _char = c;
            _item = _equip.EquippedIn(c, _slot);
            _confirmDismantle = false;
            _detNote.text = "";
            _root.SetActive(true);
            transform.SetAsLastSibling();
            Refresh();
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
            _char = null;
            OnClosed?.Invoke();
        }

        // ---------------- 갱신 ----------------

        private string Key => PlayerEquipment.Key(_char);

        private void Refresh()
        {
            if (!IsOpen || _char == null) return;
            if (_item != null && !ContainsItem(_item)) _item = null;   // 분해된 장비

            _title.text = $"장비  ·  {_char.displayName}";

            // 장착 칸
            foreach (var slot in EquipStats.AllSlots)
            {
                var card = _slotCards[slot];
                var it = _equip.EquippedIn(_char, slot);
                card.hl.color = new Color(0.3f, 0.85f, 1f, slot == _slot ? 1f : 0f);
                if (it == null)
                {
                    card.frame.color = new Color(1, 1, 1, 0.12f);
                    card.set.text = "<color=#6B7280>비어 있음</color>";
                    card.main.text = "";
                    card.level.text = "";
                }
                else
                {
                    var col = EquipSets.Get(it.set)?.color ?? Color.white;
                    card.frame.color = new Color(col.r, col.g, col.b, 0.9f);
                    card.set.text = $"{EquipSets.Rich(it.set)}  <size=75%>{EquipStats.Stars(it.Rarity, it.transcend)}</size>";
                    card.main.text = EquipStats.Format(it.main, it.MainValue);
                    card.level.text = $"+{it.level}";
                }
            }

            // 장비 능력치 + 세트 효과
            var equipped = _equip.EquippedOn(_char);
            var bonus = EquipRules.BonusOf(equipped);
            var sb = new StringBuilder();
            sb.Append("<color=#FFD36B><b>장비 능력치</b></color>\n");
            var lines = EquipStats.Describe(bonus);
            if (lines.Count == 0) sb.Append("<color=#6B7280>장착한 장비가 없습니다</color>\n");
            for (int i = 0; i < lines.Count; i += 2)
                sb.Append(lines[i]).Append(i + 1 < lines.Count ? "      " + lines[i + 1] : "").Append('\n');
            sb.Append("\n<color=#FFD36B><b>세트 효과</b></color>\n");
            bool anySet = false;
            foreach (var kv in EquipRules.SetCounts(equipped))
            {
                var set = EquipSets.Get(kv.Key);
                if (set == null) continue;
                anySet = true;
                sb.Append($"{EquipSets.Rich(kv.Key)} ({kv.Value})  ");
                sb.Append(kv.Value >= 2 ? $"2세트 {set.desc2}" : $"<color=#6B7280>2세트 {set.desc2}</color>");
                sb.Append(kv.Value >= 4 ? $"  ·  4세트 {set.desc4}" : $"  <color=#6B7280>·  4세트 {set.desc4}</color>");
                sb.Append('\n');
            }
            if (!anySet) sb.Append("<color=#6B7280>같은 세트 2개 / 4개 장착 시 발동</color>");
            _summary.text = sb.ToString();

            RebuildList();
            RefreshDetail();
        }

        private bool ContainsItem(EquipmentItem it)
        {
            foreach (var x in _equip.Items) if (x == it) return true;
            return false;
        }

        private void RebuildList()
        {
            for (int i = _listContent.childCount - 1; i >= 0; i--)
            {
                var ch = _listContent.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
            _listHL.Clear();

            var list = _equip.OfSlot(_slot);
            string key = Key;
            // 이 캐릭터 장착 → 미장착 → 다른 캐릭터 장착, 그 안에서 강화 높은 순
            list.Sort((a, b) =>
            {
                int ra = a.owner == key ? 0 : (a.IsEquipped ? 2 : 1);
                int rb = b.owner == key ? 0 : (b.IsEquipped ? 2 : 1);
                if (ra != rb) return ra.CompareTo(rb);
                if (a.Rarity != b.Rarity) return b.Rarity.CompareTo(a.Rarity);
                if (a.level != b.level) return b.level.CompareTo(a.level);
                return string.CompareOrdinal(a.set, b.set);
            });
            _listHeader.text = $"{EquipStats.SlotName(_slot)} 장비  <size=80%><color=#9AA3B5>{list.Count}개</color></size>";
            if (_item == null && list.Count > 0) _item = list[0];

            foreach (var it in list) BuildListCell(it, key);
        }

        private void BuildListCell(EquipmentItem it, string key)
        {
            var cell = UIFactory.NewRect("Item", _listContent);
            var hl = UIFactory.AddImage("HL", cell, new Color(0.3f, 0.85f, 1f, it == _item ? 1f : 0f));
            UIFactory.Stretch(hl.rectTransform, -3, -3, -3, -3);
            _listHL[it] = hl;

            var col = EquipSets.Get(it.set)?.color ?? Color.white;
            var frame = UIFactory.AddImage("Frame", cell, new Color(col.r, col.g, col.b, it.owner == key ? 0.95f : 0.55f), null, raycast: true);
            UIFactory.Stretch(frame.rectTransform);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            var item = it;
            btn.onClick.AddListener(() => { _item = item; _confirmDismantle = false; _detNote.text = ""; RefreshHighlights(); RefreshDetail(); });

            var inner = UIFactory.AddImage("Inner", frame.transform, new Color(0.08f, 0.09f, 0.12f, 0.95f));
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);

            Text(inner.transform, $"{EquipSets.Rich(it.set)} <size=70%>{EquipStats.Stars(it.Rarity, it.transcend)}</size>", 16, FontStyles.Bold, TextAlignmentOptions.TopLeft, 10, -8, 190, 24);
            Text(inner.transform, $"+{it.level}", 20, FontStyles.Bold, TextAlignmentOptions.TopRight, -10, -6, 70, 26, right: true);
            Text(inner.transform, EquipStats.Format(it.main, it.MainValue), 15, FontStyles.Normal, TextAlignmentOptions.TopLeft, 10, -36, 230, 22);
            string sub = $"<color=#9AA3B5>보조 {it.subs.Count}줄</color>";
            if (it.locked) sub += "  <color=#FFD36B>잠금</color>";
            if (it.IsEquipped)
                sub += it.owner == key ? "  <color=#7FE3FF>장착 중</color>" : $"  <color=#FF9A7A>{OwnerName(it.owner)} 장착</color>";
            Text(inner.transform, sub, 13, FontStyles.Normal, TextAlignmentOptions.TopLeft, 10, -64, 240, 40);
        }

        private string OwnerName(string key) => _nameOf != null ? (_nameOf(key) ?? "다른 캐릭터") : "다른 캐릭터";

        private void RefreshHighlights()
        {
            foreach (var kv in _listHL) kv.Value.color = new Color(0.3f, 0.85f, 1f, kv.Key == _item ? 1f : 0f);
        }

        private void RefreshDetail()
        {
            bool has = _item != null;
            _detailGroup.SetActive(has);
            _emptyDetail.SetActive(!has);
            if (!has) return;

            var it = _item;
            var set = EquipSets.Get(it.set);
            _detSet.text = $"{EquipSets.Rich(it.set)}  <size=75%>{EquipStats.Stars(it.Rarity, it.transcend)}</size>";
            _detLevel.text = $"{EquipStats.GradeTag(it.Rarity)}  ·  {EquipStats.SlotName(it.slot)}  ·  <color=#FFD36B>+{it.level}</color> / {it.MaxLevel}" +
                             $"  <size=80%><color=#9AA3B5>태생 {it.initialSubs}옵션</color></size>";
            _detMain.text = $"{EquipStats.Name(it.main).Replace("%", "")}  <color=#FFFFFF>{EquipStats.ValueText(it.main, it.MainValue)}</color>";

            var sb = new StringBuilder();
            foreach (var s in it.subs)
                sb.Append($"·  {EquipStats.Name(s.stat).Replace("%", "")}  <b>{EquipStats.ValueText(s.stat, it.SubValue(s))}</b>  ")
                  .Append($"<color=#7FE3FF>{new string('●', Mathf.Clamp(s.rolls, 1, 6))}</color>\n");
            if (it.subs.Count < 4) sb.Append("<color=#6B7280>·  (+4 강화 시 새 보조 옵션 추가)</color>\n");
            if (it.transcend > 0) sb.Append($"<color=#C3BEFF>초월 +{it.transcend}: 주옵션 +{6 * it.transcend}% · 보조 옵션 +{4 * it.transcend}%</color>\n");
            int next = (it.level / EquipStats.RollEvery + 1) * EquipStats.RollEvery;
            if (next <= it.MaxLevel)
                sb.Append($"<size=85%><color=#9AA3B5>다음 보조 옵션 강화: +{next}</color></size>");
            else if (!it.IsMaxed)
                sb.Append($"<size=85%><color=#9AA3B5>남은 보조 옵션 강화 없음 (최대 +{it.MaxLevel})</color></size>");
            _detSubs.text = sb.ToString();

            int count = 0;
            foreach (var e in _equip.EquippedOn(_char)) if (e.set == it.set) count++;
            _detSetBonus.text = set == null ? "" :
                $"<color=#FFD36B>세트 효과</color>  <size=85%><color=#9AA3B5>(이 캐릭터 {count}개 장착)</color></size>\n" +
                (count >= 2 ? $"2세트  {set.desc2}" : $"<color=#6B7280>2세트  {set.desc2}</color>") + "\n" +
                (count >= 4 ? $"4세트  {set.desc4}" : $"<color=#6B7280>4세트  {set.desc4}</color>");

            // 비용
            if (it.IsMaxed)
                _detCost.text = $"<color=#FFD36B>최대 강화 (+{it.MaxLevel})</color>";
            else
            {
                int g = EquipRules.EnhanceGold(it.level), o = EquipRules.EnhanceOre(it.level);
                var (mg, mo) = _equip.CostToMilestone(it);
                _detCost.text = $"+1 강화  {Cost(g, o)}\n<size=85%>다음 4강 단위까지  {Cost(mg, mo)}</size>\n" +
                                $"<size=80%><color=#9AA3B5>보유  금화 {_equip.Gold:N0} · 강화석 {_equip.Ore:N0}</color></size>";
            }

            // 버튼
            string key = Key;
            bool mine = it.owner == key;
            _equipLbl.text = mine ? "장착 해제" : (it.IsEquipped ? $"장착 <size=70%>({OwnerName(it.owner)}와 교체)</size>" : "장착");
            _lockLbl.text = it.locked ? "잠금 해제" : "잠금";
            bool canPlus = _equip.CanEnhance(it);
            _plusBtn.interactable = canPlus;
            _mileBtn.interactable = canPlus;
            int target = Mathf.Min(it.MaxLevel, (it.level / EquipStats.RollEvery + 1) * EquipStats.RollEvery);
            _plusLbl.text = "강화 +1";
            _mileLbl.text = it.IsMaxed ? "최대" : $"+{target}까지 강화";
            // 초월
            int fodder = _equip.TranscendFodder(it).Count;
            if (it.transcend >= EquipStats.MaxTranscend) { _transLbl.text = "초월 완료"; _transBtn.interactable = false; }
            else if (!it.IsMaxed) { _transLbl.text = $"초월 <size=70%>(+{it.MaxLevel} 강화 후)</size>"; _transBtn.interactable = false; }
            else
            {
                int g = EquipStats.TranscendGold(it.Rarity, it.transcend);
                _transLbl.text = $"초월 +{it.transcend + 1} <size=65%>(재료 {fodder}개 · {(_equip.Gold >= g ? "" : "<color=#FF8A8A>")}금화 {g:N0}{(_equip.Gold >= g ? "" : "</color>")})</size>";
                _transBtn.interactable = _equip.CanTranscend(it);
            }

            bool canDis = !it.locked && !it.IsEquipped;
            _dismantleBtn.interactable = canDis;
            _dismantleLbl.text = !canDis ? (it.locked ? "분해 불가 (잠금)" : "분해 불가 (장착 중)")
                : _confirmDismantle ? $"<color=#FFD36B>한 번 더 누르면 분해</color> (강화석 +{EquipRules.DismantleOre(it)})"
                : $"분해  <size=80%>(강화석 +{EquipRules.DismantleOre(it)})</size>";
        }

        private string Cost(int gold, int ore)
        {
            string g = _equip.Gold >= gold ? $"<color=#FFD36B>금화 {gold:N0}</color>" : $"<color=#FF8A8A>금화 {gold:N0}</color>";
            string o = _equip.Ore >= ore ? $"강화석 {ore}" : $"<color=#FF8A8A>강화석 {ore}</color>";
            return $"{g} · {o}";
        }

        // ---------------- 동작 ----------------

        private void OnEquip()
        {
            if (_item == null || _char == null) return;
            if (_item.owner == Key) _equip.Unequip(_item);
            else _equip.Equip(_item, _char);
        }

        private void OnLock()
        {
            if (_item != null) _equip.ToggleLock(_item);
        }

        private void OnEnhance(bool milestone)
        {
            if (_item == null) return;
            int before = _item.level;
            var (done, notes) = _equip.Enhance(_item, milestone);
            if (done == 0) { _detNote.text = "<color=#FF8A8A>금화 또는 장비 강화석이 부족합니다</color>"; return; }
            var sb = new StringBuilder($"<color=#7FE3FF>강화 +{before} → +{_item.level}</color>");
            foreach (var n in notes) sb.Append('\n').Append(n);
            _detNote.text = sb.ToString();
        }

        private void OnTranscend()
        {
            if (_item == null) return;
            if (!_equip.Transcend(_item)) { _detNote.text = "<color=#FF8A8A>초월 재료(같은 세트·부위·성급, 미장착·미잠금) 또는 금화가 부족합니다</color>"; return; }
            _detNote.text = $"<color=#C3BEFF>장비 초월 +{_item.transcend}!</color>  주옵션 +{6 * _item.transcend}% · 보조 옵션 +{4 * _item.transcend}%";
        }

        private void OnDismantle()
        {
            if (_item == null) return;
            if (!_confirmDismantle) { _confirmDismantle = true; RefreshDetail(); return; }
            int ore = _equip.Dismantle(_item);
            _confirmDismantle = false;
            _item = null;
            _detNote.text = ore > 0 ? $"<color=#7FE3D6>분해 완료 · 장비 강화석 +{ore}</color>" : "";
            Refresh();
        }

        private void SelectSlot(EquipSlot slot)
        {
            _slot = slot;
            _item = _equip.EquippedIn(_char, slot);
            _confirmDismantle = false;
            _detNote.text = "";
            Refresh();
        }

        // ---------------- UI 구성 ----------------

        private void Build()
        {
            _root = UIFactory.AddImage("Dim", transform, new Color(0, 0, 0, 0.72f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);

            var panel = UIFactory.AddImage("Panel", _root.transform, new Color(0.09f, 0.10f, 0.13f, 0.99f), null, raycast: true);
            var prt = panel.rectTransform;
            UIFactory.Anchor(prt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            prt.sizeDelta = new Vector2(1780, 980); prt.anchoredPosition = Vector2.zero;
            var p = panel.transform;

            _title = Text(p, "", 32, FontStyles.Bold, TextAlignmentOptions.Center, 0, -18, 1780, 46);
            var close = UIFactory.AddButton("Close", p, "×", 32, _font, new Color(1, 1, 1, 0.1f), Color.white);
            var crt = (RectTransform)close.transform;
            UIFactory.Anchor(crt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            crt.sizeDelta = new Vector2(56, 48); crt.anchoredPosition = new Vector2(-14, -14);
            close.onClick.AddListener(Close);

            // 왼쪽: 장착 칸 2 × 3
            Text(p, "<color=#FFD36B><b>장착</b></color>  <size=80%><color=#9AA3B5>칸을 눌러 부위 선택</color></size>", 20, FontStyles.Normal, TextAlignmentOptions.TopLeft, 30, -74, 560, 30);
            for (int i = 0; i < EquipStats.AllSlots.Length; i++)
            {
                var slot = EquipStats.AllSlots[i];
                float x = 30 + (i % 2) * 290, y = -112 - (i / 2) * 156;
                _slotCards[slot] = BuildSlotCard(p, slot, x, y);
            }
            _summary = Text(p, "", 17, FontStyles.Normal, TextAlignmentOptions.TopLeft, 30, -590, 560, 370);
            _summary.lineSpacing = 4f;
            _summary.enableAutoSizing = true; _summary.fontSizeMin = 12; _summary.fontSizeMax = 17;

            // 가운데: 보유 목록
            _listHeader = Text(p, "", 22, FontStyles.Bold, TextAlignmentOptions.TopLeft, 630, -74, 540, 32);
            var view = UIFactory.NewRect("View", p);
            UIFactory.Anchor(view, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            view.sizeDelta = new Vector2(550, 846); view.anchoredPosition = new Vector2(630, -112);
            var viewBg = view.gameObject.AddComponent<Image>();
            viewBg.color = new Color(0, 0, 0, 0.25f);
            view.gameObject.AddComponent<RectMask2D>();
            _listContent = UIFactory.NewRect("Content", view);
            UIFactory.Anchor(_listContent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            _listContent.sizeDelta = Vector2.zero; _listContent.anchoredPosition = Vector2.zero;
            var grid = _listContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(262, 108); grid.spacing = new Vector2(10, 10);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
            var fitter = _listContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = _listContent; scroll.viewport = view;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            // 오른쪽: 상세
            _emptyDetail = Text(p, "<color=#6B7280>이 부위의 장비가 없습니다\n장비 던전에서 획득하세요</color>", 20, FontStyles.Normal,
                TextAlignmentOptions.Center, 1210, -300, 540, 80).gameObject;
            var detail = UIFactory.NewRect("Detail", p);
            UIFactory.Stretch(detail);
            _detailGroup = detail.gameObject;
            var d = detail.transform;
            _detSet = Text(d, "", 28, FontStyles.Bold, TextAlignmentOptions.TopLeft, 1210, -74, 540, 38);
            _detLevel = Text(d, "", 19, FontStyles.Normal, TextAlignmentOptions.TopLeft, 1210, -116, 540, 28);
            _detMain = Text(d, "", 28, FontStyles.Bold, TextAlignmentOptions.TopLeft, 1210, -154, 540, 40);
            _detMain.color = new Color(1f, 0.85f, 0.45f);
            _detSubs = Text(d, "", 20, FontStyles.Normal, TextAlignmentOptions.TopLeft, 1210, -204, 540, 190);
            _detSubs.lineSpacing = 6f;
            _detSetBonus = Text(d, "", 17, FontStyles.Normal, TextAlignmentOptions.TopLeft, 1210, -402, 540, 96);
            _detSetBonus.lineSpacing = 4f;
            _detCost = Text(d, "", 18, FontStyles.Normal, TextAlignmentOptions.TopLeft, 1210, -506, 540, 86);

            _equipBtn = Btn(d, 1210, -600, 265, 60, new Color(0.25f, 0.55f, 0.75f, 0.95f), OnEquip, out _equipLbl);
            _lockBtn = Btn(d, 1485, -600, 265, 60, new Color(1, 1, 1, 0.14f), OnLock, out _lockLbl);
            _plusBtn = Btn(d, 1210, -670, 265, 60, new Color(0.3f, 0.62f, 0.4f, 0.95f), () => OnEnhance(false), out _plusLbl);
            _mileBtn = Btn(d, 1485, -670, 265, 60, new Color(0.25f, 0.5f, 0.35f, 0.95f), () => OnEnhance(true), out _mileLbl);
            _transBtn = Btn(d, 1210, -740, 265, 56, new Color(0.55f, 0.4f, 0.85f, 0.95f), OnTranscend, out _transLbl);
            _dismantleBtn = Btn(d, 1485, -740, 265, 56, new Color(0.6f, 0.25f, 0.25f, 0.9f), OnDismantle, out _dismantleLbl);
            foreach (var l in new[] { _transLbl, _dismantleLbl, _equipLbl }) { l.enableAutoSizing = true; l.fontSizeMin = 12; l.fontSizeMax = 21; }
            _detNote = Text(p, "", 18, FontStyles.Normal, TextAlignmentOptions.TopLeft, 1210, -810, 540, 150);
            _detNote.enableAutoSizing = true; _detNote.fontSizeMin = 12; _detNote.fontSizeMax = 18;

            _root.SetActive(false);
        }

        private SlotCard BuildSlotCard(Transform parent, EquipSlot slot, float x, float y)
        {
            var holder = UIFactory.NewRect("Slot", parent);
            UIFactory.Anchor(holder, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            holder.sizeDelta = new Vector2(270, 142); holder.anchoredPosition = new Vector2(x, y);

            var card = new SlotCard();
            card.hl = UIFactory.AddImage("HL", holder, new Color(0.3f, 0.85f, 1f, 0f));
            UIFactory.Stretch(card.hl.rectTransform, -4, -4, -4, -4);
            card.frame = UIFactory.AddImage("Frame", holder, new Color(1, 1, 1, 0.12f), null, raycast: true);
            UIFactory.Stretch(card.frame.rectTransform);
            var btn = card.frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = card.frame;
            btn.onClick.AddListener(() => SelectSlot(slot));
            var inner = UIFactory.AddImage("Inner", card.frame.transform, new Color(0.08f, 0.09f, 0.12f, 0.95f));
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);

            Text(inner.transform, EquipStats.SlotName(slot), 15, FontStyles.Bold, TextAlignmentOptions.TopLeft, 12, -8, 120, 22).color = new Color(0.7f, 0.75f, 0.85f);
            card.level = Text(inner.transform, "", 22, FontStyles.Bold, TextAlignmentOptions.TopRight, -12, -6, 80, 28, right: true);
            card.level.color = new Color(1f, 0.85f, 0.45f);
            card.set = Text(inner.transform, "", 19, FontStyles.Bold, TextAlignmentOptions.TopLeft, 12, -44, 246, 28);
            card.main = Text(inner.transform, "", 17, FontStyles.Normal, TextAlignmentOptions.TopLeft, 12, -80, 246, 26);
            return card;
        }

        private TextMeshProUGUI Text(Transform parent, string text, float size, FontStyles style, TextAlignmentOptions align,
            float x, float y, float w, float h, bool right = false)
        {
            var t = UIFactory.AddText("T", parent, text, size, _font, Color.white, align, style);
            var rt = t.rectTransform;
            var a = right ? new Vector2(1, 1) : new Vector2(0, 1);
            UIFactory.Anchor(rt, a, a, a);
            rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, y);
            t.richText = true;
            return t;
        }

        private Button Btn(Transform parent, float x, float y, float w, float h, Color bg, UnityEngine.Events.UnityAction onClick, out TextMeshProUGUI label)
        {
            var b = UIFactory.AddButton("Btn", parent, "", 21, _font, bg, Color.white);
            var rt = (RectTransform)b.transform;
            UIFactory.Anchor(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, y);
            b.onClick.AddListener(onClick);
            label = b.GetComponentInChildren<TextMeshProUGUI>();
            label.richText = true;
            return b;
        }
    }
}
