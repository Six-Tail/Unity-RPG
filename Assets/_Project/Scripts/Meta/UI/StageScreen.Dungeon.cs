using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 스테이지 화면의 '특수 던전' 쪽.
    ///  · 위: 던전 종류 탭(장비 · 돌파 · 경험치 · 금화 · 스킬 교본) — 돌파는 그 아래 속성 탭
    ///  · 왼쪽: 단계 세로 목록(1 ~ 10단계 · 별 · 개방 조건)
    ///  · 가운데: 던전 그림 + 매 클리어 보상 아이콘 + 적 구성(장비 던전은 드롭 성급 · 세트)
    ///  · 오른쪽: 상세 글 + 보상 배수 · 반복 · 출격(메인 스토리와 같은 패널)
    ///  · 장비 던전: 먼저 지도(장소 5곳)를 보여 주고, 장소를 누르면 그 장소의 단계 목록으로
    /// </summary>
    public partial class StageScreen
    {
        private GameObject _dgRoot, _dgMap;
        private RectTransform _dgList, _dgShow, _dgPins, _dgSites, _topTabs;
        private RectTransform _paint;    // Section · RewardTile · EnemyCard가 그리는 곳(던전 가운데 칸 / 메인 상세)
        private float _paintW = 668f;
        private TextMeshProUGUI _dgHeader;
        private ScrollRect _dgListScroll;
        private int _equipSite = -1;                      // 장비 던전 장소(1 ~ 5), -1 = 지도
        private Element _ascElement = Element.Physical;   // 돌파 던전 속성
        private StageKind _dgListKind = StageKind.Main;
        private int _dgListGroup = int.MinValue;

        /// <summary>장비 던전 장소 5곳: 이름 + 지도 위 위치(그림 왼쪽 위 기준 0 ~ 1).</summary>
        private static readonly (string name, Vector2 pos)[] EquipSites =
        {
            ("수호 요새", new Vector2(0.17f, 0.36f)),
            ("염빙 산맥", new Vector2(0.47f, 0.15f)),
            ("뇌풍 고원", new Vector2(0.83f, 0.33f)),
            ("강철 광산", new Vector2(0.53f, 0.47f)),
            ("샘의 숲", new Vector2(0.47f, 0.76f)),
        };

        private static string SiteName(int site) => site >= 1 && site <= EquipSites.Length ? EquipSites[site - 1].name : $"장비 던전 {site}";

        private static string DungeonArt(StageKind k) => k switch
        {
            StageKind.AscensionDungeon => "Backgrounds/battle_dungeon_ascension",
            StageKind.GoldDungeon => "Backgrounds/battle_dungeon_gold",
            StageKind.SkillBookDungeon => "Backgrounds/battle_dungeon_book",
            StageKind.EquipmentDungeon => "Backgrounds/battle_dungeon_equipment",
            _ => "Backgrounds/battle_dungeon_exp",
        };

        private static void P(RectTransform rt, float x, float y, float w, float h)
        {
            UIFactory.Anchor(rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, -y);
        }

        private bool Unlocked(StageData s) => progress == null || progress.IsUnlocked(s, database);

        /// <summary>지금 고른 종류(· 속성 · 장소)의 단계 목록.</summary>
        private List<StageData> DungeonTiers()
        {
            var list = database.Dungeons(_dungeonKind);
            if (_dungeonKind == StageKind.AscensionDungeon) return list.FindAll(d => d.dungeonElement == _ascElement);
            if (_dungeonKind == StageKind.EquipmentDungeon) return list.FindAll(d => d.chapter == _equipSite);
            return list;
        }

        // ---------------- 구성 ----------------

        private void BuildDungeonUI()
        {
            var root = UIFactory.NewRect("Dungeon", _root.transform);
            UIFactory.Anchor(root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            root.sizeDelta = new Vector2(1760, 850); root.anchoredPosition = new Vector2(0, -196);
            _dgRoot = root.gameObject;

            _dgHeader = UIFactory.AddText("Header", root, "", 28, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            _dgHeader.richText = true;
            P(_dgHeader.rectTransform, 4, 0, 1090, 44);
            _dgHeader.enableAutoSizing = true; _dgHeader.fontSizeMin = 18; _dgHeader.fontSizeMax = 28;

            // 왼쪽: 단계 목록
            var listBg = UIFactory.AddImage("TierList", root, new Color(0.08f, 0.09f, 0.13f, 0.82f), ProceduralSprites.Rounded);
            listBg.type = Image.Type.Sliced; listBg.preserveAspect = false;
            P(listBg.rectTransform, 0, 54, 400, 796);
            var view = UIFactory.NewRect("View", listBg.transform);
            UIFactory.Stretch(view, 8, 8, 8, 8);
            _dgList = UIFactory.NewRect("Content", view);
            var vl = _dgList.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 6; vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            _dgListScroll = UIFactory.MakeVerticalScroll(view, _dgList);

            // 가운데: 던전 그림 · 보상 · 적
            var show = UIFactory.AddImage("Showcase", root, new Color(0.08f, 0.09f, 0.13f, 0.82f), ProceduralSprites.Rounded);
            show.type = Image.Type.Sliced; show.preserveAspect = false;
            P(show.rectTransform, 420, 54, 680, 796);
            _dgShow = UIFactory.NewRect("Body", show.transform);
            UIFactory.Stretch(_dgShow);

            // 장비 던전 지도(장소 고르기)
            var map = UIFactory.AddImage("EquipMap", root, new Color(0.75f, 0.62f, 0.38f, 1f), ProceduralSprites.Rounded);
            map.type = Image.Type.Sliced; map.preserveAspect = false;
            P(map.rectTransform, 0, 54, 1760, 796);
            _dgMap = map.gameObject;
            var mapArea = UIFactory.NewRect("MapArea", map.transform);
            P(mapArea, 4, 4, 1310, 788);
            BackgroundArt.Create(mapArea, 0.06f).Set("Backgrounds/equip_map");
            _dgPins = UIFactory.NewRect("Pins", mapArea);
            UIFactory.Stretch(_dgPins);
            var side = UIFactory.AddImage("Sites", map.transform, new Color(0.07f, 0.08f, 0.12f, 0.97f));
            P(side.rectTransform, 1318, 4, 438, 788);
            var sideTitle = UIFactory.AddText("Title", side.transform, "장소 선택", 28, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            sideTitle.richText = true;
            P(sideTitle.rectTransform, 20, 14, 400, 70);
            _dgSites = UIFactory.NewRect("List", side.transform);
            P(_dgSites, 14, 96, 410, 680);
            var sl = _dgSites.gameObject.AddComponent<VerticalLayoutGroup>();
            sl.spacing = 10; sl.childControlWidth = true; sl.childControlHeight = true;
            sl.childForceExpandWidth = true; sl.childForceExpandHeight = false;

            _dgRoot.SetActive(false);
        }

        // ---------------- 갱신 ----------------

        /// <summary>던전 쪽 두 번째 탭 줄: 돌파 = 속성, 장비 장소 화면 = '지도로' + 장소 이름.</summary>
        private void RebuildDungeonSubTabs()
        {
            if (_dungeonKind == StageKind.AscensionDungeon)
                foreach (var e in Traits.AllElements)
                {
                    var ee = e;
                    bool on = e == _ascElement;
                    var col = Traits.ColorOf(e);
                    var b = UIFactory.AddButton("Elem", _tabs, Traits.Name(e), 19, font,
                        on ? new Color(col.r, col.g, col.b, 0.95f) : new Color(col.r, col.g, col.b, 0.28f), Color.white);
                    var le = b.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = 120; le.preferredHeight = 40;
                    b.onClick.AddListener(() => { _ascElement = ee; _selected = null; Refresh(); });
                }
            else if (_dungeonKind == StageKind.EquipmentDungeon && _equipSite > 0)
            {
                var b = UIFactory.AddButton("ToMap", _tabs, "< 지도", 19, font, new Color(1, 1, 1, 0.16f), Color.white);
                var le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 120; le.preferredHeight = 40;
                b.onClick.AddListener(() => { _equipSite = -1; _selected = null; Refresh(); });
                for (int i = 1; i <= EquipSites.Length; i++)
                {
                    int site = i;
                    bool on = i == _equipSite;
                    var sb = UIFactory.AddButton("Site", _tabs, SiteName(i), 18, font,
                        on ? new Color(0.3f, 0.72f, 0.68f, 0.95f) : new Color(0.3f, 0.72f, 0.68f, 0.25f), Color.white);
                    var sle = sb.gameObject.AddComponent<LayoutElement>();
                    sle.preferredWidth = 150; sle.preferredHeight = 40;
                    sb.onClick.AddListener(() => { _equipSite = site; _selected = null; Refresh(); });
                }
            }
        }

        /// <summary>던전 화면 전체 갱신(Refresh에서 호출). 선택이 없으면 가장 높은 개방 단계를 고름.</summary>
        private void RefreshDungeon()
        {
            bool map = _dungeonKind == StageKind.EquipmentDungeon && _equipSite <= 0;
            _dgMap.SetActive(map);
            _dgList.parent.parent.gameObject.SetActive(!map);
            _dgShow.parent.gameObject.SetActive(!map);
            _dgHeader.gameObject.SetActive(!map);
            if (map)
            {
                _selected = null;
                RebuildMap();
                return;
            }

            var tiers = DungeonTiers();
            if (_selected == null || !tiers.Contains(_selected))
            {
                _selected = tiers.FindLast(Unlocked);
                if (_selected == null && tiers.Count > 0) _selected = tiers[0];
            }

            string kindName = $"<color={SpecialDungeons.Hex(_dungeonKind)}>{SpecialDungeons.Name(_dungeonKind)}</color>";
            if (_dungeonKind == StageKind.EquipmentDungeon) kindName += $"  ·  {SiteName(_equipSite)}";
            else if (_dungeonKind == StageKind.AscensionDungeon) kindName += $"  ·  <color={Traits.Hex(_ascElement)}>{Traits.Name(_ascElement)}</color>";
            _dgHeader.text = $"{kindName}   <size=62%><color=#AEB6C8>{SpecialDungeons.Summary(_dungeonKind)}</color></size>";

            int group = _dungeonKind == StageKind.AscensionDungeon ? (int)_ascElement : _equipSite;
            bool newList = _dgListKind != _dungeonKind || _dgListGroup != group;
            _dgListKind = _dungeonKind; _dgListGroup = group;
            RebuildTierList(tiers, newList);
            RebuildShowcase(_selected);
        }

        private void RebuildTierList(List<StageData> tiers, bool scrollToSelected)
        {
            Clear(_dgList);
            var col = SpecialDungeons.ColorOf(_dungeonKind);
            foreach (var s in tiers)
            {
                var ss = s;
                bool open = Unlocked(s), on = s == _selected;
                int stars = progress != null ? progress.StarsOf(s) : 0;
                var bg = UIFactory.AddImage("Tier", _dgList, on ? new Color(col.r, col.g, col.b, 0.92f) : new Color(1, 1, 1, open ? 0.09f : 0.04f), ProceduralSprites.Rounded, raycast: true);
                bg.type = Image.Type.Sliced; bg.preserveAspect = false;
                bg.gameObject.AddComponent<LayoutElement>().preferredHeight = 72;
                var btn = bg.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => { _selected = ss; Refresh(); });
                bg.gameObject.AddComponent<ButtonFx>();
                var rt = bg.rectTransform;
                var dim = new Color(0.5f, 0.52f, 0.58f);

                var code = UIFactory.AddText("Code", rt, s.Code, 26, font, open ? Color.white : dim, TextAlignmentOptions.Left, FontStyles.Bold);
                UIFactory.Anchor(code.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f));
                code.rectTransform.sizeDelta = new Vector2(108, 0); code.rectTransform.anchoredPosition = new Vector2(16, 0);
                code.enableAutoSizing = true; code.fontSizeMin = 16; code.fontSizeMax = 26;

                var sub = UIFactory.AddText("Sub", rt, $"{TierSummary(s)}\n<size=82%><color={(on ? "#F1F3F8" : "#9AA3B5")}>권장 Lv.{s.recommendedLevel} · 행동력 {StageEntry.BaseCost(s)}</color></size>",
                    18, font, open ? Color.white : dim, TextAlignmentOptions.Left);
                sub.richText = true; sub.lineSpacing = -6f;
                sub.textWrappingMode = TextWrappingModes.NoWrap;
                sub.enableAutoSizing = true; sub.fontSizeMin = 12; sub.fontSizeMax = 18;
                UIFactory.Stretch(sub.rectTransform, 130, 4, 104, 4);

                string right = open ? StageRunner.Stars(stars)
                    : $"<color=#8A8FA0>{(s.unlockAfter != null ? s.unlockAfter.Code + "\n클리어 시 개방" : "잠김")}</color>";
                var st = UIFactory.AddText("Stars", rt, right, open ? 22 : 14, font, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
                st.richText = true; st.lineSpacing = -8f;
                UIFactory.Anchor(st.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f));
                st.rectTransform.sizeDelta = new Vector2(100, 0); st.rectTransform.anchoredPosition = new Vector2(-12, 0);
            }
            if (scrollToSelected && _dgListScroll != null)
            {
                int i = tiers.IndexOf(_selected);
                Canvas.ForceUpdateCanvases();
                _dgListScroll.verticalNormalizedPosition = tiers.Count <= 1 || i < 0 ? 1f : 1f - i / (float)(tiers.Count - 1);
            }
        }

        /// <summary>단계 목록 한 줄 요약(그 단계의 대표 보상).</summary>
        private static string TierSummary(StageData s) => s.kind switch
        {
            StageKind.GoldDungeon => $"<color=#FFD36B>금화 {s.gold:N0}</color>",
            StageKind.ExpDungeon => $"<color=#9BE28A>경험치 {ExpTotal(s):N0}</color>",
            StageKind.SkillBookDungeon => $"<color=#8FB6FF>교본 ×{BookCount(s)}</color>",
            StageKind.EquipmentDungeon => $"최대 {EquipStats.Stars(s.MaxEquipRarity)}",
            _ => $"{Materials.RichName(s.DungeonMaterial)} <size=85%>· {Materials.TierTargetLevel(s.dungeonTier)} 돌파</size>",
        };

        // ---------------- 가운데: 그림 · 보상 · 적 ----------------

        private void RebuildShowcase(StageData s)
        {
            Clear(_dgShow);
            _paint = _dgShow; _paintW = 668f;
            if (s == null) return;
            bool open = Unlocked(s), cleared = progress != null && progress.IsCleared(s);
            bool equip = s.kind == StageKind.EquipmentDungeon;

            // 던전 그림 + 이름
            float bannerH = equip ? 190 : 250;
            var banner = UIFactory.NewRect("Banner", _dgShow);
            P(banner, 6, 6, 668, bannerH);
            BackgroundArt.Create(banner, 0.1f).Set(DungeonArt(s.kind));
            var shade = UIFactory.AddImage("Shade", banner, new Color(0, 0, 0, 0.9f), Fade());
            shade.preserveAspect = false;
            UIFactory.Anchor(shade.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            shade.rectTransform.sizeDelta = new Vector2(0, bannerH * 0.75f); shade.rectTransform.anchoredPosition = Vector2.zero;
            string name = s.IsAscensionDungeon ? $"<color={Traits.Hex(s.dungeonElement)}>{Traits.Name(s.dungeonElement)}</color> 돌파 던전"
                : equip ? SiteName(s.chapter) : SpecialDungeons.Name(s.kind);
            var title = UIFactory.AddText("Title", banner, $"{name}  <color=#FFD36B>{s.Code}</color>", 36, font, Color.white, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            title.richText = true;
            UIFactory.Stretch(title.rectTransform, 22, 44, 22, 10);
            TextOutline.Apply(title, 0.2f, new Color32(8, 10, 18, 235));
            string state = !open ? $"<color=#FF9C9C>{(s.unlockAfter != null ? $"{s.unlockAfter.Code} {s.unlockAfter.stageName} 클리어 시 개방" : "잠김")}</color>"
                : cleared ? $"최고 기록 {StageRunner.Stars(progress.StarsOf(s))}  <color=#C9CEDA>· {progress.ClearsOf(s)}회 클리어</color>" : "<color=#C9CEDA>아직 클리어하지 않음</color>";
            var info = UIFactory.AddText("Info", banner, $"권장 Lv.{s.recommendedLevel}  ·  ★3 = {s.StarRoundLimit}턴 이내  ·  {state}", 19, font, Color.white, TextAlignmentOptions.BottomLeft);
            info.richText = true;
            info.enableAutoSizing = true; info.fontSizeMin = 13; info.fontSizeMax = 19;
            info.textWrappingMode = TextWrappingModes.NoWrap;
            UIFactory.Stretch(info.rectTransform, 24, 12, 22, 10);
            TextOutline.Apply(info, 0.15f, new Color32(8, 10, 18, 235));

            // 매 클리어 보상(아이콘)
            float y = bannerH + 18;
            Section("매 클리어 보상", y); y += 36;
            var tiles = new List<(Sprite icon, string label, string count, Color tint)>();
            if (s.equipmentDrops > 0 && s.equipmentSets != null && s.equipmentSets.Count > 0)
                tiles.Add((GameArt.EquipIcon(s.equipmentSets[0], EquipSlot.Hands), "장비", $"×{s.equipmentDrops}", new Color(0.5f, 0.9f, 0.85f)));
            if (s.gold > 0) tiles.Add((GameArt.Load("Items/Gold"), "금화", $"{s.gold:N0}", new Color(1f, 0.83f, 0.42f)));
            if (s.rewards != null)
                foreach (var m in s.rewards)
                    if (m != null && m.count > 0) tiles.Add((GameArt.MaterialIcon(m.type), Materials.Name(m.type), $"×{m.count}", Grades.ColorOf(Materials.Grade(m.type))));
            if (s.firstClearCrystals > 0)
                tiles.Add((GameArt.Load("Items/Crystal"), cleared ? "첫 클리어(받음)" : "첫 클리어", $"{s.firstClearCrystals}", cleared ? new Color(0.45f, 0.47f, 0.53f) : new Color(0.56f, 0.91f, 1f)));
            float tw = Mathf.Min(124f, (668f - (tiles.Count - 1) * 8f) / Mathf.Max(1, tiles.Count));
            for (int i = 0; i < tiles.Count; i++) RewardTile(tiles[i].icon, tiles[i].label, tiles[i].count, tiles[i].tint, 6 + i * (tw + 8), y, tw);
            y += 132;
            if (s.characterExp > 0)
            {
                var ex = UIFactory.AddText("CharExp", _dgShow, $"<color=#9AA3B5>출전 캐릭터마다 경험치</color> +{s.characterExp:N0}", 18, font, Color.white, TextAlignmentOptions.Left);
                ex.richText = true;
                P(ex.rectTransform, 12, y, 650, 26);
                y += 34;
            }

            if (equip) { BuildEquipInfo(s, y); return; }

            // 적 구성(그림 카드)
            Section("적 구성", y); y += 36;
            int n = 0;
            if (s.enemies != null) foreach (var e in s.enemies) if (e != null && e.unit != null) n++;
            if (n == 0) return;
            float cardH = Mathf.Max(150f, 796f - y - 10f), cardW = Mathf.Min(216f, (668f - (n - 1) * 10f) / n);
            int k = 0;
            foreach (var e in s.enemies)
            {
                if (e == null || e.unit == null) continue;
                EnemyCard(e, 6 + k * (cardW + 10), y, cardW, cardH);
                k++;
            }
        }

        private void Section(string label, float y)
        {
            var t = UIFactory.AddText("Section", _paint, label, 21, font, new Color(1f, 0.83f, 0.42f), TextAlignmentOptions.Left, FontStyles.Bold);
            P(t.rectTransform, 12, y, 400, 30);
            var line = UIFactory.AddImage("Line", _paint, new Color(1f, 0.83f, 0.42f, 0.25f));
            P(line.rectTransform, 12 + t.preferredWidth + 12, y + 16, Mathf.Max(10f, _paintW - 24f - t.preferredWidth - 12f), 2);
        }

        private void RewardTile(Sprite icon, string label, string count, Color tint, float x, float y, float w)
        {
            var bg = UIFactory.AddImage("Reward", _paint, new Color(tint.r, tint.g, tint.b, 0.55f), ProceduralSprites.Rounded);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            P(bg.rectTransform, x, y, w, 124);
            var inner = UIFactory.AddImage("Inner", bg.transform, new Color(0.09f, 0.1f, 0.14f, 1f), ProceduralSprites.Rounded);
            inner.type = Image.Type.Sliced; inner.preserveAspect = false;
            UIFactory.Stretch(inner.rectTransform, 2, 2, 2, 2);
            if (icon != null)
            {
                var im = UIFactory.AddImage("Icon", inner.transform, Color.white, icon);
                im.preserveAspect = true;
                UIFactory.Stretch(im.rectTransform, 10, 48, 10, 6);
            }
            var c = UIFactory.AddText("Count", inner.transform, count, 21, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(c.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            c.rectTransform.sizeDelta = new Vector2(-6, 26); c.rectTransform.anchoredPosition = new Vector2(0, 22);
            c.enableAutoSizing = true; c.fontSizeMin = 12; c.fontSizeMax = 21;
            var l = UIFactory.AddText("Label", inner.transform, label, 15, font, new Color(0.72f, 0.75f, 0.82f), TextAlignmentOptions.Center);
            UIFactory.Anchor(l.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            l.rectTransform.sizeDelta = new Vector2(-6, 20); l.rectTransform.anchoredPosition = new Vector2(0, 3);
            l.textWrappingMode = TextWrappingModes.NoWrap;
            l.enableAutoSizing = true; l.fontSizeMin = 9; l.fontSizeMax = 15;
        }

        private void EnemyCard(StageEnemy e, float x, float y, float w, float h)
        {
            var el = e.unit.element;
            var col = Traits.ColorOf(el);
            var bg = UIFactory.AddImage("Enemy", _paint, new Color(col.r, col.g, col.b, 0.6f), ProceduralSprites.Rounded);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            P(bg.rectTransform, x, y, w, h);
            var inner = UIFactory.AddImage("Inner", bg.transform, new Color(0.07f, 0.08f, 0.11f, 1f));
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);
            inner.gameObject.AddComponent<RectMask2D>();
            var sp = GameArt.Enemy(e.unit.DisplayName);
            if (sp != null)
            {
                var im = UIFactory.AddImage("Art", inner.transform, Color.white, sp);
                im.preserveAspect = true;
                UIFactory.Stretch(im.rectTransform, 4, 74, 4, 4);
            }
            var nm = UIFactory.AddText("Name", inner.transform, $"{e.unit.DisplayName}\n<size=82%><color=#FFD36B>Lv.{e.level}</color></size>", 19, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            nm.richText = true; nm.lineSpacing = -6f;
            UIFactory.Anchor(nm.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            nm.rectTransform.sizeDelta = new Vector2(-8, 46); nm.rectTransform.anchoredPosition = new Vector2(0, 28);
            nm.enableAutoSizing = true; nm.fontSizeMin = 12; nm.fontSizeMax = 19;
            var weak = new System.Text.StringBuilder("<color=#9AA3B5>약점</color> ");
            if (e.unit.weaknesses != null)
                foreach (var wk in e.unit.weaknesses) weak.Append($"<color={Traits.Hex(wk)}>{Traits.Name(wk)}</color> ");
            var wt = UIFactory.AddText("Weak", inner.transform, weak.ToString().TrimEnd(), 16, font, Color.white, TextAlignmentOptions.Center);
            wt.richText = true;
            UIFactory.Anchor(wt.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            wt.rectTransform.sizeDelta = new Vector2(-8, 24); wt.rectTransform.anchoredPosition = new Vector2(0, 4);
            wt.textWrappingMode = TextWrappingModes.NoWrap;
            wt.enableAutoSizing = true; wt.fontSizeMin = 10; wt.fontSizeMax = 16;
        }

        /// <summary>장비 던전: 드롭 성급 막대 + 나오는 세트 2종(세트 효과).</summary>
        private void BuildEquipInfo(StageData s, float y)
        {
            Section("드롭 성급", y); y += 36;
            var w = s.equipmentRarityWeights;
            int total = 0, first = EquipRules.WeightBase(w);
            if (w != null) foreach (var v in w) total += v;
            if (w != null && total > 0)
                for (int i = 0; i < w.Count; i++)
                {
                    if (w[i] <= 0) continue;
                    int r = first + i;
                    float pct = w[i] / (float)total;
                    var lab = UIFactory.AddText("Rarity", _dgShow, $"{EquipStats.Stars(r)}  <size=80%>{Grades.Tag(r)}</size>", 19, font, Color.white, TextAlignmentOptions.Left);
                    lab.richText = true;
                    P(lab.rectTransform, 12, y, 230, 26);
                    var barBg = UIFactory.AddImage("BarBg", _dgShow, new Color(1, 1, 1, 0.08f));
                    P(barBg.rectTransform, 250, y + 6, 330, 14);
                    var bar = UIFactory.AddImage("Bar", _dgShow, Grades.ColorOf(r));
                    P(bar.rectTransform, 250, y + 6, Mathf.Max(4f, 330f * pct), 14);
                    var pt = UIFactory.AddText("Pct", _dgShow, $"{Mathf.RoundToInt(pct * 100)}%", 19, font, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
                    P(pt.rectTransform, 590, y, 70, 26);
                    y += 30;
                }
            y += 34;

            Section("나오는 세트", y); y += 36;
            if (s.equipmentSets == null) return;
            float cardH = Mathf.Clamp((796f - y - 10f - (s.equipmentSets.Count - 1) * 8f) / Mathf.Max(1, s.equipmentSets.Count), 84f, 130f);
            foreach (var id in s.equipmentSets)
            {
                var set = EquipSets.Get(id);
                if (set == null) continue;
                var bg = UIFactory.AddImage("Set", _dgShow, new Color(set.color.r, set.color.g, set.color.b, 0.5f), ProceduralSprites.Rounded);
                bg.type = Image.Type.Sliced; bg.preserveAspect = false;
                P(bg.rectTransform, 6, y, 668, cardH);
                var inner = UIFactory.AddImage("Inner", bg.transform, new Color(0.09f, 0.1f, 0.14f, 1f), ProceduralSprites.Rounded);
                inner.type = Image.Type.Sliced; inner.preserveAspect = false;
                UIFactory.Stretch(inner.rectTransform, 2, 2, 2, 2);
                float ix = 10;
                foreach (var slot in new[] { EquipSlot.Head, EquipSlot.Hands })
                {
                    var sp = GameArt.EquipIcon(id, slot);
                    if (sp == null) continue;
                    var im = UIFactory.AddImage("Icon", inner.transform, Color.white, sp);
                    im.preserveAspect = true;
                    UIFactory.Anchor(im.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                    im.rectTransform.sizeDelta = new Vector2(cardH - 22, cardH - 22); im.rectTransform.anchoredPosition = new Vector2(ix, 0);
                    ix += cardH - 30;
                }
                var t = UIFactory.AddText("T", inner.transform,
                    $"<b>{EquipSets.Rich(id)}</b>\n<size=78%><color=#FFD36B>2세트</color> {set.desc2}\n<color=#FFD36B>4세트</color> {set.desc4}</size>",
                    24, font, Color.white, TextAlignmentOptions.Left);
                t.richText = true; t.lineSpacing = -4f;
                t.enableAutoSizing = true; t.fontSizeMin = 13; t.fontSizeMax = 24;
                UIFactory.Stretch(t.rectTransform, ix + 22, 6, 12, 6);
                y += cardH + 8;
            }
        }

        // ---------------- 장비 던전 지도 ----------------

        private void RebuildMap()
        {
            Clear(_dgPins);
            Clear(_dgSites);
            var all = database.Dungeons(StageKind.EquipmentDungeon);
            // 그림은 16:9를 지도 칸(1310×788)에 꽉 채우므로 좌우가 조금 잘림 → 그만큼 보정
            const float areaW = 1310f, areaH = 788f;
            float imgW = areaH * 16f / 9f, cut = (imgW - areaW) * 0.5f;
            for (int i = 1; i <= EquipSites.Length; i++)
            {
                int site = i;
                var tiers = all.FindAll(d => d.chapter == site);
                if (tiers.Count == 0) continue;
                int opened = tiers.FindAll(Unlocked).Count, cleared = 0;
                foreach (var d in tiers) if (progress != null && progress.StarsOf(d) >= 3) cleared++;
                var first = tiers[0];
                var sets = first.equipmentSets ?? new List<string>();
                string setLine = sets.Count >= 2 ? $"{EquipSets.Rich(sets[0])} · {EquipSets.Rich(sets[1])}" : first.stageName;
                bool open = opened > 0;
                string stateLine = open ? $"개방 <color=#FFE08A>{opened}</color> / {tiers.Count}단계  ·  ★3 {cleared}"
                    : $"<color=#FF9C9C>{(first.unlockAfter != null ? first.unlockAfter.Code + " 클리어 시 개방" : "잠김")}</color>";
                var accent = sets.Count > 0 && EquipSets.Get(sets[0]) != null ? EquipSets.Get(sets[0]).color : new Color(0.3f, 0.72f, 0.68f);

                // 지도 위 핀
                var p = EquipSites[i - 1].pos;
                float px = Mathf.Clamp(p.x * imgW - cut, 150f, areaW - 150f), py = Mathf.Clamp(p.y * areaH, 80f, areaH - 30f);
                var pin = UIFactory.AddImage("Pin", _dgPins, new Color(accent.r, accent.g, accent.b, 1f), ProceduralSprites.Rounded, raycast: true);
                pin.type = Image.Type.Sliced; pin.preserveAspect = false;
                UIFactory.Anchor(pin.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0));
                pin.rectTransform.sizeDelta = new Vector2(280, 86); pin.rectTransform.anchoredPosition = new Vector2(px, -py + 18);
                var pb = pin.gameObject.AddComponent<Button>();
                pb.targetGraphic = pin;
                pb.onClick.AddListener(() => { _equipSite = site; _selected = null; Refresh(); });
                pin.gameObject.AddComponent<ButtonFx>();
                var pinInner = UIFactory.AddImage("Inner", pin.transform, new Color(0.06f, 0.07f, 0.11f, open ? 0.94f : 0.97f), ProceduralSprites.Rounded);
                pinInner.type = Image.Type.Sliced; pinInner.preserveAspect = false;
                UIFactory.Stretch(pinInner.rectTransform, 3, 3, 3, 3);
                var pt = UIFactory.AddText("T", pinInner.transform, $"<b>{SiteName(site)}</b>\n<size=74%>{setLine}</size>\n<size=66%>{stateLine}</size>", 24, font,
                    open ? Color.white : new Color(0.7f, 0.72f, 0.78f), TextAlignmentOptions.Center);
                pt.richText = true; pt.lineSpacing = -8f;
                pt.enableAutoSizing = true; pt.fontSizeMin = 12; pt.fontSizeMax = 24;
                UIFactory.Stretch(pt.rectTransform, 8, 4, 8, 4);
                var dot = UIFactory.AddImage("Dot", _dgPins, new Color(accent.r, accent.g, accent.b, 1f), ProceduralSprites.Circle);
                UIFactory.Anchor(dot.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f));
                dot.rectTransform.sizeDelta = new Vector2(26, 26); dot.rectTransform.anchoredPosition = new Vector2(px, -py);
                var dotIn = UIFactory.AddImage("In", dot.transform, Color.white, ProceduralSprites.Circle);
                UIFactory.Stretch(dotIn.rectTransform, 7, 7, 7, 7);

                // 오른쪽 목록 카드
                var card = UIFactory.AddImage("SiteCard", _dgSites, new Color(accent.r, accent.g, accent.b, open ? 0.85f : 0.3f), ProceduralSprites.Rounded, raycast: true);
                card.type = Image.Type.Sliced; card.preserveAspect = false;
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 126;
                var cb = card.gameObject.AddComponent<Button>();
                cb.targetGraphic = card;
                cb.onClick.AddListener(() => { _equipSite = site; _selected = null; Refresh(); });
                card.gameObject.AddComponent<ButtonFx>();
                var cin = UIFactory.AddImage("Inner", card.transform, new Color(0.09f, 0.1f, 0.14f, 1f), ProceduralSprites.Rounded);
                cin.type = Image.Type.Sliced; cin.preserveAspect = false;
                UIFactory.Stretch(cin.rectTransform, 2, 2, 2, 2);
                float ix = 10;
                foreach (var id in sets)
                {
                    var sp = GameArt.EquipIcon(id, EquipSlot.Hands);
                    if (sp == null) continue;
                    var im = UIFactory.AddImage("Icon", cin.transform, open ? Color.white : new Color(0.5f, 0.5f, 0.55f), sp);
                    im.preserveAspect = true;
                    UIFactory.Anchor(im.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                    im.rectTransform.sizeDelta = new Vector2(64, 64); im.rectTransform.anchoredPosition = new Vector2(ix, 0);
                    ix += 56;
                }
                var ct = UIFactory.AddText("T", cin.transform, $"<b>{SiteName(site)}</b>\n<size=74%>{setLine}</size>\n<size=68%>{stateLine}</size>", 26, font,
                    open ? Color.white : new Color(0.7f, 0.72f, 0.78f), TextAlignmentOptions.Left);
                ct.richText = true; ct.lineSpacing = -4f;
                ct.enableAutoSizing = true; ct.fontSizeMin = 12; ct.fontSizeMax = 26;
                UIFactory.Stretch(ct.rectTransform, ix + 18, 6, 10, 6);
            }
        }

        /// <summary>아래에서 위로 옅어지는 검은 막(그림 위 글씨용).</summary>
        private static Sprite s_fade;
        private static Sprite Fade()
        {
            if (s_fade != null) return s_fade;
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++) { float k = 1f - y / 63f; tex.SetPixel(0, y, new Color(1f, 1f, 1f, k * k * (3f - 2f * k))); }
            tex.Apply();
            return s_fade = Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
        }
    }
}
