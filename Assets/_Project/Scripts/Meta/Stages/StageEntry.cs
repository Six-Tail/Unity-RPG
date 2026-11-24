using UnityEngine;

namespace SubcultureRPG.Meta
{
    /// <summary>
    /// 스테이지 입장 규칙: 행동력 소모량 · 보상 배수(x1/x2/x3) · 반복 입장.
    ///
    /// · 모든 스테이지는 입장할 때 행동력을 씁니다(패배하거나 도중에 나가면 돌려받음).
    /// · ★3으로 클리어한 스테이지(메인 · 특수 던전)는 보상 배수와 반복 입장을 쓸 수 있습니다.
    ///   배수를 고르면 행동력을 배수만큼 쓰고 보상(금화·재료·경험치·장비)도 배수만큼 받습니다.
    /// · 배수 · 반복 설정은 PlayerPrefs에 기억(던전 공용).
    /// </summary>
    public static class StageEntry
    {
        public const int MaxMultiplier = 3;

        /// <summary>반복 입장 선택지: 0 = 끔, -1 = 행동력이 다 떨어질 때까지.</summary>
        public static readonly int[] RepeatOptions = { 0, 3, 5, 10, -1 };

        private const string MultKey = "stage.multiplier", RepeatKey = "stage.repeat";

        /// <summary>1회 입장 행동력(배수 적용 전).</summary>
        public static int BaseCost(StageData s)
        {
            if (s == null) return 0;
            if (!s.IsDungeon)
                return s.difficulty switch { StageDifficulty.Hell => 10, StageDifficulty.Hard => 8, _ => 6 };
            int tier = Mathf.Max(1, s.dungeonTier);
            return s.kind switch
            {
                StageKind.EquipmentDungeon => 15 + 2 * (tier - 1),   // 15 · 17 · 19 · 21 · 23
                _ => 10 + 2 * (tier - 1),                            // 돌파 · 금화 · 교본 · 경험치: 10 · 12 · 14 …
            };
        }

        /// <summary>보상 배수 · 반복 입장을 쓸 수 있는가(★3으로 클리어한 스테이지 — 메인 · 특수 던전 모두).</summary>
        public static bool CanBoost(StageData s, StageProgress progress) =>
            s != null && progress != null && progress.StarsOf(s) >= 3;

        /// <summary>고른 보상 배수(1~3, 기억된 값).</summary>
        public static int Multiplier
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(MultKey, 1), 1, MaxMultiplier);
            set { PlayerPrefs.SetInt(MultKey, Mathf.Clamp(value, 1, MaxMultiplier)); PlayerPrefs.Save(); }
        }

        /// <summary>고른 반복 입장 횟수(0 = 끔, -1 = 행동력 소진까지).</summary>
        public static int Repeat
        {
            get
            {
                int v = PlayerPrefs.GetInt(RepeatKey, 0);
                return System.Array.IndexOf(RepeatOptions, v) >= 0 ? v : 0;
            }
            set { PlayerPrefs.SetInt(RepeatKey, value); PlayerPrefs.Save(); }
        }

        /// <summary>이 스테이지에 실제로 적용되는 배수(자격이 없으면 1).</summary>
        public static int MultiplierFor(StageData s, StageProgress progress) => CanBoost(s, progress) ? Multiplier : 1;

        /// <summary>이 스테이지에 적용되는 반복 횟수(자격이 없으면 0).</summary>
        public static int RepeatFor(StageData s, StageProgress progress) => CanBoost(s, progress) ? Repeat : 0;

        /// <summary>배수 적용 행동력.</summary>
        public static int CostOf(StageData s, StageProgress progress) => BaseCost(s) * MultiplierFor(s, progress);

        public static string RepeatLabel(int v) => v == 0 ? "끔" : v < 0 ? "최대" : $"{v}회";
    }
}
