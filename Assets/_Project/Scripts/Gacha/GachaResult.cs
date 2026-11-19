using SubcultureRPG.Characters;

namespace SubcultureRPG.Gacha
{
    /// <summary>뽑기 1회의 결과.</summary>
    public struct GachaResult
    {
        /// <summary>뽑힌 캐릭터. (해당 풀이 비어 있으면 null 일 수 있음)</summary>
        public CharacterData character;

        public Rarity rarity;

        /// <summary>픽업(rate-up) 캐릭터였는가.</summary>
        public bool isFeatured;

        /// <summary>6성에서 50/50을 '굴려서' 이겼는가. (천장 보장으로 받은 경우는 false)</summary>
        public bool wonFiftyFifty;

        /// <summary>이 배너에서의 누적 뽑기 번호.</summary>
        public int pullIndex;
    }
}
