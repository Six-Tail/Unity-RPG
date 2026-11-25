using System.Collections.Generic;
using UnityEngine;
using SubcultureRPG.Characters;
using SubcultureRPG.Meta;

namespace SubcultureRPG
{
    /// <summary>
    /// 이미지 에셋 조회. <c>Assets/_Project/Resources/Art/</c> 아래에 정해진 파일명으로 넣으면 자동으로 연결됩니다.
    /// (없으면 null → 각 화면은 지금까지의 글자·색 표시로 대체)
    ///
    ///   Characters/{캐릭터 id}      전신·반신 일러스트 (예: Characters/c_seraphine)
    ///   Cutouts/{캐릭터 id}         배경 없는 일러스트 — 스토리 대화 · 홈 화면처럼 배경 위에 인물만 세울 때 (없으면 일러스트 사용)
    ///   Thumbs/{캐릭터 id}          얼굴 썸네일 (없으면 일러스트 사용)
    ///   Enemies/{적 이름}           적 일러스트 (예: Enemies/그림자병)
    ///   Items/{MaterialType 이름}   재료 아이콘 (예: Items/ExpSmall, Items/FireAsc2)
    ///   Equipment/slot_{부위}       장비 부위 아이콘 (head · hands · body · feet · neck · ring)
    ///   Backgrounds/{키}            화면 배경 (home · gacha · stage · growth · party · bag · shop · battle · battle_ch3 …)
    ///   Banners/{키}                가챠 배너 그림 (픽업 캐릭터 id · standard)
    ///
    /// 임포트 설정(Sprite · 크기)은 Editor/ArtImportSettings 가 처음 임포트할 때 자동 적용합니다.
    /// </summary>
    public static class GameArt
    {
        public const string ResourceRoot = "Art/";

        private static readonly Dictionary<string, Sprite> Cache = new();

        /// <summary>Resources/Art/ 기준 경로로 스프라이트 로드(없으면 null, 결과 캐시).</summary>
        public static Sprite Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (Cache.TryGetValue(path, out var cached)) return cached;
            var s = Resources.Load<Sprite>(ResourceRoot + path);
            Cache[path] = s;
            return s;
        }

        /// <summary>여러 후보 중 처음 찾은 것.</summary>
        public static Sprite LoadFirst(params string[] paths)
        {
            if (paths == null) return null;
            foreach (var p in paths)
            {
                var s = Load(p);
                if (s != null) return s;
            }
            return null;
        }

        /// <summary>에디터에서 이미지를 새로 넣은 뒤 다시 찾게 할 때.</summary>
        public static void ClearCache() => Cache.Clear();

        private static string KeyOf(CharacterData c) => !string.IsNullOrEmpty(c.id) ? c.id : c.name;

        /// <summary>캐릭터 일러스트: 에셋에 직접 연결한 portrait → Characters/{id} → thumbnail.</summary>
        public static Sprite Portrait(CharacterData c)
        {
            if (c == null) return null;
            if (c.portrait != null) return c.portrait;
            var s = Load("Characters/" + KeyOf(c));
            if (s != null) return s;
            if (c.thumbnail != null) return c.thumbnail;
            return Load("Thumbs/" + KeyOf(c));
        }

        /// <summary>배경 없는 캐릭터 일러스트(Cutouts/{id}). 없으면 null.</summary>
        public static Sprite Cutout(CharacterData c) => c == null ? null : Load("Cutouts/" + KeyOf(c));

        /// <summary>
        /// 배경 위에 세울 인물 그림: "Characters/…" 경로면 배경 없는 버전(Cutouts/…)을 먼저 찾고, 없으면 그 경로 그대로.
        /// (적 그림 등 다른 경로는 원래 배경이 없으므로 그대로 씀)
        /// </summary>
        public static Sprite Standing(string path)
        {
            const string prefix = "Characters/";
            if (!string.IsNullOrEmpty(path) && path.StartsWith(prefix))
            {
                var s = Load("Cutouts/" + path.Substring(prefix.Length));
                if (s != null) return s;
            }
            return Load(path);
        }

        /// <summary>캐릭터 썸네일: 에셋에 직접 연결한 thumbnail → Thumbs/{id} → 일러스트.</summary>
        public static Sprite Thumb(CharacterData c)
        {
            if (c == null) return null;
            if (c.thumbnail != null) return c.thumbnail;
            var s = Load("Thumbs/" + KeyOf(c));
            return s != null ? s : Portrait(c);
        }

        /// <summary>적 일러스트(표시 이름 기준).</summary>
        public static Sprite Enemy(string displayName) =>
            string.IsNullOrEmpty(displayName) ? null : Load("Enemies/" + displayName);

        /// <summary>재료 아이콘.</summary>
        public static Sprite MaterialIcon(MaterialType t) => Load("Items/" + t);

        /// <summary>장비 부위 아이콘.</summary>
        public static Sprite EquipSlotIcon(EquipSlot slot) => Load("Equipment/slot_" + slot.ToString().ToLowerInvariant());

        /// <summary>세트별 장비 그림(Equipment/{세트 id}_{부위}). 없으면 부위 아이콘.</summary>
        public static Sprite EquipIcon(string set, EquipSlot slot) =>
            (!string.IsNullOrEmpty(set) ? Load("Equipment/" + set + "_" + slot.ToString().ToLowerInvariant()) : null) ?? EquipSlotIcon(slot);

        /// <summary>화면 배경.</summary>
        public static Sprite Background(string key) => Load("Backgrounds/" + key);

        /// <summary>가챠 배너 그림.</summary>
        public static Sprite Banner(string key) => Load("Banners/" + key);
    }
}
