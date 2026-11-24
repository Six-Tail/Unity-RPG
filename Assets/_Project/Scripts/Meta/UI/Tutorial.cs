using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Meta.Missions;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>튜토리얼이 가리킬 화면 요소(버튼 등) 등록소. 화면이 만들 때 Set, 튜토리얼이 Get.</summary>
    public static class TutorialTargets
    {
        private static readonly Dictionary<string, RectTransform> Map = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Map.Clear();

        public static void Set(string key, Component c)
        {
            if (c != null) Map[key] = c.transform as RectTransform;
        }

        /// <summary>지금 화면에 보이는 대상(없거나 숨겨져 있으면 null).</summary>
        public static RectTransform Get(string key)
        {
            if (string.IsNullOrEmpty(key) || !Map.TryGetValue(key, out var rt)) return null;
            return rt != null && rt.gameObject.activeInHierarchy ? rt : null;
        }
    }

    /// <summary>
    /// 신규 지휘관 튜토리얼 + 기능 개방 안내.
    /// · 처음: 환영 → 스테이지 → 1-1 출격(전투 기본 설명) → 얻은 경험치 물약으로 육성 화면에서 레벨업 → 완료 보상(우편)
    ///   화면을 막지 않고, 눌러야 할 곳을 금색 테두리로 표시하고 말풍선으로 안내합니다('건너뛰기' 가능).
    /// · 그 뒤: 새 기능이 열릴 때마다 알림(1장 = 특수 던전, 2장 = 장비 던전, 3장 = 돌파 던전 · 결투장 · 주간 보스, 하드 · 극악 난이도)
    /// · 처음 들어간 화면마다 한 번씩 간단한 설명(가챠 · 임무 · 상점 · 결투장 · 주간 보스 · 길드 · 친구)
    /// · 홈에 들어왔을 때 오늘 출석을 안 했으면 출석부를 한 번 자동으로 엶(튜토리얼이 끝난 뒤부터)
    /// 진행 상황: PlayerPrefs "tutorial.step"(0 환영 · 1 첫 전투 · 2 레벨업 · 100 완료) · "tutorial.flag.*".
    /// 씬에 내비게이터가 있으면 스스로 만들어집니다.
    /// </summary>
    public class Tutorial : MonoBehaviour
    {
        public const int Done = 100;
        private const string StepKey = "tutorial.step";

        private static bool Flag(string k) => PlayerPrefs.GetInt("tutorial.flag." + k, 0) == 1;
        private static void SetFlag(string k) { PlayerPrefs.SetInt("tutorial.flag." + k, 1); PlayerPrefs.Save(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<ScreenNavigator>() == null || FindAnyObjectByType<Tutorial>() != null) return;
            new GameObject("Tutorial").AddComponent<Tutorial>();
        }

        private ScreenNavigator _nav;
        private PlayerRoster _roster;
        private StageProgress _progress;
        private StageScreen _stageScreen;
        private PlayerInventory _inventory;
        private PlayerWallet _wallet;
        private int _step;
        private bool _ready, _grew, _first = true;
        private float _tick, _readyWait = 0.6f;
        private HubScreen _last;
        private float _unlockWait;
        private StoryScreen _story;
        private SubcultureRPG.Battle.BattleController _battle;

        private void Awake()
        {
            _nav = FindAnyObjectByType<ScreenNavigator>();
            _roster = FindAnyObjectByType<PlayerRoster>();
            _progress = FindAnyObjectByType<StageProgress>();
            _stageScreen = FindAnyObjectByType<StageScreen>();
            _inventory = FindAnyObjectByType<PlayerInventory>();
            _wallet = FindAnyObjectByType<PlayerWallet>();
            _battle = FindAnyObjectByType<SubcultureRPG.Battle.BattleController>();
            _step = PlayerPrefs.GetInt(StepKey, 0);
            MissionEvents.OnProgress += HandleMission;
            BuildGuide();
        }

        private void OnDestroy()
        {
            MissionEvents.OnProgress -= HandleMission;
            if (_paused) Time.timeScale = 1f;
        }

        private void HandleMission(MissionGoal goal, int amount)
        {
            if (goal == MissionGoal.Growth && _step == 2) _grew = true;   // 육성 화면에서 레벨업 등 성장 행동을 함
        }

        private void SetStep(int step)
        {
            _step = step;
            PlayerPrefs.SetInt(StepKey, step);
            PlayerPrefs.Save();
        }

        private StageDatabase Db => _stageScreen != null ? _stageScreen.Database : null;

        private bool FirstStageCleared()
        {
            var db = Db;
            if (db == null || _progress == null) return false;
            var list = db.Ordered(StageDifficulty.Easy);
            return list.Count > 0 && _progress.IsCleared(list[0]);
        }

        // ---------------- 진행 ----------------

        private void Update()
        {
            if (_nav == null) return;
            _tick -= Time.unscaledDeltaTime;
            bool tick = _tick <= 0f;
            if (tick) _tick = 0.25f;

            if (!_ready)
            {
                // 타이틀 화면 · 이름 정하기가 끝날 때까지 기다림
                _readyWait -= Time.unscaledDeltaTime;
                if (!tick || _readyWait > 0f) return;
                if (FindAnyObjectByType<TitleScreen>() != null || !PlayerProfile.Instance.HasName) return;
                _ready = true;
            }

            var cur = _nav.Current;
            if (_story == null && (tick || cur == HubScreen.Battle)) _story = FindAnyObjectByType<StoryScreen>();
            if (Popups.IsOpen || OverlayWindow.AnyOpen || (_story != null && _story.IsPlaying)) { HideGuide(); return; }
            bool entered = _first || cur != _last;
            _first = false;
            _last = cur;

            switch (_step)
            {
                case 0: StepWelcome(cur); break;
                case 1: StepFirstBattle(cur, tick); break;
                case 2: StepLevelUp(cur); break;
                default: HideGuide(); break;
            }
            if (Popups.IsOpen) return;

            if (_step >= 1 && entered) ShowTip(cur);
            _unlockWait -= Time.unscaledDeltaTime;
            if (_step >= 2 && (cur == HubScreen.Home || cur == HubScreen.Stage) && (entered || _unlockWait <= 0f))
            {
                _unlockWait = 1f;
                CheckUnlocks();
                if (Popups.IsOpen) return;
            }
            if (_step >= Done && tick && cur == HubScreen.Home && !Popups.IsOpen && PlayerAttendance.Instance.ShouldAutoOpen)
            {
                PlayerAttendance.Instance.MarkAutoShown();
                AttendanceWindow.Open();
            }
        }

        private void StepWelcome(HubScreen cur)
        {
            HideGuide();
            if (cur != HubScreen.Home) return;
            if (FirstStageCleared())
            {
                // 튜토리얼 도입 전부터 하던 세이브: 조용히 완료 처리(이미 연 기능의 개방 알림도 생략)
                foreach (var u in Unlocks()) if (u.open) SetFlag(u.flag);
                SetStep(Done);
                return;
            }
            Popups.Show("스텔라 앵커에 오신 것을 환영합니다",
                $"<b>{PlayerProfile.Instance.DisplayName}</b> 지휘관님, 반갑습니다.\n\n첫 전투부터 캐릭터 레벨업까지 차근차근 안내해 드릴게요.\n눌러야 할 곳은 <color=#FFD36B>금색 테두리</color>로 표시됩니다.",
                "시작하기", () => SetStep(1), "건너뛰기", Skip, icon: "Title/logo");
        }

        private void Skip()
        {
            SetStep(Done);
            HideGuide();
        }

        private bool _paused;

        private void StepFirstBattle(HubScreen cur, bool tick)
        {
            if (tick && cur != HubScreen.Battle && FirstStageCleared()) { SetStep(2); HideGuide(); return; }
            switch (cur)
            {
                case HubScreen.Home:
                    Guide("먼저 <color=#FFD36B>스테이지</color>에 들어가 첫 전투를 치러 봅시다.", "home.stage");
                    break;
                case HubScreen.Stage:
                    Guide("<color=#FFD36B>1-1</color>이 선택돼 있습니다. 오른쪽에서 적과 보상을 확인하고\n<color=#FFD36B>편성 후 출격</color>을 누르세요.", "stage.sortie");
                    break;
                case HubScreen.Party:
                    Guide("출전할 캐릭터를 눌러 편성한 뒤(최대 4명)\n<color=#FFD36B>출격</color> 버튼을 누르세요.", "party.start");
                    break;
                case HubScreen.Battle:
                    HideGuide();
                    if (!Flag("battle") && _battle != null && _battle.IsRunning)   // 스토리가 끝나고 전투가 실제로 시작된 뒤
                    {
                        SetFlag("battle");
                        _paused = true;
                        Time.timeScale = 0f;   // 설명을 읽는 동안 전투를 멈춤
                        Popups.Show("전투 기본",
                            "· 위쪽 줄이 <color=#FFD36B>행동 순서</color>입니다. 내 캐릭터 차례가 오면 아래에서 기본 공격 · 스킬 · 궁극기를 고르고 적을 누릅니다.\n" +
                            "· 스킬은 <color=#8FE9FF>스킬 포인트</color>를 쓰고, 기본 공격을 하면 스킬 포인트가 찹니다.\n" +
                            "· 적의 <color=#FFD36B>약점 속성</color>으로 때리면 피해가 커지고, 내구도를 다 깎으면 브레이크!\n" +
                            "· 모든 아군과 적이 한 번씩 움직이면 1턴. 정해진 턴 안에 이기면 <color=#FFD36B>별 3개</color>입니다.\n" +
                            "· 오른쪽 위에서 자동 전투와 배속을 켤 수 있습니다.",
                            "전투 시작", () => { _paused = false; Time.timeScale = 1f; });
                    }
                    break;
                default:
                    Guide("홈으로 돌아가 <color=#FFD36B>스테이지</color>에 들어가 보세요.", null);
                    break;
            }
        }

        private void StepLevelUp(HubScreen cur)
        {
            if (_grew) { Complete(); return; }
            if (cur == HubScreen.Battle) { HideGuide(); return; }
            if (!Flag("levelIntro"))
            {
                SetFlag("levelIntro");
                HideGuide();
                EnsureLevelUpSupplies();
                Popups.Show("첫 승리!",
                    "전투에서 <color=#7CFF9A>경험치 물약</color>을 얻었습니다.\n육성 화면에서 물약을 써서 캐릭터 레벨을 올려 봅시다.\n\n레벨이 오르면 체력 · 공격력 · 방어력이 함께 오릅니다.",
                    "육성으로 이동", () => _nav.Go(HubScreen.Growth), "나중에", null, icon: "Items/ExpSmall");
                return;
            }
            switch (cur)
            {
                case HubScreen.Home:
                    Guide("<color=#FFD36B>육성</color> 화면에서 캐릭터 레벨을 올려 봅시다.", "home.growth");
                    break;
                case HubScreen.Growth:
                    Guide("왼쪽에서 캐릭터를 고르고 <color=#FFD36B>레벨</color> 탭에서\n① <color=#FFD36B>자동 선택</color>으로 물약을 고른 뒤  ② <color=#FFD36B>레벨업</color>을 누르세요.", "growth.auto", "growth.confirm");
                    break;
                case HubScreen.Stage:
                    Guide("왼쪽 위 <color=#FFD36B>홈</color> 버튼으로 돌아가 육성 화면을 열어 보세요.", null);
                    break;
                default:
                    HideGuide();
                    break;
            }
        }

        /// <summary>레벨업 연습에 쓸 물약 · 금화가 없으면 조금 채워 줌.</summary>
        private void EnsureLevelUpSupplies()
        {
            if (_inventory != null)
            {
                int n = 0;
                foreach (var m in Materials.ExpMaterials) n += _inventory.Count(m);
                if (n == 0) _inventory.Add(MaterialType.ExpSmall, 5);
            }
            if (_wallet != null && _wallet.Gold < 2000) _wallet.AddGold(3000);
        }

        private void Complete()
        {
            SetStep(Done);
            HideGuide();
            PlayerMail.Instance.Send("튜토리얼 완료 보상", "첫 전투와 첫 레벨업을 마쳤습니다. 앞으로의 여정을 응원합니다!",
                new RewardBundle().Crystals(300).Gold(20000).Mat(MaterialType.ExpPotion, 10));
            Popups.Show("튜토리얼 완료!",
                "기본은 여기까지입니다. <color=#FFD36B>우편함</color>에 완료 보상을 보냈으니 홈 오른쪽 위 우편 아이콘에서 받으세요.\n\n" +
                "스테이지를 진행하면 새 기능이 차례로 열립니다.\n" +
                "<color=#9AD4FF>1장</color> 특수 던전  ·  <color=#9AD4FF>2장</color> 장비 던전  ·  <color=#9AD4FF>3장</color> 돌파 던전 · 결투장 · 주간 보스",
                "확인", icon: "Items/UiMail");
        }

        // ---------------- 기능 개방 알림 ----------------

        private struct Unlock
        {
            public string flag, title, body, icon;
            public bool open;
            public System.Action go;
        }

        private List<Unlock> Unlocks()
        {
            int ch = FeatureGate.ClearedChapters();
            var db = Db;
            var list = new List<Unlock>
            {
                new()
                {
                    flag = "unlock.dungeon", open = ch >= 1, title = "특수 던전 개방", icon = "Items/ExpPotion",
                    body = "<color=#FFD36B>경험치 던전 · 금화 던전 · 스킬 교본 던전</color>이 열렸습니다.\n\n스테이지 화면 위쪽 <b>특수 던전</b> 탭에서 들어갑니다.\n육성에 필요한 재료를 골라서 모을 수 있고, 장을 더 진행하면 높은 단계가 열립니다.",
                    go = () => _nav.OpenDungeon(StageKind.ExpDungeon),
                },
                new()
                {
                    flag = "unlock.equip", open = ch >= 2, title = "장비 던전 개방", icon = "Items/EquipOre",
                    body = "<color=#FFD36B>장비 던전</color>이 열렸습니다.\n\n장비와 장비 강화석을 얻을 수 있습니다. 육성 화면 <b>장비</b> 탭에서 6부위에 착용하고 강화하세요.\n같은 세트를 2개 · 4개 맞추면 세트 효과가 생깁니다.",
                    go = () => _nav.OpenDungeon(StageKind.EquipmentDungeon),
                },
                new()
                {
                    flag = "unlock.asc", open = ch >= 3, title = "돌파 던전 개방", icon = "Items/FireAsc1",
                    body = "<color=#FFD36B>돌파 던전</color>이 열렸습니다.\n\n속성별 돌파 재료를 얻습니다. 육성 화면 <b>돌파</b> 탭에서 캐릭터의 레벨 상한을 올릴 수 있습니다.",
                    go = () => _nav.OpenDungeon(StageKind.AscensionDungeon),
                },
                new()
                {
                    flag = "unlock.arena", open = ch >= FeatureGate.ArenaChapter, title = "결투장 개방", icon = "Items/ArenaCoin",
                    body = "<color=#FFD36B>결투장(PVP)</color>이 열렸습니다.\n\n다른 지휘관의 팀과 겨뤄 랭크 점수를 올리세요. 티어가 오르면 테두리를 받고, 매주 시즌 순위에 따라 보상이 나옵니다.\n도전권은 하루 5장입니다.",
                    go = () => _nav.Go(HubScreen.Arena),
                },
                new()
                {
                    flag = "unlock.raid", open = ch >= FeatureGate.RaidChapter, title = "주간 보스 개방", icon = "Items/RaidIcon",
                    body = "<color=#FFD36B>주간 보스</color>가 열렸습니다.\n\n제한 시간 동안 보스에게 준 피해를 한 주 동안 쌓아 단계별 보상을 받습니다. 약점 속성은 매주 바뀝니다.\n하루 3번 도전할 수 있습니다.",
                    go = () => _nav.Go(HubScreen.Raid),
                },
            };
            if (db != null && _progress != null)
                foreach (var d in new[] { StageDifficulty.Hard, StageDifficulty.Hell })
                {
                    if (db.Ordered(d).Count == 0) continue;
                    list.Add(new Unlock
                    {
                        flag = "unlock.diff" + (int)d, open = _progress.IsDifficultyUnlocked(db, d),
                        title = $"{Difficulties.Name(d)} 난이도 개방", icon = "Items/Crystal",
                        body = $"<color={Difficulties.Hex(d)}>{Difficulties.Name(d)}</color> 난이도가 열렸습니다.\n\n스테이지 화면 위쪽에서 난이도를 바꿀 수 있습니다.\n적이 더 강하지만 보상이 좋고, 첫 클리어 크리스탈과 챕터 별 보상을 다시 받을 수 있습니다.",
                    });
                }
            return list;
        }

        private void CheckUnlocks()
        {
            foreach (var u in Unlocks())
            {
                if (!u.open || Flag(u.flag)) continue;
                SetFlag(u.flag);
                if (u.go != null) Popups.Show(u.title, u.body, "바로 가기", u.go, "확인", null, u.icon);
                else Popups.Show(u.title, u.body, icon: u.icon);
            }
        }

        // ---------------- 처음 들어간 화면 설명 ----------------

        private static readonly Dictionary<HubScreen, (string title, string body, string icon)> Tips = new()
        {
            [HubScreen.Gacha] = ("가챠", "크리스탈이나 뽑기권으로 캐릭터를 소환합니다.\n<color=#FFB3D9>픽업</color> 배너는 대표 캐릭터가 나올 확률이 높고, <color=#B8C8FF>상시</color> 배너는 언제나 열려 있습니다.\n\n이미 가진 캐릭터가 또 나오면 합성 · 초월 재료가 됩니다.", "Items/PickupTicket"),
            [HubScreen.Mission] = ("임무", "<color=#FFD36B>일일 · 주간 임무</color>를 달성하고 보상을 받으세요.\n\n받을 보상이 생기면 홈의 임무 타일에 <color=#FF6B6B>빨간 느낌표</color>가 뜹니다.", "Items/Crystal"),
            [HubScreen.Shop] = ("상점", "금화 · 크리스탈로 재료를 사고, <b>교환소</b>에서 재료를 다른 재료나 뽑기권으로 바꿀 수 있습니다.\n\n품목마다 매일 또는 매주 살 수 있는 수가 정해져 있습니다.", "Items/UiShop"),
            [HubScreen.Guild] = ("길드", "길드에 가입하거나 직접 만들 수 있습니다.\n\n날마다 <color=#FFD36B>출석</color>과 <color=#FFD36B>기부</color>로 금화 · 크리스탈을 받고 길드 레벨을 올리세요.", "Items/GuildIcon"),
            [HubScreen.Friends] = ("친구", "추천 지휘관이나 UID 검색으로 친구를 추가하세요.\n\n친구마다 하루 한 번 <color=#FFD36B>인사</color>를 보내면 행동력과 금화를 받습니다.", "Items/FriendIcon"),
        };

        private void ShowTip(HubScreen screen)
        {
            if (!Tips.TryGetValue(screen, out var tip) || Flag("tip." + screen)) return;
            SetFlag("tip." + screen);
            Popups.Show(tip.title, tip.body, icon: tip.icon);
        }

        // ---------------- 안내 표시(금색 테두리 + 말풍선) ----------------

        private RectTransform _layer, _bubble;
        private readonly RectTransform[] _frames = new RectTransform[2];
        private readonly TextMeshProUGUI[] _frameNum = new TextMeshProUGUI[2];
        private readonly CanvasGroup[] _frameGroup = new CanvasGroup[2];
        private TextMeshProUGUI _bubbleText;
        private GameObject _guideRoot;
        private readonly Vector3[] _corners = new Vector3[4];

        private void BuildGuide()
        {
            var font = OverlayWindow.DefaultFont();
            var cgo = UIFactory.NewRect("TutorialCanvas", transform);
            var canvas = cgo.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 140;   // 화면(70 ~ 120) 위, 창(200 ~) 아래
            var scaler = cgo.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            cgo.gameObject.AddComponent<GraphicRaycaster>();
            _guideRoot = cgo.gameObject;

            _layer = UIFactory.NewRect("Layer", cgo);
            UIFactory.Stretch(_layer);

            var gold = new Color(1f, 0.82f, 0.35f, 1f);
            for (int i = 0; i < 2; i++)
            {
                var f = UIFactory.NewRect("Frame" + i, _layer);
                UIFactory.Anchor(f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                _frames[i] = f;
                _frameGroup[i] = f.gameObject.AddComponent<CanvasGroup>();
                _frameGroup[i].blocksRaycasts = false; _frameGroup[i].interactable = false;
                var fill = UIFactory.AddImage("Fill", f, new Color(1f, 0.85f, 0.4f, 0.10f));
                UIFactory.Stretch(fill.rectTransform);
                Edge(f, gold, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 5));     // 위
                Edge(f, gold, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 5));     // 아래
                Edge(f, gold, new Vector2(0, 0), new Vector2(0, 1), new Vector2(5, 0));     // 왼쪽
                Edge(f, gold, new Vector2(1, 0), new Vector2(1, 1), new Vector2(5, 0));     // 오른쪽
                var numBg = UIFactory.AddImage("Num", f, gold, ProceduralSprites.Circle);
                UIFactory.Anchor(numBg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f));
                numBg.rectTransform.sizeDelta = new Vector2(40, 40); numBg.rectTransform.anchoredPosition = Vector2.zero;
                _frameNum[i] = UIFactory.AddText("T", numBg.transform, (i + 1).ToString(), 26, font, new Color(0.2f, 0.12f, 0.02f), TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Stretch(_frameNum[i].rectTransform);
                f.gameObject.SetActive(false);
            }

            var rim = UIFactory.AddImage("Bubble", _layer, gold, ProceduralSprites.Rounded);
            rim.type = Image.Type.Sliced; rim.preserveAspect = false;
            _bubble = rim.rectTransform;
            UIFactory.Anchor(_bubble, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _bubble.sizeDelta = new Vector2(660, 150);
            var inner = UIFactory.AddImage("Inner", _bubble, new Color(0.07f, 0.08f, 0.13f, 0.97f), ProceduralSprites.Rounded);
            inner.type = Image.Type.Sliced; inner.preserveAspect = false;
            UIFactory.Stretch(inner.rectTransform, 3, 3, 3, 3);
            _bubbleText = UIFactory.AddText("T", _bubble, "", 23, font, Color.white, TextAlignmentOptions.Left);
            _bubbleText.richText = true;
            _bubbleText.textWrappingMode = TextWrappingModes.Normal;
            _bubbleText.lineSpacing = 6f;
            _bubbleText.enableAutoSizing = true; _bubbleText.fontSizeMin = 15; _bubbleText.fontSizeMax = 23;
            UIFactory.Stretch(_bubbleText.rectTransform, 24, 44, 24, 14);
            var skip = UIFactory.AddButton("Skip", _bubble, "튜토리얼 건너뛰기", 15, font, new Color(1, 1, 1, 0.14f), new Color(0.82f, 0.85f, 0.92f));
            var srt = (RectTransform)skip.transform;
            UIFactory.Anchor(srt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
            srt.sizeDelta = new Vector2(180, 32); srt.anchoredPosition = new Vector2(-12, 10);
            skip.onClick.AddListener(() => Popups.Show("튜토리얼 건너뛰기", "안내를 그만 볼까요?\n완료 보상(우편)은 튜토리얼을 끝까지 진행해야 받을 수 있습니다.", "건너뛰기", Skip, "계속 진행"));
            _bubble.gameObject.SetActive(false);
        }

        private static void Edge(RectTransform parent, Color c, Vector2 min, Vector2 max, Vector2 thickness)
        {
            var img = UIFactory.AddImage("Edge", parent, c);
            var rt = img.rectTransform;
            rt.anchorMin = min; rt.anchorMax = max;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = thickness; rt.anchoredPosition = Vector2.zero;
        }

        private void HideGuide()
        {
            if (_bubble == null) return;
            if (_bubble.gameObject.activeSelf) _bubble.gameObject.SetActive(false);
            for (int i = 0; i < 2; i++) if (_frames[i].gameObject.activeSelf) _frames[i].gameObject.SetActive(false);
        }

        /// <summary>말풍선 + 대상 테두리(대상이 지금 안 보이면 말풍선만).</summary>
        private void Guide(string text, string key, string key2 = null)
        {
            if (_bubble == null) return;
            var t1 = TutorialTargets.Get(key);
            var t2 = TutorialTargets.Get(key2);
            bool two = t1 != null && t2 != null;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);

            bool any = false;
            Vector2 min = Vector2.zero, max = Vector2.zero;
            var targets = new[] { t1, t2 };
            for (int i = 0; i < 2; i++)
            {
                var t = targets[i];
                var f = _frames[i];
                if (t == null) { if (f.gameObject.activeSelf) f.gameObject.SetActive(false); continue; }
                t.GetWorldCorners(_corners);
                Vector2 a = _layer.InverseTransformPoint(_corners[0]), b = _layer.InverseTransformPoint(_corners[2]);
                float pad = 8f + 6f * pulse;
                f.anchoredPosition = (a + b) * 0.5f;
                f.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x) + pad * 2f, Mathf.Abs(b.y - a.y) + pad * 2f);
                _frameGroup[i].alpha = 0.65f + 0.35f * pulse;
                _frameNum[i].transform.parent.gameObject.SetActive(two);
                if (!f.gameObject.activeSelf) f.gameObject.SetActive(true);
                if (!any) { min = Vector2.Min(a, b); max = Vector2.Max(a, b); any = true; }
                else { min = Vector2.Min(min, Vector2.Min(a, b)); max = Vector2.Max(max, Vector2.Max(a, b)); }
            }

            if (_bubbleText.text != text) _bubbleText.text = text;
            var size = _bubble.sizeDelta;
            var area = _layer.rect;
            Vector2 pos;
            if (!any) pos = new Vector2(0, area.height * 0.5f - 240f);
            else
            {
                float x = Mathf.Clamp((min.x + max.x) * 0.5f, area.xMin + size.x * 0.5f + 20f, area.xMax - size.x * 0.5f - 20f);
                float below = min.y - 34f - size.y * 0.5f, above = max.y + 34f + size.y * 0.5f;
                float y = below - size.y * 0.5f > area.yMin + 10f ? below : above;
                pos = new Vector2(x, Mathf.Clamp(y, area.yMin + size.y * 0.5f + 10f, area.yMax - size.y * 0.5f - 10f));
            }
            _bubble.anchoredPosition = pos;
            if (!_bubble.gameObject.activeSelf) _bubble.gameObject.SetActive(true);
        }

        [ContextMenu("테스트: 튜토리얼 처음부터")]
        private void TestRestart()
        {
            SetStep(0);
            foreach (var k in new[] { "battle", "levelIntro" }) PlayerPrefs.DeleteKey("tutorial.flag." + k);
            _grew = false;
        }

        [ContextMenu("테스트: 기능 개방 · 화면 설명 다시 보기")]
        private void TestResetNotices()
        {
            foreach (var u in Unlocks()) PlayerPrefs.DeleteKey("tutorial.flag." + u.flag);
            foreach (var kv in Tips) PlayerPrefs.DeleteKey("tutorial.flag.tip." + kv.Key);
            _first = true;
        }
    }
}
