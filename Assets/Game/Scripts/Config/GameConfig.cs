using UnityEngine;

namespace Game.Config
{
    /// <summary>
    /// 游戏全局配置。
    /// 所有运行时可调的数值参数集中在此处，逻辑代码不应硬编码任何数值。
    /// 通过 Inspector 注入到 GameSystemRunner 使用。
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Game/GameConfig", order = 0)]
    public class GameConfig : ScriptableObject
    {
        // ==================== 玩家属性 ====================

        [Header("玩家属性")]
        [Tooltip("玩家基础移动速度（单位/秒）")]
        [SerializeField] private float m_playerBaseSpeed = 5f;

        [Tooltip("玩家基础感染半径（单位）")]
        [SerializeField] private float m_playerBaseInfectionRadius = 1.5f;

        /// <summary>玩家基础移动速度。</summary>
        public float PlayerBaseSpeed => m_playerBaseSpeed;

        /// <summary>玩家基础感染半径。</summary>
        public float PlayerBaseInfectionRadius => m_playerBaseInfectionRadius;

        // ==================== 人类属性 ====================

        [Header("人类属性")]
        [Tooltip("人类移动速度（单位/秒）")]
        [SerializeField] private float m_humanMoveSpeed = 3f;

        [Tooltip("人类感知半径，用于侦测玩家和僵尸同伴以触发逃跑")]
        [SerializeField] private float m_humanPerceptionRadius = 4f;

        [Tooltip("场上人类数量上限")]
        [SerializeField] private int m_humanMaxCount = 50;

        [Tooltip("单局开始时生成的初始人类数量")]
        [SerializeField] private int m_humanInitialCount = 20;

        [Tooltip("人类刷新间隔（秒）")]
        [SerializeField] private float m_humanSpawnInterval = 1f;

        /// <summary>人类移动速度。</summary>
        public float HumanMoveSpeed => m_humanMoveSpeed;

        /// <summary>人类感知半径。</summary>
        public float HumanPerceptionRadius => m_humanPerceptionRadius;

        /// <summary>场上人类数量上限。</summary>
        public int HumanMaxCount => m_humanMaxCount;

        /// <summary>单局开始时生成的初始人类数量。</summary>
        public int HumanInitialCount => m_humanInitialCount;

        /// <summary>人类刷新间隔（秒）。</summary>
        public float HumanSpawnInterval => m_humanSpawnInterval;

        // ==================== 僵尸同伴属性 ====================

        [Header("僵尸同伴属性")]
        [Tooltip("僵尸同伴移动速度（单位/秒）")]
        [SerializeField] private float m_zombieCompanionSpeed = 4f;

        [Tooltip("僵尸同伴感知范围，用于寻找可追击的人类")]
        [SerializeField] private float m_zombieCompanionPerceptionRadius = 5f;

        [Tooltip("僵尸同伴数量上限（可被局内升级提升）")]
        [SerializeField] private int m_zombieCompanionMaxCount = 30;

        /// <summary>僵尸同伴移动速度。</summary>
        public float ZombieCompanionSpeed => m_zombieCompanionSpeed;

        /// <summary>僵尸同伴感知范围。</summary>
        public float ZombieCompanionPerceptionRadius => m_zombieCompanionPerceptionRadius;

        /// <summary>僵尸同伴数量上限。</summary>
        public int ZombieCompanionMaxCount => m_zombieCompanionMaxCount;

        // ==================== 感染参数 ====================

        [Header("感染参数")]
        [Tooltip("感染判定的基础半径，玩家和僵尸同伴都使用此值作为起点")]
        [SerializeField] private float m_baseInfectionRadius = 1.5f;

        /// <summary>感染判定的基础半径。</summary>
        public float BaseInfectionRadius => m_baseInfectionRadius;

        // ==================== 奖励参数 ====================

        [Header("奖励参数")]
        [Tooltip("每次成功感染获得的经验值")]
        [SerializeField] private int m_expPerInfection = 5;

        [Tooltip("每次成功感染获得的金币")]
        [SerializeField] private int m_goldPerInfection = 1;

        /// <summary>每次成功感染获得的经验值。</summary>
        public int ExpPerInfection => m_expPerInfection;

        /// <summary>每次成功感染获得的金币。</summary>
        public int GoldPerInfection => m_goldPerInfection;

        // ==================== 升级参数 ====================

        [Header("升级参数")]
        [Tooltip("每一级所需的累计经验阈值，下标 0 对应 1 级升 2 级所需经验")]
        [SerializeField] private int[] m_expThresholds = new int[] { 10, 25, 50, 90, 150, 230, 330, 450, 600, 800 };

        [Tooltip("每次升级感染半径的增量")]
        [SerializeField] private float m_infectionRadiusPerUpgrade = 0.3f;

        [Tooltip("每次升级移动速度的增量")]
        [SerializeField] private float m_speedPerUpgrade = 0.5f;

        [Tooltip("每次升级僵尸同伴上限的增量")]
        [SerializeField] private int m_zombieCapPerUpgrade = 5;

        [Tooltip("每次升级经验倍率的增量（1.0 表示额外 100%）")]
        [SerializeField] private float m_expMultiplierPerUpgrade = 0.1f;

        /// <summary>每一级所需的累计经验阈值。</summary>
        public int[] ExpThresholds => m_expThresholds;

        /// <summary>每次升级感染半径的增量。</summary>
        public float InfectionRadiusPerUpgrade => m_infectionRadiusPerUpgrade;

        /// <summary>每次升级移动速度的增量。</summary>
        public float SpeedPerUpgrade => m_speedPerUpgrade;

        /// <summary>每次升级僵尸同伴上限的增量。</summary>
        public int ZombieCapPerUpgrade => m_zombieCapPerUpgrade;

        /// <summary>每次升级经验倍率的增量。</summary>
        public float ExpMultiplierPerUpgrade => m_expMultiplierPerUpgrade;

        // ==================== 玩家旋转 ====================

        [Header("玩家旋转")]
        [Tooltip("玩家朝向旋转速度（度/秒），值越大转向越快")]
        [SerializeField] private float m_playerRotationSpeed = 720f;

        /// <summary>玩家朝向旋转速度（度/秒）。</summary>
        public float PlayerRotationSpeed => m_playerRotationSpeed;

        // ==================== 单局时长 ====================

        [Header("单局时长")]
        [Tooltip("单局游戏总时长（秒），建议范围 180 ~ 300")]
        [SerializeField, Range(180f, 300f)] private float m_matchDuration = 180f;

        /// <summary>单局游戏总时长（秒）。</summary>
        public float MatchDuration => m_matchDuration;

        // ==================== 对象池参数 ====================

        [Header("对象池参数")]
        [Tooltip("Human 对象池初始预创建数量，应不小于 HumanMaxCount")]
        [SerializeField] private int m_humanPoolInitialSize = 60;

        [Tooltip("ZombieCompanion 对象池初始预创建数量，应不小于 ZombieCompanionMaxCount")]
        [SerializeField] private int m_zombiePoolInitialSize = 40;

        /// <summary>Human 对象池初始预创建数量。</summary>
        public int HumanPoolInitialSize => m_humanPoolInitialSize;

        /// <summary>ZombieCompanion 对象池初始预创建数量。</summary>
        public int ZombiePoolInitialSize => m_zombiePoolInitialSize;

        // ==================== 单局目标与评级 ====================

        [Header("单局目标与评级")]
        [Tooltip("通关所需的目标感染人数")]
        [SerializeField] private int m_targetInfectedCount = 80;

        [Tooltip("A 级评分所需感染人数阈值")]
        [SerializeField] private int m_aScoreInfectedCount = 150;

        [Tooltip("S 级评分所需感染人数阈值")]
        [SerializeField] private int m_sScoreInfectedCount = 250;

        [Tooltip("SS 级评分所需感染人数阈值")]
        [SerializeField] private int m_ssScoreInfectedCount = 350;

        /// <summary>通关所需的目标感染人数。</summary>
        public int TargetInfectedCount => m_targetInfectedCount;

        /// <summary>A 级评分所需感染人数阈值。</summary>
        public int AScoreInfectedCount => m_aScoreInfectedCount;

        /// <summary>S 级评分所需感染人数阈值。</summary>
        public int SScoreInfectedCount => m_sScoreInfectedCount;

        /// <summary>SS 级评分所需感染人数阈值。</summary>
        public int SSScoreInfectedCount => m_ssScoreInfectedCount;

        // ==================== 感染爆发 (Chain Infection Burst) ====================

        [Header("感染爆发")]
        [Tooltip("是否启用感染爆发机制")]
        [SerializeField] private bool m_enableInfectionBurst = true;

        [Tooltip("感染爆发的二次检测半径")]
        [SerializeField] private float m_infectionBurstRadius = 1.2f;

        [Tooltip("单次爆发最多额外感染的 Human 数量")]
        [SerializeField] private int m_infectionBurstMaxTargets = 3;

        /// <summary>是否启用感染爆发机制。</summary>
        public bool EnableInfectionBurst => m_enableInfectionBurst;

        /// <summary>感染爆发的二次检测半径。</summary>
        public float InfectionBurstRadius => m_infectionBurstRadius;

        /// <summary>单次爆发最多额外感染的 Human 数量。</summary>
        public int InfectionBurstMaxTargets => m_infectionBurstMaxTargets;

        // ==================== 新生僵尸冲刺 (Newborn Zombie Rush) ====================

        [Header("新生僵尸冲刺")]
        [Tooltip("是否启用新生僵尸冲刺")]
        [SerializeField] private bool m_enableNewbornRush = true;

        [Tooltip("新生僵尸冲刺持续时间（秒）")]
        [SerializeField] private float m_newbornRushDuration = 0.8f;

        [Tooltip("新生僵尸冲刺速度倍率")]
        [SerializeField] private float m_newbornRushSpeedMultiplier = 2.0f;

        /// <summary>是否启用新生僵尸冲刺。</summary>
        public bool EnableNewbornRush => m_enableNewbornRush;

        /// <summary>新生僵尸冲刺持续时间（秒）。</summary>
        public float NewbornRushDuration => m_newbornRushDuration;

        /// <summary>新生僵尸冲刺速度倍率。</summary>
        public float NewbornRushSpeedMultiplier => m_newbornRushSpeedMultiplier;

        // ==================== 末日狂潮 (Final Frenzy) ====================

        [Header("末日狂潮")]
        [Tooltip("是否启用末日狂潮")]
        [SerializeField] private bool m_enableFinalFrenzy = true;

        [Tooltip("末日狂潮触发时的剩余时间阈值（秒）")]
        [SerializeField] private float m_finalFrenzyStartRemainingTime = 30f;

        [Tooltip("末日狂潮期间的簇刷新间隔（秒）")]
        [SerializeField] private float m_finalFrenzyClusterSpawnInterval = 1f;

        [Tooltip("末日狂潮大簇最少人数")]
        [SerializeField] private int m_finalFrenzyLargeClusterMin = 15;

        [Tooltip("末日狂潮大簇最多人数")]
        [SerializeField] private int m_finalFrenzyLargeClusterMax = 20;

        /// <summary>是否启用末日狂潮。</summary>
        public bool EnableFinalFrenzy => m_enableFinalFrenzy;

        /// <summary>末日狂潮触发时的剩余时间阈值（秒）。</summary>
        public float FinalFrenzyStartRemainingTime => m_finalFrenzyStartRemainingTime;

        /// <summary>末日狂潮期间的簇刷新间隔（秒）。</summary>
        public float FinalFrenzyClusterSpawnInterval => m_finalFrenzyClusterSpawnInterval;

        /// <summary>末日狂潮大簇最少人数。</summary>
        public int FinalFrenzyLargeClusterMin => m_finalFrenzyLargeClusterMin;

        /// <summary>末日狂潮大簇最多人数。</summary>
        public int FinalFrenzyLargeClusterMax => m_finalFrenzyLargeClusterMax;

        [Tooltip("末日狂潮期间是否临时提高人类上限")]
        [SerializeField] private bool m_enableFinalFrenzyHumanCapOverride = true;

        [Tooltip("末日狂潮期间的人类数量上限")]
        [SerializeField] private int m_finalFrenzyHumanMaxCount = 100;

        /// <summary>末日狂潮期间是否临时提高人类上限。</summary>
        public bool EnableFinalFrenzyHumanCapOverride => m_enableFinalFrenzyHumanCapOverride;

        /// <summary>末日狂潮期间的人类数量上限。</summary>
        public int FinalFrenzyHumanMaxCount => m_finalFrenzyHumanMaxCount;
    }
}
