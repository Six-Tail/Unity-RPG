using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using SubcultureRPG.Meta.Cloud;
#if VIVOX_PRESENT
using Unity.Services.Vivox;
#endif

namespace SubcultureRPG.Meta
{
    /// <summary>채팅 메시지 1개.</summary>
    public class ChatMessage
    {
        public string peer;        // 전체 채팅이면 "", 길드 채팅이면 ChatService.GuildPeer, 1:1이면 상대 플레이어 id
        public string senderId, name, uid, text;
        public DateTime time;
        public bool mine;
        public bool Direct => !string.IsNullOrEmpty(peer);
    }

    /// <summary>
    /// 채팅(Unity Vivox 텍스트 채팅 위에 올림): 전체 채팅 채널 + 길드 채팅(실제 길드 — 채널 stella_guild_{길드 id}) + 친구 1:1 대화.
    /// · 로그인(계정)해야 씀 — 게스트는 못 씀. 로그인하면 자동으로 연결
    /// · 메시지 = 짧은 JSON(이름 · UID · 내용) — 이름 표시가 Vivox 표시 이름 규칙에 묶이지 않게
    /// · 안전: 금칙어 가리기(<see cref="ChatFilter"/>) · 차단(차단한 사람 메시지 숨김, 이 계정에 저장) · 신고(서버 기록 + 자동 차단) · 도배 방지(2초에 1개 · 80자)
    /// · Vivox 패키지(com.unity.services.vivox)가 없으면 '채팅 준비 중'으로 표시만(에디터 스크립트 VivoxDefine이 패키지가 있으면 VIVOX_PRESENT를 켬)
    /// 문서: docs/chat.md
    /// </summary>
    public class ChatService : MonoBehaviour
    {
        /// <summary>전체 채팅 채널. 지난 기록을 모두 지우려면 이름 뒤 번호를 올림(새 빈 채널 — 예전 채널 기록은 더 이상 안 불러옴).</summary>
        public const string WorldChannel = "stella_world_2";
        /// <summary>길드 채팅: 채널 이름 = GuildPrefix + 길드 id, 대화 목록에서의 이름 = GuildPeer.</summary>
        public const string GuildPrefix = "stella_guild_", GuildPeer = "#guild";
        public const int MaxLength = 80, KeepWorld = 100, KeepDirect = 60;
        public const float SendInterval = 2f;
        private const string BlockKey = "chat.blocked";

        private static ChatService _instance;
        public static ChatService Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<ChatService>();
                if (_instance == null) _instance = new GameObject("ChatService").AddComponent<ChatService>();
                return _instance;
            }
        }

        /// <summary>채팅 서비스(Vivox 패키지)가 들어 있는가.</summary>
#if VIVOX_PRESENT
        public static bool Available => true;
#else
        public static bool Available => false;
#endif

        public bool Connected { get; private set; }
        public bool Connecting { get; private set; }
        public string Error { get; private set; }

        public readonly List<ChatMessage> World = new();
        private readonly Dictionary<string, List<ChatMessage>> _direct = new();
        private readonly Dictionary<string, int> _unread = new();
        public int UnreadWorld { get; private set; }
        public int UnreadGuild => UnreadOf(GuildPeer);
        private string _guildChannel = "";   // 들어가 있는(들어갈) 길드 채널

        /// <summary>메시지가 오거나 연결 상태가 바뀜.</summary>
        public event Action OnChanged;

        private float _nextTry, _lastSend = -99f;
        private static string _loggedPid;   // Vivox에 로그인한 계정(계정이 바뀌면 다시 로그인)

        private void Awake() { if (_instance == null) _instance = this; LoadBlocked(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() { _ = Instance; }

        // ---------------- 연결 ----------------

        /// <summary>
        /// 진행 중인 연결(장면을 다시 불러와 ChatService가 새로 생겨도 하나만) — 로그인이 겹치면 Vivox가
        /// 'Handle Taken(5006)'으로 거절하고 앞의 로그인은 'task canceled'가 됨.
        /// </summary>
        private static Task s_connect;

        private void Update()
        {
            if (!Available || Connected || Connecting) return;
            if (s_connect != null && !s_connect.IsCompleted) return;
            if (!CloudAccount.SignedIn || !CloudAccount.Synced) return;
            if (Time.unscaledTime < _nextTry) return;
            s_connect = ConnectAsync();
        }

        /// <summary>게임을 끌 때 채팅에서 로그아웃(서버에 남은 접속 때문에 다음 로그인이 'Handle Taken'이 되지 않게).</summary>
        private void OnApplicationQuit()
        {
#if VIVOX_PRESENT
            try { if (VivoxService.Instance.IsLoggedIn) _ = VivoxService.Instance.LogoutAsync(); } catch { }
            _loggedPid = null;
#endif
        }

#if VIVOX_PRESENT
        /// <summary>
        /// 로그인. 'Handle Taken(5006)'(같은 계정의 이전 접속이 서버에 아직 남아 있음)이면 로그아웃하고 잠시 뒤 한 번 더.
        /// </summary>
        private static async Task LoginWithRetryAsync(IVivoxService v, string pid)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    await v.LoginAsync(new LoginOptions { DisplayName = "p" + (PlayerProfile.Instance.Uid ?? "0"), EnableTTS = false });
                    _loggedPid = pid;
                    return;
                }
                catch (Exception e) when (attempt == 0 && e.Message != null && (e.Message.Contains("5006") || e.Message.Contains("Handle Taken")))
                {
                    Debug.LogWarning("[Chat] 이전 채팅 접속이 남아 있어 정리 후 다시 로그인합니다.");
                    try { if (v.IsLoggedIn) await v.LogoutAsync(); } catch { }
                    await Task.Delay(4000);
                }
            }
        }
#endif

        private async Task ConnectAsync()
        {
            Connecting = true; Error = null;
            Changed();
            try
            {
#if VIVOX_PRESENT
                var v = VivoxService.Instance;
                await v.InitializeAsync();
                string pid = CloudAccount.PlayerId;
                if (v.IsLoggedIn && _loggedPid != pid) await v.LogoutAsync();
                if (!v.IsLoggedIn) await LoginWithRetryAsync(v, pid);
                v.ChannelMessageReceived -= OnVivoxChannel;
                v.ChannelMessageReceived += OnVivoxChannel;
                v.DirectedMessageReceived -= OnVivoxDirect;
                v.DirectedMessageReceived += OnVivoxDirect;
                if (!v.ActiveChannels.ContainsKey(WorldChannel))
                    await v.JoinGroupChannelAsync(WorldChannel, ChatCapability.TextOnly);
                // 최근 전체 채팅 몇 개
                try
                {
                    var history = await v.GetChannelTextMessageHistoryAsync(WorldChannel, 30);
                    if (history != null)
                        foreach (var m in history) Receive(m.ChannelName, m.SenderPlayerId, m.MessageText, m.ReceivedTime, m.FromSelf, false, quiet: true);
                    World.Sort((a, b) => a.time.CompareTo(b.time));
                }
                catch (Exception he) { Debug.LogWarning($"[Chat] 지난 메시지를 불러오지 못했습니다: {he.Message}"); }
                Connected = true;
                var guild = PlayerGuild.Instance;
                if (guild.IsReal) await JoinGuildAsync(guild.Gid);
#endif
                await Task.CompletedTask;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Chat] 연결 실패: {e.Message}");
                bool taken = e.Message != null && (e.Message.Contains("5006") || e.Message.Contains("Handle Taken"));
                Error = taken ? "같은 계정이 다른 곳(다른 PC · 기기)에서 채팅에 접속해 있습니다. 잠시 뒤 다시 시도합니다."
                              : "채팅 서버에 연결하지 못했습니다. 잠시 뒤 다시 시도합니다.";
                _nextTry = Time.unscaledTime + 30f;
            }
            finally
            {
                Connecting = false;
                Changed();
            }
        }

        private void OnDestroy()
        {
#if VIVOX_PRESENT
            try
            {
                var v = VivoxService.Instance;
                v.ChannelMessageReceived -= OnVivoxChannel;
                v.DirectedMessageReceived -= OnVivoxDirect;
            }
            catch { }
#endif
            if (_instance == this) _instance = null;
        }

#if VIVOX_PRESENT
        private void OnVivoxChannel(VivoxMessage m) => Receive(m.ChannelName, m.SenderPlayerId, m.MessageText, m.ReceivedTime, m.FromSelf, false);
        private void OnVivoxDirect(VivoxMessage m)
        {
            if (m.FromSelf) return;   // 내가 보낸 1:1은 보낼 때 이미 넣음
            Receive(null, m.SenderPlayerId, m.MessageText, m.ReceivedTime, false, true);
        }
#endif

        // ---------------- 받기 ----------------

        [Serializable]
        private class Payload { public int v = 1; public string n, u, m; }

        private void Receive(string channel, string senderId, string raw, DateTime time, bool fromSelf, bool direct, bool quiet = false)
        {
            bool guild = !direct && !string.IsNullOrEmpty(channel) && channel == _guildChannel && _guildChannel != "";
            if (!direct && !guild && channel != null && channel != WorldChannel) return;
            Payload p = null;
            try { p = JsonUtility.FromJson<Payload>(raw); } catch { }
            if (p == null || string.IsNullOrEmpty(p.m)) return;
            string me = CloudAccount.PlayerId;
            bool mine = fromSelf || senderId == me;
            if (!mine && IsBlocked(senderId)) return;
            var msg = new ChatMessage
            {
                peer = direct ? senderId : guild ? GuildPeer : "", senderId = senderId, name = Clean(p.n, 16), uid = Clean(p.u, 8),
                text = ChatFilter.Mask(Clean(p.m, MaxLength)), time = time == default ? DateTime.Now : time.ToLocalTime(), mine = mine,
            };
            if (direct)
            {
                if (!PlayerSocial.Instance.IsRealFriend(senderId)) return;   // 1:1은 친구끼리만
                Add(Log(senderId), msg, KeepDirect);
                _unread[senderId] = UnreadOf(senderId) + 1;
            }
            else if (guild)
            {
                var log = Log(GuildPeer);
                if (log.Exists(x => x.senderId == senderId && x.time == msg.time && x.text == msg.text)) return;
                Add(log, msg, KeepWorld);
                if (!mine && !quiet) _unread[GuildPeer] = UnreadOf(GuildPeer) + 1;
            }
            else
            {
                if (World.Exists(x => x.senderId == senderId && x.time == msg.time && x.text == msg.text)) return;   // 기록 · 실시간이 겹치면 한 번만
                Add(World, msg, KeepWorld);
                if (!mine && !quiet) UnreadWorld++;
            }
            if (!quiet) Changed();
        }

        private static string Clean(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ").Replace("\r", " ").Replace("<", "‹").Replace(">", "›");   // 서식 태그 막기
            return s.Length > max ? s.Substring(0, max) : s;
        }

        private static void Add(List<ChatMessage> list, ChatMessage m, int keep)
        {
            list.Add(m);
            if (list.Count > keep) list.RemoveRange(0, list.Count - keep);
        }

        public List<ChatMessage> Log(string peer)
        {
            if (!_direct.TryGetValue(peer, out var l)) { l = new List<ChatMessage>(); _direct[peer] = l; }
            return l;
        }

        public int UnreadOf(string peer) => _unread.TryGetValue(peer, out int n) ? n : 0;
        public int UnreadDirect { get { int n = 0; foreach (var kv in _unread) if (kv.Key != GuildPeer) n += kv.Value; return n; } }

        // ---------------- 길드 채널 ----------------

        /// <summary>실제 길드에 들어가면 그 길드 채널에 들어감(연결 전이면 연결될 때).</summary>
        public void JoinGuild(string gid)
        {
            if (string.IsNullOrEmpty(gid)) return;
            if (Connected) _ = JoinGuildAsync(gid);
        }

        private async Task JoinGuildAsync(string gid)
        {
            string ch = GuildPrefix + gid;
            if (_guildChannel == ch) return;
            if (!string.IsNullOrEmpty(_guildChannel)) await LeaveGuildAsync();
            _guildChannel = ch;
            try
            {
#if VIVOX_PRESENT
                var v = VivoxService.Instance;
                if (!v.ActiveChannels.ContainsKey(ch)) await v.JoinGroupChannelAsync(ch, ChatCapability.TextOnly);
                try
                {
                    var history = await v.GetChannelTextMessageHistoryAsync(ch, 30);
                    if (history != null)
                        foreach (var m in history) Receive(m.ChannelName, m.SenderPlayerId, m.MessageText, m.ReceivedTime, m.FromSelf, false, quiet: true);
                    Log(GuildPeer).Sort((a, b) => a.time.CompareTo(b.time));
                }
                catch (Exception he) { Debug.LogWarning($"[Chat] 길드 채팅 기록을 불러오지 못했습니다: {he.Message}"); }
#endif
                await Task.CompletedTask;
            }
            catch (Exception e) { Debug.LogWarning($"[Chat] 길드 채널 입장 실패: {e.Message}"); _guildChannel = ""; }
            Changed();
        }

        /// <summary>길드를 나가면 길드 채널에서 나감 · 길드 대화 지움.</summary>
        public void LeaveGuild(string gid) { _ = LeaveGuildAsync(); }

        private async Task LeaveGuildAsync()
        {
            string ch = _guildChannel;
            _guildChannel = "";
            Log(GuildPeer).Clear();
            _unread.Remove(GuildPeer);
            if (string.IsNullOrEmpty(ch)) { Changed(); return; }
            try
            {
#if VIVOX_PRESENT
                var v = VivoxService.Instance;
                if (v.ActiveChannels.ContainsKey(ch)) await v.LeaveChannelAsync(ch);
#endif
                await Task.CompletedTask;
            }
            catch (Exception e) { Debug.LogWarning($"[Chat] 길드 채널 나가기 실패: {e.Message}"); }
            Changed();
        }
        /// <summary>읽음 표시. 안 읽은 것이 있었을 때만 알림(창을 다시 그리는 중에 불러도 되돌아오지 않게).</summary>
        public void MarkRead(string peer)
        {
            bool had;
            if (string.IsNullOrEmpty(peer)) { had = UnreadWorld > 0; UnreadWorld = 0; }
            else had = _unread.Remove(peer);
            if (had) Changed();
        }

        private void Changed() => OnChanged?.Invoke();

        // ---------------- 보내기 ----------------

        /// <summary>보낼 수 없는 이유(보낼 수 있으면 null).</summary>
        public string CannotSend()
        {
            if (!Available) return "채팅 기능을 준비 중입니다.";
            if (!CloudAccount.SignedIn) return "채팅은 로그인해야 쓸 수 있습니다 (설정 → 로그인하기)";
            if (!Connected) return Connecting ? "채팅 서버에 연결하는 중입니다..." : (Error ?? "채팅 서버에 연결하는 중입니다...");
            return null;
        }

        /// <summary>메시지 보내기(peer가 비면 전체 채팅). 실패 사유(성공이면 null).</summary>
        public async Task<string> SendAsync(string text, string peer = null)
        {
            string why = CannotSend();
            if (why != null) return why;
            text = (text ?? "").Trim();
            if (text.Length == 0) return "";
            if (text.Length > MaxLength) return $"{MaxLength}자까지 보낼 수 있습니다";
            if (Time.unscaledTime - _lastSend < SendInterval) return "너무 빨리 보내고 있습니다. 잠시 뒤에 보내 주세요";
            bool toGuild = peer == GuildPeer;
            if (toGuild && string.IsNullOrEmpty(_guildChannel)) return "길드 채팅에 연결하는 중입니다...";
            if (!toGuild && !string.IsNullOrEmpty(peer) && !PlayerSocial.Instance.IsRealFriend(peer)) return "1:1 대화는 친구끼리만 할 수 있습니다";
            _lastSend = Time.unscaledTime;
            var pf = PlayerProfile.Instance;
            string payload = JsonUtility.ToJson(new Payload { n = pf.DisplayName, u = pf.Uid, m = ChatFilter.Mask(text) });
            try
            {
#if VIVOX_PRESENT
                if (string.IsNullOrEmpty(peer)) await VivoxService.Instance.SendChannelTextMessageAsync(WorldChannel, payload);
                else if (toGuild) await VivoxService.Instance.SendChannelTextMessageAsync(_guildChannel, payload);
                else
                {
                    await VivoxService.Instance.SendDirectTextMessageAsync(peer, payload);
                    Add(Log(peer), new ChatMessage { peer = peer, senderId = CloudAccount.PlayerId, name = pf.DisplayName, uid = pf.Uid, text = ChatFilter.Mask(text), time = DateTime.Now, mine = true }, KeepDirect);
                    Changed();
                }
#endif
                await Task.CompletedTask;
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Chat] 보내기 실패: {e.Message}");
                return string.IsNullOrEmpty(peer) ? "보내지 못했습니다. 잠시 뒤 다시 시도해 주세요" : "보내지 못했습니다(상대가 접속 중이 아닐 수 있습니다)";
            }
        }

        // ---------------- 차단 · 신고 ----------------

        [Serializable] private class Blocked { public string pid, name; }
        [Serializable] private class BlockList { public List<Blocked> list = new(); }
        private BlockList _blocked = new();

        private void LoadBlocked()
        {
            try { _blocked = JsonUtility.FromJson<BlockList>(SaveStore.GetString(BlockKey, "")) ?? new BlockList(); }
            catch { _blocked = new BlockList(); }
            _blocked.list ??= new();
        }

        private void SaveBlocked() { SaveStore.SetString(BlockKey, JsonUtility.ToJson(_blocked)); SaveStore.Save(); }

        public bool IsBlocked(string pid) => !string.IsNullOrEmpty(pid) && _blocked.list.Exists(b => b.pid == pid);
        public IReadOnlyList<(string pid, string name)> BlockedList
        {
            get { var l = new List<(string, string)>(); foreach (var b in _blocked.list) l.Add((b.pid, b.name)); return l; }
        }

        /// <summary>차단: 그 사람의 메시지를 숨김(지금 보이는 것도 지움).</summary>
        public void Block(string pid, string name)
        {
            if (string.IsNullOrEmpty(pid) || IsBlocked(pid) || pid == CloudAccount.PlayerId) return;
            _blocked.list.Add(new Blocked { pid = pid, name = name });
            SaveBlocked();
            World.RemoveAll(m => m.senderId == pid);
            _direct.Remove(pid); _unread.Remove(pid);
            Changed();
        }

        public void Unblock(string pid)
        {
            if (_blocked.list.RemoveAll(b => b.pid == pid) > 0) { SaveBlocked(); Changed(); }
        }

        /// <summary>
        /// 플레이어 신고(채팅 · 랭킹 · 친구 · 정보창 공통): 서버(신고한 사람의 계정 데이터 reports — 대시보드 Cloud Save에서 확인)에
        /// 시각 · 대상 id · UID · 이름 · 사유 · 내용을 한 줄로 기록(최근 50건). block이면 채팅에서도 차단. 같은 사람은 하루 한 번만.
        /// 실패 사유(성공이면 null).
        /// </summary>
        public async Task<string> ReportPlayerAsync(string pid, string uid, string name, string reason, string detail, bool block)
        {
            if (string.IsNullOrEmpty(pid) || pid == CloudAccount.PlayerId) return "신고할 수 없는 대상입니다";
            if (!CloudAccount.SignedIn) return "신고는 로그인해야 할 수 있습니다";
            string dayKey = $"report.{pid}";
            string today = DateTime.Now.ToString("yyyyMMdd");
            if (PlayerPrefs.GetString(dayKey, "") == today) return "이 플레이어는 오늘 이미 신고했습니다";
            if (block) Block(pid, name);
            try
            {
                var entry = $"{DateTime.UtcNow:o}\t{pid}\t{uid}\t{name}\t{reason}\t{(detail ?? "").Replace("\t", " ").Replace("\n", " ")}";
                var key = "reports";
                string prev = "";
                var got = await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { key });
                if (got != null && got.TryGetValue(key, out var item) && item?.Value != null) prev = item.Value.GetAs<string>() ?? "";
                var lines = new List<string>(prev.Split('\n'));
                lines.RemoveAll(string.IsNullOrEmpty);
                lines.Add(entry);
                if (lines.Count > 50) lines.RemoveRange(0, lines.Count - 50);
                await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { key, string.Join("\n", lines) } });
                PlayerPrefs.SetString(dayKey, today);
                PlayerPrefs.Save();
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Chat] 신고 기록 실패: {e.Message}");
                return "신고를 보내지 못했습니다. 인터넷 연결을 확인하고 잠시 뒤 다시 시도해 주세요";
            }
        }
    }

    /// <summary>
    /// 금칙어 가리기: 띄어쓰기 · 기호를 끼워 넣어도 잡도록 글자만 이어 붙여 비교하고, 걸린 글자를 *로 바꿈.
    /// 목록은 <see cref="Words"/>(튜닝 지점).
    /// </summary>
    public static class ChatFilter
    {
        public static readonly string[] Words =
        {
            "시발", "씨발", "씨바", "시바", "ㅅㅂ", "ㅆㅂ", "병신", "ㅂㅅ", "븅신", "좆", "ㅈㄹ", "지랄", "개새끼", "개새", "새끼", "ㅅㄲ", "미친놈", "미친년",
            "닥쳐", "꺼져", "엿먹", "fuck", "fck", "shit", "bitch", "asshole", "dick", "cunt", "nigger", "retard",
        };

        public static string Mask(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // 글자(한글 · 자모 · 영문 · 숫자)만 모은 문자열과 원래 위치
            var sb = new StringBuilder();
            var map = new List<int>();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsLetterOrDigit(c)) { sb.Append(char.ToLowerInvariant(c)); map.Add(i); }
            }
            string flat = sb.ToString();
            var chars = text.ToCharArray();
            bool any = false;
            foreach (var w in Words)
            {
                int at = 0;
                while ((at = flat.IndexOf(w, at, StringComparison.Ordinal)) >= 0)
                {
                    for (int k = 0; k < w.Length; k++) chars[map[at + k]] = '*';
                    at += w.Length;
                    any = true;
                }
            }
            return any ? new string(chars) : text;
        }
    }
}
