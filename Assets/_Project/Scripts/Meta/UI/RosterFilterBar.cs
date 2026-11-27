using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>보유 목록 정렬 기준.</summary>
    public enum RosterSort
    {
        Rarity,
        Level,
        Name,
        Power,   // 전투력(장비 · 호감도 포함)
    }

    /// <summary>필터·정렬을 거친 보유 캐릭터 1명.</summary>
    public struct RosterEntry
    {
        public CharacterData character;
        public OwnedCharacter owned;
    }

    /// <summary>보유 목록 정렬·필터 조건(순수 로직, UI와 분리).</summary>
    public class RosterFilter
    {
        public RosterSort sort = RosterSort.Rarity;
        private readonly Dictionary<CharacterData, int> _power = new();
        private int PowerOf(RosterEntry e) => _power.TryGetValue(e.character, out var p) ? p : 0;
        public bool descending = true;
        public readonly HashSet<Element> elements = new();  // 비어 있으면 전체
        public readonly HashSet<Role> roles = new();        // 비어 있으면 전체

        public bool Matches(CharacterData c) =>
            c != null
            && (elements.Count == 0 || elements.Contains(c.element))
            && (roles.Count == 0 || roles.Contains(c.role));

        /// <summary>로스터를 필터·정렬한 결과.</summary>
        public List<RosterEntry> Apply(PlayerRoster roster, GameDatabase db)
        {
            var list = new List<RosterEntry>();
            if (roster == null || db == null) return list;
            foreach (var o in roster.Owned)
            {
                var c = db.GetCharacter(o.id);
                if (Matches(c)) list.Add(new RosterEntry { character = c, owned = o });
            }
            // 전투력 정렬: 한 번씩만 계산해 두고 비교
            _power.Clear();
            if (sort == RosterSort.Power)
            {
                var eq = UnityEngine.Object.FindAnyObjectByType<PlayerEquipment>();
                foreach (var e in list) _power[e.character] = CombatPower.Of(e.character, roster, eq);
            }
            list.Sort(Compare);
            return list;
        }

        public void Reset()
        {
            sort = RosterSort.Rarity;
            descending = true;
            elements.Clear();
            roles.Clear();
        }

        /// <summary>정렬용 등급(현재 성급 × 10 + 초월). 합성·초월 반영.</summary>
        public static int Grade(RosterEntry e) =>
            Transcendence.GradeValue(Transcendence.CurrentStar(e.character.rarity, e.owned.star), e.owned.transcend);

        private int Compare(RosterEntry a, RosterEntry b)
        {
            int r = sort switch
            {
                RosterSort.Level => a.owned.level.CompareTo(b.owned.level),
                RosterSort.Power => PowerOf(a).CompareTo(PowerOf(b)),
                RosterSort.Name => string.Compare(a.character.displayName, b.character.displayName, StringComparison.CurrentCulture),
                _ => Grade(a).CompareTo(Grade(b)),
            };
            if (descending) r = -r;
            if (r != 0) return r;

            // 동률이면 등급↓ → 태생 성급↓ → 레벨↓ → 이름↑
            r = Grade(b).CompareTo(Grade(a));
            if (r != 0) return r;
            r = ((int)b.character.rarity).CompareTo((int)a.character.rarity);
            if (r != 0) return r;
            r = b.owned.level.CompareTo(a.owned.level);
            if (r != 0) return r;
            return string.Compare(a.character.displayName, b.character.displayName, StringComparison.CurrentCulture);
        }
    }

    /// <summary>
    /// 정렬/필터 바 UI(코드 구성). 육성·편성 화면이 함께 사용.
    /// 1줄: 정렬 기준·순서·초기화·표시 수 / 2줄: 속성 칩 / 3줄: 역할 칩 (칩은 다중 선택, 미선택=전체).
    /// </summary>
    public class RosterFilterBar
    {
        // 휴대폰에서도 누르기 · 읽기 쉽게(줄 높이 46 · 글자 23)
        public const float RowH = 46f;
        public const float Gap = 8f;
        private const float LabelW = 78f;
        private const float FontSize = 23f;
        /// <summary>바 전체 높이(3줄).</summary>
        public static float Height => RowH * 3 + Gap * 2;

        public readonly RosterFilter filter = new();

        /// <summary>조건이 바뀔 때(목록을 다시 그리세요).</summary>
        public event Action OnChanged;

        private TMP_FontAsset _font;
        private TextMeshProUGUI _sortLabel, _orderLabel, _countText;
        private readonly Dictionary<Element, Image> _elemChips = new();
        private readonly Dictionary<Role, Image> _roleChips = new();

        /// <summary>parent(전체 화면 루트) 상단 기준 위치에 바를 생성.</summary>
        public static RosterFilterBar Build(Transform parent, Vector2 topCenterPos, float width, TMP_FontAsset font)
        {
            var bar = new RosterFilterBar { _font = font };
            bar.BuildUI(parent, topCenterPos, width);
            bar.RefreshVisuals();
            return bar;
        }

        /// <summary>표시 개수 갱신.</summary>
        public void SetCount(int shown, int total)
        {
            if (_countText != null)
                _countText.text = shown == total ? $"{total}명" : $"{shown} / {total}명";
        }

        // ---------------- UI ----------------

        private void BuildUI(Transform parent, Vector2 pos, float width)
        {
            var root = UIFactory.NewRect("FilterBar", parent);
            UIFactory.Anchor(root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            root.sizeDelta = new Vector2(width, Height);
            root.anchoredPosition = pos;
            var v = root.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = Gap;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;

            // 1행: 정렬 / 순서 / 초기화 / 개수
            var row1 = Row(root);
            _sortLabel = MakeButton(row1, 210, new Color(1, 1, 1, 0.14f), CycleSort).GetComponentInChildren<TextMeshProUGUI>();
            _orderLabel = MakeButton(row1, 150, new Color(1, 1, 1, 0.14f), ToggleOrder).GetComponentInChildren<TextMeshProUGUI>();
            MakeButton(row1, 120, new Color(1, 1, 1, 0.08f), ResetAll).GetComponentInChildren<TextMeshProUGUI>().text = "초기화";
            _countText = UIFactory.AddText("Count", row1, "", FontSize, _font, new Color(0.85f, 0.88f, 0.95f), TextAlignmentOptions.Right, FontStyles.Bold);
            var cle = _countText.gameObject.AddComponent<LayoutElement>();
            cle.flexibleWidth = 1; cle.preferredHeight = RowH;

            // 2행: 속성 칩
            var row2 = Row(root);
            MakeLabel(row2, "속성");
            float ew = (width - LabelW - Gap * Traits.AllElements.Length) / Traits.AllElements.Length;
            foreach (var e in Traits.AllElements)
            {
                var ee = e;
                var b = MakeButton(row2, ew, new Color(1, 1, 1, 0.1f), () => Toggle(filter.elements, ee));
                var lbl = b.GetComponentInChildren<TextMeshProUGUI>();
                lbl.text = Traits.Name(e);
                var esp = GameArt.Load("Elements/" + e);
                if (esp != null)
                {
                    var ei = UIFactory.AddImage("El", b.transform, Color.white, esp);
                    ei.preserveAspect = true; ei.raycastTarget = false;
                    UIFactory.Anchor(ei.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                    ei.rectTransform.sizeDelta = new Vector2(RowH - 10, RowH - 10); ei.rectTransform.anchoredPosition = new Vector2(8, 0);
                    lbl.rectTransform.offsetMin = new Vector2(RowH - 4, lbl.rectTransform.offsetMin.y);
                }
                _elemChips[e] = (Image)b.targetGraphic;
            }

            // 3행: 역할 칩
            var row3 = Row(root);
            MakeLabel(row3, "역할");
            float rw = (width - LabelW - Gap * Traits.AllRoles.Length) / Traits.AllRoles.Length;
            foreach (var r in Traits.AllRoles)
            {
                var rr = r;
                var b = MakeButton(row3, rw, new Color(1, 1, 1, 0.1f), () => Toggle(filter.roles, rr));
                b.GetComponentInChildren<TextMeshProUGUI>().text = Traits.Name(r);
                _roleChips[r] = (Image)b.targetGraphic;
            }
        }

        private RectTransform Row(Transform parent)
        {
            var row = UIFactory.NewRect("Row", parent);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = RowH;
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = Gap;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return row;
        }

        private void MakeLabel(Transform row, string text)
        {
            var t = UIFactory.AddText("Label", row, text, FontSize, _font, new Color(0.75f, 0.78f, 0.85f), TextAlignmentOptions.Left, FontStyles.Bold);
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = LabelW - Gap; le.preferredHeight = RowH;
        }

        private Button MakeButton(Transform row, float width, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIFactory.AddButton("Btn", row, "", FontSize, _font, bg, Color.white);
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.preferredHeight = RowH;
            b.onClick.AddListener(onClick);
            return b;
        }

        // ---------------- 동작 ----------------

        private void CycleSort()
        {
            filter.sort = filter.sort switch
            {
                RosterSort.Rarity => RosterSort.Power,
                RosterSort.Power => RosterSort.Level,
                RosterSort.Level => RosterSort.Name,
                _ => RosterSort.Rarity,
            };
            filter.descending = filter.sort != RosterSort.Name; // 이름은 가나다순이 기본
            Changed();
        }

        private void ToggleOrder()
        {
            filter.descending = !filter.descending;
            Changed();
        }

        private void ResetAll()
        {
            filter.Reset();
            Changed();
        }

        private void Toggle<T>(HashSet<T> set, T value)
        {
            if (!set.Remove(value)) set.Add(value);
            Changed();
        }

        private void Changed()
        {
            RefreshVisuals();
            OnChanged?.Invoke();
        }

        private void RefreshVisuals()
        {
            if (_sortLabel != null)
                _sortLabel.text = "정렬: " + (filter.sort switch
                {
                    RosterSort.Level => "레벨",
                    RosterSort.Name => "이름",
                    RosterSort.Power => "전투력",
                    _ => "성급",
                });
            if (_orderLabel != null)
                _orderLabel.text = filter.descending ? "내림차순" : "오름차순";

            foreach (var kv in _elemChips)
                kv.Value.color = filter.elements.Contains(kv.Key)
                    ? Color.Lerp(Traits.ColorOf(kv.Key), Color.black, 0.35f)
                    : new Color(1, 1, 1, 0.1f);

            foreach (var kv in _roleChips)
                kv.Value.color = filter.roles.Contains(kv.Key)
                    ? new Color(0.78f, 0.58f, 0.22f, 0.95f)
                    : new Color(1, 1, 1, 0.1f);
        }
    }
}
