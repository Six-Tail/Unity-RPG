using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// Vivox 패키지(com.unity.services.vivox)가 설치돼 있으면 스크립트 기호 VIVOX_PRESENT를 켜고, 없으면 끔(PC · 안드로이드 · iOS).
    /// 채팅 코드(ChatService)는 이 기호가 있을 때만 Vivox를 씀 — 패키지 없이도 게임은 컴파일됨.
    /// </summary>
    [InitializeOnLoad]
    public static class VivoxDefine
    {
        private const string Symbol = "VIVOX_PRESENT";

        static VivoxDefine()
        {
            bool has = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Unity.Services.Vivox");
            foreach (var t in new[] { NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                var defs = PlayerSettings.GetScriptingDefineSymbols(t).Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                bool on = defs.Contains(Symbol);
                if (has == on) continue;
                if (has) defs.Add(Symbol); else defs.Remove(Symbol);
                PlayerSettings.SetScriptingDefineSymbols(t, string.Join(";", defs));
                UnityEngine.Debug.Log($"[Chat] {t.TargetName}: {Symbol} {(has ? "켬(Vivox 설치됨)" : "끔(Vivox 없음)")}");
            }
        }
    }
}
