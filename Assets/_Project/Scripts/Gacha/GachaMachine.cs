using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG.Gacha
{
    /// <summary>
    /// 확률·천장 로직을 담은 뽑기 엔진. MonoBehaviour가 아니라 순수 로직이라 테스트가 쉽습니다.
    /// RNG를 주입할 수 있어(시드 고정) 결정적 테스트가 가능합니다.
    /// </summary>
    public class GachaMachine
    {
        private readonly Func<float> _roll; // [0,1)

        public GachaMachine(Func<float> roll01 = null)
        {
            _roll = roll01 ?? (() => UnityEngine.Random.value);
        }

        /// <summary>1회 뽑기. state를 직접 갱신합니다.</summary>
        public GachaResult Pull(GachaBanner banner, GachaPityState state)
        {
            var cfg = banner.config;

            state.pullsSince6++;
            state.pullsSince5++;
            state.totalPulls++;

            Rarity rarity = DecideRarity(cfg, state);
            var result = new GachaResult { rarity = rarity, pullIndex = state.totalPulls };

            switch (rarity)
            {
                case Rarity.R6:
                    ResolveFeatured6(banner, state, ref result);
                    state.pullsSince6 = 0;
                    state.pullsSince5 = 0; // 6성은 5성 '이상' 보장도 충족 → 둘 다 리셋
                    break;
                case Rarity.R5:
                    ResolveFeatured5(banner, state, ref result);
                    state.pullsSince5 = 0;
                    break;
                case Rarity.R4:
                    result.character = PickRandom(banner.pool4);
                    break;
                default:
                    result.character = PickRandom(banner.pool3);
                    break;
            }

            return result;
        }

        /// <summary>다회 뽑기(예: 10연). 10연 보장은 5성 하드 천장 카운터로 자연히 처리됩니다.</summary>
        public List<GachaResult> PullMany(GachaBanner banner, GachaPityState state, int count)
        {
            var results = new List<GachaResult>(count);
            for (int i = 0; i < count; i++)
                results.Add(Pull(banner, state));
            return results;
        }

        // ---------------- 내부 로직 ----------------

        private Rarity DecideRarity(GachaConfig cfg, GachaPityState state)
        {
            // 1) 6성 (소프트/하드 천장 반영)
            float p6 = Compute6StarChance(cfg, state);
            if (_roll() < p6) return Rarity.R6;

            // 2) 5성 (하드 천장: N연 내 5성 이상 보장)
            bool force5 = state.pullsSince5 >= cfg.fiveStarHardPity;
            if (force5 || _roll() < cfg.rate5 / 100f) return Rarity.R5;

            // 3) 4성 vs 3성 (남은 확률을 비율로 분배)
            float denom = cfg.rate4 + cfg.Rate3;
            if (denom <= 0f) return Rarity.R3;
            if (_roll() < cfg.rate4 / denom) return Rarity.R4;
            return Rarity.R3;
        }

        /// <summary>이번 뽑기의 6성 확률(0~1). 하드 천장이면 1.</summary>
        private static float Compute6StarChance(GachaConfig cfg, GachaPityState state)
        {
            int n = state.pullsSince6; // 이미 이번 뽑기만큼 증가된 값
            if (n >= cfg.sixStarHardPity) return 1f;

            float baseRate = cfg.rate6 / 100f;
            if (n >= cfg.sixStarSoftPityStart)
            {
                int steps = n - cfg.sixStarSoftPityStart + 1;
                return Mathf.Clamp01(baseRate + steps * (cfg.sixStarSoftPityIncrement / 100f));
            }
            return baseRate;
        }

        private void ResolveFeatured6(GachaBanner banner, GachaPityState state, ref GachaResult result)
        {
            // 상시 배너(픽업 없음): 상시 6성 풀에서만, 50/50·확정 플래그 없음
            if (banner.featured6 == null)
            {
                result.isFeatured = false;
                result.wonFiftyFifty = false;
                result.character = PickRandom(banner.pool6);
                return;
            }

            bool featured;
            bool won = false;

            if (state.guaranteedFeatured6)
            {
                featured = true;                       // 천장 보장
                state.guaranteedFeatured6 = false;
            }
            else if (_roll() < banner.config.featured6Chance)
            {
                featured = true;
                won = true;                            // 50/50 승리
            }
            else
            {
                featured = false;
                state.guaranteedFeatured6 = true;      // 50/50 패배 → 다음 6성 픽업 확정
            }

            result.isFeatured = featured;
            result.wonFiftyFifty = won;
            result.character = featured ? banner.featured6 : PickRandom(banner.pool6);
            if (result.character == null) result.character = banner.featured6; // 비픽업 풀이 비면 폴백
        }

        private void ResolveFeatured5(GachaBanner banner, GachaPityState state, ref GachaResult result)
        {
            bool hasFeatured5 = banner.featured5 != null && banner.featured5.Count > 0;
            bool featured;

            if (state.guaranteedFeatured5 && hasFeatured5)
            {
                featured = true;
                state.guaranteedFeatured5 = false;
            }
            else if (hasFeatured5 && _roll() < banner.config.featured5Chance)
            {
                featured = true;
            }
            else
            {
                featured = false;
                if (hasFeatured5) state.guaranteedFeatured5 = true;
            }

            result.isFeatured = featured;
            result.character = featured ? PickRandom(banner.featured5) : PickRandom(banner.pool5);
            if (result.character == null)
                result.character = PickRandom(banner.pool5) ?? PickRandom(banner.featured5);
        }

        private CharacterData PickRandom(List<CharacterData> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            int idx = Mathf.Clamp((int)(_roll() * pool.Count), 0, pool.Count - 1);
            return pool[idx];
        }
    }
}
