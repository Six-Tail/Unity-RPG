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
            if (screen == HubScreen.Dungeon)
            {
                if (stage != null) stage.OpenSpecial();
                screen = HubScreen.Stage;
            }

            // 홈/스테이지 목록으로 돌아오면 스테이지 출격 모드 해제
            if (screen == HubScreen.Home || screen == HubScreen.Stage)
            {
                if (stageRunner != null) stageRunner.Clear();
                if (party != null)
                {
                    party.ClearLaunchMode();
                    party.SetBackAction(GoHome);
                }
            }

            Current = screen;
            if (home != null) home.Show(screen == HubScreen.Home);
            if (stage != null) stage.Show(screen == HubScreen.Stage);
            if (gacha != null) gacha.Show(screen == HubScreen.Gacha);
            if (growth != null) growth.Show(screen == HubScreen.Growth);
            if (party != null) party.Show(screen == HubScreen.Party);
            if (bag != null) bag.Show(screen == HubScreen.Bag);
            if (shop != null) shop.Show(screen == HubScreen.Shop);
            if (battleHud != null) battleHud.Show(screen == HubScreen.Battle);
        }

        public void GoHome() => Go(HubScreen.Home);

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
            bool stageBattle = stageRunner != null && stageRunner.Current != null;
            if (battleHud != null) battleHud.SetExitLabel(stageBattle ? "스테이지로" : "홈으로");
            Go(HubScreen.Battle);
        }

        private void ExitBattle()
        {
            bool stageBattle = stageRunner != null && stageRunner.Current != null;
            if (stageBattle && stage != null) stage.Focus(stageRunner.Current);   // '다음 스테이지'로 진행했으면 그 스테이지 선택
            if (battle != null) battle.Stop();
            Go(stageBattle ? HubScreen.Stage : HubScreen.Home);
        }
    }
}
