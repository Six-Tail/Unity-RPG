using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Characters;
using SubcultureRPG.Meta.Cloud;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>친구 · 길드 화면 공통: 지휘관 한 줄(아바타 · 이름 · 칭호 · 레벨 · 전투력).</summary>
    public abstract class SocialScreenBase : ModeScreenBase
    {
        protected const float RowW = 1480f, RowH = 112f;
        /// <summary>오른쪽 내용 영역 왼쪽 끝(왼쪽 300px은 세로 탭) — 2026-11 개편.</summary>
        protected const float ListX = 370f;

        protected RectTransform _side;

        /// <summary>왼쪽 세로 탭 칸(위에서부터 쌓임).</summary>
        protected void BuildSide(float h = 620)
        {
            _side = UIFactory.NewRect("SideTabs", _frame);
            Place(_side, 40, -130, 300, h);
            var tv = _side.gameObject.AddComponent<VerticalLayoutGroup>();
            tv.spacing = 10; tv.childAlignment = TextAnchor.UpperLeft;
            tv.childControlWidth = true; tv.childControlHeight = true;
            tv.childForceExpandWidth = true; tv.childForceExpandHeight = false;
        }

        /// <summary>세로 탭 하나(아이콘 · 이름 · 한 줄 상태 · 빨간 느낌표).</summary>
        protected void SideTab(string icon, string name, string sub, bool on, bool badge, System.Action onClick)
        {
            var bg = UIFactory.AddImage("Tab", _side, on ? Blue : new Color(0.08f, 0.09f, 0.13f, 0.9f), ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight = 92;
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(() => onClick());
            bg.gameObject.AddComponent<ButtonFx>();
            var rt = bg.rectTransform;
            var sp = GameArt.Load(icon);
            if (sp != null)
            {
                var im = UIFactory.AddImage("Icon", rt, Color.white, sp);
                im.preserveAspect = true; im.raycastTarget = false;
                UIFactory.Anchor(im.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));
                im.rectTransform.sizeDelta = new Vector2(70, 70); im.rectTransform.anchoredPosition = new Vector2(10, 0);
            }
            var t = Text(rt, $"<b>{name}</b>\n<size=72%>{sub}</size>", 24, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            t.lineSpacing = -4f;
            UIFactory.Stretch(t.rectTransform, 90, 8, 10, 8);
            if (badge) NotifyBadge.Attach(rt, font, 4, 4, 26).Set(true);
        }

        protected static GameDatabase Db()
        {
            var roster = FindAnyObjectByType<PlayerRoster>();
            return roster != null ? roster.Database : null;
        }

        /// <summary>스크롤 목록(세로 쌓기) 만들기. 반환 = 내용 칸.</summary>
        protected RectTransform MakeList(float y, float h)
        {
            var view = UIFactory.NewRect("View", _frame);
            Place(view, ListX, y, RowW + 20, h);
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
            else av.Set(Commanders.Avatar(c, Db()), c.frameId, "");   // 다른 지휘관은 기본 의상

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
            if (!me)
            {
                // 줄을 누르면 지휘관 정보창(줄 오른쪽 버튼들은 그 버튼이 먼저 받음)
                row.raycastTarget = true;
                var open = row.gameObject.AddComponent<Button>();
                open.transition = Selectable.Transition.None;
                var who = c;
                open.onClick.AddListener(() => PlayerInfoWindow.Open(who, font));
            }
            return row;
        }

        /// <summary>실제 플레이어 한 줄(서버 공개 프로필). 누르면 지휘관 정보창.</summary>
        protected Image SnapRow(Transform list, ProfileSnap s, string tag = "", string note = "", float noteW = 300)
        {
            var db = Db();
            var row = Row(list, new Color(0.07f, 0.1f, 0.12f, 0.9f));
            var av = new ProfileAvatar(row.transform, 92);
            Place(av.Rect, 18, -10, 92, 92);
            av.Set(db != null && !string.IsNullOrEmpty(s.avatarId) ? db.GetCharacter(s.avatarId) : null, s.frameId, s.avatarOutfit ?? "");

            var title = ProfileRewards.Title(s.titleId);
            var t1 = Text(row.transform, $"<b>{s.name}</b>   <size=70%><color=#7CFF9A>● 플레이어</color>" + (tag != "" ? $"   {tag}" : "") + "</size>",
                28, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(t1.rectTransform, 128, -12, 520, 40);
            string line2 = s.level > 0
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(title.color)}>{title.name}</color>   Lv.{s.level}   <color=#FFB347>전투력 {SnapStats.TotalPower(s, db):N0}</color>"
                : "<color=#9AA3B5>정보를 불러오는 중...</color>";
            var t2 = Text(row.transform, line2, 20, new Color(0.85f, 0.88f, 0.94f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(t2.rectTransform, 128, -56, 620, 30);
            string third = note != "" ? note
                : (string.IsNullOrEmpty(s.greeting) ? "" : $"\"{s.greeting}\"   ") + (s.updated > 0 ? $"<color=#9AA3B5>{PlayerInfoWindow.Ago(s.updated)} 갱신</color>" : "");
            var t3 = Text(row.transform, third, 18, new Color(0.72f, 0.75f, 0.82f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(t3.rectTransform, 660, -14, noteW, 84);
            t3.textWrappingMode = TextWrappingModes.Normal;
            if (s.level > 0)
            {
                row.raycastTarget = true;
                var open = row.gameObject.AddComponent<Button>();
                open.transition = Selectable.Transition.None;
                var who = s;
                open.onClick.AddListener(() => PlayerInfoWindow.Open(who, font));
            }
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
        private RectTransform _list;
        private TextMeshProUGUI _info, _meName, _meInfo;
        private ProfileAvatar _meAvatar;
        private GameObject _findBar, _greetAll;
        private TMP_InputField _uid;
        private int _found = -1;
        private ProfileSnap _foundSnap;       // 검색으로 찾은 실제 플레이어
        private bool _searching;
        private readonly HashSet<string> _loading = new();

        private void Awake()
        {
            BuildBase("친구", "Backgrounds/friends", 73);
            CurrencyBar.Create(_frame, font, null, new Vector2(-50, -32), Currency.Stamina, Currency.Gold);

            BuildSide(330);

            // 왼쪽 아래: 내 카드(아바타 · 이름 · UID) + 인사 안내 · 모두 인사
            var me = Panel(_frame, new Color(0.06f, 0.07f, 0.11f, 0.9f));
            Place(me.rectTransform, 40, -480, 300, 520);
            _meAvatar = new ProfileAvatar(me.transform, 150);
            UIFactory.Anchor(_meAvatar.Rect, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
            _meAvatar.Rect.anchoredPosition = new Vector2(0, -22);
            _meName = Text(me.transform, "", 26, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Top(_meName.rectTransform, 0, -184, 280, 36, new Vector2(0.5f, 1));
            _meInfo = Text(me.transform, "", 19, new Color(0.85f, 0.88f, 0.94f), FontStyles.Normal, TextAlignmentOptions.Center);
            _meInfo.lineSpacing = 8f;
            Top(_meInfo.rectTransform, 0, -224, 280, 200, new Vector2(0.5f, 1));
            var gb = UIFactory.AddButton("GreetAll", me.transform, "모두 인사", 24, font, Green, Color.white);
            UIFactory.Anchor((RectTransform)gb.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            ((RectTransform)gb.transform).sizeDelta = new Vector2(256, 64); ((RectTransform)gb.transform).anchoredPosition = new Vector2(0, 20);
            gb.onClick.AddListener(() =>
            {
                int n = PlayerSocial.Instance.GreetAll();
                Toast(n > 0 ? $"{n}명에게 인사 — 행동력 +{n * PlayerSocial.GreetStamina} · 금화 +{n * PlayerSocial.GreetGold:N0}" : "오늘은 모두에게 인사했습니다");
            });
            _greetAll = gb.gameObject;

            // 오른쪽 위: 탭 이름 · 안내
            _info = Text(_frame, "", 30, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(_info.rectTransform, ListX + 10, -130, RowW, 50);

            // 친구 찾기: UID 입력
            var bar = UIFactory.NewRect("FindBar", _frame);
            Place(bar, ListX + 10, -192, RowW, 60);
            _findBar = bar.gameObject;
            _uid = Input(bar, "UID 8자리 입력", 8, 24);
            _uid.contentType = TMP_InputField.ContentType.IntegerNumber;
            Place((RectTransform)_uid.transform, 0, 0, 420, 56);
            var search = UIFactory.AddButton("Search", bar, "검색", 22, font, Blue, Color.white);
            Place((RectTransform)search.transform, 434, 0, 140, 56);
            search.onClick.AddListener(Search);
            var re = UIFactory.AddButton("Re", bar, "추천 새로고침", 22, font, Dim, Color.white);
            Place((RectTransform)re.transform, 588, 0, 200, 56);
            re.onClick.AddListener(() => { _found = -1; _foundSnap = null; PlayerSocial.Instance.RefreshRecommend(); });

            _list = MakeList(-196, 844);
            _root.SetActive(false);
            PlayerSocial.Instance.OnChanged += () => { if (_root != null && _root.activeSelf) Refresh(); };
            FriendLink.Instance.OnChanged += () => { if (_root != null && _root.activeSelf) Refresh(); };
        }

        private float _syncAsked = -999f;

        /// <summary>
        /// UID 검색: 먼저 서버에서 실제 플레이어를 찾고(로그인 중), 없으면 그 UID의 가상 지휘관.
        /// (실제 플레이어 UID와 가상 지휘관 UID가 같은 8자리 범위라, 서버를 먼저 봐야 다른 지휘관이 나오지 않음)
        /// </summary>
        private async void Search()
        {
            if (_searching) return;
            string uid = _uid.text.Trim();
            if (uid == PlayerProfile.Instance.Uid) { Toast("내 UID입니다"); return; }
            int seed = Commanders.SeedOfUid(uid);
            if (seed < 0) { Toast("UID 8자리를 정확히 입력해 주세요"); return; }
            _found = -1; _foundSnap = null;

            if (PublicProfile.Online)
            {
                _searching = true;
                Toast("검색 중...");
                var (snap, err) = await PublicProfile.Instance.FindByUidAsync(uid);
                _searching = false;
                if (this == null) return;
                if (snap != null) { _foundSnap = snap; Toast($"{snap.name} 님을 찾았습니다"); Refresh(); return; }
                if (err != null) Toast(err);
            }
            _found = seed;
            Refresh();
        }

        /// <summary>실제 플레이어 친구의 공개 프로필(없으면 불러오고 끝나면 다시 그림). 불러오기 전엔 저장된 이름만.</summary>
        private ProfileSnap FriendSnap(PlayerSocial.RealFriend f)
        {
            if (PublicProfile.Instance.TryCached(f.pid, out var snap)) return snap;
            if (PublicProfile.Online && _loading.Add(f.pid)) LoadFriend(f.pid);
            return new ProfileSnap { pid = f.pid, uid = f.uid, name = f.name, real = true, level = 0 };
        }

        private async void LoadFriend(string pid)
        {
            var snap = await PublicProfile.Instance.LoadAsync(pid);
            if (this == null) return;
            if (snap != null) PlayerSocial.Instance.UpdateReal(pid, snap.uid, snap.name);
            if (_root != null && _root.activeSelf && _tab == 0 && snap != null) Refresh();
        }

        protected override void Refresh()
        {
            var s = PlayerSocial.Instance;
            var db = Db();
            var link = FriendLink.Instance;
            // 화면을 보는 동안 30초마다 서버와 친구 신청 · 수락을 맞춤
            if (FriendLink.Online && Time.unscaledTime - _syncAsked > 30f) { _syncAsked = Time.unscaledTime; link.RequestSync(); }
            if (link.Notices.Count > 0) { Toast(string.Join("  ·  ", link.Notices)); link.Notices.Clear(); }
            int req = s.Requests.Count + link.Incoming.Count;
            Clear(_side);
            SideTab("Items/FriendTabList", "친구", $"<color=#9AA3B5>{s.FriendCount} / {PlayerSocial.MaxFriends}명</color>", _tab == 0, false, () => SelectTab(0));
            SideTab("Items/FriendTabRequest", "받은 신청", req > 0 ? $"<color=#FFD36B>새 신청 {req}</color>" : "<color=#9AA3B5>없음</color>", _tab == 1, req > 0, () => SelectTab(1));
            SideTab("Items/FriendTabFind", "친구 찾기", "<color=#9AA3B5>UID 검색 · 추천</color>", _tab == 2, false, () => SelectTab(2));

            var pf = PlayerProfile.Instance;
            _meAvatar.Set(ProfileAvatar.CharacterOf(pf, FindAnyObjectByType<PlayerRoster>()), pf.FrameId);
            _meName.text = pf.DisplayName;
            _meInfo.text = $"<color=#9AA3B5>내 UID</color>  <b>{pf.Uid}</b>\n<color=#9AA3B5>Lv.</color>{pf.Level}   <color=#FFB347>{MyPower():N0}</color>\n\n<size=90%><color=#9AA3B5>인사 1번마다</color>\n행동력 +{PlayerSocial.GreetStamina} · 금화 +{PlayerSocial.GreetGold}\n<color=#9AA3B5>매일 05:00 초기화</color></size>";

            _findBar.SetActive(_tab == 2);
            _greetAll.SetActive(_tab == 0);
            _info.text = _tab switch
            {
                0 => "<b>친구</b>",
                1 => "<b>받은 신청</b>",
                _ => "<b>친구 찾기</b>",
            };
            ((RectTransform)_list.parent).anchoredPosition = new Vector2(ListX, _tab == 2 ? -262 : -196);
            ((RectTransform)_list.parent).sizeDelta = new Vector2(RowW + 20, _tab == 2 ? 778 : 844);

            Clear(_list);
            if (_tab == 0)
            {
                if (s.FriendCount == 0) Empty("아직 친구가 없습니다. '친구 찾기'에서 추천 지휘관을 추가하거나 UID로 찾아 보세요.");
                foreach (var f in new List<PlayerSocial.RealFriend>(s.RealFriends))
                {
                    string pid = f.pid, fname = f.name;
                    var row = SnapRow(_list, FriendSnap(f), $"<color=#9AA3B5>UID {f.uid}</color>", "", 150);
                    int unread = ChatService.Instance.UnreadOf(pid);
                    RowButton(row, 3, unread > 0 ? $"대화 <color=#FF6B6B>●{unread}</color>" : "대화", Green, () => ChatWindow.Open(font, pid)).GetComponentInChildren<TextMeshProUGUI>().richText = true;
                    bool done = s.GreetedReal(pid);
                    RowButton(row, 1, done ? "인사 완료" : "인사", Green, () =>
                    {
                        if (PlayerSocial.Instance.GreetReal(pid)) Toast($"인사를 보냈습니다 — 행동력 +{PlayerSocial.GreetStamina} · 금화 +{PlayerSocial.GreetGold}");
                    }, !done);
                    RowButton(row, 0, "삭제", Red, () => Confirm($"{fname} 님을 친구에서 삭제할까요?", "삭제", () => PlayerSocial.Instance.RemoveReal(pid)));
                    var rf = f;
                    RowButton(row, 2, "친선 전투", Gold, () =>
                    {
                        var snap = FriendSnap(rf);
                        if (snap.level <= 0 || !FriendlyBattle.Request(snap)) Toast("상대 정보를 불러오는 중입니다. 잠시 뒤 다시 눌러 주세요");
                    });
                }
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
                    RowButton(row, 2, "친선 전투", Gold, () =>
                    {
                        var dbx = Db();
                        if (!FriendlyBattle.Request(PublicProfile.FromCommander(Commanders.FromSeed(id, dbx), dbx))) Toast("지금은 친선 전투를 시작할 수 없습니다");
                    });
                }
            }
            else if (_tab == 1)
            {
                if (!string.IsNullOrEmpty(link.Error)) Empty($"<color=#FF8A8A>{link.Error}</color>");
                // 실제 플레이어의 신청
                foreach (var snap in new List<ProfileSnap>(link.Incoming))
                {
                    var sn = snap;
                    var row = SnapRow(_list, sn, $"<color=#FFE08A>친구 신청</color>  <color=#9AA3B5>UID {sn.uid}</color>");
                    RowButton(row, 1, "수락", Blue, () => { string err = PlayerSocial.Instance.AcceptReal(sn.pid, sn.uid, sn.name); Toast(err ?? $"{sn.name} 님과 친구가 되었습니다"); });
                    RowButton(row, 0, "거절", Dim, () => PlayerSocial.Instance.DeclineReal(sn.pid));
                }
                if (s.Requests.Count == 0 && link.Incoming.Count == 0 && s.Outgoing.Count == 0) Empty("받은 친구 신청이 없습니다. 신청은 날마다 조금씩 들어옵니다.");
                foreach (int seed in new List<int>(s.Requests))
                {
                    int id = seed;
                    var row = CommanderRow(_list, Commanders.FromSeed(seed, db), "<color=#FFE08A>친구 신청</color>");
                    RowButton(row, 1, "수락", Blue, () => { string err = PlayerSocial.Instance.Add(id); Toast(err ?? "친구가 되었습니다"); });
                    RowButton(row, 0, "거절", Dim, () => PlayerSocial.Instance.Decline(id));
                }
                // 내가 보낸 신청(수락 대기)
                foreach (var o in new List<PlayerSocial.RealFriend>(s.Outgoing))
                {
                    string pid = o.pid;
                    var row = SnapRow(_list, FriendSnap(o), $"<color=#9AD4FF>보낸 신청 · 수락 대기 중</color>",
                        $"<color=#9AA3B5>상대가 '받은 신청'에서 수락하면 친구가 됩니다 (보낸 신청 {s.Outgoing.Count}/{FriendLink.Slots})</color>");
                    RowButton(row, 0, "신청 취소", Dim, () => PlayerSocial.Instance.CancelRequest(pid));
                }
            }
            else
            {
                if (_foundSnap != null)
                {
                    var fs = _foundSnap;
                    var row = SnapRow(_list, fs, $"<color=#9AD4FF>검색 결과</color>  <color=#9AA3B5>UID {fs.uid}</color>");
                    bool friend = PlayerSocial.Instance.IsRealFriend(fs.pid), pending = PlayerSocial.Instance.IsPending(fs.pid);
                    RowButton(row, 0, friend ? "친구" : pending ? "신청함" : "친구 신청", Blue, () =>
                    {
                        string err = PlayerSocial.Instance.SendRequest(fs.pid, fs.uid, fs.name);
                        Toast(err ?? $"{fs.name} 님에게 친구 신청을 보냈습니다 — 상대가 수락하면 친구가 됩니다");
                    }, !friend && !pending);
                }
                if (_found >= 0) AddRow(_found, "<color=#9AD4FF>검색 결과</color>", db);
                // 검색했으면 검색 결과만(추천은 숨김 — 추천 새로고침 · 탭 전환으로 다시 보임)
                if (_foundSnap == null && _found < 0)
                    foreach (int seed in new List<int>(s.Recommended)) AddRow(seed, "<color=#9AA3B5>추천</color>", db);
            }
        }

        private void SelectTab(int i) { _tab = i; _found = -1; _foundSnap = null; Refresh(); }

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
    /// 길드 화면(docs/guild.md).
    /// · 미가입: 창설(로그인 중이면 실제 길드) · 탭 [길드 찾기](실제 플레이어 길드 + 가상 길드 추천) · [순위]
    /// · 가입: 위 = 길드 정보 · 출석 · 기부 · 채팅(실제 길드) · 탈퇴, 탭 [길드원] · [가입 신청](실제 길드장) · [길드 보스] · [길드전] · [순위]
    /// </summary>
    public class GuildScreen : SocialScreenBase
    {
        private enum T { Find, Members, Requests, Boss, War, Rank }

        private RectTransform _list, _head;
        private GameObject _createBar;
        private TMP_InputField _name;
        private TextMeshProUGUI _createTip;
        private Button _freeBtn;
        private bool _createFree = true;
        private T _tab = T.Find;
        private GuildRanking.Tab _rankTab = GuildRanking.Tab.Boss;

        /// <summary>길드 보스 도전 · 길드전 공격(편성 화면으로) — ScreenNavigator가 연결.</summary>
        public event System.Action OnBossChallenge;
        public event System.Action<int> OnWarChallenge;

        private void Awake()
        {
            BuildBase("길드", "Backgrounds/guild", 73);
            CurrencyBar.Create(_frame, font, null, new Vector2(-50, -32), Currency.Crystal, Currency.Gold);

            BuildSide();
            var head = Panel(_frame, new Color(0.06f, 0.07f, 0.11f, 0.9f));
            Place(head.rectTransform, ListX + 10, -130, RowW, 170);
            _head = head.rectTransform;

            // 미가입: 창설 줄
            var cbg = Panel(_frame, new Color(0.06f, 0.07f, 0.11f, 0.9f));
            var bar = cbg.rectTransform;
            Place(bar, ListX + 10, -130, RowW, 170);
            _createBar = bar.gameObject;
            _createTip = Text(bar, "", 28, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Place(_createTip.rectTransform, 28, -18, 1420, 80);
            _name = Input(bar, $"새 길드 이름 ({PlayerGuild.MinNameLength} ~ {PlayerGuild.MaxNameLength}자)", PlayerGuild.MaxNameLength, 24);
            Place((RectTransform)_name.transform, 28, -100, 440, 54);
            _freeBtn = UIFactory.AddButton("Free", bar, "", 20, font, Dim, Color.white);
            Place((RectTransform)_freeBtn.transform, 482, -100, 190, 54);
            _freeBtn.onClick.AddListener(() => { _createFree = !_createFree; Refresh(); });
            var create = UIFactory.AddButton("Create", bar, "길드 창설", 22, font, Gold, Color.white);
            Place((RectTransform)create.transform, 686, -100, 180, 54);
            create.onClick.AddListener(() =>
            {
                string n = PlayerGuild.CleanName(_name.text);
                if (n.Length < PlayerGuild.MinNameLength) { Toast($"길드 이름은 {PlayerGuild.MinNameLength} ~ {PlayerGuild.MaxNameLength}자입니다"); return; }
                string kind = GuildLink.Online ? $"플레이어 길드({(_createFree ? "자유 가입" : "승인 후 가입")})" : "길드(로그인하지 않아 가상 길드원과 함께)";
                Confirm($"금화 {PlayerGuild.CreateCost:N0}으로 {kind} '{n}'을(를) 만들까요?", "창설", () =>
                {
                    string err = PlayerGuild.Instance.Create(n, _createFree);
                    Toast(err ?? $"길드 '{n}' 창설!");
                    if (err == null) _tab = T.Members;
                });
            });
            var re = UIFactory.AddButton("Re", bar, "추천 새로고침", 22, font, Dim, Color.white);
            Place((RectTransform)re.transform, 1262, -100, 200, 54);
            re.onClick.AddListener(() => PlayerGuild.Instance.RefreshRecommend());

            _list = MakeList(-316, 724);
            _root.SetActive(false);
            PlayerGuild.Instance.OnChanged += RefreshIfOpen;
            GuildLink.Instance.OnChanged += RefreshIfOpen;
            GuildWar.Instance.OnChanged += RefreshIfOpen;
        }

        private void RefreshIfOpen() { if (_root != null && _root.activeSelf) Refresh(); }

        private float _askedAt = -999f;

        /// <summary>화면이 열려 있는 동안 30초에 한 번 동기화 · 검색을 앞당김.</summary>
        private void AskSync()
        {
            if (!GuildLink.Online || Time.unscaledTime - _askedAt < 30f) return;
            _askedAt = Time.unscaledTime;
            GuildLink.Instance.RequestSync();
            GuildLink.Instance.RequestSearch();
        }

        // ---------------- 탭 ----------------

        private List<T> TabsNow()
        {
            var g = PlayerGuild.Instance;
            if (!g.Joined) return new List<T> { T.Find, T.Rank };
            var list = new List<T> { T.Members };
            if (g.IsReal && g.IsLeader) list.Add(T.Requests);
            list.Add(T.Boss); list.Add(T.War); list.Add(T.Rank);
            return list;
        }

        private string TabLabel(T t)
        {
            var g = PlayerGuild.Instance;
            return t switch
            {
                T.Find => "길드 찾기",
                T.Members => "길드원",
                T.Requests => "가입 신청",
                T.Boss => "길드 보스",
                T.War => "길드전",
                _ => "순위",
            };
        }

        private static string TabIcon(T t) => t switch
        {
            T.Find => "Items/GuildTabFind",
            T.Members => "Items/GuildTabMembers",
            T.Requests => "Items/GuildTabRequests",
            T.Boss => "Items/GuildTabBoss",
            T.War => "Items/GuildTabWar",
            _ => "Items/GuildTabRank",
        };

        /// <summary>왼쪽 세로 탭(아이콘 · 이름 · 한 줄 상태 · 빨간 느낌표).</summary>
        private void BuildTabs()
        {
            Clear(_side);
            var g = PlayerGuild.Instance;
            var tabs = TabsNow();
            if (!tabs.Contains(_tab)) _tab = tabs[0];
            foreach (var t in tabs)
            {
                var tt = t;
                string sub; bool badge = false;
                switch (t)
                {
                    case T.Find: sub = "<color=#9AA3B5>가입할 길드 찾기</color>"; break;
                    case T.Members:
                        sub = g.Attended ? $"<color=#9AA3B5>길드원 {g.Members}/{PlayerGuild.MaxMembers}</color>" : "<color=#FFD36B>오늘 출석 전</color>";
                        badge = !g.Attended; break;
                    case T.Requests:
                        sub = g.Requests.Count > 0 ? $"<color=#FFD36B>새 신청 {g.Requests.Count}</color>" : "<color=#9AA3B5>모집 설정</color>";
                        badge = g.Requests.Count > 0; break;
                    case T.Boss:
                        int cl = GuildBoss.ClaimableCount;
                        sub = cl > 0 ? $"<color=#FFD36B>받을 보상 {cl}</color>" : $"<color=#9AA3B5>{GuildBoss.Current(GuildBoss.GuildTotal)}단계 · 도전 {GuildBoss.AttemptsLeft}/{GuildBoss.AttemptsPerDay}</color>";
                        badge = cl > 0; break;
                    case T.War:
                        sub = $"<color=#FFD36B>★{GuildWar.OurStars()}</color> <color=#9AA3B5>: ★{GuildWar.TheirStars()} · 공격 {GuildWar.AttemptsLeft}/{GuildWar.AttemptsPerDay}</color>"; break;
                    default: sub = "<color=#9AA3B5>보스 · 길드전 · 레벨</color>"; break;
                }
                SideTab(TabIcon(t), TabLabel(t), sub, t == _tab, badge, () => { _tab = tt; Refresh(); });
            }
        }

        // ---------------- 그리기 ----------------

        protected override void Refresh()
        {
            var g = PlayerGuild.Instance;
            AskSync();
            if (g.Notices.Count > 0)
            {
                Notice("길드 알림", string.Join("\n", g.Notices), 380);
                g.Notices.Clear();
            }
            bool joined = g.Joined;
            _createBar.SetActive(!joined);
            _head.gameObject.SetActive(joined);
            Clear(_list);
            for (int i = _head.childCount - 1; i >= 0; i--) Destroy(_head.GetChild(i).gameObject);
            if (joined) BuildHead(g); else BuildCreateBar(g);
            BuildTabs();
            switch (_tab)
            {
                case T.Find: BuildFind(g); break;
                case T.Members: BuildMembers(g); break;
                case T.Requests: BuildRequests(g); break;
                case T.Boss: BuildBoss(g); break;
                case T.War: BuildWar(g); break;
                default: BuildRank(g); break;
            }
        }

        private void BuildCreateBar(PlayerGuild g)
        {
            string req = !string.IsNullOrEmpty(g.RequestedGid)
                ? $"   <size=75%><color=#FFE08A>'{g.RequestedName}' 길드에 가입 신청 중 — 길드장이 수락하면 가입됩니다</color></size>" : "";
            string online = GuildLink.Online
                ? "로그인 중이라 만든 길드는 <color=#7CFF9A>다른 플레이어가 찾아 가입하는 플레이어 길드</color>가 됩니다."
                : "로그인하지 않아 만든 길드는 가상 길드원과 함께하는 길드가 됩니다(플레이어 길드는 설정 → 로그인).";
            _createTip.text = $"<b>길드에 가입해 보세요</b>{req}\n<size=75%><color=#9AA3B5>길드 창설 비용 금화 {PlayerGuild.CreateCost:N0}</color></size>";
            _freeBtn.gameObject.SetActive(GuildLink.Online);
            _freeBtn.GetComponentInChildren<TextMeshProUGUI>().text = _createFree ? "가입: 자유" : "가입: 승인 후";
        }

        private void BuildHead(PlayerGuild g)
        {
            var icon = GameArt.Load("Items/GuildIcon");
            if (icon != null)
            {
                var im = UIFactory.AddImage("Icon", _head, Color.white, icon);
                im.preserveAspect = true;
                Place(im.rectTransform, 22, -20, 130, 130);
            }
            string tag = g.IsReal ? "   <color=#7CFF9A>● 플레이어 길드</color>" : "";
            var name = Text(_head, $"<b>{g.Name}</b>   <size=70%><color=#FFD36B>Lv.{g.Level}</color>   <color=#9AA3B5>길드원 {g.Members}/{PlayerGuild.MaxMembers}</color>{tag}</size>",
                34, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(name.rectTransform, 172, -16, 700, 48);
            string sync = g.IsReal && GuildLink.Instance.Error != null ? $"   <color=#FFB347>{GuildLink.Instance.Error}</color>" : "";
            var intro = Text(_head, $"\"{g.Intro}\"   <color=#9AA3B5>이번 주 내 기여도 {g.Contribution:N0}</color>{sync}", 20, new Color(0.82f, 0.85f, 0.92f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(intro.rectTransform, 172, -68, 700, 30);
            bool max = g.Level >= PlayerGuild.MaxLevel;
            int need = PlayerGuild.ExpForNext(g.Level);
            var bg = UIFactory.AddImage("ExpBg", _head, new Color(0, 0, 0, 0.5f));
            Place(bg.rectTransform, 172, -112, 600, 14);
            var fill = UIFactory.AddImage("Fill", bg.transform, new Color(0.45f, 0.8f, 1f));
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(max ? 1f : Mathf.Clamp01(g.Exp / (float)need), 1f);
            fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;
            var exp = Text(_head, max ? "최고 레벨" : $"길드 경험치 {g.Exp:N0} / {need:N0}", 18, new Color(0.75f, 0.78f, 0.86f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(exp.rectTransform, 172, -130, 600, 26);

            HeadButton(g.Attended ? "출석 완료" : $"출석\n<size=62%>금화 {g.AttendGoldNow:N0} · 크리스탈 {PlayerGuild.AttendCrystals}</size>", 880, 220, Green, !g.Attended, () =>
            {
                int gold = PlayerGuild.Instance.AttendGoldNow;
                if (PlayerGuild.Instance.Attend()) Toast($"출석 완료 — 금화 +{gold:N0} · 크리스탈 +{PlayerGuild.AttendCrystals}");
            });
            HeadButton($"기부 {g.DonationsLeft}/{PlayerGuild.DonatePerDay}\n<size=62%>금화 {PlayerGuild.DonateCost:N0} → 크리스탈 {PlayerGuild.DonateCrystals}</size>", 1110, 170, Gold, g.DonationsLeft > 0, () =>
            {
                string err = PlayerGuild.Instance.Donate();
                Toast(err ?? $"기부 완료 — 기여도 +{PlayerGuild.DonateExp} · 크리스탈 +{PlayerGuild.DonateCrystals}");
            });
            if (g.IsReal)
            {
                int unread = ChatService.Instance.UnreadGuild;
                HeadButton("채팅" + (unread > 0 ? $"\n<size=62%><color=#FF6B6B>●{unread}</color></size>" : ""), 1290, 80, Blue, true,
                    () => ChatWindow.Open(font, ChatService.GuildPeer));
            }
            bool disband = g.IsReal && g.IsLeader;
            HeadButton(disband ? "해산" : "탈퇴", 1380, 82, Red, true, () => Confirm(
                disband ? $"길드장이 나가면 길드 '{g.Name}'이(가) 해산됩니다.\n<size=80%><color=#9AA3B5>길드원은 다음 동기화 때 자동으로 나가집니다(길드장 위임은 다음 버전).</color></size>" : $"길드 '{g.Name}'에서 탈퇴할까요?",
                disband ? "해산" : "탈퇴", () => { PlayerGuild.Instance.Leave(); _tab = T.Find; }));
        }

        private void HeadButton(string label, float x, float w, Color color, bool enabled, System.Action onClick)
        {
            var b = UIFactory.AddButton("B", _head, label, 24, font, enabled ? color : new Color(1, 1, 1, 0.08f), Color.white);
            Place((RectTransform)b.transform, x, -40, w, 90);
            b.GetComponentInChildren<TextMeshProUGUI>().richText = true;
            b.interactable = enabled;
            b.onClick.AddListener(() => onClick());
        }

        private TextMeshProUGUI RightNote(Image row, string text, float w = 360, float right = 28)
        {
            var c = Text(row.transform, text, 24, Color.white, FontStyles.Normal, TextAlignmentOptions.Right);
            UIFactory.Anchor(c.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            c.rectTransform.sizeDelta = new Vector2(w, 90); c.rectTransform.anchoredPosition = new Vector2(-right, 0);
            c.richText = true;
            return c;
        }

        private void EmptyRow(string msg)
        {
            var row = Row(_list, new Color(0.07f, 0.08f, 0.12f, 0.75f), 90);
            var t = Text(row.transform, msg, 22, new Color(0.7f, 0.74f, 0.82f), FontStyles.Normal, TextAlignmentOptions.Center);
            UIFactory.Stretch(t.rectTransform, 20, 10, 20, 10);
            t.textWrappingMode = TextWrappingModes.Normal;
        }

        // ---------------- 길드 찾기 ----------------

        private void BuildFind(PlayerGuild g)
        {
            int myLevel = PlayerProfile.Instance.Level;
            var link = GuildLink.Instance;
            if (GuildLink.Online)
            {
                if (link.Error != null) EmptyRow($"<color=#FFB347>{link.Error}</color>");
                else if (link.Searching && link.Found.Count == 0) EmptyRow("플레이어 길드를 찾는 중...");
                foreach (var info in link.Found) GuildRow(info, myLevel, g);
                if (!link.Searching && link.Found.Count == 0 && link.Error == null) EmptyRow("모집 중인 플레이어 길드가 아직 없습니다 — 직접 만들어 친구를 초대해 보세요!");
            }
            foreach (var info in g.Recommended()) GuildRow(info, myLevel, g);
        }

        private void GuildRow(GuildInfo gi, int myLevel, PlayerGuild g)
        {
            var row = Row(_list, gi.Real ? new Color(0.07f, 0.11f, 0.1f, 0.92f) : new Color(0.07f, 0.08f, 0.12f, 0.9f), 100);
            OpenOnClick(row, gi);
            var icon = GameArt.Load("Items/GuildIcon");
            if (icon != null)
            {
                var im = UIFactory.AddImage("Icon", row.transform, Color.white, icon);
                im.preserveAspect = true;
                Place(im.rectTransform, 18, -10, 80, 80);
            }
            string tag = gi.Real ? $"   <color=#7CFF9A>● 플레이어 길드</color>   <color=#9AA3B5>{(gi.free ? "자유 가입" : "승인 후 가입")}{(gi.leaderName != "" ? $" · 길드장 {gi.leaderName}" : "")}</color>" : "";
            var t1 = Text(row.transform, $"<b>{gi.name}</b>   <size=72%><color=#FFD36B>Lv.{gi.level}</color>   <color=#9AA3B5>길드원 {gi.members}/{PlayerGuild.MaxMembers}</color>{tag}</size>",
                28, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(t1.rectTransform, 116, -12, 1100, 40);
            bool ok = myLevel >= gi.minLevel;
            var t2 = Text(row.transform, $"\"{gi.intro}\"   <color={(ok ? "#9AA3B5" : "#FF8A8A")}>가입 조건 지휘관 Lv.{gi.minLevel} 이상</color>", 20,
                new Color(0.82f, 0.85f, 0.92f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(t2.rectTransform, 116, -56, 1000, 30);
            bool requested = gi.Real && g.RequestedGid == gi.gid;
            if (requested)
            {
                RowButton(row, 0, "신청 취소", Dim, () => { PlayerGuild.Instance.CancelRequest(); Toast("가입 신청을 취소했습니다"); });
                return;
            }
            string label = gi.Real && !gi.free ? "가입 신청" : "가입";
            RowButton(row, 0, label, Blue, () =>
            {
                if (!string.IsNullOrEmpty(PlayerGuild.Instance.RequestedGid) && gi.Real)
                {
                    Toast("이미 다른 길드에 가입 신청 중입니다 — 먼저 신청을 취소해 주세요");
                    return;
                }
                string err = PlayerGuild.Instance.Join(gi);
                Toast(err ?? (gi.Real && !gi.free ? $"'{gi.name}'에 가입 신청을 보냈습니다" : $"'{gi.name}'에 가입했습니다"));
                if (err == null && PlayerGuild.Instance.Joined) _tab = T.Members;
            }, ok);
        }

        // ---------------- 다른 길드 보기(길드원 목록) ----------------

        /// <summary>줄을 누르면 그 길드의 길드원 창(줄 오른쪽 버튼은 그 버튼이 먼저 받음).</summary>
        private void OpenOnClick(Image row, GuildInfo gi)
        {
            row.raycastTarget = true;
            var b = row.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            var who = gi;
            b.onClick.AddListener(() => ShowGuild(who));
        }

        private int _viewToken;

        /// <summary>길드 정보 + 길드원 목록 창. 길드원을 누르면 지휘관 정보창.</summary>
        private void ShowGuild(GuildInfo gi)
        {
            var db = Db();
            int token = ++_viewToken;
            string tag = gi.Real ? "   <color=#7CFF9A>● 플레이어 길드</color>" : "";
            // 추천 가상 길드는 이번 주 기록이 비어 있음 → 같은 계산으로 채움
            long boss = gi.Real || gi.mine || gi.bossDamage > 0 ? gi.bossDamage
                : VirtualGuilds.BossDamage(gi.seed, gi.members, gi.level, PlayerGuild.Instance.Week, VirtualGuilds.WeekProgress());
            var p = OpenDialog($"{gi.name}   <size=65%><color=#FFD36B>Lv.{gi.level}</color>   <color=#9AA3B5>길드원 {gi.members}명</color>{tag}</size>", RowW + 80, 940);
            var info = Text(p, $"\"{gi.intro}\"   <color=#9AA3B5>가입 조건 지휘관 Lv.{gi.minLevel} 이상" +
                               (gi.Real ? $" · {(gi.free ? "자유 가입" : "승인 후 가입")}" : "") +
                               $"   ·   길드전 점수 {gi.warRating:N0}   ·   이번 주 길드 보스 {GuildBoss.Cleared(boss)}단계</color>",
                22, new Color(0.85f, 0.88f, 0.94f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(info.rectTransform, 40, -78, RowW, 34);

            var view = UIFactory.NewRect("View", p);
            Place(view, 30, -122, RowW + 20, 790);
            var content = UIFactory.NewRect("Content", view);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 8; vl.padding = new RectOffset(10, 10, 4, 4);
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandHeight = false;
            UIFactory.MakeVerticalScroll(view, content);

            void Note(string msg)
            {
                var row = Row(content, new Color(0.07f, 0.08f, 0.12f, 0.75f), 90);
                var t = Text(row.transform, msg, 22, new Color(0.7f, 0.74f, 0.82f), FontStyles.Normal, TextAlignmentOptions.Center);
                UIFactory.Stretch(t.rectTransform, 20, 10, 20, 10);
                t.textWrappingMode = TextWrappingModes.Normal;
            }
            void Stat(Image row, GuildMember m)
            {
                if (m == null) return;
                RightNote(row, $"<size=65%><color=#9AA3B5>이번 주 기여도</color></size>\n<b>{m.contribution:N0}</b>" +
                               $"\n<size=60%><color=#C9A0FF>보스 {Short(m.bossDamage)}</color>  <color=#FFD36B>★{m.warStars}</color></size>", 300);
            }
            string RoleTag(string role) => $"<color={(role == "길드장" ? "#FFD36B" : role == "부길드장" ? "#9AD4FF" : "#9AA3B5")}>{role}</color>";

            if (gi.mine)
            {
                // 우리 길드: 길드원 탭과 같은 목록
                foreach (var m in PlayerGuild.Instance.MemberList(db))
                {
                    var row = m.me ? CommanderRow(content, null, RoleTag(m.role)) : m.Real ? SnapRow(content, m.snap, RoleTag(m.role), "", 120) : CommanderRow(content, m.commander, RoleTag(m.role));
                    Stat(row, m);
                }
                return;
            }
            if (!gi.Real)
            {
                // 가상 길드: 번호로 늘 같은 길드원(가입하면 그대로 내 길드원이 됨)
                for (int i = 0; i < gi.members; i++)
                    CommanderRow(content, VirtualGuilds.Member(gi.seed, i, db), RoleTag(VirtualGuilds.RoleOf(i)));
                return;
            }

            // 실제 길드: 서버에서 불러옴
            Note("길드원을 불러오는 중...");
            LoadReal(gi, content, token, RoleTag, Stat, Note);
        }

        private async void LoadReal(GuildInfo gi, RectTransform content, int token, System.Func<string, string> roleTag,
            System.Action<Image, GuildMember> stat, System.Action<string> note)
        {
            var (members, err) = await GuildLink.Instance.LoadGuildMembersAsync(gi.gid);
            if (this == null || content == null || token != _viewToken) return;   // 창을 닫았거나 다른 길드를 엶
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            if (err != null) { note($"<color=#FFB347>{err}</color>"); return; }
            foreach (var m in members)
            {
                var row = m.me ? CommanderRow(content, null, roleTag(m.role)) : SnapRow(content, m.snap, roleTag(m.role), "", 120);
                stat(row, m);
            }
            if (members.Count < gi.members) note($"<size=85%>길드원 {gi.members}명 중 {members.Count}명을 불러왔습니다 — 다른 길드원은 아직 서버에 정보를 올리지 않았을 수 있습니다</size>");
        }

        // ---------------- 길드원 · 가입 신청 ----------------

        private void BuildMembers(PlayerGuild g)
        {
            var db = Db();
            int rank = 1;
            bool leader = g.IsReal && g.IsLeader;
            if (g.IsReal && g.RealMembers.Count == 0 && GuildLink.Instance.SyncedAt == System.DateTime.MinValue)
                EmptyRow(GuildLink.Instance.Busy ? "길드원 정보를 불러오는 중..." : "길드원 정보를 아직 불러오지 못했습니다");
            foreach (var m in g.MemberList(db))
            {
                string roleColor = m.role == "길드장" ? "#FFD36B" : m.role == "부길드장" ? "#9AD4FF" : "#9AA3B5";
                string role = $"<color={roleColor}>{m.role}</color>";
                Image row = m.me ? CommanderRow(_list, null, role)
                    : m.Real ? SnapRow(_list, m.snap, role, "", 120)
                    : CommanderRow(_list, m.commander, role);
                string note = $"<size=65%><color=#9AA3B5>주간 기여도 {rank}위</color></size>\n<b>{m.contribution:N0}</b>" +
                              $"\n<size=60%><color=#C9A0FF>보스 {Short(m.bossDamage)}</color>  <color=#FFD36B>★{m.warStars}</color></size>";
                RightNote(row, note, 300, leader && m.Real && !m.me ? 190 : 28);
                if (leader && m.Real && !m.me)
                {
                    var who = m;
                    RowButton(row, 0, "추방", Red, () => Confirm($"{who.snap?.name ?? "길드원"} 님을 길드에서 내보낼까요?", "추방", () =>
                    {
                        GuildLink.Instance.Kick(who.pid);
                        Toast("길드에서 내보냈습니다 — 상대가 다음 동기화 때 나가집니다");
                    }));
                }
                rank++;
            }
        }

        private void BuildRequests(PlayerGuild g)
        {
            // 모집 설정
            var info = g.RealInfo;
            bool free = info == null || info.free;
            var set = Row(_list, new Color(0.08f, 0.09f, 0.14f, 0.92f), 96);
            var st = Text(set.transform, $"<b>모집 설정</b>   <size=80%><color=#9AA3B5>가입 방식</color> {(free ? "자유 가입" : "승인 후 가입")}   <color=#9AA3B5>가입 조건</color> 지휘관 Lv.{info?.minLv ?? 1} 이상\n<color=#9AA3B5>소개</color> \"{g.Intro}\"</size>",
                24, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(st.rectTransform, 24, -10, 900, 80);
            int minLv = info?.minLv ?? 1;
            RowButton(set, 0, free ? "승인제로" : "자유 가입으로", Dim, () => { PlayerGuild.Instance.EditRecruit(PlayerGuild.Instance.Intro, !free, minLv); Toast("가입 방식을 바꿨습니다"); });
            RowButton(set, 1, "소개 바꾸기", Dim, () => EditIntro(free, minLv));
            RowButton(set, 2, $"조건 Lv.{NextMinLevel(minLv)}", Dim, () => { PlayerGuild.Instance.EditRecruit(PlayerGuild.Instance.Intro, free, NextMinLevel(minLv)); });

            if (g.Requests.Count == 0)
            {
                EmptyRow(free ? "자유 가입 길드라 신청 없이 바로 들어옵니다" : "받은 가입 신청이 없습니다");
                return;
            }
            foreach (var s in g.Requests)
            {
                var sn = s;
                var row = SnapRow(_list, sn, "<color=#FFE08A>가입 신청</color>");
                RowButton(row, 0, "거절", Red, () => { GuildLink.Instance.Decline(sn.pid); Toast("가입 신청을 거절했습니다"); });
                RowButton(row, 1, "수락", Green, () =>
                {
                    Toast(GuildLink.Instance.Accept(sn.pid) ? $"{sn.name} 님의 가입을 수락했습니다 — 상대가 다음 동기화 때 들어옵니다" : "정원이 가득 찼습니다");
                });
            }
        }

        private static int NextMinLevel(int cur)
        {
            int[] steps = { 1, 10, 20, 30, 40, 50, 60 };
            for (int i = 0; i < steps.Length; i++) if (steps[i] > cur) return steps[i];
            return 1;
        }

        private void EditIntro(bool free, int minLv)
        {
            var p = OpenDialog("길드 소개 바꾸기", 900, 300);
            var field = Input(p, $"소개 ({PlayerGuild.MaxIntroLength}자까지)", PlayerGuild.MaxIntroLength, 24);
            field.text = PlayerGuild.Instance.Intro;
            Place((RectTransform)field.transform, 30, -90, 840, 60);
            var ok = UIFactory.AddButton("Ok", p, "저장", 24, font, Blue, Color.white);
            Place((RectTransform)ok.transform, 340, -190, 220, 60);
            ok.onClick.AddListener(() =>
            {
                PlayerGuild.Instance.EditRecruit(field.text, free, minLv);
                CloseDialog();
                Toast("길드 소개를 바꿨습니다");
            });
        }

        // ---------------- 길드 보스 ----------------

        private void BuildBoss(PlayerGuild g)
        {
            long total = GuildBoss.GuildTotal;
            int cleared = GuildBoss.Cleared(total), cur = GuildBoss.Current(total);
            long hp = GuildBoss.StageHp(cur), into = System.Math.Min(hp, total - GuildBoss.ClearAt(cur - 1));
            bool done = cleared >= GuildBoss.MaxStage;

            var top = Row(_list, new Color(0.09f, 0.06f, 0.12f, 0.94f), 250);
            var art = GameArt.Enemy(GuildBoss.BossName);
            if (art != null)
            {
                var im = UIFactory.AddImage("Boss", top.transform, Color.white, art);
                im.preserveAspect = true;
                Place(im.rectTransform, 18, -10, 230, 230);
            }
            var title = Text(top.transform, $"<b>{GuildBoss.BossName}</b>   <size=70%><color=#FFB3A0>{(done ? "모든 단계 격파!" : $"{cur}단계 · Lv.{GuildBoss.LevelOf(cur)} · {WorldRaid.Diff(GuildBoss.DifficultyOf(cur)).name}")}</color></size>",
                34, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(title.rectTransform, 270, -18, 900, 46);
            var barBg = UIFactory.AddImage("HpBg", top.transform, new Color(0, 0, 0, 0.55f));
            Place(barBg.rectTransform, 270, -78, 860, 26);
            var bar = UIFactory.AddImage("Hp", barBg.transform, new Color(0.85f, 0.3f, 0.38f));
            float left = done ? 0f : 1f - Mathf.Clamp01(into / (float)hp);
            bar.rectTransform.anchorMin = Vector2.zero; bar.rectTransform.anchorMax = new Vector2(left, 1f);
            bar.rectTransform.offsetMin = Vector2.zero; bar.rectTransform.offsetMax = Vector2.zero;
            var hpText = Text(top.transform, done ? "이번 주 보스를 모두 쓰러뜨렸습니다" : $"남은 체력 {hp - into:N0} / {hp:N0}", 20, new Color(1f, 0.86f, 0.88f), FontStyles.Normal, TextAlignmentOptions.Left);
            Place(hpText.rectTransform, 270, -108, 860, 28);
            string kind = g.IsReal ? "길드원 피해의 합" : "나 + 가상 길드원이 날마다 보탠 피해";
            var info = Text(top.transform,
                $"<color=#9AA3B5>길드 합계</color>  {total:N0}\n" +
                $"<color=#9AA3B5>이번 주 내 피해</color>  {g.MyBossDamage:N0}   <color=#9AA3B5>오늘 남은 도전</color>  <color={(GuildBoss.AttemptsLeft > 0 ? "#7CFF9A" : "#FF8A8A")}>{GuildBoss.AttemptsLeft}</color> / {GuildBoss.AttemptsPerDay}",
                22, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(info.rectTransform, 270, -142, 900, 100);
            var go = UIFactory.AddButton("Go", top.transform, done ? "격파 완료" : "도전", 30, font, done || GuildBoss.AttemptsLeft <= 0 ? new Color(1, 1, 1, 0.08f) : Red, Color.white);
            UIFactory.Anchor((RectTransform)go.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            ((RectTransform)go.transform).sizeDelta = new Vector2(240, 110); ((RectTransform)go.transform).anchoredPosition = new Vector2(-30, 0);
            go.interactable = !done && GuildBoss.AttemptsLeft > 0;
            go.onClick.AddListener(() => OnBossChallenge?.Invoke());

            for (int s = 1; s <= GuildBoss.MaxStage; s++)
            {
                int st = s;
                bool reached = cleared >= s, claimed = g.BossClaimed(s);
                var row = Row(_list, reached && !claimed ? new Color(0.1f, 0.14f, 0.1f, 0.92f) : new Color(0.07f, 0.08f, 0.12f, 0.85f), 76);
                var t = Text(row.transform, $"<b>{s}단계 격파</b>   <size=80%><color=#9AA3B5>누적 {GuildBoss.ClearAt(s):N0}</color>   <color=#8FE9FF>{GuildBoss.StageRewards[s - 1].label}</color></size>",
                    24, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                Place(t.rectTransform, 24, -18, 1100, 40);
                var b = RowButton(row, 0, claimed ? "받음" : reached ? "받기" : "미달성", claimed || !reached ? Dim : Green, () =>
                {
                    var r = GuildBoss.Claim(st);
                    if (r != null) Toast($"{st}단계 보상 — {r.Describe()}");
                }, reached && !claimed);
            }
        }

        private static string Short(long v) =>
            v >= 100_000_000 ? $"{v / 100_000_000.0:0.#}억" : v >= 10_000 ? $"{v / 10_000.0:0.#}만" : v.ToString("N0");

        // ---------------- 길드전 ----------------

        private void BuildWar(PlayerGuild g)
        {
            var db = Db();
            var war = GuildWar.Instance;
            var opp = GuildWar.Opponent();
            int ours = GuildWar.OurStars(), theirs = GuildWar.TheirStars();
            var left = SubcultureRPG.Meta.Shop.PlayerShop.UntilReset(SubcultureRPG.Meta.Shop.ShopReset.Weekly, System.DateTime.Now) ?? System.TimeSpan.Zero;

            var top = Row(_list, new Color(0.07f, 0.09f, 0.14f, 0.94f), 170);
            int oppRank = GuildWar.OpponentRank();
            var t1 = Text(top.transform, $"<b>이번 주 상대  {opp.name}</b>   <size=70%><color=#FFD36B>Lv.{opp.level}</color>   <color=#FF9A6B>길드전 점수 {opp.warRating:N0}{(oppRank > 0 ? $" · {oppRank}위" : "")}</color>   " +
                                         $"<color=#9AA3B5>결과까지 {left.Days}일 {left.Hours}시간</color></size>",
                32, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(t1.rectTransform, 24, -14, 1400, 44);
            string state = ours > theirs ? "<color=#7CFF9A>우세</color>" : ours == theirs ? "<color=#FFD36B>접전</color>" : "<color=#FF8A8A>열세</color>";
            var t2 = Text(top.transform, $"우리 길드  <color=#FFD36B><b>★{ours}</b></color>  :  <b>★{theirs}</b>  상대 길드   {state}   <size=75%><color=#9AA3B5>(최대 ★{GuildWar.MaxStars})</color></size>",
                34, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(t2.rectTransform, 24, -64, 1100, 50);
            var t3 = Text(top.transform, $"<color=#9AA3B5>오늘 남은 공격</color>  <color={(GuildWar.AttemptsLeft > 0 ? "#7CFF9A" : "#FF8A8A")}>{GuildWar.AttemptsLeft}</color> / {GuildWar.AttemptsPerDay}   " +
                                         $"<size=85%><color=#9AA3B5>모두 생존 ★3 · 절반 이상 ★2 · 그 밖 ★1</color></size>",
                22, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(t3.rectTransform, 24, -118, 1420, 40);

            var defs = war.DefenderTeams(db);
            int[] best = GuildBestStars(g);
            for (int i = 0; i < defs.Count; i++)
            {
                int idx = i;
                var com = war.DefenderCommander(i, db);
                com.power = defs[i].power;
                var row = CommanderRow(_list, com, $"<color=#FF9A6B>{i + 1}번 방어</color>",
                    $"<color=#9AA3B5>길드 최고</color> {StarStr(best[i])}\n<color=#9AA3B5>내 최고</color> {StarStr(g.WarStarsOf(i))}\n<size=85%><color=#9AA3B5>Lv.{defs[i].level} · +{defs[i].transcend}</color></size>");
                bool can = GuildWar.AttemptsLeft > 0;
                RowButton(row, 0, "공격", can ? Red : Dim, () => OnWarChallenge?.Invoke(idx), can);
            }
        }

        /// <summary>상대 길드원별 우리 길드 최고 별.</summary>
        private static int[] GuildBestStars(PlayerGuild g)
        {
            var best = g.MyWarStars;
            if (g.IsReal)
                foreach (var m in g.RealMembers)
                    if (m.warDetail != null) for (int i = 0; i < best.Length && i < m.warDetail.Length; i++) best[i] = Mathf.Max(best[i], m.warDetail[i]);
            return best;
        }

        private static string StarStr(int n) => $"<color=#FFD36B>{new string('★', Mathf.Clamp(n, 0, 3))}</color><color=#555B66>{new string('☆', 3 - Mathf.Clamp(n, 0, 3))}</color>";

        // ---------------- 순위 ----------------

        private void BuildRank(PlayerGuild g)
        {
            if (GuildLink.Online && (System.DateTime.Now - GuildLink.Instance.RankedAt).TotalMinutes > 3) _ = GuildLink.Instance.RefreshRankAsync();
            var sub = Row(_list, new Color(0.06f, 0.07f, 0.11f, 0.9f), 76);
            var tabs = new[] { GuildRanking.Tab.Boss, GuildRanking.Tab.War, GuildRanking.Tab.Level };
            for (int i = 0; i < tabs.Length; i++)
            {
                var tt = tabs[i];
                var b = UIFactory.AddButton("Sub", sub.transform, GuildRanking.TabName(tt), 22, font, tt == _rankTab ? Blue : Dim, Color.white);
                Place((RectTransform)b.transform, 20 + i * 230, -12, 214, 52);
                b.onClick.AddListener(() => { _rankTab = tt; Refresh(); });
            }
            string realNote = GuildLink.Online ? (GuildLink.Instance.Error ?? $"플레이어 길드 {GuildLink.Instance.Ranked.Count}곳 포함") : "로그인하면 플레이어 길드도 함께 보입니다";
            var note = Text(sub.transform, GuildLink.Online && GuildLink.Instance.Error != null ? $"<size=80%><color=#FFB347>{GuildLink.Instance.Error}</color></size>" : "", 22, Color.white, FontStyles.Normal, TextAlignmentOptions.Right);
            Place(note.rectTransform, 720, -20, 740, 40);

            var list = GuildRanking.Build(_rankTab);
            int mine = list.FindIndex(x => x.mine);
            if (mine >= 0) RankRow(list[mine], mine + 1, true);
            for (int i = 0; i < list.Count && i < 50; i++) RankRow(list[i], i + 1, false);
            if (mine >= 0 && _rankTab == GuildRanking.Tab.Boss) PlayerGuild.Instance.RefreshSeen();   // 지난주 정산용 순위 기억
        }

        private void RankRow(GuildInfo gi, int rank, bool pinned)
        {
            Color bg = gi.mine ? new Color(0.25f, 0.4f, 0.65f, 0.55f) : gi.Real ? new Color(0.07f, 0.11f, 0.1f, 0.9f) : new Color(0.07f, 0.08f, 0.12f, 0.88f);
            var row = Row(_list, bg, 78);
            OpenOnClick(row, gi);
            string medal = rank == 1 ? "#FFD36B" : rank == 2 ? "#D8E0EA" : rank == 3 ? "#E0A070" : "#FFFFFF";
            var r = Text(row.transform, $"<color={medal}><b>{rank}</b></color>", 34, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            Place(r.rectTransform, 10, -14, 110, 50);
            string tag = (gi.mine ? "  <size=70%><color=#9AD4FF>우리 길드</color></size>" : "") + (gi.Real ? "  <size=70%><color=#7CFF9A>● 플레이어</color></size>" : "");
            var n = Text(row.transform, $"<b>{gi.name}</b>{tag}   <size=72%><color=#FFD36B>Lv.{gi.level}</color>   <color=#9AA3B5>길드원 {gi.members}</color></size>",
                26, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(n.rectTransform, 130, -18, 900, 42);
            string score = _rankTab switch
            {
                GuildRanking.Tab.Boss => $"<color=#C9A0FF>{gi.bossDamage:N0}</color>  <size=70%><color=#9AA3B5>{GuildBoss.Cleared(gi.bossDamage)}단계</color></size>",
                GuildRanking.Tab.War => $"<color=#FF9A6B>{gi.warRating:N0}점</color>  <size=70%><color=#FFD36B>이번 주 ★{gi.warStars}</color></size>",
                _ => $"<color=#8FE9FF>경험치 {gi.exp:N0}</color>",
            };
            RightNote(row, (pinned ? "<size=70%><color=#9AA3B5>내 길드 순위</color></size>\n" : "") + score, 520);
        }
    }
}
