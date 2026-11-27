using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// Unity IAP 패키지(com.unity.purchasing)가 설치돼 있으면 스크립트 기호 IAP_PRESENT를 켜고, 없으면 끔(PC · 안드로이드 · iOS).
    /// 결제 코드(IapStore)는 이 기호가 있을 때만 Unity IAP를 씀 — 패키지 없이도 게임은 컴파일됨(에디터 · 개발 빌드는 '테스트 결제').
    /// </summary>
    [InitializeOnLoad]
    public static class IapDefine
    {
        private const string Symbol = "IAP_PRESENT";

        static IapDefine()
        {
            bool has = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Unity.Purchasing");
            foreach (var t in new[] { NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                var defs = PlayerSettings.GetScriptingDefineSymbols(t).Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                bool on = defs.Contains(Symbol);
                if (has == on) continue;
                if (has) defs.Add(Symbol); else defs.Remove(Symbol);
                PlayerSettings.SetScriptingDefineSymbols(t, string.Join(";", defs));
                UnityEngine.Debug.Log($"[IAP] {t.TargetName}: {Symbol} {(has ? "켬(Unity IAP 설치됨)" : "끔(Unity IAP 없음)")}");
            }
        }
    }
}
