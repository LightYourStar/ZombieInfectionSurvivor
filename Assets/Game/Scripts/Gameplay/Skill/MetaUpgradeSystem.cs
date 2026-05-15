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
            MetaUpgradeData data = Load();
            return GetBonusAtLevel(type, GetLevelFromData(data, type));
        }

        /// <summary>
        /// 尝试购买升级。扣除金币并提升对应等级。
        /// 返回 true 表示升级成功，false 表示金币不足或已满级。
        /// </summary>
        public bool TryUpgrade(UpgradeType type)
        {
            MetaUpgradeData data = Load();
            int currentLevel = GetLevel(type);
            int maxLevel = GetMaxLevel(type);

            if (currentLevel >= maxLevel) return false;

            int cost = GetUpgradeCost(type, currentLevel);
            if (cost < 0 || data.TotalGold < cost) return false;

            data.TotalGold -= cost;

            switch (type)
            {
                case UpgradeType.MoveSpeed: data.SpeedLevel++; break;
                case UpgradeType.InfectionRadius: data.InfectionRadiusLevel++; break;
                case UpgradeType.ZombieCompanionCap: data.ZombieCapLevel++; break;
                default: return false;
            }

            Save(data);
            return true;
        }

        /// <summary>
        /// 获取指定等级升级到下一级的花费。
        /// </summary>
        private static int GetUpgradeCost(int currentLevel)
        {
            switch (currentLevel)
            {
                case 0: return 50;
                case 1: return 100;
                case 2: return 200;
                case 3: return 350;
                case 4: return 500;
                default: return -1;
            }
        }

        private int GetUpgradeCost(UpgradeType type, int currentLevel)
        {
            MetaUpgradeConfig.UpgradeTrack track = GetTrack(type);
            if (track != null)
            {
                int configCost = track.GetCost(currentLevel);
                if (configCost >= 0)
                {
                    return configCost;
                }
            }

            return GetUpgradeCost(currentLevel);
        }

        /// <summary>
        /// 获取指定升级项的当前等级。
        /// </summary>
        public int GetLevel(UpgradeType type)
        {
            MetaUpgradeData data = Load();
            switch (type)
            {
                case UpgradeType.MoveSpeed: return data.SpeedLevel;
                case UpgradeType.InfectionRadius: return data.InfectionRadiusLevel;
                case UpgradeType.ZombieCompanionCap: return data.ZombieCapLevel;
                default: return 0;
            }
        }

        /// <summary>
        /// 获取指定升级项的最大等级。
        /// </summary>
        public int GetMaxLevel(UpgradeType type)
        {
            MetaUpgradeConfig.UpgradeTrack track = GetTrack(type);
            if (track != null)
            {
                if (track.HasBonusData)
                {
                    return track.MaxLevel;
                }

                if (track.CostCount > 0)
                {
                    return track.CostCount;
                }
            }

            return 5;
        }

        /// <summary>
        /// 获取指定升级项下一级的花费。已满级返回 -1。
        /// </summary>
        public int GetNextCost(UpgradeType type)
        {
            int level = GetLevel(type);
            if (level >= GetMaxLevel(type))
            {
                return -1;
            }

            return GetUpgradeCost(type, level);
        }

        public float GetBonusAtLevel(UpgradeType type, int level)
        {
            MetaUpgradeConfig.UpgradeTrack track = GetTrack(type);
            if (track != null && track.HasBonusData)
            {
                return track.GetBonus(level);
            }

            return GetFallbackBonus(type, level);
        }

        /// <summary>
        /// 计算一局结算应获得的金币数量。
        /// </summary>
        public static int CalculateGoldReward(int infectedCount, Game.Core.SessionRating rating)
        {
            int baseGold = infectedCount / 5;
            switch (rating)
            {
                case Game.Core.SessionRating.S: return Mathf.RoundToInt(baseGold * 1.2f);
                case Game.Core.SessionRating.SS: return Mathf.RoundToInt(baseGold * 1.4f);
                default: return baseGold;
            }
        }

        /// <summary>
        /// 清除所有本地存档数据（仅开发调试用）。
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void ClearSaveData()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
            m_cachedData = null;
            Debug.Log("[MetaUpgradeSystem] 本地存档已清除");
        }

        /// <summary>MetaUpgradeConfig 引用，供外部读取轨道信息</summary>
        public MetaUpgradeConfig Config => m_metaConfig;

        private MetaUpgradeConfig.UpgradeTrack GetTrack(UpgradeType type)
        {
            if (m_metaConfig == null)
            {
                return null;
            }

            switch (type)
            {
                case UpgradeType.InfectionRadius:
                    return m_metaConfig.InfectionRadiusTrack;
                case UpgradeType.MoveSpeed:
                    return m_metaConfig.MoveSpeedTrack;
                case UpgradeType.ZombieCompanionCap:
                    return m_metaConfig.ZombieCapTrack;
                case UpgradeType.ExpMultiplier:
                    return m_metaConfig.ExpMultiplierTrack;
                default:
                    return null;
            }
        }

        private static int GetLevelFromData(MetaUpgradeData data, UpgradeType type)
        {
            if (data == null)
            {
                return 0;
            }

            switch (type)
            {
                case UpgradeType.MoveSpeed:
                    return data.SpeedLevel;
                case UpgradeType.InfectionRadius:
                    return data.InfectionRadiusLevel;
                case UpgradeType.ZombieCompanionCap:
                    return data.ZombieCapLevel;
                case UpgradeType.ExpMultiplier:
                    return data.ExpMultiplierLevel;
                default:
                    return 0;
            }
        }

        private static float GetFallbackBonus(UpgradeType type, int level)
        {
            int clampedLevel = Mathf.Clamp(level, 0, 5);
            switch (type)
            {
                case UpgradeType.MoveSpeed:
                    return clampedLevel * 0.1f;
                case UpgradeType.InfectionRadius:
                    return clampedLevel * 0.05f;
                case UpgradeType.ZombieCompanionCap:
                    return clampedLevel * 2f;
                default:
                    return 0f;
            }
        }
    }
}
