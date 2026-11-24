using System.Collections.Generic;
using UnityEngine;

namespace SubcultureRPG.Characters
{
    /// <summary>
    /// 합성(성급 승급)·초월 규칙 — 비용/보정/단계별 보상의 단일 기준.
    ///
    /// · 합성: 중복 캐릭터를 소모해 성급을 1씩 올림 (태생 → … → 6성)
    /// · 초월: 6성에서 +1 ~ +6
    /// · 비용(중복 장수)은 태생 성급·단계별 표(PromoteTable / TranscendTable). 단계가 오를수록 비싸짐.
    ///   → 풀 초월(6성+6)까지 총 중복: 6성 6장 · 5성 21장 · 4성 40장 · 3성 66장
    ///   (흔하게 뽑히는 저성급일수록 더 많이 필요 — 기본 확률 기준 대략 3성 ≈ 310뽑 · 4성 ≈ 400뽑 · 5성 ≈ 500뽑 · 픽업 6성 ≈ 560뽑)
    /// · 승급 보정: 올린 성급만큼 1단계당 -3% → 3성 태생 6성+6은 태생 6성+6보다 약 9% 약함
    /// </summary>
    public static class Transcendence
    {
        public const int MinStar = 3;
        public const int MaxStar = 6;
        public const int MaxTranscend = 6;

        /// <summary>초월 단계당 전 능력치 보정.</summary>
        public const float StatPerTranscend = 0.05f;
        /// <summary>초월 단계당 스킬 계수 보정.</summary>
        public const float SkillPerTranscend = 0.05f;
        /// <summary>승급한 성급 1단계당 능력치 패널티.</summary>
        public const float PromotionPenalty = 0.03f;

        // ---------------- 비용 표 ----------------
        // [태생-3][단계] = 필요 중복 장수

        /// <summary>합성 비용: [태생][현재성급 - 태생] → 다음 성급으로.</summary>
        private static readonly int[][] PromoteTable =
        {
            new[] { 4, 6, 8 },   // 태생 3★: 3→4, 4→5, 5→6  (18)
            new[] { 4, 6 },      // 태생 4★: 4→5, 5→6       (10)
            new[] { 3 },         // 태생 5★: 5→6            (3)
            new int[0],          // 태생 6★: 없음
        };

        /// <summary>초월 비용: [태생][현재 초월] → 다음 초월(+1 ~ +6).</summary>
        private static readonly int[][] TranscendTable =
        {
            new[] { 6, 6, 8, 8, 10, 10 },   // 태생 3★ (48)
            new[] { 4, 4, 5, 5, 6, 6 },     // 태생 4★ (30)
            new[] { 2, 2, 3, 3, 4, 4 },     // 태생 5★ (18)
            new[] { 1, 1, 1, 1, 1, 1 },     // 태생 6★ (6) — 같은 캐릭터 6명 더
        };

        private static int Row(Rarity native) => Mathf.Clamp((int)native - MinStar, 0, PromoteTable.Length - 1);

        /// <summary>다음 1단계(합성 또는 초월)에 필요한 중복 수. 풀 초월이면 0.</summary>
        public static int NextCost(Rarity native, int star, int transcend)
        {
            int n = (int)native;
            star = Mathf.Clamp(star, n, MaxStar);
            if (star < MaxStar)
            {
                var row = PromoteTable[Row(native)];
                int i = star - n;
                return i < row.Length ? row[i] : 1;
            }
            if (transcend < MaxTranscend)
                return TranscendTable[Row(native)][Mathf.Clamp(transcend, 0, MaxTranscend - 1)];
            return 0;
        }

        /// <summary>현재 상태에서 풀 초월(6성+6)까지 남은 중복 수.</summary>
        public static int RemainingCost(Rarity native, int star, int transcend)
        {
            int sum = 0;
            int s = Mathf.Clamp(star, (int)native, MaxStar), t = Mathf.Clamp(transcend, 0, MaxTranscend);
            while (s < MaxStar || t < MaxTranscend)
            {
                sum += NextCost(native, s, t);
                if (s < MaxStar) s++; else t++;
            }
            return sum;
        }

        /// <summary>현재 성급(저장값 0이면 태생).</summary>
        public static int CurrentStar(Rarity native, int savedStar) =>
            Mathf.Clamp(Mathf.Max((int)native, savedStar), MinStar, MaxStar);

        /// <summary>태생부터 6성+6까지 필요한 총 중복 수.</summary>
        public static int TotalCost(Rarity native) => RemainingCost(native, (int)native, 0);

        /// <summary>정렬용 등급값(성급 우선, 같은 성급이면 초월 단계).</summary>
        public static int GradeValue(int star, int transcend) => star * 10 + transcend;

        // ---------------- 능력 보정 ----------------

        /// <summary>성급별 기본 배율(3성=1.0 … 6성≈2.05). RuntimeUnits 생성 배율과 동일.</summary>
        public static float StarMultiplier(int star) => 1f + 0.35f * (Mathf.Clamp(star, MinStar, MaxStar) - 3);

        /// <summary>
        /// 태생 대비 최종 능력치 배율:
        /// (현재 성급 배율 / 태생 배율) × 승급 패널티 × 초월 보정.
        /// </summary>
        public static float StatMultiplier(Rarity native, int star, int transcend)
        {
            int n = (int)native;
            star = Mathf.Clamp(star, n, MaxStar);
            transcend = Mathf.Clamp(transcend, 0, MaxTranscend);

            float m = StarMultiplier(star) / StarMultiplier(n);
            m *= 1f - PromotionPenalty * (star - n);
            m *= 1f + StatPerTranscend * transcend;
            return m;
        }

        /// <summary>초월에 따른 스킬 계수 배율.</summary>
        public static float SkillMultiplier(int transcend) =>
            1f + SkillPerTranscend * Mathf.Clamp(transcend, 0, MaxTranscend);

        // ---------------- 초월 단계별 보상 ----------------
        // +1 / +3 / +6 : 캐릭터 고유 능력(Battle.CharacterKits) — 고유 패시브를 강화
        // +2 / +4 / +5 : 기본 공격 / 스킬 / 궁극기 레벨 +3 (재료 없이 바로 적용, 최대 10 → 13)

        /// <summary>초월로 얻는 스킬 레벨.</summary>
        public const int SkillLevelStep = 3;

        /// <summary>스킬 칸(0 기본 공격 · 1 스킬 · 2 궁극기)의 레벨을 올려 주는 초월 단계.</summary>
        public static int SkillLevelUnlock(int slot) => slot == 0 ? 2 : slot == 1 ? 4 : 5;

        /// <summary>이 초월 단계에서 해당 스킬 칸에 더해지는 레벨(재료 없이).</summary>
        public static int SkillLevelBonus(int transcend, int slot) => transcend >= SkillLevelUnlock(slot) ? SkillLevelStep : 0;

        /// <summary>고유 능력이 열리는 초월 단계인지(+1 / +3 / +6).</summary>
        public static bool IsAbilityStep(int transcend) => transcend == 1 || transcend == 3 || transcend == 6;
    }
}
