using System.Collections.Generic;
using UnityEngine;

namespace SubcultureRPG.Meta
{
    /// <summary>스토리 한 줄.</summary>
    public class StoryLine
    {
        public enum Kind { Dialogue, Narration, Background, Title }

        public Kind kind;
        public string speaker;      // 화자 이름(대사)
        public string portrait;     // 그림 경로(Resources/Art 기준: "Characters/c_ruru" · "Enemies/어둠군주"). 없으면 그림 없이 이름만
        public bool right;          // 그림을 오른쪽에 세움
        public Color accent = Color.white;   // 이름표 색
        public string text;         // 대사 · 내레이션 · 장 제목
        public string sub;          // 장 부제(Title) / 배경 경로(Background)
    }

    /// <summary>스토리 한 편(스테이지 전·후에 재생).</summary>
    public class StoryScript
    {
        public string id;           // 본 기록 키
        public string title;        // 다시 보기 목록용 제목
        public readonly List<StoryLine> lines = new();
    }

    /// <summary>
    /// 스토리 대본. 1장 ~ 15장(쉬움 난이도). 1장은 이 파일, 2 ~ 15장은 StoryChapters*.cs.
    /// · Before(stage): 그 스테이지에 처음 출격할 때 전투 전에 재생
    /// · After(stage): 처음 클리어했을 때 결과 화면 위에서 재생
    /// 한 번 본 스토리는 다시 자동 재생되지 않고(PlayerPrefs), 스테이지 상세의 '스토리 다시 보기'로 볼 수 있습니다.
    /// 새 장을 추가하려면 Build에 Add(...)를 이어 쓰면 됩니다.
    /// </summary>
    public static partial class StoryLibrary
    {
        private static Dictionary<string, StoryScript> _scripts;

        private static string Key(StageData s, bool after) =>
            s == null || s.IsDungeon || s.difficulty != StageDifficulty.Easy ? null
            : $"ch{s.chapter}_{s.number}_{(after ? "after" : "before")}";

        public static StoryScript Before(StageData s) => Find(Key(s, false));
        public static StoryScript After(StageData s) => Find(Key(s, true));

        private static StoryScript Find(string key)
        {
            if (key == null) return null;
            _scripts ??= Build();
            return _scripts.TryGetValue(key, out var sc) ? sc : null;
        }

        public static bool Seen(StoryScript s) => s != null && PlayerPrefs.GetInt("story.seen." + s.id, 0) == 1;

        public static void MarkSeen(StoryScript s)
        {
            if (s == null) return;
            PlayerPrefs.SetInt("story.seen." + s.id, 1);
            PlayerPrefs.Save();
        }

        /// <summary>테스트용: 모든 스토리를 안 본 상태로.</summary>
        public static void ResetSeen()
        {
            _scripts ??= Build();
            foreach (var k in _scripts.Keys) PlayerPrefs.DeleteKey("story.seen." + k);
            PlayerPrefs.Save();
        }

        // ---------------- 등장인물 ----------------

        private class Cast
        {
            public string name, portrait;
            public bool right;
            public Color accent;
        }

        private static readonly Cast Ruru = new() { name = "루루", portrait = "Characters/c_ruru", accent = new Color(0.8f, 0.55f, 1f) };
        private static readonly Cast Pin = new() { name = "핀", portrait = "Characters/c_pin", accent = new Color(0.78f, 0.78f, 0.82f) };
        private static readonly Cast Riri = new() { name = "리리", portrait = "Characters/c_riri", right = true, accent = new Color(0.45f, 0.9f, 0.6f) };
        private static readonly Cast Ark = new() { name = "아크", portrait = "Characters/c_ark", right = true, accent = new Color(1f, 0.85f, 0.4f) };
        private static readonly Cast DarkLord = new() { name = "어둠군주", portrait = "Enemies/어둠군주", right = true, accent = new Color(0.75f, 0.4f, 1f) };

        // 2장 이후 합류 · 등장(속성 색 이름표)
        private static Cast Ally(string name, string id, bool right, Color accent) =>
            new() { name = name, portrait = "Characters/" + id, right = right, accent = accent };
        private static Cast Foe(string name, Color accent) =>
            new() { name = name, portrait = "Enemies/" + name, right = true, accent = accent };

        private static readonly Color CIce = new(0.45f, 0.85f, 1f), CFire = new(1f, 0.5f, 0.35f), CWind = new(0.45f, 0.9f, 0.6f),
            CBolt = new(0.8f, 0.55f, 1f), CPhys = new(0.78f, 0.78f, 0.82f);

        private static readonly Cast Sora = Ally("소라", "c_sora", true, CIce);
        private static readonly Cast Ciel = Ally("시엘", "c_ciel", true, CIce);
        private static readonly Cast Mina = Ally("미나", "c_mina", false, CFire);
        private static readonly Cast Haru = Ally("하루", "c_haru", true, CFire);
        private static readonly Cast Seraphine = Ally("세라핀", "c_seraphine", true, CFire);
        private static readonly Cast Yuna = Ally("유나", "c_yuna", false, CWind);
        private static readonly Cast Kiki = Ally("키키", "c_kiki", true, CWind);
        private static readonly Cast Kael = Ally("카엘", "c_kael", true, CWind);
        private static readonly Cast Bella = Ally("벨라", "c_bella", true, CBolt);
        private static readonly Cast Teo = Ally("테오", "c_teo", false, CBolt);
        private static readonly Cast Rena = Ally("레나", "c_rena", true, CBolt);
        private static readonly Cast Grey = Ally("그레이", "c_grey", true, CPhys);
        private static readonly Cast Coco = Ally("코코", "c_coco", false, CPhys);
        private static readonly Cast Selene = Ally("셀레네", "c_selene", true, CIce);
        private static readonly Cast Dodo = Ally("도도", "c_dodo", false, CIce);
        private static readonly Cast Luna = Ally("루나", "c_luna", true, CIce);
        private static readonly Cast Momo = Ally("모모", "c_momo", false, CFire);
        private static readonly Cast Violet = Ally("바이올렛", "c_violet", true, CPhys);
        private static readonly Cast Rio = Ally("리오", "c_rio", false, CWind);
        private static readonly Cast Aurora = Ally("오로라", "c_aurora", true, CIce);
        private static readonly Cast Estelle = Ally("에스텔", "c_estelle", true, CFire);

        private static readonly Cast IceGiant = Foe("빙결거인", CIce);
        private static readonly Cast FireGuardian = Foe("화염 수호자", CFire);
        private static readonly Cast WindGuardian = Foe("질풍 수호자", CWind);
        private static readonly Cast BoltGuardian = Foe("뇌광 수호자", CBolt);
        private static readonly Cast SteelGolem = Foe("강철 골렘", CPhys);
        private static readonly Cast FrostGuardian = Foe("서리 수호자", CIce);
        private static readonly Cast EndLord = Foe("종말의 군주", new Color(1f, 0.45f, 0.5f));
        /// <summary>검은 순례자: 봉인을 풀고 다니는 수수께끼의 인물(그림 없음).</summary>
        private static readonly Cast Pilgrim = new() { name = "검은 순례자", right = true, accent = new Color(0.6f, 0.6f, 0.7f) };

        // ---------------- 작성 도우미 ----------------

        private class Writer
        {
            public readonly StoryScript script;
            public Writer(string id, string title) { script = new StoryScript { id = id, title = title }; }

            public Writer Bg(string key)
            { script.lines.Add(new StoryLine { kind = StoryLine.Kind.Background, sub = "Backgrounds/" + key }); return this; }

            public Writer Title(string chapter, string name)
            { script.lines.Add(new StoryLine { kind = StoryLine.Kind.Title, text = chapter, sub = name }); return this; }

            /// <summary>내레이션.</summary>
            public Writer N(string text)
            { script.lines.Add(new StoryLine { kind = StoryLine.Kind.Narration, text = text }); return this; }

            /// <summary>대사.</summary>
            public Writer L(Cast who, string text)
            {
                script.lines.Add(new StoryLine
                {
                    kind = StoryLine.Kind.Dialogue, speaker = who.name, portrait = who.portrait, right = who.right, accent = who.accent, text = text,
                });
                return this;
            }
        }

        private static Dictionary<string, StoryScript> Build()
        {
            var d = new Dictionary<string, StoryScript>();
            void Add(Writer w) => d[w.script.id] = w.script;
            _ = Pilgrim;

            // ===================== 제1장 그림자 숲 =====================

            Add(new Writer("ch1_1_before", "1-1 숲의 입구")
                .Bg("story_ch1_gate")
                .Title("제1장", "그림자 숲")
                .N("변경 마을 에덴브룩의 북쪽. 오래된 숲에서 밤마다 검은 안개가 피어오르기 시작했다.")
                .N("숲에 들어갔다가 돌아오지 못한 사람이 벌써 셋. 왕국 기사단은 신임 지휘관 한 명과 견습 대원들을 보냈다.")
                .L(Ruru, "지휘관님! 여기가 그림자 숲 입구예요. 지도랑 통신은 저, 루루가 맡을게요!")
                .L(Pin, "드디어 첫 임무네요. 걱정 마세요, 제 검이 길을 열 테니까!")
                .L(Riri, "핀, 너무 앞서가지 마세요. ...숲 안쪽에서 차가운 기운이 흘러나와요.")
                .L(Ruru, "앗, 저기 보세요! 문 너머에 그림자 같은 게 움직여요!")
                .L(Pin, "저게 소문의 '그림자병'인가. 지휘관님, 지시를 내려 주세요!")
                .N("첫 전투다. 적의 약점 속성을 살피고, 침착하게 지휘하자."));

            Add(new Writer("ch1_2_before", "1-2 갈림길")
                .Bg("story_ch1_gate")
                .L(Ruru, "길이 둘로 갈려요. 왼쪽은 옛 사냥꾼 길, 오른쪽은... 지도에 없는 길이에요.")
                .L(Riri, "오른쪽 길의 풀이 전부 시들었어요. 그림자가 지나간 자국이에요.")
                .L(Pin, "그럼 정답은 오른쪽이네요. 흔적을 따라가죠!")
                .L(Ruru, "잠깐만요, 매복이에요! 나무 뒤에 여럿 숨어 있어요!"));

            Add(new Writer("ch1_3_before", "1-3 안개 낀 오솔길")
                .Bg("story_ch1_forest")
                .N("숲이 깊어질수록 안개가 짙어진다. 몇 걸음 앞도 보이지 않는다.")
                .L(Riri, "이 안개... 자연의 것이 아니에요. 누군가의 마력이 섞여 있어요.")
                .L(Pin, "으, 검을 휘둘러도 안개만 베이네. 이래선 어디서 덤벼드는지도 모르겠어.")
                .L(Ruru, "제가 번개로 길을 밝힐게요. 다들 제 불빛에서 벗어나지 마세요!")
                .N("번쩍이는 빛 사이로, 안개 속에 숨어 있던 그림자들이 모습을 드러냈다."));

            Add(new Writer("ch1_4_before", "1-4 궁수의 둥지")
                .Bg("story_ch1_forest")
                .L(Ruru, "위예요! 나무 위에 망루가— 그림자궁수가 잔뜩 있어요!")
                .L(Pin, "화살이 비처럼 쏟아지잖아! 피할 데가 없어!")
                .N("그 순간, 번개를 두른 커다란 방패가 화살비를 가로막았다.")
                .L(Ark, "신참들이 여기까지 들어왔나. 겁이 없군.")
                .L(Riri, "기사단의 방패 대장, 아크 님...!")
                .L(Ark, "인사는 나중에. 궁수는 높은 곳을 잃으면 무너진다. 지휘관, 망루부터 친다. 따라와라."));

            Add(new Writer("ch1_5_before", "1-5 그림자 야영지")
                .Bg("story_ch1_camp")
                .N("망루를 넘어서자 숲 한가운데에 버려진 야영지가 나타났다. 보랏빛 모닥불이 아직 타고 있다.")
                .L(Ark, "그림자병들의 진지다. 놈들은 스스로 생겨난 게 아니야. 누군가 불러내고 있다.")
                .L(Ruru, "지휘관님, 이 깃발의 문양... 기록에 있어요. 옛 전쟁 때 봉인된 '어둠군주'의 문장이에요!")
                .L(Riri, "그럼 사라진 마을 사람들은... 제단의 제물로...?")
                .L(Pin, "그런 건 절대 못 둬요. 지휘관님, 서둘러요!")
                .L(Ark, "흥분하지 마라, 신참. 진지를 정리해야 등 뒤가 안전하다. 하나씩 간다."));

            Add(new Writer("ch1_6_before", "1-6 어둠군주의 제단")
                .Bg("story_ch1_altar")
                .N("붉은 달 아래, 무너진 신전의 제단. 그림자가 소용돌이치며 하늘로 솟구친다.")
                .L(DarkLord, "...오랜만의 손님이로군. 봉인이 풀리는 밤에 제 발로 걸어 들어오다니.")
                .L(Pin, "마을 사람들은 어디 있어! 당장 돌려보내!")
                .L(DarkLord, "저 아래 잠들어 있다. 달이 중천에 닿으면 그들의 그림자가 나의 군대가 되겠지.")
                .L(Ark, "방패는 부서져도, 지키겠다는 뜻은 부서지지 않는다. 지휘관, 여기가 끝이다.")
                .L(Riri, "모두의 상처는 제가 맡을게요. 끝까지 버텨요!")
                .L(Ruru, "약점 분석 끝났어요! 지휘관님, 명령만 내려 주세요!")
                .L(DarkLord, "오너라. 너희의 그림자도 거두어 주마."));

            Add(new Writer("ch1_6_after", "1-6 새벽")
                .Bg("story_ch1_dawn")
                .N("어둠군주가 쓰러지자 제단의 문양이 하나둘 꺼졌다. 숲을 덮던 안개가 걷히고, 동이 튼다.")
                .L(Riri, "마을 사람들 모두 무사해요. 깊이 잠들어 있었을 뿐이에요.")
                .L(Pin, "해냈다...! 봤죠, 지휘관님? 우리 꽤 괜찮은 팀이라고요!")
                .L(Ark, "첫 임무치고는 나쁘지 않았다. ...아니, 훌륭했다. 지휘관.")
                .L(Ruru, "그런데 지휘관님, 이것 좀 보세요. 어둠군주가 쓰러지면서 남긴 조각이에요.")
                .N("검은 수정 조각 안쪽에서, 차가운 서리가 맺히고 있었다.")
                .L(Ark, "북쪽 '얼어붙은 성채'의 냉기와 같다. 누군가 봉인을 하나씩 풀고 있어.")
                .L(Ruru, "그럼 다음 목적지는 정해졌네요. 지휘관님, 우리의 여정은 이제 시작이에요!")
                .Title("제1장 끝", "그림자 숲"));

            // 2 ~ 15장(StoryChapters*.cs)
            AddChapters2To5(Add);
            AddChapters6To10(Add);
            AddChapters11To15(Add);

            return d;
        }
    }
}
