using UnityEngine;
using SubcultureRPG.Battle;
using SubcultureRPG.Battle.UI;
using SubcultureRPG.Meta.UI;

namespace SubcultureRPG.Meta
{
    /// <summary>허브에서 오가는 화면.</summary>
    public enum HubScreen
    {
        Home,
        Stage,
        Gacha,
        Growth,
        Party,
        Battle,
        Bag,
        Dungeon,   // 스테이지 화면의 특수 던전 탭으로 열기
        Shop,      // 값이 직렬화되므로 새 화면은 뒤에만 추가
        Mission,
        Codex,
        Arena,     // 결투장(PVP)
        Raid,      // 주간 보스(월드 레이드)
        Friends,   // 친구
        Guild,     // 길드
        Event,     // 기간 한정 이벤트
        Dispatch,  // 파견
        Tower,     // 무한의 탑
    }

    /// <summary>
    /// 메인 씬 화면 전환 관리자. 한 번에 한 화면만 보이게 하고,
    /// 각 화면의 '홈' 버튼·편성 후 전투 진입·전투 나가기를 연결합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SubcultureRPG/Meta/Screen Navigator")]
    public class ScreenNavigator : MonoBehaviour
    {
        [SerializeField] private HomeScreen home;
        [SerializeField] private StageScreen stage;
        [SerializeField] private StageRunner stageRunner;
        [SerializeField] private GachaScreen gacha;
        [SerializeField] private GrowthScreen growth;
        [SerializeField] private PartyScreen party;
        [SerializeField] private BagScreen bag;
        [SerializeField] private ShopScreen shop;
        [SerializeField] private MissionScreen mission;
        [SerializeField] private CodexScreen codex;
        [SerializeField] private ArenaScreen arena;
        [SerializeField] private RaidScreen raid;
        [SerializeField] private EventScreen eventScreen;
        [SerializeField] private DispatchScreen dispatch;
        [SerializeField] private TowerScreen tower;
        [SerializeField] private FriendScreen friends;
        [SerializeField] private GuildScreen guild;

        /// <summary>결투장 · 주간 보스 전투에서 나갈 때 돌아갈 화면(없으면 스테이지/홈 규칙).</summary>
        private HubScreen? _modeReturn;
        [SerializeField] private BattleHUD battleHud;
        [SerializeField] private BattleController battle;
        [SerializeField] private HubScreen startScreen = HubScreen.Home;

        public HubScreen Current { get; private set; }

        private void Awake()
        {
            if (home == null) home = FindAnyObjectByType<HomeScreen>();
            if (stage == null) stage = FindAnyObjectByType<StageScreen>();
            if (stageRunner == null) stageRunner = FindAnyObjectByType<StageRunner>();
            if (gacha == null) gacha = FindAnyObjectByType<GachaScreen>();
            if (growth == null) growth = FindAnyObjectByType<GrowthScreen>();
            if (party == null) party = FindAnyObjectByType<PartyScreen>();
            if (bag == null) bag = FindAnyObjectByType<BagScreen>();
            if (battleHud == null) battleHud = FindAnyObjectByType<BattleHUD>();
            if (battle == null) battle = FindAnyObjectByType<BattleController>();
            if (shop == null) shop = FindAnyObjectByType<ShopScreen>();
            if (shop == null && home != null) shop = CreateShop();   // 상점 도입 전에 만든 씬도 재생성 없이 동작
            if (mission == null) mission = FindAnyObjectByType<MissionScreen>();
            if (mission == null && home != null) mission = CreateMission();   // 임무도 마찬가지
            if (codex == null) codex = FindAnyObjectByType<CodexScreen>();
            if (codex == null && home != null) codex = CreateCodex();         // 도감도 마찬가지
            if (arena == null) arena = FindAnyObjectByType<ArenaScreen>();
            if (arena == null && home != null) arena = CreateMode<ArenaScreen>("ArenaScreen");
            if (raid == null) raid = FindAnyObjectByType<RaidScreen>();
            if (raid == null && home != null) raid = CreateMode<RaidScreen>("RaidScreen");
            if (eventScreen == null) eventScreen = FindAnyObjectByType<EventScreen>();
            if (eventScreen == null && home != null) eventScreen = CreateMode<EventScreen>("EventScreen");
            if (dispatch == null) dispatch = FindAnyObjectByType<DispatchScreen>();
            if (dispatch == null && home != null) dispatch = CreateMode<DispatchScreen>("DispatchScreen");
            if (tower == null) tower = FindAnyObjectByType<TowerScreen>();
            if (tower == null && home != null) tower = CreateMode<TowerScreen>("TowerScreen");
            if (friends == null) friends = FindAnyObjectByType<FriendScreen>();
            if (friends == null && home != null) friends = CreateMode<FriendScreen>("FriendScreen");
            if (guild == null) guild = FindAnyObjectByType<GuildScreen>();
            if (guild == null && home != null) guild = CreateMode<GuildScreen>("GuildScreen");
        }

        /// <summary>결투장 · 주간 보스 화면을 코드로 추가(폰트는 홈 화면과 같게).</summary>
        private T CreateMode<T>(string name) where T : ModeScreenBase
        {
            var go = new GameObject(name);
            go.SetActive(false);   // Awake(UI 구성) 전에 폰트 지정
            var s = go.AddComponent<T>();
            s.SetFont(home.Font);
            go.SetActive(true);
            return s;
        }

        /// <summary>출격 모드 편성 화면을 열고, 전투에서 나가면 back 화면으로.</summary>
        private void OpenModeParty(string title, string start, System.Action<System.Collections.Generic.List<SubcultureRPG.Characters.CharacterData>> launch,
            HubScreen back, string backLabel)
        {
            if (party == null) return;
            _modeReturn = back;
            party.SetLaunchMode(team => launch(team), title, start);
            party.SetBackAction(() => Go(back), backLabel);
            Go(HubScreen.Party);
        }

        /// <summary>씬에 도감 화면이 없으면 코드로 추가(폰트는 홈 화면과 같게).</summary>
        private CodexScreen CreateCodex()
        {
            var go = new GameObject("CodexScreen");
            go.SetActive(false);   // Awake(UI 구성) 전에 폰트 지정
            var s = go.AddComponent<CodexScreen>();
            s.SetFont(home.Font);
            go.SetActive(true);
            return s;
        }

        /// <summary>씬에 임무 화면이 없으면 코드로 추가(폰트는 홈 화면과 같게).</summary>
        private MissionScreen CreateMission()
        {
            var go = new GameObject("MissionScreen");
            go.SetActive(false);   // Awake(UI 구성) 전에 폰트 지정
            var s = go.AddComponent<MissionScreen>();
            s.SetFont(home.Font);
            go.SetActive(true);
            return s;
        }

        /// <summary>씬에 상점 화면이 없으면 코드로 추가(폰트는 홈 화면과 같게).</summary>
        private ShopScreen CreateShop()
        {
            var go = new GameObject("ShopScreen");
            go.SetActive(false);   // Awake(UI 구성) 전에 폰트 지정
            var s = go.AddComponent<ShopScreen>();
            s.SetFont(home.Font);
            go.SetActive(true);
            return s;
        }

        private void Start()
        {
            if (gacha != null) gacha.SetBackAction(GoHome);
            if (growth != null) growth.SetBackAction(GoHome);
            if (bag != null) bag.SetBackAction(GoHome);
            if (shop != null) shop.SetBackAction(GoHome);
            if (mission != null) mission.SetBackAction(GoHome);
            if (codex != null) codex.SetBackAction(GoHome);
            if (arena != null)
            {
                arena.SetBackAction(GoHome);
                arena.OnChallenge += o => OpenModeParty(
                    $"결투장  ·  {o.name}  <size=60%><color=#FFB347>상대 전투력 {o.power:N0}</color></size>", "도전!",
                    team => PlayerArena.Instance.Fight(o, team), HubScreen.Arena, "← 결투장");
            }
            if (friends != null) friends.SetBackAction(GoHome);
            if (guild != null) guild.SetBackAction(GoHome);
            if (raid != null)
            {
                raid.SetBackAction(GoHome);
                raid.OnChallenge += () => OpenModeParty(
                    $"주간 보스  ·  {WorldRaid.BossName}  <size=60%><color=#C9A0FF>제한 시간 {WorldRaid.TimeLimit / 60f:0}분</color>  <color=#7CFF9A>보너스 캐릭터 1명당 점수 +{WorldRaid.FeaturedBonus * 100f:0}% (최대 {WorldRaid.FeaturedMax}명)</color></size>", "도전!",
                    team => WorldRaid.Instance.Fight(team), HubScreen.Raid, "← 주간 보스");
            }
            if (dispatch != null) dispatch.SetBackAction(GoHome);
            if (tower != null)
            {
                tower.SetBackAction(GoHome);
                tower.OnChallenge += f => OpenModeParty(
                    $"무한의 탑  ·  {f}층{(PlayerTower.IsBoss(f) ? "  <color=#FF9A6B>BOSS</color>" : "")}  <size=60%><color=#FFD36B>적 Lv.{PlayerTower.EnemyLevel(f)}</color></size>", "도전!",
                    team => PlayerTower.Instance.Fight(f, team), HubScreen.Tower, "← 무한의 탑");
            }
            if (eventScreen != null)
            {
                eventScreen.SetBackAction(GoHome);
                eventScreen.OnChallenge += d => OpenModeParty(
                    $"{LimitedEvent.CurrentTheme.name}  ·  {LimitedEvent.Difficulties[d].name}  <size=60%><color=#FFD36B>{Characters.Traits.Name(LimitedEvent.CurrentTheme.element)} 캐릭터 1명당 코인 +10%</color></size>", "도전!",
                    team => LimitedEvent.Instance.Fight(d, team), HubScreen.Event, "← 이벤트");
            }
            if (party != null)
            {
                party.SetBackAction(GoHome);
                party.ClearLaunchMode();
                party.OnLaunched += OnPartyLaunched;
            }
            if (stage != null)
            {
                stage.SetBackAction(GoHome);
                stage.OnSortie += OnStageSortie;
            }
            if (battleHud != null) battleHud.SetExitAction(ExitBattle);

            Go(startScreen);
        }

        private void OnDestroy()
        {
            if (party != null) party.OnLaunched -= OnPartyLaunched;
            if (stage != null) stage.OnSortie -= OnStageSortie;
        }

        /// <summary>지정 화면만 표시.</summary>
        public void Go(HubScreen screen)
        {
            // 결투장 · 주간 보스는 3장을 클리어해야 열림
            if (screen == HubScreen.Event && !FeatureGate.EventOpen)
            {
                Popups.Show("기간 한정 이벤트",
                    $"아직 열리지 않았습니다.\n<color=#FFD36B>{FeatureGate.EventChapter}장</color>(쉬움)을 클리어하면 열립니다.\n\n<color=#9AA3B5>지금 {FeatureGate.ClearedChapters()}장까지 클리어</color>");
                return;
            }
            if ((screen == HubScreen.Dispatch && !FeatureGate.DispatchOpen) || (screen == HubScreen.Tower && !FeatureGate.TowerOpen))
            {
                int need = screen == HubScreen.Dispatch ? FeatureGate.DispatchChapter : FeatureGate.TowerChapter;
                Popups.Show(screen == HubScreen.Dispatch ? "파견" : "무한의 탑",
                    $"아직 열리지 않았습니다.\n<color=#FFD36B>{need}장</color>(쉬움)을 클리어하면 열립니다.\n\n<color=#9AA3B5>지금 {FeatureGate.ClearedChapters()}장까지 클리어</color>");
                return;
            }
            if ((screen == HubScreen.Arena && !FeatureGate.ArenaOpen) || (screen == HubScreen.Raid && !FeatureGate.RaidOpen))
            {
                int need = screen == HubScreen.Arena ? FeatureGate.ArenaChapter : FeatureGate.RaidChapter;
                Popups.Show(screen == HubScreen.Arena ? "결투장" : "주간 보스",
                    $"아직 열리지 않았습니다.\n<color=#FFD36B>{need}장</color>(쉬움)을 클리어하면 열립니다.\n\n<color=#9AA3B5>지금 {FeatureGate.ClearedChapters()}장까지 클리어</color>");
                return;
            }

            if (screen == HubScreen.Dungeon)
            {
                if (stage != null) stage.OpenSpecial();
                screen = HubScreen.Stage;
            }

            // 홈/스테이지 목록으로 돌아오면 스테이지 출격 모드 해제
            if (screen == HubScreen.Home || screen == HubScreen.Stage || screen == HubScreen.Arena || screen == HubScreen.Raid || screen == HubScreen.Event || screen == HubScreen.Tower)
            {
                _modeReturn = null;
                PlayerArena.Instance.Leave();   // 끝나지 않은 전투에서 나오면 도전권 · 도전 횟수 반환
                WorldRaid.Instance.Leave();
                LimitedEvent.Instance.Leave();
                PlayerTower.Instance.Leave();
                if (stageRunner != null) stageRunner.Clear();
                if (party != null)
                {
                    party.ClearLaunchMode();
                    party.SetBackAction(GoHome);
                }
            }

            Current = screen;
            SoundManager.Bgm(BgmOf(screen, _modeReturn));
            if (home != null) home.Show(screen == HubScreen.Home);
            if (stage != null) stage.Show(screen == HubScreen.Stage);
            if (gacha != null) gacha.Show(screen == HubScreen.Gacha);
            if (growth != null) growth.Show(screen == HubScreen.Growth);
            if (party != null) party.Show(screen == HubScreen.Party);
            if (bag != null) bag.Show(screen == HubScreen.Bag);
            if (shop != null) shop.Show(screen == HubScreen.Shop);
            if (mission != null) mission.Show(screen == HubScreen.Mission);
            if (codex != null) codex.Show(screen == HubScreen.Codex);
            if (arena != null) arena.Show(screen == HubScreen.Arena);
            if (raid != null) raid.Show(screen == HubScreen.Raid);
            if (eventScreen != null) eventScreen.Show(screen == HubScreen.Event);
            if (dispatch != null) dispatch.Show(screen == HubScreen.Dispatch);
            if (tower != null) tower.Show(screen == HubScreen.Tower);
            if (friends != null) friends.Show(screen == HubScreen.Friends);
            if (guild != null) guild.Show(screen == HubScreen.Guild);
            if (battleHud != null) battleHud.Show(screen == HubScreen.Battle);
        }

        public void GoHome() => Go(HubScreen.Home);

        /// <summary>
        /// 화면별 배경음 키(Resources/Audio/BGM/{키}). 없는 키는 menu → home 으로 대체.
        /// 전투는 어디서 들어왔는지(mode)에 따라: 주간 보스 · 무한의 탑 = boss, 결투장 = arena, 그 밖 = battle.
        /// </summary>
        private static string BgmOf(HubScreen s, HubScreen? mode) => s switch
        {
            HubScreen.Home => "home",
            HubScreen.Stage or HubScreen.Dungeon => "stage",
            HubScreen.Gacha => "gacha",
            HubScreen.Battle => mode switch
            {
                HubScreen.Raid or HubScreen.Tower => "boss",
                HubScreen.Arena => "arena",
                _ => "battle",
            },
            HubScreen.Arena or HubScreen.Raid or HubScreen.Tower => "arena",
            HubScreen.Event => "event",
            _ => "menu",
        };

        /// <summary>스테이지 화면을 특수 던전(종류 지정) 탭으로 엶.</summary>
        public void OpenDungeon(StageKind kind)
        {
            if (stage != null) stage.OpenSpecial(kind);
            Go(HubScreen.Stage);
        }

        /// <summary>스테이지 상세의 '편성 후 출격' → 출격 모드 편성 화면.</summary>
        private void OnStageSortie(StageData s)
        {
            if (party == null || stageRunner == null || s == null) return;
            var target = s;
            party.SetLaunchMode(team => stageRunner.Launch(target, team),
                $"출격 준비  ·  {s.Code} {s.stageName}  <size=60%><color=#FFD36B>권장 Lv.{s.recommendedLevel}</color></size>",
                "출격!");
            party.SetBackAction(() => Go(HubScreen.Stage), "← 스테이지");
            Go(HubScreen.Party);
        }

        private void OnPartyLaunched()
        {
            if (_modeReturn.HasValue) { Go(HubScreen.Battle); return; }   // 결투장 · 주간 보스는 각자 나가기 문구를 정함
            bool stageBattle = stageRunner != null && stageRunner.Current != null;
            if (battleHud != null) battleHud.SetExitLabel(stageBattle ? "스테이지로" : "홈으로");
            Go(HubScreen.Battle);
        }

        private void ExitBattle()
        {
            if (_modeReturn.HasValue)
            {
                var back = _modeReturn.Value;
                if (battle != null) battle.Stop();
                Go(back);
                return;
            }
            bool stageBattle = stageRunner != null && stageRunner.Current != null;
            if (stageBattle && stage != null) stage.Focus(stageRunner.Current);   // '다음 스테이지'로 진행했으면 그 스테이지 선택
            if (battle != null) battle.Stop();
            Go(stageBattle ? HubScreen.Stage : HubScreen.Home);
        }
    }
}
