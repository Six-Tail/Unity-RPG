using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;
using SubcultureRPG.Meta.Cloud;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 채팅 창: 왼쪽 = 대화 목록(전체 채팅 + 실제 플레이어 친구, 안 읽은 수) · 오른쪽 = 메시지 · 아래 입력(Enter로 보내기).
    /// 이름을 누르면 지휘관 정보창, 다른 사람 메시지의 '…'를 누르면 신고 · 차단. 위 오른쪽 '차단 목록'.
    /// </summary>
    public static class ChatWindow
    {
        private static string _peer = "";   // "" = 전체 채팅

        public static void Open(TMP_FontAsset font = null, string peer = null)
        {
            if (peer != null) _peer = peer;
            var chat = ChatService.Instance;
            var w = OverlayWindow.Open("채팅", 1500, 930, font, dimCloses: true, order: 260);
            var p = w.Panel;

            var status = w.Text(p, "", 19, new Color(0.75f, 0.78f, 0.86f), TextAlignmentOptions.Right, 700, -28, 480, 30);
            w.Btn(p, "차단 목록", 18, new Color(1, 1, 1, 0.12f), 1190, -22, 150, 40, () => BlockList(w.Font));

            // 왼쪽: 대화 목록
            var left = UIFactory.NewRect("Peers", p);
            OverlayWindow.Place(left, 30, -86, 300, 814);
            var ll = left.gameObject.AddComponent<VerticalLayoutGroup>();
            ll.spacing = 6; ll.childControlWidth = true; ll.childControlHeight = true; ll.childForceExpandHeight = false;

            // 오른쪽: 메시지
            var view = UIFactory.NewRect("View", p);
            OverlayWindow.Place(view, 350, -86, 1120, 720);
            var bg = UIFactory.AddImage("Bg", view, new Color(0, 0, 0, 0.25f), ProceduralSprites.Rounded);
            bg.type = Image.Type.Sliced; UIFactory.Stretch(bg.rectTransform);
            var content = UIFactory.NewRect("Content", view);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 6; vl.padding = new RectOffset(14, 14, 10, 10);
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandHeight = false;
            var scroll = UIFactory.MakeVerticalScroll(view, content);

            // 아래: 입력
            var input = Field(p, w.Font, $"메시지 입력 ({ChatService.MaxLength}자까지 · Enter로 보내기)", ChatService.MaxLength);
            OverlayWindow.Place((RectTransform)input.transform, 350, -820, 960, 70);
            var hint = w.Text(p, "", 18, new Color(1f, 0.6f, 0.55f), TextAlignmentOptions.Left, 350, -894, 1120, 28);

            async void Send()
            {
                if (!w.IsOpen) return;
                string text = input.text;
                string err = await chat.SendAsync(text, string.IsNullOrEmpty(_peer) ? null : _peer);
                if (!w.IsOpen) return;
                if (err == null) input.text = "";
                hint.text = err ?? "";
                input.ActivateInputField();
            }
            w.Btn(p, "보내기", 24, OverlayWindow.Blue, 1322, -820, 148, 70, Send);
            input.onSubmit.AddListener(_ => Send());

            bool building = false;
            void Rebuild()
            {
                if (!w.IsOpen || building) return;
                building = true;
                try { RebuildNow(); }
                finally { building = false; }
            }

            void RebuildNow()
            {
                chat.MarkRead(_peer);
                string why = chat.CannotSend();
                status.text = why != null ? $"<color=#FFB347>{why}</color>" : "<color=#7CFF9A>● 연결됨</color>";

                OverlayWindow.Clear(left);
                PeerButton(left, w.Font, "전체 채팅", "", chat.UnreadWorld, Rebuild);
                if (PlayerGuild.Instance.IsReal) PeerButton(left, w.Font, "길드 채팅", ChatService.GuildPeer, chat.UnreadGuild, Rebuild);
                else if (_peer == ChatService.GuildPeer) _peer = "";
                foreach (var f in PlayerSocial.Instance.RealFriends)
                    PeerButton(left, w.Font, f.name, f.pid, chat.UnreadOf(f.pid), Rebuild);
                if (PlayerSocial.Instance.RealFriends.Count == 0)
                {
                    var tip = UIFactory.AddText("Tip", left, "실제 플레이어 친구가 생기면\n여기서 1:1 대화를 할 수 있습니다", 17, w.Font, new Color(0.6f, 0.64f, 0.72f), TextAlignmentOptions.Center);
                    tip.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
                }

                OverlayWindow.Clear(content);
                var list = string.IsNullOrEmpty(_peer) ? chat.World : chat.Log(_peer);
                if (list.Count == 0)
                {
                    string empty = string.IsNullOrEmpty(_peer) ? "아직 메시지가 없습니다. 첫 인사를 남겨 보세요!"
                        : _peer == ChatService.GuildPeer ? "길드원에게 첫 인사를 남겨 보세요!"
                        : "아직 대화가 없습니다. 상대가 접속 중일 때 메시지가 전달됩니다.";
                    var e = UIFactory.AddText("Empty", content, $"<color=#9AA3B5>{empty}</color>", 20, w.Font, Color.white, TextAlignmentOptions.Center);
                    e.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
                }
                foreach (var m in list) MessageRow(content, w, m, Rebuild);
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 0f;   // 맨 아래(최근)
            }

            chat.OnChanged += Rebuild;
            w.OnClosed += () => chat.OnChanged -= Rebuild;
            Rebuild();
            input.ActivateInputField();
        }

        private static void PeerButton(Transform parent, TMP_FontAsset font, string label, string peer, int unread, System.Action rebuild)
        {
            bool on = _peer == peer;
            var b = UIFactory.AddButton("Peer", parent, label + (unread > 0 ? $"  <color=#FF6B6B>●{unread}</color>" : ""), 21, font,
                on ? new Color(0.25f, 0.55f, 0.9f, 0.96f) : new Color(1, 1, 1, 0.1f), Color.white);
            b.GetComponentInChildren<TextMeshProUGUI>().richText = true;
            b.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            b.onClick.AddListener(() => { _peer = peer; rebuild(); });
        }

        private static void MessageRow(Transform parent, OverlayWindow w, ChatMessage m, System.Action rebuild)
        {
            var row = UIFactory.AddImage("Msg", parent, m.mine ? new Color(0.25f, 0.4f, 0.65f, 0.45f) : new Color(1, 1, 1, 0.05f), ProceduralSprites.Rounded);
            row.type = Image.Type.Sliced;
            var hl = row.gameObject.AddComponent<VerticalLayoutGroup>();
            hl.padding = new RectOffset(16, 70, 8, 10); hl.spacing = 2;
            hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandHeight = false;

            var head = UIFactory.AddText("Head", row.transform,
                $"<b>{m.name}</b>{(m.mine ? "  <size=80%><color=#9AD4FF>나</color></size>" : "")}  <size=75%><color=#9AA3B5>{m.time:HH:mm}</color></size>",
                20, w.Font, new Color(1f, 0.85f, 0.55f), TextAlignmentOptions.Left);
            head.richText = true;
            var body = UIFactory.AddText("Body", row.transform, m.text, 22, w.Font, Color.white, TextAlignmentOptions.Left);
            body.richText = false;
            body.textWrappingMode = TextWrappingModes.Normal;

            if (m.mine) return;
            // 이름 → 정보창
            var hb = head.gameObject.AddComponent<Button>();
            hb.transition = Selectable.Transition.None;
            head.raycastTarget = true;
            hb.onClick.AddListener(() => PlayerInfoWindow.OpenPlayer(m.senderId, w.Font));
            // … → 신고 · 차단
            var more = UIFactory.AddButton("More", row.transform, "...", 20, w.Font, new Color(1, 1, 1, 0.12f), Color.white);
            more.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var mrt = (RectTransform)more.transform;
            UIFactory.Anchor(mrt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            mrt.sizeDelta = new Vector2(54, 36); mrt.anchoredPosition = new Vector2(-8, -8);
            more.onClick.AddListener(() => MessageMenu(m, w.Font));
        }

        /// <summary>다른 사람 메시지 메뉴: 정보 보기 · 차단 · 신고 · 닫기.</summary>
        private static void MessageMenu(ChatMessage m, TMP_FontAsset font)
        {
            var w = OverlayWindow.Open(m.name, 700, 400, font, order: 300);
            var body = w.Text(w.Panel, $"\"{m.text}\"", 22, new Color(0.9f, 0.92f, 0.96f), TextAlignmentOptions.Center, 40, -84, 620, 100);
            body.richText = false;
            body.textWrappingMode = TextWrappingModes.Normal;
            bool blocked = ChatService.Instance.IsBlocked(m.senderId);
            w.Btn(w.Panel, "정보 보기", 22, OverlayWindow.Blue, 40, -200, 300, 64, () => { w.Close(); PlayerInfoWindow.OpenPlayer(m.senderId, font); });
            w.Btn(w.Panel, blocked ? "차단됨" : "채팅 차단", 22, OverlayWindow.Dim, 360, -200, 300, 64, () => { w.Close(); ChatService.Instance.Block(m.senderId, m.name); }, !blocked);
            w.Btn(w.Panel, "신고하기", 22, OverlayWindow.Red, 40, -280, 620, 64, () =>
            {
                w.Close();
                ReportWindow.Open(m.senderId, m.uid, m.name, $"채팅: {m.text}", font);
            });
        }

        /// <summary>차단 목록(해제).</summary>
        private static void BlockList(TMP_FontAsset font)
        {
            var w = OverlayWindow.Open("차단 목록", 760, 640, font, order: 300);
            var view = UIFactory.NewRect("View", w.Panel);
            OverlayWindow.Place(view, 30, -86, 700, 520);
            var content = UIFactory.NewRect("Content", view);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 6; vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandHeight = false;
            UIFactory.MakeVerticalScroll(view, content);
            void Rebuild()
            {
                if (!w.IsOpen) return;
                OverlayWindow.Clear(content);
                var list = ChatService.Instance.BlockedList;
                if (list.Count == 0)
                {
                    var t = UIFactory.AddText("Empty", content, "<color=#9AA3B5>차단한 사람이 없습니다</color>", 22, w.Font, Color.white, TextAlignmentOptions.Center);
                    t.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;
                }
                foreach (var (pid, name) in list)
                {
                    var row = UIFactory.AddImage("Row", content, new Color(1, 1, 1, 0.06f), ProceduralSprites.Rounded);
                    row.type = Image.Type.Sliced;
                    row.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
                    var t = UIFactory.AddText("Name", row.transform, name, 22, w.Font, Color.white, TextAlignmentOptions.Left);
                    UIFactory.Stretch(t.rectTransform, 20, 6, 180, 6);
                    var id = pid;
                    var b = UIFactory.AddButton("Unblock", row.transform, "차단 해제", 19, w.Font, new Color(1, 1, 1, 0.14f), Color.white);
                    var brt = (RectTransform)b.transform;
                    UIFactory.Anchor(brt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
                    brt.sizeDelta = new Vector2(150, 46); brt.anchoredPosition = new Vector2(-10, 0);
                    b.onClick.AddListener(() => { ChatService.Instance.Unblock(id); Rebuild(); });
                }
            }
            Rebuild();
        }

        private static TMP_InputField Field(Transform parent, TMP_FontAsset font, string placeholder, int limit)
        {
            const float size = 24;
            var bg = UIFactory.AddImage("Input", parent, new Color(1, 1, 1, 0.1f), ProceduralSprites.Rounded, raycast: true);
            bg.type = Image.Type.Sliced; bg.preserveAspect = false;
            var area = UIFactory.NewRect("Text Area", bg.transform);
            UIFactory.Stretch(area, 20, 6, 20, 6);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = UIFactory.AddText("Placeholder", area, placeholder, size, font, new Color(0.6f, 0.63f, 0.7f), TextAlignmentOptions.Left, FontStyles.Italic);
            UIFactory.Stretch(ph.rectTransform);
            var text = UIFactory.AddText("Text", area, "", size, font, Color.white, TextAlignmentOptions.Left);
            UIFactory.Stretch(text.rectTransform);
            text.richText = false;
            var field = bg.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg; field.textViewport = area; field.textComponent = text; field.placeholder = ph;
            field.characterLimit = limit;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.richText = false;
            if (font != null) field.fontAsset = font;
            field.pointSize = size;
            InputFocusFx.Attach(field);
            return field;
        }
    }
}
