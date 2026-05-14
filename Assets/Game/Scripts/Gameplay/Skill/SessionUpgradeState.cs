namespace Game.Gameplay.Skill
{
    /// <summary>
    /// 单局运行时升级状态。
    /// 记录当前局内通过 2.0 升级系统获得的各项加成叠加次数和修正值。
    /// 每局开始时由 UpgradeSystem.Reset() 重置为初始状态。
    /// InfectionSystem、ZombieCompanionAI、GameSystemRunner 等系统读取此状态来获取修正后的数值。
    /// </summary>
    public class SessionUpgradeState
    {
        // ==================== 叠加计数 ====================

        /// <summary>连锁强化叠加次数（最多 1）</summary>
        public int ChainPlusOneStacks { get; private set; }

        /// <summary>扩散毒圈叠加次数（最多 2）</summary>
        public int BurstRadiusUpStacks { get; private set; }

        /// <summary>新生狂奔叠加次数（最多 2）</summary>
        public int NewbornRushDurationUpStacks { get; private set; }

        /// <summary>尸群嗅觉叠加次数（最多 2）</summary>
        public int ZombiePerceptionUpStacks { get; private set; }

        /// <summary>狂潮提前叠加次数（最多 1）</summary>
        public int FinalFrenzyEarlyStacks { get; private set; }

        /// <summary>回响爆发叠加次数（最多 1）</summary>
        public int EchoBurstStacks { get; private set; }

        // ==================== 最大叠加限制 ====================

        public const int ChainPlusOneMaxStacks = 1;
        public const int BurstRadiusUpMaxStacks = 2;
        public const int NewbornRushDurationUpMaxStacks = 2;
        public const int ZombiePerceptionUpMaxStacks = 2;
        public const int FinalFrenzyEarlyMaxStacks = 1;
        public const int EchoBurstMaxStacks = 1;

        // ==================== 回响爆发计数器 ====================

        /// <summary>当前局累计感染次数，用于回响爆发每 5 次触发判定</summary>
        public int InfectionCounter { get; set; }

        // ==================== 公开 API ====================

        /// <summary>
        /// 尝试应用一次升级。如果已达到最大叠加次数则返回 false。
        /// </summary>
        public bool TryApply(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.ChainPlusOne:
                    if (ChainPlusOneStacks >= ChainPlusOneMaxStacks) return false;
                    ChainPlusOneStacks++;
                    return true;

                case UpgradeType.BurstRadiusUp:
                    if (BurstRadiusUpStacks >= BurstRadiusUpMaxStacks) return false;
                    BurstRadiusUpStacks++;
                    return true;

                case UpgradeType.NewbornRushDurationUp:
                    if (NewbornRushDurationUpStacks >= NewbornRushDurationUpMaxStacks) return false;
                    NewbornRushDurationUpStacks++;
                    return true;

                case UpgradeType.ZombiePerceptionUp:
                    if (ZombiePerceptionUpStacks >= ZombiePerceptionUpMaxStacks) return false;
                    ZombiePerceptionUpStacks++;
                    return true;

                case UpgradeType.FinalFrenzyEarly:
                    if (FinalFrenzyEarlyStacks >= FinalFrenzyEarlyMaxStacks) return false;
                    FinalFrenzyEarlyStacks++;
                    return true;

                case UpgradeType.EchoBurst:
                    if (EchoBurstStacks >= EchoBurstMaxStacks) return false;
                    EchoBurstStacks++;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 判断指定升级是否已达到最大叠加次数。
        /// </summary>
        public bool IsMaxed(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.ChainPlusOne: return ChainPlusOneStacks >= ChainPlusOneMaxStacks;
                case UpgradeType.BurstRadiusUp: return BurstRadiusUpStacks >= BurstRadiusUpMaxStacks;
                case UpgradeType.NewbornRushDurationUp: return NewbornRushDurationUpStacks >= NewbornRushDurationUpMaxStacks;
                case UpgradeType.ZombiePerceptionUp: return ZombiePerceptionUpStacks >= ZombiePerceptionUpMaxStacks;
                case UpgradeType.FinalFrenzyEarly: return FinalFrenzyEarlyStacks >= FinalFrenzyEarlyMaxStacks;
                case UpgradeType.EchoBurst: return EchoBurstStacks >= EchoBurstMaxStacks;
                default: return false;
            }
        }

        /// <summary>
        /// 重置所有升级状态为初始值。新一局开始时调用。
        /// </summary>
        public void Reset()
        {
            ChainPlusOneStacks = 0;
            BurstRadiusUpStacks = 0;
            NewbornRushDurationUpStacks = 0;
            ZombiePerceptionUpStacks = 0;
            FinalFrenzyEarlyStacks = 0;
            EchoBurstStacks = 0;
            InfectionCounter = 0;
        }

        // ==================== 数值查询（结合 GameConfig 基础值） ====================

        /// <summary>获取当前局修正后的感染爆发额外目标数</summary>
        public int GetBurstMaxTargets(int baseValue)
        {
            return baseValue + ChainPlusOneStacks;
        }

        /// <summary>获取当前局修正后的感染爆发半径</summary>
        public float GetBurstRadius(float baseValue)
        {
            return baseValue * (1f + 0.2f * BurstRadiusUpStacks);
        }

        /// <summary>获取当前局修正后的新生僵尸冲刺时间</summary>
        public float GetNewbornRushDuration(float baseValue)
        {
            return baseValue + 0.25f * NewbornRushDurationUpStacks;
        }

        /// <summary>获取当前局修正后的僵尸感知范围</summary>
        public float GetZombiePerceptionRadius(float baseValue)
        {
            return baseValue * (1f + 0.2f * ZombiePerceptionUpStacks);
        }

        /// <summary>获取当前局修正后的最终狂潮触发剩余时间</summary>
        public float GetFinalFrenzyStartTime(float baseValue)
        {
            return baseValue + 8f * FinalFrenzyEarlyStacks;
        }

        /// <summary>是否启用回响爆发</summary>
        public bool IsEchoBurstActive => EchoBurstStacks > 0;
    }
}
