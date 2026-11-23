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
        Gacha,
        Growth,
        Party,
        Battle,
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
        [SerializeField] private GachaScreen gacha;
        [SerializeField] private GrowthScreen growth;
        [SerializeField] private PartyScreen party;
        [SerializeField] private BattleHUD battleHud;
        [SerializeField] private BattleController battle;
        [SerializeField] private HubScreen startScreen = HubScreen.Home;

        public HubScreen Current { get; private set; }

        private void Awake()
        {
            if (home == null) home = FindAnyObjectByType<HomeScreen>();
            if (gacha == null) gacha = FindAnyObjectByType<GachaScreen>();
            if (growth == null) growth = FindAnyObjectByType<GrowthScreen>();
            if (party == null) party = FindAnyObjectByType<PartyScreen>();
            if (battleHud == null) battleHud = FindAnyObjectByType<BattleHUD>();
            if (battle == null) battle = FindAnyObjectByType<BattleController>();
        }

        private void Start()
        {
            if (gacha != null) gacha.SetBackAction(GoHome);
            if (growth != null) growth.SetBackAction(GoHome);
            if (party != null)
            {
                party.SetBackAction(GoHome);
                party.OnLaunched += OnPartyLaunched;
            }
            if (battleHud != null) battleHud.SetExitAction(ExitBattle);

            Go(startScreen);
        }

        private void OnDestroy()
        {
            if (party != null) party.OnLaunched -= OnPartyLaunched;
        }

        /// <summary>지정 화면만 표시.</summary>
        public void Go(HubScreen screen)
        {
            Current = screen;
            if (home != null) home.Show(screen == HubScreen.Home);
            if (gacha != null) gacha.Show(screen == HubScreen.Gacha);
            if (growth != null) growth.Show(screen == HubScreen.Growth);
            if (party != null) party.Show(screen == HubScreen.Party);
            if (battleHud != null) battleHud.Show(screen == HubScreen.Battle);
        }

        public void GoHome() => Go(HubScreen.Home);

        private void OnPartyLaunched() => Go(HubScreen.Battle);

        private void ExitBattle()
        {
            if (battle != null) battle.Stop();
            Go(HubScreen.Home);
        }
    }
}
