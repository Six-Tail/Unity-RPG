namespace SubcultureRPG.Characters
{
    /// <summary>
    /// 캐릭터를 화면에 '보여주는' 역할의 공통 인터페이스.
    /// 전투/가챠/연출 코드는 이 인터페이스만 알면 되고,
    /// 내부가 정적 일러스트인지 Live2D인지 몰라도 됩니다.
    /// </summary>
    public interface ICharacterView
    {
        /// <summary>캐릭터 데이터를 연결하고 화면에 표시합니다.</summary>
        void Bind(CharacterData data);

        /// <summary>모션/연출 재생. 예) "idle", "attack", "hit", "win".</summary>
        void PlayMotion(string key);

        /// <summary>표시/숨김.</summary>
        void SetVisible(bool visible);
    }
}
