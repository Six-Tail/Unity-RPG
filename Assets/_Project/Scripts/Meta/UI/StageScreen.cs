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
        private RectTransform _tabs, _nodes;
        private GridLayoutGroup _grid;
        private bool _dungeonMode;
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
            if (!s.IsDungeon) _chapter = s.chapter;
            _selected = s;
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
            if (target != null) { _chapter = target.chapter; _selected = target; }
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

            var chapters = database.Chapters();
            if (chapters.Count > 0 && !chapters.Contains(_chapter)) _chapter = chapters[0];

            int total = 0, max = 0;
            foreach (var s in database.Ordered()) { total += progress != null ? progress.StarsOf(s) : 0; max += 3; }
            _totalStars.text = $"<color=#FFD36B>★</color> {total} / {max}";

            if (_dungeonMode)
            {
                _chapterTitle.text = "돌파 던전  <size=60%><color=#AEB6C8>캐릭터 속성의 돌파 재료 · 초급 40 / 중급 60 / 상급 80 돌파</color></size>";
            }
            else
            {
                int chStars = progress != null ? progress.TotalStars(database, _chapter) : 0;
                int chMax = database.InChapter(_chapter).Count * 3;
                _chapterTitle.text = $"챕터 {_chapter}  ·  {database.ChapterName(_chapter)}    <size=70%><color=#FFD36B>★</color> {chStars} / {chMax}</size>";
            }

            RebuildTabs(chapters);
            RebuildNodes();
            if (_dungeonMode)
            {
                if (_selected == null || !_selected.IsDungeon)
                {
                    var list = database.Dungeons();
                    _selected = list.Find(d => progress == null || progress.IsUnlocked(d, database));
                    if (_selected == null && list.Count > 0) _selected = list[0];
                }
            }
            else if (_selected == null || _selected.IsDungeon || _selected.chapter != _chapter)
            {
                var list = database.InChapter(_chapter);
                _selected = list.Count > 0 ? list[0] : null;
            }
            RefreshHighlights();
            RefreshDetail();
        }

        private void RebuildTabs(List<int> chapters)
        {
            Clear(_tabs);
            foreach (int ch in chapters)
            {
                int c = ch;
                var first = database.InChapter(ch);
                bool open = first.Count > 0 && (progress == null || progress.IsUnlocked(first[0], database));
                bool on = !_dungeonMode && ch == _chapter;
                var b = UIFactory.AddButton("Tab", _tabs, open ? $"챕터 {ch}" : $"챕터 {ch} (잠김)", 20, font,
                    on ? new Color(0.78f, 0.58f, 0.22f, 0.95f) : new Color(1, 1, 1, open ? 0.12f : 0.05f), Color.white);
                var le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 190; le.preferredHeight = 44;
                b.interactable = open;
                b.onClick.AddListener(() => { _dungeonMode = false; _chapter = c; _selected = null; Refresh(); });
            }

            var dungeons = database.Dungeons();
            if (dungeons.Count == 0) return;
            int openCount = dungeons.FindAll(d => progress == null || progress.IsUnlocked(d, database)).Count;
            var db = UIFactory.AddButton("DungeonTab", _tabs, openCount > 0 ? "돌파 던전" : "돌파 던전 (잠김)", 20, font,
                _dungeonMode ? new Color(0.55f, 0.35f, 0.85f, 0.95f) : new Color(0.55f, 0.35f, 0.85f, openCount > 0 ? 0.35f : 0.12f), Color.white);
            var dle = db.gameObject.AddComponent<LayoutElement>();
            dle.preferredWidth = 230; dle.preferredHeight = 44;
            db.onClick.AddListener(() => { _dungeonMode = true; _selected = null; Refresh(); });
        }

        private void RebuildNodes()
        {
            Clear(_nodes);
            _nodeHL.Clear();
            if (_dungeonMode)
            {
                // 5속성 × 3등급 → 작은 칸
                _grid.cellSize = new Vector2(300, 118); _grid.spacing = new Vector2(30, 14);
                foreach (var s in database.Dungeons()) BuildNode(s);
            }
            else
            {
                _grid.cellSize = new Vector2(300, 170); _grid.spacing = new Vector2(30, 30);
                foreach (var s in database.InChapter(_chapter)) BuildNode(s);
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
            if (s.IsDungeon) frameCol = Traits.ColorOf(s.dungeonElement) * new Color(1, 1, 1, cleared ? 1f : 0.75f);
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
            string el = unlocked ? $"<color={Traits.Hex(s.dungeonElement)}>{Traits.Name(s.dungeonElement)}</color>" : Traits.Name(s.dungeonElement);
            var top = UIFactory.AddText("Code", inner, $"{el}  ·  {s.Code}", 26, font, unlocked ? Color.white : dim,
                TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = top.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            trt.sizeDelta = new Vector2(0, 34); trt.anchoredPosition = new Vector2(0, -8);

            var mat = UIFactory.AddText("Mat", inner, $"{Materials.Name(s.DungeonMaterial)}  <size=80%>· {Materials.TierTargetLevel(s.dungeonTier)} 돌파</size>",
                17, font, unlocked ? new Color(0.85f, 0.88f, 0.95f) : new Color(0.45f, 0.45f, 0.5f), TextAlignmentOptions.Center);
            var mrt = mat.rectTransform;
            UIFactory.Anchor(mrt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            mrt.sizeDelta = new Vector2(-12, 24); mrt.anchoredPosition = new Vector2(0, -44);

            string bottom = unlocked ? StageRunner.Stars(stars)
                : $"<color=#6B7280>{(s.unlockAfter != null ? s.unlockAfter.Code + " 클리어 시 개방" : "잠김")}</color>";
            var st = UIFactory.AddText("Stars", inner, bottom, unlocked ? 22 : 16, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var srt = st.rectTransform;
            UIFactory.Anchor(srt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0));
            srt.sizeDelta = new Vector2(0, 28); srt.anchoredPosition = new Vector2(0, 8);
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

            _detTitle.text = s.IsDungeon
                ? $"<color={Traits.Hex(s.dungeonElement)}>{Traits.Name(s.dungeonElement)}</color> 돌파 던전  <size=70%>{s.Code}</size>"
                : $"{s.Code}  {s.stageName}" + (s.isBoss ? "  <size=60%><color=#FF8A8A>BOSS</color></size>" : "");

            var sb = new StringBuilder();
            if (s.IsDungeon)
            {
                sb.Append($"<color=#AEB6C8>{Traits.Name(s.dungeonElement)} 속성 캐릭터의 <b>{Materials.TierTargetLevel(s.dungeonTier)} 돌파</b> 재료 ")
                  .Append(Materials.RichName(s.DungeonMaterial)).Append("<color=#AEB6C8> 획득 · 반복 도전 가능</color>\n");
                if (!unlocked && s.unlockAfter != null)
                    sb.Append($"<color=#FF8A8A>개방 조건: 스테이지 {s.unlockAfter.Code} {s.unlockAfter.stageName} 클리어</color>\n");
                sb.Append("\n");
            }
            else if (!string.IsNullOrEmpty(s.description)) sb.Append("<color=#AEB6C8>").Append(s.description).Append("</color>\n\n");

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
            if (parts.Count > 0) sb.Append("  매 클리어  ").Append(string.Join(" · ", parts)).Append('\n');

            if (!s.IsDungeon)
            {
                var opens = database.DungeonsUnlockedBy(s);
                if (opens.Count > 0)
                    sb.Append("\n<color=#B48CFF>클리어 시 개방</color>  돌파 던전 ")
                      .Append(Materials.TierName(opens[0].dungeonTier))
                      .Append($" <size=85%>({Materials.TierTargetLevel(opens[0].dungeonTier)} 돌파 재료 · 전 속성)</size>\n");
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

            var title = UIFactory.AddText("Title", _root.transform, "스테이지", 40, font, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            var trt = title.rectTransform;
            UIFactory.Anchor(trt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            trt.sizeDelta = new Vector2(600, 56); trt.anchoredPosition = new Vector2(0, -30);

            _totalStars = UIFactory.AddText("TotalStars", _root.transform, "", 26, font, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
            var tsrt = _totalStars.rectTransform;
            UIFactory.Anchor(tsrt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            tsrt.sizeDelta = new Vector2(300, 40); tsrt.anchoredPosition = new Vector2(-50, -36);

            // 챕터 탭
            _tabs = UIFactory.NewRect("Tabs", _root.transform);
            UIFactory.Anchor(_tabs, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            _tabs.sizeDelta = new Vector2(1000, 44); _tabs.anchoredPosition = new Vector2(-380, -104);
            var th = _tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            th.spacing = 10; th.childAlignment = TextAnchor.MiddleLeft;
            th.childControlWidth = true; th.childControlHeight = true;
            th.childForceExpandWidth = false; th.childForceExpandHeight = false;

            _chapterTitle = UIFactory.AddText("Chapter", _root.transform, "", 30, font, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            var chrt = _chapterTitle.rectTransform;
            UIFactory.Anchor(chrt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            chrt.sizeDelta = new Vector2(1000, 44); chrt.anchoredPosition = new Vector2(-380, -166);

            // 스테이지 노드(3열)
            _nodes = UIFactory.NewRect("Nodes", _root.transform);
            UIFactory.Anchor(_nodes, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            _nodes.sizeDelta = new Vector2(1000, 700); _nodes.anchoredPosition = new Vector2(-380, -230);
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
