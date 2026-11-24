using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>친구 · 길드 화면 공통: 지휘관 한 줄(아바타 · 이름 · 칭호 · 레벨 · 전투력).</summary>
    public abstract class SocialScreenBase : ModeScreenBase
    {
        protected const float RowW = 1480f, RowH = 112f;

        protected static GameDatabase Db()
        {
            var roster = FindAnyObjectByType<PlayerRoster>();
            return roster != null ? roster.Database : null;
        }

        /// <summary>스크롤 목록(세로 쌓기) 만들기. 반환 = 내용 칸.</summary>
        protected RectTransform MakeList(float y, float h)
        {
            var view = UIFactory.NewRect("View", _frame);
            Top(view, 0, y, RowW + 20, h, new Vector2(0.5f, 1));
            var content = UIFactory.NewRect("Content", view);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 8; vl.padding = new RectOffset(10, 10, 4, 4);
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandHeight = false;
            UIFactory.MakeVerticalScroll(view, content);
            return content;
        }

        protected Image Row(Transform list, Color c, float h = RowH)
        {
            var row = Panel(list, c);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
            return row;
        }

        /// <summary>지휘관 한 줄. c == null이면 나. 오른쪽 520px은 버튼 · 추가 정보용으로 비워 둠.</summary>
        protected Image CommanderRow(Transform list, Commander c, string tag = "", string note = "")
        {
            bool me = c == null;
            var row = Row(list, me ? new Color(0.25f, 0.4f, 0.65f, 0.55f) : new Color(0.07f, 0.08f, 0.12f, 0.9f));
            var pf = PlayerProfile.Instance;
            var av = new ProfileAvatar(row.transform, 92);
            Place(av.Rect, 18, -10, 92, 92);
            if (me) av.Set(ProfileAvatar.CharacterOf(pf, FindAnyObjectByType<PlayerRoster>()), pf.FrameId);
            else av.Set(Commanders.Avatar(c, Db()), c.frameId);

            var title = ProfileRewards.Title(me ? pf.TitleId : c.titleId);
            string name = me ? pf.DisplayName + "  <size=70%><color=#9AD4FF>나</color></size>" : c.name;
            var t1 = Text(row.transform, $"<b>{name}</b>" + (tag != "" ? $"   <size=70%>{tag}</size>" : ""), 28, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(t1.rectTransform, 128, -12, 520, 40);
            int level = me ? pf.Level : c.level;
            long power = me ? MyPower() : c.power;
            var t2 = Text(row.transform, $"<color=#{ColorUtility.ToHtmlStringRGB(title.color)}>{title.name}</color>   Lv.{level}   <color=#FFB347>전투력 {power:N0}</color>",
                20, new Color(0.85f, 0.88f, 0.94f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(t2.rectTransform, 128, -56, 620, 30);
            string third = note != "" ? note : me ? "" : (string.IsNullOrEmpty(c.greeting) ? "" : $"\"{c.greeting}\"   ") + $"<color=#9AA3B5>{Commanders.LastSeen(c)}</color>";
            var t3 = Text(row.transform, third, 18, new Color(0.72f, 0.75f, 0.82f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(t3.rectTransform, 660, -14, 300, 84);
            t3.textWrappingMode = TextWrappingModes.Normal;
            return row;
        }

        protected static long MyPower()
        {
            long sum = 0; int n = 0;
            foreach (var kv in CombatPower.All(FindAnyObjectByType<PlayerRoster>(), FindAnyObjectByType<PlayerEquipment>())) { if (n++ >= 4) break; sum += kv.Value; }
            return sum;
        }

        /// <summary>줄 오른쪽 버튼(오른쪽에서 index번째).</summary>
        protected Button RowButton(Image row, int index, string label, Color color, System.Action onClick, bool enabled = true)
        {
            var b = UIFactory.AddButton(label, row.transform, label, 22, font, enabled ? color : new Color(1, 1, 1, 0.08f), Color.white);
            var rt = (RectTransform)b.transform;
            UIFactory.Anchor(rt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            rt.sizeDelta = new Vector2(150, 58); rt.anchoredPosition = new Vector2(-18 - index * 162, 0);
            b.interactable = enabled;
            b.onClick.AddListener(() => onClick());
            return b;
        }

        protected Button TopButton(string label, float x, float y, float w, Color color, System.Action onClick)
        {
            var b = UIFactory.AddButton(label, _frame, label, 22, font, color, Color.white);
            Top((RectTransform)b.transform, x, y, w, 52, new Vector2(0.5f, 1));
            b.onClick.AddListener(() => onClick());
            return b;
        }

        protected static readonly Color Blue = new(0.25f, 0.55f, 0.9f, 0.96f), Green = new(0.3f, 0.62f, 0.42f, 0.96f),
            Red = new(0.7f, 0.32f, 0.32f, 0.96f), Dim = new(1, 1, 1, 0.14f), Gold = new(0.8f, 0.58f, 0.22f, 0.96f);
    }

    /// <summary>
    /// 친구 화면: 탭 3개 — 친구(인사 · 삭제) / 받은 신청(수락 · 거절) / 친구 찾기(UID 검색 · 추천).
    /// </summary>
    public class FriendScreen : SocialScreenBase
    {
        private int _tab;
        private readonly Button[] _tabs = new Button[3];
        private RectTransform _list;
        private TextMeshProUGUI _info;
        private GameObject _findBar, _greetAll;
        private TMP_InputField _uid;
        private int _found = -1;

        private void Awake()
        {
            BuildBase("친구", "Backgrounds/friends", 73);
            CurrencyBar.Create(_frame, font, null, new Vector2(-50, -32), Currency.Stamina, Currency.Gold);

            string[] names = { "친구", "받은 신청", "친구 찾기" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                _tabs[i] = TopButton(names[i], -620 + i * 250, -104, 236, Dim, () => { _tab = idx; _found = -1; Refresh(); });
            }
            _info = Text(_frame, "", 20, new Color(0.85f, 0.88f, 0.94f), FontStyles.Normal, TextAlignmentOptions.Right);
            Top(_info.rectTransform, 90, -186, 920, 32, new Vector2(0.5f, 1));    // 탭 줄 아래, '모두 인사' 버튼 왼쪽
            // 재화 표시(행동력 '가득참' 글자)와 붙지 않게 탭 줄이 아니라 목록 바로 위 오른쪽 끝에 둠
            _greetAll = TopButton("모두 인사", 645, -176, 190, Green, () =>
            {
                int n = PlayerSocial.Instance.GreetAll();
                Toast(n > 0 ? $"{n}명에게 인사 — 행동력 +{n * PlayerSocial.GreetStamina} · 금화 +{n * PlayerSocial.GreetGold:N0}" : "오늘은 모두에게 인사했습니다");
            }).gameObject;

            // 친구 찾기: UID 입력
            var bar = UIFactory.NewRect("FindBar", _frame);
            Top(bar, 0, -170, RowW, 60, new Vector2(0.5f, 1));
            _findBar = bar.gameObject;
            _uid = Input(bar, "UID 8자리 입력", 8, 24);
            _uid.contentType = TMP_InputField.ContentType.IntegerNumber;
            Place((RectTransform)_uid.transform, 0, 0, 420, 56);
            var search = UIFactory.AddButton("Search", bar, "검색", 22, font, Blue, Color.white);
            Place((RectTransform)search.transform, 434, 0, 140, 56);
            search.onClick.AddListener(Search);
            var re = UIFactory.AddButton("Re", bar, "추천 새로고침", 22, font, Dim, Color.white);
            Place((RectTransform)re.transform, 588, 0, 200, 56);
            re.onClick.AddListener(() => { _found = -1; PlayerSocial.Instance.RefreshRecommend(); });
            var mine = Text(bar, "", 22, new Color(0.8f, 0.83f, 0.9f), FontStyles.Normal, TextAlignmentOptions.Right);
            Place(mine.rectTransform, 880, -8, 600, 40);
            mine.text = $"내 UID  <color=#FFFFFF><b>{PlayerProfile.Instance.Uid}</b></color>";

            _list = MakeList(-244, 760);
            _root.SetActive(false);
            PlayerSocial.Instance.OnChanged += () => { if (_root != null && _root.activeSelf) Refresh(); };
        }

        private void Search()
        {
            string uid = _uid.text.Trim();
            if (uid == PlayerProfile.Instance.Uid) { Toast("내 UID입니다"); return; }
            int seed = Commanders.SeedOfUid(uid);
            if (seed < 0) { Toast("UID 8자리를 정확히 입력해 주세요"); return; }
            _found = seed;
            Refresh();
        }

        protected override void Refresh()
        {
            var s = PlayerSocial.Instance;
            var db = Db();
            string[] names = { $"친구 {s.Friends.Count}/{PlayerSocial.MaxFriends}", $"받은 신청 {s.Requests.Count}", "친구 찾기" };
            for (int i = 0; i < 3; i++)
            {
                _tabs[i].image.color = i == _tab ? Blue : Dim;
                _tabs[i].GetComponentInChildren<TextMeshProUGUI>().text = names[i];
            }
            _findBar.SetActive(_tab == 2);
            _greetAll.SetActive(_tab == 0);
            _info.text = _tab == 0 ? $"<color=#9AA3B5>친구에게 인사 1번마다 행동력 +{PlayerSocial.GreetStamina} · 금화 +{PlayerSocial.GreetGold}  (매일 05:00 초기화)</color>" : "";
            ((RectTransform)_list.parent).anchoredPosition = new Vector2(0, _tab == 2 ? -244 : _tab == 0 ? -210 : -176);
            ((RectTransform)_list.parent).sizeDelta = new Vector2(RowW + 20, _tab == 2 ? 760 : _tab == 0 ? 794 : 828);

            Clear(_list);
            if (_tab == 0)
            {
                if (s.Friends.Count == 0) Empty("아직 친구가 없습니다. '친구 찾기'에서 추천 지휘관을 추가해 보세요.");
                foreach (int seed in new List<int>(s.Friends))
                {
                    int id = seed;
                    var row = CommanderRow(_list, Commanders.FromSeed(seed, db));
                    bool done = s.Greeted(seed);
                    RowButton(row, 1, done ? "인사 완료" : "인사", Green, () =>
                    {
                        if (PlayerSocial.Instance.Greet(id)) Toast($"인사를 보냈습니다 — 행동력 +{PlayerSocial.GreetStamina} · 금화 +{PlayerSocial.GreetGold}");
                    }, !done);
                    RowButton(row, 0, "삭제", Red, () => Confirm($"{Commanders.FromSeed(id, Db()).name} 님을 친구에서 삭제할까요?", "삭제", () => PlayerSocial.Instance.Remove(id)));
                }
            }
            else if (_tab == 1)
            {
                if (s.Requests.Count == 0) Empty("받은 친구 신청이 없습니다. 신청은 날마다 조금씩 들어옵니다.");
                foreach (int seed in new List<int>(s.Requests))
                {
                    int id = seed;
                    var row = CommanderRow(_list, Commanders.FromSeed(seed, db), "<color=#FFE08A>친구 신청</color>");
                    RowButton(row, 1, "수락", Blue, () => { string err = PlayerSocial.Instance.Add(id); Toast(err ?? "친구가 되었습니다"); });
                    RowButton(row, 0, "거절", Dim, () => PlayerSocial.Instance.Decline(id));
                }
            }
            else
            {
                if (_found >= 0) AddRow(_found, "<color=#9AD4FF>검색 결과</color>", db);
                foreach (int seed in new List<int>(s.Recommended)) if (seed != _found) AddRow(seed, "<color=#9AA3B5>추천</color>", db);
            }
        }

        private void AddRow(int seed, string tag, GameDatabase db)
        {
            var c = Commanders.FromSeed(seed, db);
            var row = CommanderRow(_list, c, tag + $"  <color=#9AA3B5>UID {c.uid}</color>");
            bool friend = PlayerSocial.Instance.IsFriend(seed);
            RowButton(row, 0, friend ? "친구" : "친구 추가", Blue, () =>
            {
                string err = PlayerSocial.Instance.Add(seed);
                Toast(err ?? $"{c.name} 님과 친구가 되었습니다");
            }, !friend);
        }

        private void Empty(string msg)
        {
            var row = Row(_list, new Color(1, 1, 1, 0.05f), 140);
            var t = Text(row.transform, msg, 24, new Color(0.75f, 0.78f, 0.86f), FontStyles.Normal, TextAlignmentOptions.Center);
            UIFactory.Stretch(t.rectTransform, 20, 10, 20, 10);
        }
    }

    /// <summary>
    /// 길드 화면: 미가입 — 추천 길드 가입 · 창설 / 가입 — 길드 정보(레벨 · 경험치) · 출석 · 기부 · 길드원 목록 · 탈퇴.
    /// </summary>
    public class GuildScreen : SocialScreenBase
    {
        private RectTransform _list, _head;
        private GameObject _createBar;
        private TMP_InputField _name;

        private void Awake()
        {
            BuildBase("길드", "Backgrounds/guild", 73);
            CurrencyBar.Create(_frame, font, null, new Vector2(-50, -32), Currency.Crystal, Currency.Gold);

            var head = Panel(_frame, new Color(0.06f, 0.07f, 0.11f, 0.9f));
            Top(head.rectTransform, 0, -104, RowW, 170, new Vector2(0.5f, 1));
            _head = head.rectTransform;

            // 미가입: 창설 줄
            var bar = UIFactory.NewRect("CreateBar", _frame);
            Top(bar, 0, -104, RowW, 170, new Vector2(0.5f, 1));
            _createBar = bar.gameObject;
            var tip = Text(bar, $"<b>길드에 가입해 보세요</b>\n<size=75%><color=#9AA3B5>출석 · 기부로 금화와 크리스탈을 받고 길드를 함께 키웁니다. 직접 만들려면 금화 {PlayerGuild.CreateCost:N0}이 필요합니다.</color></size>",
                28, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Place(tip.rectTransform, 28, -18, 1420, 80);
            _name = Input(bar, $"새 길드 이름 ({PlayerGuild.MinNameLength} ~ {PlayerGuild.MaxNameLength}자)", PlayerGuild.MaxNameLength, 24);
            Place((RectTransform)_name.transform, 28, -100, 440, 54);
            var create = UIFactory.AddButton("Create", bar, "길드 창설", 22, font, Gold, Color.white);
            Place((RectTransform)create.transform, 482, -100, 180, 54);
            create.onClick.AddListener(() =>
            {
                string n = PlayerGuild.CleanName(_name.text);
                if (n.Length < PlayerGuild.MinNameLength) { Toast($"길드 이름은 {PlayerGuild.MinNameLength} ~ {PlayerGuild.MaxNameLength}자입니다"); return; }
                Confirm($"금화 {PlayerGuild.CreateCost:N0}으로 길드 '{n}'을(를) 만들까요?", "창설", () =>
                {
                    string err = PlayerGuild.Instance.Create(n);
                    Toast(err ?? $"길드 '{n}' 창설!");
                });
            });
            var re = UIFactory.AddButton("Re", bar, "추천 새로고침", 22, font, Dim, Color.white);
            Place((RectTransform)re.transform, 1262, -100, 200, 54);
            re.onClick.AddListener(() => PlayerGuild.Instance.RefreshRecommend());

            _list = MakeList(-290, 720);
            _root.SetActive(false);
            PlayerGuild.Instance.OnChanged += () => { if (_root != null && _root.activeSelf) Refresh(); };
        }

        protected override void Refresh()
        {
            var g = PlayerGuild.Instance;
            bool joined = g.Joined;
            _head.gameObject.SetActive(true);
            _createBar.SetActive(!joined);
            Clear(_list);
            for (int i = _head.childCount - 1; i >= 0; i--) Destroy(_head.GetChild(i).gameObject);
            if (!joined) { BuildRecommend(); return; }

            // --- 길드 정보 ---
            var icon = GameArt.Load("Items/GuildIcon");
            if (icon != null)
            {
                var im = UIFactory.AddImage("Icon", _head, Color.white, icon);
                im.preserveAspect = true;
                Place(im.rectTransform, 22, -20, 130, 130);
            }
            var name = Text(_head, $"<b>{g.Name}</b>   <size=70%><color=#FFD36B>Lv.{g.Level}</color>   <color=#9AA3B5>길드원 {g.Members}/{PlayerGuild.MaxMembers}</color></size>",
                34, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(name.rectTransform, 172, -16, 760, 48);
            var intro = Text(_head, $"\"{g.Intro}\"   <color=#9AA3B5>이번 주 내 기여도 {g.Contribution:N0}</color>", 20, new Color(0.82f, 0.85f, 0.92f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(intro.rectTransform, 172, -68, 760, 30);
            // 경험치 막대
            bool max = g.Level >= PlayerGuild.MaxLevel;
            int need = PlayerGuild.ExpForNext(g.Level);
            var bg = UIFactory.AddImage("ExpBg", _head, new Color(0, 0, 0, 0.5f));
            Place(bg.rectTransform, 172, -112, 600, 14);
            var fill = UIFactory.AddImage("Fill", bg.transform, new Color(0.45f, 0.8f, 1f));
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(max ? 1f : Mathf.Clamp01(g.Exp / (float)need), 1f);
            fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;
            var exp = Text(_head, max ? "최고 레벨" : $"길드 경험치 {g.Exp:N0} / {need:N0}", 18, new Color(0.75f, 0.78f, 0.86f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(exp.rectTransform, 172, -130, 600, 26);

            HeadButton(g.Attended ? "출석 완료" : $"출석\n<size=62%>금화 {g.AttendGoldNow:N0} · 크리스탈 {PlayerGuild.AttendCrystals}</size>", 960, 230, Green, !g.Attended, () =>
            {
                int gold = PlayerGuild.Instance.AttendGoldNow;
                if (PlayerGuild.Instance.Attend()) Toast($"출석 완료 — 금화 +{gold:N0} · 크리스탈 +{PlayerGuild.AttendCrystals}");
            });
            HeadButton($"기부 {g.DonationsLeft}/{PlayerGuild.DonatePerDay}\n<size=62%>금화 {PlayerGuild.DonateCost:N0} → 크리스탈 {PlayerGuild.DonateCrystals}</size>", 1200, 170, Gold, g.DonationsLeft > 0, () =>
            {
                string err = PlayerGuild.Instance.Donate();
                Toast(err ?? $"기부 완료 — 길드 경험치 +{PlayerGuild.DonateExp} · 크리스탈 +{PlayerGuild.DonateCrystals}");
            });
            HeadButton("탈퇴", 1380, 82, Red, true, () => Confirm($"길드 '{PlayerGuild.Instance.Name}'에서 탈퇴할까요?", "탈퇴", () => PlayerGuild.Instance.Leave()));

            // --- 길드원 ---
            int rank = 1;
            foreach (var m in g.MemberList(Db()))
            {
                string roleColor = m.role == "길드장" ? "#FFD36B" : m.role == "부길드장" ? "#9AD4FF" : "#9AA3B5";
                var row = CommanderRow(_list, m.me ? null : m.commander, $"<color={roleColor}>{m.role}</color>");
                var c = Text(row.transform, $"<size=70%><color=#9AA3B5>주간 기여도 {rank}위</color></size>\n<b>{m.contribution:N0}</b>", 26, Color.white, FontStyles.Normal, TextAlignmentOptions.Right);
                UIFactory.Anchor(c.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
                c.rectTransform.sizeDelta = new Vector2(300, 80); c.rectTransform.anchoredPosition = new Vector2(-28, 0);
                rank++;
            }
        }

        private void HeadButton(string label, float x, float w, Color color, bool enabled, System.Action onClick)
        {
            var b = UIFactory.AddButton("B", _head, label, 24, font, enabled ? color : new Color(1, 1, 1, 0.08f), Color.white);
            Place((RectTransform)b.transform, x, -40, w, 90);
            b.GetComponentInChildren<TextMeshProUGUI>().richText = true;
            b.interactable = enabled;
            b.onClick.AddListener(() => onClick());
        }

        private void BuildRecommend()
        {
            _head.gameObject.SetActive(false);
            int myLevel = PlayerProfile.Instance.Level;
            foreach (var info in PlayerGuild.Instance.Recommended())
            {
                var gi = info;
                var row = Row(_list, new Color(0.07f, 0.08f, 0.12f, 0.9f), 100);
                var icon = GameArt.Load("Items/GuildIcon");
                if (icon != null)
                {
                    var im = UIFactory.AddImage("Icon", row.transform, Color.white, icon);
                    im.preserveAspect = true;
                    Place(im.rectTransform, 18, -10, 80, 80);
                }
                var t1 = Text(row.transform, $"<b>{gi.name}</b>   <size=72%><color=#FFD36B>Lv.{gi.level}</color>   <color=#9AA3B5>길드원 {gi.members}/{PlayerGuild.MaxMembers}</color></size>",
                    28, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                Place(t1.rectTransform, 116, -12, 700, 40);
                bool ok = myLevel >= gi.minLevel;
                var t2 = Text(row.transform, $"\"{gi.intro}\"   <color={(ok ? "#9AA3B5" : "#FF8A8A")}>가입 조건 지휘관 Lv.{gi.minLevel} 이상</color>", 20,
                    new Color(0.82f, 0.85f, 0.92f), FontStyles.Normal, TextAlignmentOptions.Left);
                Place(t2.rectTransform, 116, -56, 1000, 30);
                RowButton(row, 0, "가입", Blue, () =>
                {
                    string err = PlayerGuild.Instance.Join(gi);
                    Toast(err ?? $"'{gi.name}'에 가입했습니다");
                }, ok);
            }
        }
    }
}
