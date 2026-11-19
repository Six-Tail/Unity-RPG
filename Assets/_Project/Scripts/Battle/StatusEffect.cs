namespace SubcultureRPG.Battle
{
    /// <summary>
    /// 유닛에 걸린 상태효과 1개(런타임).
    /// magnitude 의미: 버프/디버프는 비율(0.3 = 30%), Dot는 틱당 피해량(절대값).
    /// </summary>
    public class StatusEffect
    {
        public StatusType type;
        public float magnitude;
        public int remaining;   // 남은 턴

        public bool IsBuff => type == StatusType.AtkUp || type == StatusType.DefUp;
        public bool IsDebuff => type == StatusType.AtkDown || type == StatusType.DefDown || type == StatusType.Dot;

        public StatusEffect(StatusType type, float magnitude, int remaining)
        {
            this.type = type;
            this.magnitude = magnitude;
            this.remaining = remaining;
        }
    }
}
