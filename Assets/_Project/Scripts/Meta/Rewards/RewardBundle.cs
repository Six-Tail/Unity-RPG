using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Gacha;

namespace SubcultureRPG.Meta
{
    [Serializable]
    public class RewardMat
    {
        public int type;    // MaterialType(정수로 저장)
        public int count;
        public MaterialType Type => (MaterialType)type;
    }

    /// <summary>
    /// 보상 묶음(우편 · 출석 · 챕터 별 보상 공용): 크리스탈 · 금화 · 뽑기권 · 재료(행동력 물약 · 캐릭터 선택권 포함).
    /// 세이브(JSON)에 그대로 들어가므로 필드 이름을 바꾸지 마세요.
    /// </summary>
    [Serializable]
    public class RewardBundle
    {
        public int crystals, gold, pickup, standard;
        public List<RewardMat> mats = new();

        public RewardBundle Crystals(int n) { crystals += n; return this; }
        public RewardBundle Gold(int n) { gold += n; return this; }
        public RewardBundle Pickup(int n) { pickup += n; return this; }
        public RewardBundle Standard(int n) { standard += n; return this; }

        public RewardBundle Mat(MaterialType t, int n)
        {
            mats ??= new();
            var e = mats.Find(m => m.type == (int)t);
            if (e != null) e.count += n; else mats.Add(new RewardMat { type = (int)t, count = n });
            return this;
        }

        public bool IsEmpty => crystals <= 0 && gold <= 0 && pickup <= 0 && standard <= 0 && (mats == null || mats.Count == 0);

        /// <summary>들어 있는 캐릭터 선택권(없으면 null).</summary>
        public MaterialType? Selector
        {
            get
            {
                if (mats != null) foreach (var m in mats) if (Materials.IsSelector(m.Type) && m.count > 0) return m.Type;
                return null;
            }
        }

        /// <summary>지급(지갑 · 가방).</summary>
        public void Grant()
        {
            var wallet = UnityEngine.Object.FindAnyObjectByType<PlayerWallet>();
            var inv = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>();
            if (wallet != null)
            {
                if (crystals > 0) wallet.Add(crystals);
                if (gold > 0) wallet.AddGold(gold);
                if (pickup > 0) wallet.AddTickets(GachaBannerType.Pickup, pickup);
                if (standard > 0) wallet.AddTickets(GachaBannerType.Standard, standard);
            }
            if (inv != null && mats != null)
                foreach (var m in mats) if (m.count > 0) inv.Add(m.Type, m.count);
        }

        /// <summary>구성품(아이콘 경로 · 이름 · 수량 · 글자색).</summary>
        public List<(string icon, string name, int count, string hex)> Parts()
        {
            var list = new List<(string, string, int, string)>();
            if (crystals > 0) list.Add(("Items/Crystal", "크리스탈", crystals, "#8FE9FF"));
            if (pickup > 0) list.Add(("Items/PickupTicket", "픽업 뽑기권", pickup, "#FFB3D9"));
            if (standard > 0) list.Add(("Items/StandardTicket", "상시 뽑기권", standard, "#B8C8FF"));
            if (mats != null)
                foreach (var m in mats)
                    if (m.count > 0) list.Add(("Items/" + m.Type, Materials.Name(m.Type), m.count, Grades.Hex(Materials.Grade(m.Type))));
            if (gold > 0) list.Add(("Items/Gold", "금화", gold, "#FFD36B"));
            return list;
        }

        /// <summary>한 줄 설명(색 포함).</summary>
        public string Describe(string sep = "  ·  ")
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (_, name, count, hex) in Parts())
            {
                if (sb.Length > 0) sb.Append(sep);
                sb.Append($"<color={hex}>{name}</color> {(count >= 1000 ? count.ToString("N0") : "×" + count)}");
            }
            return sb.ToString();
        }

        /// <summary>대표 아이콘(첫 구성품).</summary>
        public Sprite Icon()
        {
            var parts = Parts();
            return parts.Count > 0 ? GameArt.Load(parts[0].icon) : null;
        }
    }
}
