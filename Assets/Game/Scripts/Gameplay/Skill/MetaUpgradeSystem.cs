using System;
using Game.Config;
using UnityEngine;

namespace Game.Gameplay.Skill
{
    /// <summary>
    /// 局外永久升级数据管理系统，使用 <see cref="PlayerPrefs"/> 持久化 <see cref="MetaUpgradeData"/>。
    /// 对外提供四类能力：
    /// 1. <see cref="Save"/>：将传入的数据写入 PlayerPrefs 并更新内部缓存；
    /// 2. <see cref="Load"/>：读取持久化数据，数据不存在或损坏时返回默认值；
    /// 3. <see cref="AddGold"/>：向持久化数据累加金币（结算流程使用）；
    /// 4. <see cref="GetBonus"/>：结合 <see cref="MetaUpgradeConfig"/> 的升级轨道，按当前等级返回加成数值。
    /// </summary>
    /// <remarks>
    /// 为避免每次调用都走一次 JSON 解析，系统在首次 <see cref="Load"/>（或 <see cref="Awake"/>）后将数据缓存到 <see cref="m_cachedData"/>；
    /// <see cref="Save"/> 会同步更新缓存，保证后续读取结果与磁盘一致。
    /// 当 JSON 为空或解析抛出异常时，视为数据损坏，返回一份全新的默认 <see cref="MetaUpgradeData"/>，不会中断游戏流程（需求 11.1）。
    /// </remarks>
    public class MetaUpgradeSystem : MonoBehaviour
    {
        /// <summary>
        /// PlayerPrefs 中存储 MetaUpgradeData JSON 的键名，统一加 ZIS 前缀避免与其他项目冲突。
        /// </summary>
        private const string PrefsKey = "ZIS_MetaUpgrade";

        [Header("局外升级配置")]
        [Tooltip("提供各升级轨道每级花费与加成数值的 ScriptableObject 配置")]
        [SerializeField] private MetaUpgradeConfig m_metaConfig;

        /// <summary>
        /// 运行时缓存的持久化数据。null 表示尚未从 PlayerPrefs 加载过，下一次 <see cref="Load"/> 会触发读取。
        /// </summary>
        private MetaUpgradeData m_cachedData;

        // ==================== 生命周期 ====================

        /// <summary>
        /// Awake 阶段预先加载一次数据，让后续的 <see cref="GetBonus"/>、<see cref="AddGold"/> 调用直接命中缓存。
        /// </summary>
        private void Awake()
        {
            // 预热缓存，忽略返回值；异常已在 Load 内部吞掉并回退到默认值。
            Load();
        }

        // ==================== 公开 API ====================

        /// <summary>
        /// 将传入的升级数据写入 PlayerPrefs，并同步刷新内部缓存。
        /// 传入 null 时视为无效调用，仅输出警告，不清空已持久化的数据。
        /// </summary>
        /// <param name="data">需要保存的数据。字段含义见 <see cref="MetaUpgradeData"/>。</param>
        public void Save(MetaUpgradeData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[MetaUpgradeSystem] Save 收到空数据，已忽略本次保存请求");
                return;
            }

            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();

            // 使用外部传入的对象作为新缓存，保持引用一致性，后续 AddGold 等操作直接修改该对象再 Save。
            m_cachedData = data;
        }

        /// <summary>
        /// 从 PlayerPrefs 读取升级数据。若已有缓存则直接返回缓存；
        /// 若首次读取时 JSON 为空或解析失败（数据损坏），返回一份全新默认值并写入缓存，避免后续重复解析失败日志。
        /// </summary>
        /// <returns>非 null 的 <see cref="MetaUpgradeData"/> 实例</returns>
        public MetaUpgradeData Load()
        {
            if (m_cachedData != null)
            {
                return m_cachedData;
            }

            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                // 首次启动、或用户清理过 PlayerPrefs，均视为使用默认值
                m_cachedData = new MetaUpgradeData();
                return m_cachedData;
            }

            try
            {
                MetaUpgradeData parsed = JsonUtility.FromJson<MetaUpgradeData>(json);
                // FromJson 在部分异常 JSON 上会返回 null（例如空 JSON 字符串），统一兜底为默认值
                m_cachedData = parsed ?? new MetaUpgradeData();
            }
            catch (Exception ex)
            {
                // 需求 11.1：数据损坏时返回默认值而不中断游戏
                Debug.LogWarning($"[MetaUpgradeSystem] 解析 PlayerPrefs 中的 MetaUpgradeData 失败，回退到默认值。原因: {ex.Message}");
                m_cachedData = new MetaUpgradeData();
            }

            return m_cachedData;
        }

        /// <summary>
        /// 向持久化的金币总量累加指定数量（通常由结算流程在局末调用）。
        /// 非正数视为无效输入，不写入 PlayerPrefs。
        /// </summary>
        /// <param name="amount">待累加的金币数量，必须为正整数</param>
        public void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            MetaUpgradeData data = Load();
            data.TotalGold += amount;
            Save(data);
        }

        /// <summary>
        /// 按当前等级从 <see cref="MetaUpgradeConfig"/> 对应轨道读取加成数值。
        /// 未注入配置、或遇到未识别的 <see cref="UpgradeType"/> 时返回 0，不抛异常。
        /// </summary>
        /// <param name="type">升级项枚举</param>
        /// <returns>当前等级对应的加成数值</returns>
        public float GetBonus(UpgradeType type)
        {
            if (m_metaConfig == null)
            {
                Debug.LogWarning("[MetaUpgradeSystem] GetBonus 调用时 MetaUpgradeConfig 未注入，返回 0 加成");
                return 0f;
            }

            MetaUpgradeData data = Load();

            switch (type)
            {
                case UpgradeType.InfectionRadius:
                    return m_metaConfig.InfectionRadiusTrack.GetBonus(data.InfectionRadiusLevel);
                case UpgradeType.MoveSpeed:
                    return m_metaConfig.MoveSpeedTrack.GetBonus(data.SpeedLevel);
                case UpgradeType.ZombieCompanionCap:
                    return m_metaConfig.ZombieCapTrack.GetBonus(data.ZombieCapLevel);
                case UpgradeType.ExpMultiplier:
                    return m_metaConfig.ExpMultiplierTrack.GetBonus(data.ExpMultiplierLevel);
                default:
                    Debug.LogWarning($"[MetaUpgradeSystem] GetBonus 收到未识别的 UpgradeType: {type}，返回 0 加成");
                    return 0f;
            }
        }
    }
}
