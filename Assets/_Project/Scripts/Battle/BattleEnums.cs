namespace SubcultureRPG.Battle
{
    /// <summary>전투 진영.</summary>
    public enum Team
    {
        Ally,
        Enemy,
    }

    /// <summary>스킬 분류.</summary>
    public enum SkillCategory
    {
        Basic,     // 기본 공격(에너지 충전원, SP +1)
        Skill,     // 일반 스킬(SP -1)
        Ultimate,  // 필살기(에너지 가득 필요)
    }

    /// <summary>대상 지정 방식.</summary>
    public enum TargetType
    {
        SingleEnemy,
        AllEnemies,
        SingleAlly,
        AllAllies,
        Self,
        RandomEnemies, // 타격마다 무작위 적(연타)
        Blast,         // 대상 + 양옆(양옆은 splash 배율)
    }

    // 속성(Element)은 캐릭터 고유 특성이라 SubcultureRPG.Characters(CharacterTraits.cs)로 이동했습니다.

    /// <summary>버프/디버프/지속효과 종류.</summary>
    public enum StatusType
    {
        None,
        AtkUp,
        AtkDown,
        DefUp,
        DefDown,
        Dot, // 지속 피해
        SpdUp,
        SpdDown,
        CritUp,   // 치명타 확률 증가
        Regen,    // 턴 시작마다 회복
        Taunt,    // 도발: 적의 단일 대상 공격을 끌어옴
        Stun,     // 기절: 다음 자기 턴 행동 불가
    }
}
