using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 장비 선택 제작 창: 세트 · 부위 · 주옵션 중 원하는 것만 골라(0 ~ 3개) 6★ 장비를 만듦. 고르지 않은 항목은 무작위.
    /// 고른 개수가 많을수록 장비 정수 · 금화가 더 듦(EquipRules.CraftEssence / CraftGold). 주옵션은 부위를 골라야 고를 수 있음.
    /// </summary>
    public static class EquipCraftWindow
    {
        public static void Open(PlayerEquipment equip, TMP_FontAsset font, System.Action<EquipmentItem> onMade)
        {
            if (equip == null) return;
            var w = OverlayWindow.Open("장비 선택 제작", 1320, 920, font);
            string set = null;
            EquipSlot? slot = null;
            EquipStat? main = null;
            var body = UIFactory.NewRect("Body", w.Panel);
            UIFactory.Stretch(body);

            void Rebuild()
            {
                if (!w.IsOpen) return;
                OverlayWindow.Clear(body);
                int n = (set != null ? 1 : 0) + (slot.HasValue ? 1 : 0) + (main.HasValue ? 1 : 0);
                int essence = EquipRules.CraftEssence[n], gold = EquipRules.CraftGold[n];
                w.Text(body, $"<color=#9AA3B5>원하는 항목만 고르세요. 고르지 않은 항목은 무작위 · 항상 6★ +0 · 보조 옵션은 무작위</color>   " +
                             $"보유 <color=#C9A0FF>장비 정수 {equip.Essence:N0}</color>", 19, Color.white, TextAlignmentOptions.Left, 36, -78, 1250, 30);

                // ---------- 세트 ----------
                w.Text(body, "<b>① 세트</b>", 22, new Color(1f, 0.9f, 0.62f), TextAlignmentOptions.Left, 36, -120, 300, 32);
                Cell(w, body, 36, -156, 104, 124, set == null, "무작위", null, new Color(0.6f, 0.62f, 0.7f), () => { set = null; Rebuild(); });
                for (int i = 0; i < EquipSets.All.Length; i++)
                {
                    var s = EquipSets.All[i];
                    string id = s.id;
                    Cell(w, body, 36 + (i + 1) * 112, -156, 104, 124, set == id, EquipSets.Short(id), GameArt.EquipIcon(id, slot ?? EquipSlot.Body), s.color,
                        () => { set = id; Rebuild(); });
                }
                if (set != null)
                {
                    var es = EquipSets.Get(set);
                    w.Text(body, $"{EquipSets.Rich(set)}   2세트 {es.desc2}   ·   4세트 {es.desc4}", 17, new Color(0.85f, 0.88f, 0.94f), TextAlignmentOptions.Left, 36, -286, 1250, 26);
                }

                // ---------- 부위 ----------
                w.Text(body, "<b>② 부위</b>", 22, new Color(1f, 0.9f, 0.62f), TextAlignmentOptions.Left, 36, -322, 300, 32);
                Cell(w, body, 36, -358, 150, 124, !slot.HasValue, "무작위", null, new Color(0.6f, 0.62f, 0.7f), () => { slot = null; main = null; Rebuild(); });
                for (int i = 0; i < EquipStats.AllSlots.Length; i++)
                {
                    var sl = EquipStats.AllSlots[i];
                    Cell(w, body, 36 + (i + 1) * 158, -358, 150, 124, slot == sl, EquipStats.SlotName(sl), GameArt.EquipIcon(set, sl),
                        set != null ? EquipSets.Get(set).color : new Color(0.75f, 0.78f, 0.86f),
                        () => { if (slot != sl) main = null; slot = sl; Rebuild(); });
                }

                // ---------- 주옵션 ----------
                w.Text(body, "<b>③ 주옵션</b>", 22, new Color(1f, 0.9f, 0.62f), TextAlignmentOptions.Left, 36, -524, 300, 32);
                if (!slot.HasValue)
                    w.Text(body, "<color=#6B7280>부위를 고르면 주옵션을 고를 수 있습니다</color>", 19, Color.white, TextAlignmentOptions.Left, 36, -566, 800, 40);
                else
                {
                    var pool = EquipStats.MainPool(slot.Value);
                    if (pool.Length <= 1)
                        w.Text(body, $"<color=#9AA3B5>{EquipStats.SlotName(slot.Value)} 장비의 주옵션은 <b>{EquipStats.Name(pool[0]).Replace("%", "")}</b> 고정입니다 (고를 필요 없음)</color>",
                            19, Color.white, TextAlignmentOptions.Left, 36, -566, 1000, 40);
                    else
                    {
                        float x = 36;
                        TextBtn(w, body, x, -562, 150, 54, !main.HasValue, "무작위", () => { main = null; Rebuild(); }); x += 158;
                        foreach (var st in pool)
                        {
                            var s = st;
                            string label = EquipStats.Name(s).Replace("%", s == EquipStat.HPPct || s == EquipStat.AtkPct || s == EquipStat.DefPct ? "%" : "");
                            if (s == EquipStat.ElementDmg) label = "속성 피해";
                            TextBtn(w, body, x, -562, 150, 54, main == s, label, () => { main = s; Rebuild(); });
                            x += 158;
                        }
                        if (main == EquipStat.ElementDmg)
                            w.Text(body, "<color=#9AA3B5>속성 피해: 장착한 캐릭터의 속성 피해로 적용됩니다</color>", 17, Color.white, TextAlignmentOptions.Left, 36, -626, 900, 26);
                    }
                }

                // ---------- 비용 · 제작 ----------
                var costBox = w.Box(body, new Color(1, 1, 1, 0.06f), 36, -680, 1248, 200);
                string picked = n == 0 ? "모두 무작위" : $"고른 항목 <b>{n}</b>개";
                var steps = new System.Text.StringBuilder();
                for (int k = 0; k < EquipRules.CraftEssence.Length; k++)
                {
                    string row = $"{k}개 — 정수 {EquipRules.CraftEssence[k]} · 금화 {EquipRules.CraftGold[k]:N0}";
                    steps.Append(k == n ? $"<color=#FFD36B><b>{row}</b></color>" : $"<color=#6B7280>{row}</color>");
                    if (k < EquipRules.CraftEssence.Length - 1) steps.Append("     ");
                }
                w.Text(costBox.transform, $"{picked}    필요  <color={(equip.Essence >= essence ? "#C9A0FF" : "#FF8A8A")}>장비 정수 {essence}</color>  ·  " +
                                          $"<color={(equip.Gold >= gold ? "#FFD36B" : "#FF8A8A")}>금화 {gold:N0}</color>",
                    24, Color.white, TextAlignmentOptions.Left, 24, -20, 860, 40);
                var st2 = w.Text(costBox.transform, steps.ToString(), 16, Color.white, TextAlignmentOptions.Left, 24, -72, 860, 30);
                st2.enableAutoSizing = true; st2.fontSizeMin = 11; st2.fontSizeMax = 16;
                w.Text(costBox.transform, "<color=#9AA3B5>장비 정수는 장비를 분해하면 얻습니다 (6★ 8개 · 5★ 4개 · 4★ 2개 · 3★ 1개, 강화 4단계마다 +1)</color>",
                    16, Color.white, TextAlignmentOptions.Left, 24, -116, 880, 60);
                bool can = equip.Essence >= essence && equip.Gold >= gold;
                w.Btn(costBox.transform, "제작", 30, OverlayWindow.Gold, 930, -48, 290, 104, () =>
                {
                    string err = equip.Craft(set, slot, main, out var made);
                    if (err != null) { Popups.Show("장비 제작", err); return; }
                    Rebuild();
                    Popups.Show("제작 완료",
                        $"{EquipSets.Rich(made.set)}  {EquipStats.SlotName(made.slot)}  <color=#FFD36B>6★</color>\n" +
                        $"주옵션  <b>{EquipStats.Format(made.main, made.MainValue)}</b>\n" +
                        $"<size=85%><color=#9AA3B5>보조 옵션 {made.subs.Count}줄 · 장비 화면에서 확인하세요</color></size>",
                        "확인", () => onMade?.Invoke(made), icon: "Equipment/" + made.set + "_" + made.slot.ToString().ToLowerInvariant());
                }, can);
            }
            Rebuild();
        }

        private static void Cell(OverlayWindow w, Transform parent, float x, float y, float cw, float ch, bool on, string label, Sprite icon, Color c, System.Action onClick)
        {
            var bg = w.Box(parent, on ? new Color(c.r, c.g, c.b, 0.55f) : new Color(c.r, c.g, c.b, 0.14f), x, y, cw, ch, raycast: true);
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(() => onClick());
            bg.gameObject.AddComponent<ButtonFx>();
            if (on)
            {
                var ring = w.Box(bg.transform, new Color(1f, 0.85f, 0.4f, 0.9f), -3, 3, cw + 6, ch + 6);
                ring.transform.SetAsFirstSibling();
                ring.raycastTarget = false;
                var inner = w.Box(bg.transform, new Color(0.07f, 0.08f, 0.12f, 1f), 0, 0, cw, ch);
                inner.transform.SetSiblingIndex(1);
                var tint = w.Box(bg.transform, new Color(c.r, c.g, c.b, 0.35f), 0, 0, cw, ch);
                tint.transform.SetSiblingIndex(2);
            }
            if (icon != null) w.Icon(bg.transform, icon, (cw - 74) / 2f, -8, 74);
            var t = w.Text(bg.transform, label, 16, on ? Color.white : new Color(0.85f, 0.87f, 0.93f), TextAlignmentOptions.Center, 4, icon != null ? -84 : -(ch - 30) / 2f, cw - 8, 30);
            t.fontStyle = FontStyles.Bold;
            t.enableAutoSizing = true; t.fontSizeMin = 11; t.fontSizeMax = 16;
        }

        private static void TextBtn(OverlayWindow w, Transform parent, float x, float y, float bw, float bh, bool on, string label, System.Action onClick)
        {
            var b = w.Btn(parent, label, 18, on ? OverlayWindow.Gold : OverlayWindow.Dim, x, y, bw, bh, onClick);
            var t = b.GetComponentInChildren<TextMeshProUGUI>();
            t.enableAutoSizing = true; t.fontSizeMin = 11; t.fontSizeMax = 18;
        }
    }
}
