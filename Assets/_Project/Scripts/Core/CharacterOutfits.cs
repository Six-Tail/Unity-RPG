using System;
using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;

namespace SubcultureRPG
{
    /// <summary>캐릭터 의상 1벌(옷장에 보이는 칸).</summary>
    public class Outfit
    {
        public string id;          // 세이브 키 — 바꾸지 말 것
        public string name;
        public string suffix;      // 아트 파일 이름 뒤에 붙는 말(Characters/{캐릭터 id}{suffix} …), 기본 의상은 ""
        public string unlockText;  // 잠겨 있을 때 안내
    }

    /// <summary>
    /// 캐릭터 의상(옷장): 지금은 '기본' · '개화'(Lv.100 숨겨진 특성 개화 시 해금). 추후 스킨은 All 에 추가.
    /// 고른 의상은 일러스트 · 썸네일 · 배경 없는 그림 · 전투 그림 · 움직이는 영상 모두에 적용되고,
    /// 그 의상의 파일이 없으면 기본 의상 그림으로 대체합니다(GameArt · MotionVideoStage).
    ///
    ///   Resources/Art/Characters/{id}_bloom · Thumbs/{id}_bloom · Cutouts/{id}_bloom · Figures/{id}_bloom · Motion/{id}_bloom.mp4
    ///
    /// 저장: SaveStore "outfits"(계정 데이터). 문서: docs/outfits.md
    /// </summary>
    public static class CharacterOutfits
    {
        public const string BaseId = "base", BloomId = "bloom";
        private const string SaveKey = "outfits";

        public static readonly List<Outfit> All = new()
        {
            new Outfit { id = BaseId, name = "기본", suffix = "", unlockText = "" },
            new Outfit { id = BloomId, name = "개화", suffix = "_bloom", unlockText = "Lv.100 달성(숨겨진 특성 개화)" },
        };

        public static Outfit Find(string id) => All.Find(o => o.id == id) ?? All[0];

        [Serializable] private class Entry { public string id, outfit; }
        [Serializable] private class SaveData { public List<Entry> list = new(); }

        private static SaveData _d;
        private static string _loadedFrom;

        /// <summary>의상을 바꿨을 때(캐릭터). 화면이 그림을 다시 그리게.</summary>
        public static event Action<CharacterData> Changed;

        private static SaveData D
        {
            get
            {
                // 세이브 초기화 · 클라우드 불러오기로 값이 바뀌었으면 다시 읽음
                string raw = SaveStore.HasKey(SaveKey) ? SaveStore.GetString(SaveKey) : "";
                if (_d != null && raw == _loadedFrom) return _d;
                _loadedFrom = raw;
                _d = new SaveData();
                if (raw != "")
                {
                    try { _d = JsonUtility.FromJson<SaveData>(raw) ?? new SaveData(); }
                    catch { _d = new SaveData(); }
                }
                _d.list ??= new List<Entry>();
                return _d;
            }
        }

        private static string KeyOf(CharacterData c) => c == null ? null : !string.IsNullOrEmpty(c.id) ? c.id : c.name;

        /// <summary>지금 입고 있는 의상 id(캐릭터 id 기준).</summary>
        public static string CurrentId(string charKey)
        {
            if (string.IsNullOrEmpty(charKey)) return BaseId;
            return D.list.Find(e => e.id == charKey)?.outfit ?? BaseId;
        }

        public static Outfit Current(CharacterData c) => Find(CurrentId(KeyOf(c)));

        /// <summary>아트 파일 접미사(기본 의상이면 "").</summary>
        public static string SuffixOf(string charKey) => Find(CurrentId(charKey)).suffix;
        public static string SuffixOf(CharacterData c) => SuffixOf(KeyOf(c));

        /// <summary>이 의상을 입을 수 있는가(기본 = 항상, 개화 = Lv.100).</summary>
        public static bool Unlocked(CharacterData c, Outfit o, int level)
        {
            if (o == null || o.id == BaseId) return true;
            if (o.id == BloomId) return level >= Battle.CharacterKits.HiddenLevel;
            return false;   // 추후 스킨: 보유 여부
        }

        /// <summary>의상 입히기(잠겨 있어도 저장은 하지만, 화면에서는 해금된 것만 고르게 함).</summary>
        public static void Set(CharacterData c, string outfitId)
        {
            string k = KeyOf(c);
            if (k == null) return;
            var d = D;
            var e = d.list.Find(x => x.id == k);
            if (outfitId == BaseId) { if (e != null) d.list.Remove(e); }
            else if (e == null) d.list.Add(new Entry { id = k, outfit = outfitId });
            else e.outfit = outfitId;
            string json = JsonUtility.ToJson(d);
            SaveStore.SetString(SaveKey, json);
            SaveStore.Save();
            _loadedFrom = json;
            Changed?.Invoke(c);
        }

        /// <summary>그 의상의 일러스트가 있는가(개화 일러스트를 아직 만들지 않은 캐릭터 대비).</summary>
        public static bool HasArt(CharacterData c, Outfit o) =>
            o == null || o.suffix == "" || Resources.Load<Sprite>(GameArt.ResourceRoot + "Characters/" + KeyOf(c) + o.suffix) != null;
    }
}
