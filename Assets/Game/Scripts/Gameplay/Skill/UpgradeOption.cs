namespace Game.Gameplay.Skill
{
    /// <summary>
    /// 局内升级选项数据结构。
    /// 由 <see cref="UpgradeSystem.DrawOptions"/> 生成，传递给 UpgradePanel 展示，
    /// 玩家选择后由 <see cref="UpgradeSystem.ApplyUpgrade"/> 应用到 <see cref="Player.PlayerStats"/>。
    /// </summary>
    /// <remarks>
    /// 设计为不可变对象：一旦创建后 Type / Value / DisplayName 不再变化，
    /// 避免 UI 层或其他系统意外修改选项内容。
    /// </remarks>
    public class UpgradeOption
    {
        /// <summary>升级类型，决定加成落到哪项属性</summary>
        public UpgradeType Type { get; }

        /// <summary>本次升级带来的增量值</summary>
        public float Value { get; }

        /// <summary>面板上展示给玩家的中文名称</summary>
        public string DisplayName { get; }

        /// <summary>
        /// 创建一个升级选项实例。
        /// </summary>
        /// <param name="type">升级类型</param>
        /// <param name="value">增量值</param>
        /// <param name="displayName">展示名称；为 null 时自动替换为空字符串</param>
        public UpgradeOption(UpgradeType type, float value, string displayName)
        {
            Type = type;
            Value = value;
            DisplayName = displayName ?? string.Empty;
        }
    }
}
