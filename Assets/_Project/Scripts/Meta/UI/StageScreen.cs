using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 스테이지 선택 화면. 챕터 탭 → 스테이지 노드(별·잠금) → 상세(적·별 조건·보상) → '편성 후 출격'.
    /// '돌파 던전' 탭: 속성(행) × 등급(열: 초급 40 / 중급 60 / 상급 80 돌파) 격자, 스테이지 진행으로 개방.
    /// 코드 자동 구성 — database/progress 연결(progress는 비우면 자동 탐색).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Stage Screen")]
    public class StageScreen : MonoBehaviour
    {
        [SerializeField] private StageDatabase database;
        [SerializeField] private StageProgress progress;
        [SerializeField] private TMP_FontAsset font;

        public StageDatabase Database => database;

        /// <summary>'편성 후 출격'을 눌렀을 때(선택 스테이지).</summary>
        public event System.Action<StageData> OnSortie;

        private GameObject _root, _detPanel;
        private RectTransform _modeTabs, _tabs, _nodes;
        private GridLayoutGroup _grid;
        private bool _dungeonMode;
        private StageKind _dungeonKind = StageKind.AscensionDungeon;
        private StageDifficulty _difficulty = StageDifficulty.Easy;
        private TextMeshProUGUI _totalStars, _chapterTitle, _detTitle, _detBody, _sortieLbl;
        private Button _backBtn, _sortieBtn;
        private System.Action _onBack;

        private int _chapter = 1;
        private StageData _selected;
        private readonly Dictionary<StageData, Image> _nodeHL = new();

        private static readonly Color Gold = new(1f, 0.83f, 0.42f);

        private void Awake()
        {
            if (progress == null) progress = FindAnyObjectByType<StageProgress>();
            BuildUI();
        }

        private void OnEnable() { if (progress != null) progress.OnChanged += Refresh; }
        private void OnDisable() { if (progress != null) progress.OnChanged -= Refresh; }

        private void Start()
        {
            JumpToFrontier();
            Refresh();
        }

        public void Show(bool on)
        {
            if (_root != null) _root.SetActive(on);
            if (on) Refresh();
        }

        public void SetBackAction(System.Action onBack)
        {
            _onBack = onBack;
            if (_backBtn != null) _backBtn.gameObject.SetActive(onBack != null);
        }

        /// <summary>지정 스테이지를 선택(챕터/던전 탭도 맞춤).</summary>
        public void Focus(StageData s)
        {
            if (s == null) return;
            _dungeonMode = s.IsDungeon;
            if (s.IsDungeon) _dungeonKind = s.kind;
            else { _chapter = s.chapter; _difficulty = s.difficulty; }
            _selected = s;
        }

        /// <summary>특수 던전 탭으로 열기(HubScreen.Dungeon).</summary>
        public void OpenSpecial(StageKind kind = StageKind.AscensionDungeon)
        {
            _dungeonMode = true;
            if (kind != StageKind.Main) _dungeonKind = kind;
            _selected = null;
        }

        /// <summary>가장 앞선 해금 스테이지(아직 안 깬 것)로 챕터·선택 이동.</summary>
        public void JumpToFrontier()
        {
            if (database == null) return;
            StageData target = null;
            foreach (var s in database.Ordered())
            {
                if (progress != null && !progress.IsUnlocked(s, database)) break;
                target = s;
                if (progress == null || !progress.IsCleared(s)) break;
            }
            if (target != null) { _chapter = target.chapter; _difficulty = target.difficulty; _selected = target; }
        }

        // ---------------- 갱신 ----------------

        private void Refresh()
        {
            if (_root == null) return;
            if (database == null)
            {
                _chapterTitle.text = "StageDatabase가 연결되지 않았습니다";
                _detPanel.SetActive(false);
                return;
            }

            if (progress != null && !progress.IsDifficultyUnlocked(database, _difficulty)) _difficulty = StageDifficulty.Easy;
            var chapters = database.Chapters(_difficulty);
            if (chapters.Count > 0 && !chapters.Contains(_chapter)) _chapter = chapters[0];

            int total = 0, max = 0;
            foreach (var s in database.Ordered(_difficulty)) { total += progress != null ? progress.StarsOf(s) : 0; max += 3; }
            _totalStars.text = $"<size=75%><color={Difficulties.Hex(_difficulty)}>{Difficulties.Name(_difficulty)}</color></size>  <color=#FFD36B>★</color> {total} / {max}";

            if (_dungeonMode)
            {
                _chapterTitle.text = $"<color={SpecialDungeons.Hex(_dungeonKind)}>{SpecialDungeons.Name(_dungeonKind)}</color>  " +
                                     $"<size=60%><color=#AEB6C8>{SpecialDungeons.Summary(_dungeonKind)}</color></size>";
            }
            else
            {
                int chStars = progress != null ? progress.TotalStars(database, _chapter, _difficulty) : 0;
                int chMax = database.InChapter(_chapter, _difficulty).Count * 3;
                string diff = _difficulty == StageDifficulty.Easy ? "" : $"<color={Difficulties.Hex(_difficulty)}>{Difficulties.Name(_difficulty)}</color>  ";
                _chapterTitle.text = $"{diff}챕터 {_chapter}  ·  {database.ChapterName(_chapter)}    <size=70%><color=#FFD36B>★</color> {chStars} / {chMax}</size>";
            }

            RebuildTabs(chapters);
            RebuildNodes();
            if (_dungeonMode)
            {
                if (_selected == null || _selected.kind != _dungeonKind)
                {
                    var list = database.Dungeons(_dungeonKind);
                    _selected = list.Find(d => progress == null || progress.IsUnlocked(d, database));
                    if (_selected == null && list.Count > 0) _selected = list[0];
                }
            }
            else if (_selected == null || _selected.IsDungeon || _selected.chapter != _chapter || _selected.difficulty != _difficulty)
            {
                var list = database.InChapter(_chapter, _difficulty);
                _selected = list.Count > 0 ? list[0] : null;
            }
            RefreshHighlights();
            RefreshDetail();
        }

        private void RebuildTabs(List<int> chapters)
        {
            Clear(_modeTabs);
            Clear(_tabs);

            // 난이도
            foreach (var d in Difficulties.All)
            {
                var dd = d;
                if (database.Ordered(d).Count == 0) continue;
                bool open = progress == null || progress.IsDifficultyUnlocked(database, d);
                bool on = !_dungeonMode && d == _difficulty;
                var col = Difficulties.ColorOf(d);
                string label = open ? Difficulties.Name(d) : $"{Difficulties.Name(d)} <size=70%>({database.UnlockChapterOf(d)}챕터 클리어)</size>";
                var b = UIFactory.AddButton("Diff", _modeTabs, label, 20, font,
                    on ? new Color(col.r, col.g, col.b, 0.95f) : new Color(col.r, col.g, col.b, open ? 0.3f : 0.1f), Color.white);
                var le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = open ? 140 : 230; le.preferredHeight = 44;
                b.interactable = open;
                b.onClick.AddListener(() =>
                {
                    _dungeonMode = false;
                    if (_difficulty != dd) { _difficulty = dd; _selected = null; JumpToFrontierIn(dd); }
                    Refresh();
                });
            }

            // 두 번째 줄: 메인 = 챕터 번호(1~15), 특수 던전 = 던전 종류
            if (_dungeonMode)
                foreach (var k in SpecialDungeons.All)
                {
                    var kk = k;
                    var ofKind = database.Dungeons(k);
                    if (ofKind.Count == 0) continue;
                    bool open = ofKind.Exists(d => progress == null || progress.IsUnlocked(d, database));
                    bool on = k == _dungeonKind;
                    var col = SpecialDungeons.ColorOf(k);
                    var b = UIFactory.AddButton("Kind", _tabs, open ? SpecialDungeons.Name(k) : $"{SpecialDungeons.Name(k)} <size=70%>(잠김)</size>", 19, font,
                        on ? new Color(col.r, col.g, col.b, 0.95f) : new Color(col.r, col.g, col.b, open ? 0.3f : 0.1f), Color.white);
                    var le = b.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = 210; le.preferredHeight = 40;
                    b.onClick.AddListener(() => { _dungeonKind = kk; _selected = null; Refresh(); });
                }
            if (!_dungeonMode)
                foreach (int ch in chapters)
                {
                    int c = ch;
                    var first = database.InChapter(ch, _difficulty);
                    bool open = first.Count > 0 && (progress == null || progress.IsUnlocked(first[0], database));
                    bool on = ch == _chapter;
                    var cleared = progress != null && progress.IsChapterCleared(database, ch, _difficulty);
                    var b = UIFactory.AddButton("Tab", _tabs, ch.ToString(), 19, font,
                        on ? new Color(0.78f, 0.58f, 0.22f, 0.95f)
                           : (cleared ? new Color(1f, 0.83f, 0.42f, 0.28f) : new Color(1, 1, 1, open ? 0.12f : 0.04f)),
                        open ? Color.white : new Color(0.45f, 0.45f, 0.5f));
                    var le = b.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = 58; le.preferredHeight = 40;
                    b.interactable = open;
                    b.onClick.AddListener(() => { _chapter = c; _selected = null; Refresh(); });
                }

            var dungeons = database.Dungeons();
            if (dungeons.Count == 0) return;
            var spacer = UIFactory.NewRect("Spacer", _modeTabs);
            spacer.gameObject.AddComponent<LayoutElement>().preferredWidth = 40;
            int openCount = dungeons.FindAll(d => progress == null || progress.IsUnlocked(d, database)).Count;
            var db = UIFactory.AddButton("DungeonTab", _modeTabs, "특수 던전", 20, font,
                _dungeonMode ? new Color(0.55f, 0.35f, 0.85f, 0.95f) : new Color(0.55f, 0.35f, 0.85f, openCount > 0 ? 0.35f : 0.12f), Color.white);
            var dle = db.gameObject.AddComponent<LayoutElement>();
            dle.preferredWidth = 230; dle.preferredHeight = 44;
            db.onClick.AddListener(() => { _dungeonMode = true; _selected = null; Refresh(); });
        }

        private void RebuildNodes()
        {
            Clear(_nodes);
            _nodeHL.Clear();
            _grid.constraintCount = 3;
            if (_dungeonMode && _dungeonKind == StageKind.EquipmentDungeon)
            {
                // 세트 묶음 5줄 × 1~5단계
                _grid.constraintCount = 5;
                _grid.cellSize = new Vector2(186, 118); _grid.spacing = new Vector2(17, 14);
                foreach (var s in database.Dungeons(_dungeonKind)) BuildNode(s);
            }
            else if (_dungeonMode && _dungeonKind == StageKind.AscensionDungeon)
            {
                // 5속성 × 3등급 → 작은 칸
                _grid.cellSize = new Vector2(300, 118); _grid.spacing = new Vector2(30, 14);
                foreach (var s in database.Dungeons(_dungeonKind)) BuildNode(s);
            }
            else if (_dungeonMode)
            {
                _grid.cellSize = new Vector2(300, 170); _grid.spacing = new Vector2(30, 30);
                foreach (var s in database.Dungeons(_dungeonKind)) BuildNode(s);
            }
            else
            {
                _grid.cellSize = new Vector2(300, 170); _grid.spacing = new Vector2(30, 30);
                foreach (var s in database.InChapter(_chapter, _difficulty)) BuildNode(s);
            }
        }

        private void BuildNode(StageData s)
        {
            bool unlocked = progress == null || progress.IsUnlocked(s, database);
            bool cleared = progress != null && progress.IsCleared(s);
            int stars = progress != null ? progress.StarsOf(s) : 0;

            var node = UIFactory.NewRect("Node", _nodes);

            var hl = UIFactory.AddImage("HL", node, new Color(0.3f, 0.8f, 1f, 0f));
            UIFactory.Stretch(hl.rectTransform, -4, -4, -4, -4);
            _nodeHL[s] = hl;

            Color frameCol = s.isBoss ? new Color(0.85f, 0.32f, 0.36f) : (cleared ? Gold : new Color(0.45f, 0.5f, 0.62f));
            if (s.IsDungeon)
                frameCol = (s.IsAscensionDungeon ? Traits.ColorOf(s.dungeonElement) : SpecialDungeons.ColorOf(s.kind))
                           * new Color(1, 1, 1, cleared ? 1f : 0.75f);
            if (!unlocked) frameCol = new Color(0.25f, 0.26f, 0.3f);
            var frame = UIFactory.AddImage("Frame", node, frameCol, null, raycast: true);
            UIFactory.Stretch(frame.rectTransform);
            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = frame;
            var ss = s;
            btn.onClick.AddListener(() => { _selected = ss; RefreshHighlights(); RefreshDetail(); });

            var inner = UIFactory.AddImage("Inner", frame.transform, unlocked ? new Color(0.11f, 0.12f, 0.16f, 1f) : new Color(0.07f, 0.07f, 0.09f, 1f));
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);

            if (s.IsDungeon) { BuildDungeonNodeText(inner.transform, s, unlocked, stars); return; }

            var code = UIFactory.AddText("Code", inner.transform, s.isBoss ? $"{s.Code}  <size=60%><color=#FF8A8A>BOSS</color></size>" : s.Code,
                34, font, unlocked ? Color.white : new Color(0.5f, 0.5f, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);
            var crt = code.rectTransform;
            UIFactory.Anchor(crt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            crt.sizeDelta = new Vector2(0, 44); crt.anchoredPosition = new Vector2(0, -12);

            var nm = UIFactory.AddText("Name", inner.transform, s.stageName, 18, font,
                unlocked ? new Color(0.85f, 0.88f, 0.95f) : new Color(0.45f, 0.45f, 0.5f), TextAlignmentOptions.Center);
            var nrt = nm.rectTransform;
            UIFactory.Anchor(nrt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            nrt.sizeDelta = new Vector2(-12, 26); nrt.anchoredPosition = new Vector2(0, -58);

            string bottom = unlocked ? StageRunner.Stars(stars) : "<color=#6B7280>잠김</color>";
            var st = UIFactory.AddText("Stars", inner.transform, bottom, unlocked ? 26 : 18, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var srt = st.rectTransform;
            UIFactory.Anchor(srt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            srt.sizeDelta = new Vector2(0, 34); srt.anchoredPosition = new Vector2(0, 10);
        }

        /// <summary>던전 칸: '화염 · 초급' / 재료 이름 · N 돌파 / 별 또는 개방 조건.</summary>
        private void BuildDungeonNodeText(Transform inner, StageData s, bool unlocked, int stars)
        {
            var dim = new Color(0.5f, 0.5f, 0.55f);
            bool asc = s.IsAscensionDungeon;
            string head = asc
                ? (unlocked ? $"<color={Traits.Hex(s.dungeonElement)}>{Traits.Name(s.dungeonElement)}</color>" : Traits.Name(s.dungeonElement))
                : (unlocked ? $"<color={SpecialDungeons.Hex(s.kind)}>{s.Code}</color>" : s.Code);   // 종류는 위 탭에 있으므로 단계만
            bool equip = s.kind == StageKind.EquipmentDungeon;
            if (equip && s.equipmentSets != null && s.equipmentSets.Count >= 2)
                head = $"{EquipSets.Short(s.equipmentSets[0])}·{EquipSets.Short(s.equipmentSets[1])}  " + head;
            var top = UIFactory.AddText("Code", inner, asc ? $"{head}  ·  {s.Code}" : head, asc ? 26 : (equip ? 22 : 32), font, unlocked ? Color.white : dim,
                TextAlignmentOptions.Center, FontStyles.Bold);
            top.enableAutoSizing = true; top.fontSizeMin = 14; top.fontSizeMax = asc ? 26 : 32;
            var trt = top.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            trt.sizeDelta = new Vector2(0, 34); trt.anchoredPosition = new Vector2(0, asc ? -8 : -16);

            string sub = asc ? $"{Materials.Name(s.DungeonMaterial)}  <size=80%>· {Materials.TierTargetLevel(s.dungeonTier)} 돌파</size>"
                : s.kind == StageKind.GoldDungeon ? $"금화 {s.gold:N0}  <size=80%>· 권장 Lv.{s.recommendedLevel}</size>"
                : s.kind == StageKind.EquipmentDungeon ? $"최대 {EquipStats.Stars(s.MaxEquipRarity)}  <size=80%>· Lv.{s.recommendedLevel}</size>"
                : s.kind == StageKind.ExpDungeon ? $"EXP {ExpTotal(s):N0}  <size=80%>· 권장 Lv.{s.recommendedLevel}</size>"
                : $"교본 ×{BookCount(s)}  <size=80%>· 권장 Lv.{s.recommendedLevel}</size>";
            var mat = UIFactory.AddText("Mat", inner, sub,
                17, font, unlocked ? new Color(0.85f, 0.88f, 0.95f) : new Color(0.45f, 0.45f, 0.5f), TextAlignmentOptions.Center);
            mat.enableAutoSizing = true; mat.fontSizeMin = 12; mat.fontSizeMax = 17;
            var mrt = mat.rectTransform;
            UIFactory.Anchor(mrt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            mrt.sizeDelta = new Vector2(-12, 24); mrt.anchoredPosition = new Vector2(0, asc ? -44 : -62);

            string bottom = unlocked ? StageRunner.Stars(stars)
                : $"<color=#6B7280>{(s.unlockAfter != null ? s.unlockAfter.Code + " 클리어 시 개방" : "잠김")}</color>";
            var st = UIFactory.AddText("Stars", inner, bottom, unlocked ? 22 : 16, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var srt = st.rectTransform;
            UIFactory.Anchor(srt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            srt.sizeDelta = new Vector2(0, 28); srt.anchoredPosition = new Vector2(0, 8);
        }

        private static int BookCount(StageData s)
        {
            int n = 0;
            if (s.rewards != null) foreach (var m in s.rewards) if (m != null && Materials.IsSkillBook(m.type)) n += m.count;
            return n;
        }

        private static int ExpTotal(StageData s)
        {
            int n = 0;
            if (s.rewards != null) foreach (var m in s.rewards) if (m != null) n += m.count * Materials.ExpValue(m.type);
            return n;
        }

        private void RefreshHighlights()
        {
            foreach (var kv in _nodeHL)
                kv.Value.color = new Color(0.3f, 0.8f, 1f, kv.Key == _selected ? 0.9f : 0f);
        }

        private void RefreshDetail()
        {
            bool has = _selected != null;
            _detPanel.SetActive(has);
            if (!has) return;

            var s = _selected;
            bool unlocked = progress == null || progress.IsUnlocked(s, database);
            bool cleared = progress != null && progress.IsCleared(s);
            int best = progress != null ? progress.StarsOf(s) : 0;

            _detTitle.text = s.IsAscensionDungeon
                ? $"<color={Traits.Hex(s.dungeonElement)}>{Traits.Name(s.dungeonElement)}</color> 돌파 던전  <size=70%>{s.Code}</size>"
                : s.IsDungeon
                ? $"<color={SpecialDungeons.Hex(s.kind)}>{SpecialDungeons.Name(s.kind)}</color>  <size=70%>{s.Code}</size>"
                : $"{s.Code}  {s.stageName}" + (s.isBoss ? "  <size=60%><color=#FF8A8A>BOSS</color></size>" : "");

            var sb = new StringBuilder();
            if (s.IsDungeon)
            {
                if (s.IsAscensionDungeon)
                    sb.Append($"<color=#AEB6C8>{Traits.Name(s.dungeonElement)} 속성 캐릭터의 <b>{Materials.TierTargetLevel(s.dungeonTier)} 돌파</b> 재료 ")
                      .Append(Materials.RichName(s.DungeonMaterial)).Append("<color=#AEB6C8> 획득 · 반복 도전 가능</color>\n");
                else
                    sb.Append($"<color=#AEB6C8>{SpecialDungeons.Summary(s.kind)} · 반복 도전 가능</color>\n");
                if (s.kind == StageKind.EquipmentDungeon && s.equipmentRarityWeights != null)
                {
                    int total = 0;
                    foreach (var w in s.equipmentRarityWeights) total += w;
                    var rates = new List<string>();
                    for (int i = 0; i < s.equipmentRarityWeights.Count; i++)
                        if (s.equipmentRarityWeights[i] > 0 && total > 0)
                            rates.Add($"{EquipStats.Stars(3 + i)} {Grades.Tag(3 + i)} {Mathf.RoundToInt(100f * s.equipmentRarityWeights[i] / total)}%");
                    sb.Append("<color=#FFD36B>드롭 성급</color>  ").Append(string.Join("  ·  ", rates))
                      .Append("  <size=80%><color=#9AA3B5>(최대 강화 3★ +9 · 4★ +12 · 5★ +16 · 6★ +20)</color></size>\n");
                }
                if (s.kind == StageKind.EquipmentDungeon && s.equipmentSets != null)
                    foreach (var id in s.equipmentSets)
                    {
                        var set = EquipSets.Get(id);
                        if (set == null) continue;
                        sb.Append($"<b>{EquipSets.Rich(id)}</b>  <size=85%>2세트 {set.desc2} / 4세트 {set.desc4}</size>\n");
                    }
                if (!unlocked && s.unlockAfter != null)
                    sb.Append($"<color=#FF8A8A>개방 조건: 스테이지 {s.unlockAfter.Code} {s.unlockAfter.stageName} 클리어</color>\n");
                sb.Append("\n");
            }
            else if (!string.IsNullOrEmpty(s.description)) sb.Append("<color=#AEB6C8>").Append(s.description).Append("</color>\n\n");

            if (!s.IsDungeon && s.difficulty != StageDifficulty.Easy)
                sb.Append($"<color={Difficulties.Hex(s.difficulty)}>{Difficulties.Name(s.difficulty)} 난이도</color>  " +
                          $"<size=85%>적 능력치 +{Mathf.RoundToInt((s.enemyStatMultiplier - 1f) * 100)}%</size>\n");
            sb.Append($"<color=#FFD36B>권장 레벨</color>  Lv.{s.recommendedLevel}");
            if (cleared) sb.Append($"      <color=#FFD36B>최고 기록</color>  {StageRunner.Stars(best)}  <size=80%>({progress.ClearsOf(s)}회 클리어)</size>");
            sb.Append("\n\n<color=#FFD36B>적 구성</color>\n");
            if (s.enemies != null)
                foreach (var e in s.enemies)
                {
                    if (e == null || e.unit == null) continue;
                    sb.Append("  · ").Append(e.unit.DisplayName).Append($"  Lv.{e.level}");
                    if (e.unit.weaknesses != null && e.unit.weaknesses.Count > 0)
                    {
                        sb.Append("   <size=85%>약점 ");
                        for (int i = 0; i < e.unit.weaknesses.Count; i++)
                        {
                            var w = e.unit.weaknesses[i];
                            if (i > 0) sb.Append(' ');
                            sb.Append($"<color={Traits.Hex(w)}>{Traits.Name(w)}</color>");
                        }
                        sb.Append("</size>");
                    }
                    sb.Append('\n');
                }

            sb.Append("\n<color=#FFD36B>별 조건</color>\n");
            sb.Append($"  ★ 클리어\n  ★ 아군 전원 생존\n  ★ 아군 행동 {s.turnLimit}회 이내 클리어\n");

            sb.Append("\n<color=#FFD36B>보상</color>\n");
            if (s.firstClearCrystals > 0)
                sb.Append(cleared
                    ? $"  <color=#6B7280>첫 클리어  크리스탈 {s.firstClearCrystals}  (획득 완료)</color>\n"
                    : $"  첫 클리어  <color=#8FE9FF>크리스탈 {s.firstClearCrystals}</color>\n");
            var parts = new List<string>();
            if (s.gold > 0) parts.Add($"<color=#FFD36B>금화 {s.gold:N0}</color>");
            if (s.rewards != null)
                foreach (var m in s.rewards)
                    if (m != null && m.count > 0) parts.Add($"{Materials.RichName(m.type)} ×{m.count}");
            if (s.characterExp > 0) parts.Add($"캐릭터 경험치 +{s.characterExp}");
            if (s.equipmentDrops > 0) parts.Insert(0, $"<color=#7FE3D6>장비 ×{s.equipmentDrops}</color>");
            if (parts.Count > 0) sb.Append("  매 클리어  ").Append(string.Join(" · ", parts)).Append('\n');

            if (!s.IsDungeon)
            {
                var opens = database.DungeonsUnlockedBy(s);
                if (opens.Count > 0)
                {
                    // 종류별로 한 줄씩: 돌파 던전은 등급, 금화/교본 던전은 단계
                    var shown = new HashSet<StageKind>();
                    sb.Append("\n<color=#B48CFF>클리어 시 개방</color>");
                    foreach (var d in opens)
                    {
                        if (!shown.Add(d.kind)) continue;
                        sb.Append("\n  · ").Append($"<color={SpecialDungeons.Hex(d.kind)}>{SpecialDungeons.Name(d.kind)}</color> ");
                        sb.Append(d.IsAscensionDungeon
                            ? $"{Materials.TierName(d.dungeonTier)} <size=85%>({Materials.TierTargetLevel(d.dungeonTier)} 돌파 재료 · 전 속성)</size>"
                            : d.Code);
                    }
                    sb.Append("\n");
                }
            }

            _detBody.text = sb.ToString();

            _sortieBtn.interactable = unlocked;
            _sortieLbl.text = unlocked ? "편성 후 출격"
                : (s.IsDungeon && s.unlockAfter != null ? $"{s.unlockAfter.Code} 클리어 시 개방" : "이전 스테이지를 클리어하세요");
        }

        // ---------------- UI 구성 ----------------

        private void BuildUI()
        {
            var cgo = UIFactory.NewRect("StageCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _root = UIFactory.AddImage("Root", canvas.transform, new Color(0.05f, 0.06f, 0.09f, 1f), null, raycast: true).gameObject;
            UIFactory.Stretch((RectTransform)_root.transform);
            BackgroundArt.Create(_root.transform, 0.7f).Set("Backgrounds/stage");

            var title = UIFactory.AddText("Title", _root.transform, "스테이지", 40, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = title.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            trt.sizeDelta = new Vector2(600, 56); trt.anchoredPosition = new Vector2(0, -30);

            _totalStars = UIFactory.AddText("TotalStars", _root.transform, "", 26, font, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
            var tsrt = _totalStars.rectTransform;
            UIFactory.Anchor(tsrt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            tsrt.sizeDelta = new Vector2(300, 40); tsrt.anchoredPosition = new Vector2(-50, -36);

            // 위: 난이도 + 돌파 던전 · 아래: 챕터 번호
            _modeTabs = TabRow("ModeTabs", -98, 44, 10);
            _tabs = TabRow("Tabs", -150, 40, 6);

            _chapterTitle = UIFactory.AddText("Chapter", _root.transform, "", 30, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            var chrt = _chapterTitle.rectTransform;
            UIFactory.Anchor(chrt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            chrt.sizeDelta = new Vector2(1000, 44); chrt.anchoredPosition = new Vector2(-380, -200);

            // 스테이지 노드(3열)
            _nodes = UIFactory.NewRect("Nodes", _root.transform);
            UIFactory.Anchor(_nodes, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            _nodes.sizeDelta = new Vector2(1000, 700); _nodes.anchoredPosition = new Vector2(-380, -256);
            var gl = _nodes.gameObject.AddComponent<GridLayoutGroup>();
            _grid = gl;
            gl.cellSize = new Vector2(300, 170); gl.spacing = new Vector2(30, 30);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 3;
            gl.childAlignment = TextAnchor.UpperLeft;

            BuildDetail();

            _backBtn = UIFactory.AddButton("BackBtn", _root.transform, "← 홈", 20, font, new Color(1, 1, 1, 0.14f), Color.white);
            var bkrt = (RectTransform)_backBtn.transform;
            UIFactory.Anchor(bkrt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            bkrt.sizeDelta = new Vector2(150, 44); bkrt.anchoredPosition = new Vector2(30, -30);
            _backBtn.onClick.AddListener(() => _onBack?.Invoke());
            _backBtn.gameObject.SetActive(false);
        }

        private void BuildDetail()
        {
            var panel = UIFactory.AddImage("Detail", _root.transform, new Color(0.10f, 0.10f, 0.14f, 0.96f));
            _detPanel = panel.gameObject;
            var prt = panel.rectTransform;
            UIFactory.Anchor(prt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            prt.sizeDelta = new Vector2(640, 860); prt.anchoredPosition = new Vector2(560, -40);

            _detTitle = UIFactory.AddText("Title", panel.transform, "", 34, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = _detTitle.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            trt.sizeDelta = new Vector2(-40, 50); trt.anchoredPosition = new Vector2(0, -24);

            _detBody = UIFactory.AddText("Body", panel.transform, "", 21, font, Color.white, TextAlignmentOptions.TopLeft);
            var brt = _detBody.rectTransform;
            UIFactory.Stretch(brt, 36, 120, 36, 90);
            _detBody.lineSpacing = 6f;
            _detBody.enableAutoSizing = true;
            _detBody.fontSizeMin = 14; _detBody.fontSizeMax = 21;

            _sortieBtn = UIFactory.AddButton("Sortie", panel.transform, "", 28, font, new Color(0.85f, 0.45f, 0.25f, 0.95f), Color.white);
            var srt = (RectTransform)_sortieBtn.transform;
            UIFactory.Anchor(srt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            srt.sizeDelta = new Vector2(420, 76); srt.anchoredPosition = new Vector2(0, 30);
            _sortieBtn.onClick.AddListener(() => { if (_selected != null) OnSortie?.Invoke(_selected); });
            _sortieLbl = _sortieBtn.GetComponentInChildren<TextMeshProUGUI>();

            _detPanel.SetActive(false);
        }

        private RectTransform TabRow(string name, float y, float h, float spacing)
        {
            var rt = UIFactory.NewRect(name, _root.transform);
            UIFactory.Anchor(rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            rt.sizeDelta = new Vector2(1000, h); rt.anchoredPosition = new Vector2(-380, y);
            var hl = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = spacing; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            return rt;
        }

        /// <summary>난이도를 바꿨을 때 그 난이도의 진행 지점으로 이동.</summary>
        private void JumpToFrontierIn(StageDifficulty d)
        {
            StageData target = null;
            foreach (var s in database.Ordered(d))
            {
                if (progress != null && !progress.IsUnlocked(s, database)) break;
                target = s;
                if (progress == null || !progress.IsCleared(s)) break;
            }
            if (target != null) { _chapter = target.chapter; _selected = target; }
            else _chapter = 1;
        }

        private static void Clear(RectTransform rt)
        {
            for (int i = rt.childCount - 1; i >= 0; i--)
            {
                var ch = rt.GetChild(i);
                ch.SetParent(null, false);
                Destroy(ch.gameObject);
            }
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
