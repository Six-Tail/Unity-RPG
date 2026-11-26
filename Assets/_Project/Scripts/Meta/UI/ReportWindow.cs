using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SubcultureRPG.UI;

namespace SubcultureRPG.Meta.UI
{
    /// <summary>
    /// 플레이어 신고 창(채팅 메시지 메뉴 · 지휘관 정보창 공통 — 랭킹 · 친구 · 길드 어디서 열어도): 사유를 고르고(4가지),
    /// '채팅에서도 차단'을 켜고 끈 뒤 신고 → <see cref="ChatService.ReportPlayerAsync"/>. 실제 플레이어만(가상 지휘관은 신고 대상 아님).
    /// </summary>
    public static class ReportWindow
    {
        public static readonly string[] Reasons = { "욕설 · 비매너", "부적절한 이름 · 인사말", "부정행위(핵 · 조작) 의심", "기타" };

        public static void Open(string pid, string uid, string name, string detail, TMP_FontAsset font = null)
        {
            var w = OverlayWindow.Open($"신고 — {name}", 760, 640, font, order: 320);
            var p = w.Panel;
            w.Text(p, $"<color=#9AA3B5>UID {uid}</color>" + (string.IsNullOrEmpty(detail) ? "" : $"\n<size=85%><color=#C8CEDA>{Escape(detail)}</color></size>"),
                20, Color.white, TextAlignmentOptions.Left, 40, -80, 680, 70).textWrappingMode = TextWrappingModes.Normal;
            w.Text(p, "<b>신고 사유</b>", 22, Color.white, TextAlignmentOptions.Left, 40, -160, 300, 34);

            int picked = -1;
            bool block = true;
            var btns = new Button[Reasons.Length];
            Button blockBtn = null, send = null;
            void Refresh()
            {
                for (int i = 0; i < btns.Length; i++) btns[i].GetComponent<Image>().color = i == picked ? OverlayWindow.Blue : new Color(1, 1, 1, 0.12f);
                blockBtn.GetComponentInChildren<TextMeshProUGUI>().text = block ? "[v] 채팅에서도 차단" : "[  ] 채팅에서도 차단";
                send.interactable = picked >= 0;
                send.GetComponent<Image>().color = picked >= 0 ? OverlayWindow.Red : new Color(1, 1, 1, 0.08f);
            }
            for (int i = 0; i < Reasons.Length; i++)
            {
                int idx = i;
                btns[i] = w.Btn(p, Reasons[i], 21, new Color(1, 1, 1, 0.12f), 40 + (i % 2) * 344, -202 - (i / 2) * 74, 336, 64, () => { picked = idx; Refresh(); });
            }
            blockBtn = w.Btn(p, "", 20, new Color(1, 1, 1, 0.08f), 40, -360, 680, 54, () => { block = !block; Refresh(); });
            var msg = w.Text(p, "", 19, new Color(1f, 0.6f, 0.55f), TextAlignmentOptions.Center, 40, -424, 680, 30);
            w.Text(p, "<color=#9AA3B5>신고는 운영팀이 확인합니다. 허위 신고는 제재될 수 있습니다.</color>", 17, Color.white, TextAlignmentOptions.Center, 40, -456, 680, 28);
            send = w.Btn(p, "신고하기", 24, OverlayWindow.Red, 40, -500, 680, 70, async () =>
            {
                if (picked < 0) return;
                send.interactable = false;
                string err = await ChatService.Instance.ReportPlayerAsync(pid, uid, name, Reasons[picked], detail, block);
                if (!w.IsOpen) return;
                if (err != null) { msg.text = err; send.interactable = true; return; }
                w.Close();
                Popups.Show("신고", $"{name} 님을 신고했습니다." + (block ? "\n채팅에서 이 플레이어의 메시지가 보이지 않습니다." : ""));
            });
            Refresh();
        }

        private static string Escape(string s) => (s ?? "").Replace("<", "‹").Replace(">", "›");
    }
}
