using UnityEditor;
using UnityEngine;

namespace SubcultureRPG.EditorTools
{
    /// <summary>
    /// 세이브(PlayerPrefs) 초기화 메뉴. 처음 시작한 계정처럼 되돌립니다.
    /// 전투 자동 · 배속 설정과 지갑 1회 복구 기록은 남김(복구 기록을 지우면 새 계정에 옛 재화가 들어옴).
    /// </summary>
    public static class SaveDataTools
    {
        [MenuItem("SubcultureRPG/데이터/내 정보 초기화 (처음부터)", false, 100)]
        private static void ResetAll()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("내 정보 초기화", "플레이 중에는 초기화할 수 없습니다. 플레이를 멈춘 뒤 다시 실행해 주세요.", "확인");
                return;
            }
            if (!EditorUtility.DisplayDialog("내 정보 초기화",
                    "캐릭터 · 재화 · 스테이지 · 스토리 · 프로필 · 결투장 · 주간 보스 · 친구 · 길드 등 모든 진행 상황을 지웁니다.\n되돌릴 수 없습니다.",
                    "초기화", "취소")) return;

            int auto = PlayerPrefs.GetInt("battle.auto", 0), speed = PlayerPrefs.GetInt("battle.speed", 0);
            PlayerPrefs.DeleteAll();
            PlayerPrefs.SetInt("battle.auto", auto);
            PlayerPrefs.SetInt("battle.speed", speed);
            PlayerPrefs.SetInt("wallet.restore.v1", 1);
            PlayerPrefs.Save();
            Debug.Log("[SaveDataTools] 내 정보를 초기화했습니다. 플레이하면 처음 시작한 계정으로 시작합니다.");
        }
    }
}
