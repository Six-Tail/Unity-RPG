namespace SubcultureRPG.Battle
{
    /// <summary>
    /// 전투 중 유닛 1명의 누적 기록(결과 화면 '전투 기록' 창용).
    /// 피해 · 회복은 실제로 깎이고 채워진 양(쓰러뜨릴 때 남은 체력을 넘은 피해 · 가득 찬 체력을 넘은 회복은 빼고 셈).
    /// 화면에 뜨는 숫자는 그대로(계산된 양).
    /// </summary>
    public class BattleRecord
    {
        public long dealt;          // 준 피해(지속 피해 포함 — 효과를 건 유닛에게 기록)
        public long taken;          // 받은 피해
        public long healed;         // 회복해 준 양
        public long healReceived;   // 회복받은 양
        public int actions;         // 행동 횟수
        public int crits;           // 치명타 적중(대상 기준)
        public int breaks;          // 브레이크시킨 횟수
        public int kills;           // 쓰러뜨린 수
    }
}
