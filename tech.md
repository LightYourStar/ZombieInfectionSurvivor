# Technology Stack

Unity Version:
- Unity 2022.3 LTS

Language:
- C#

Rendering:
- 2D
- SpriteRenderer
- UGUI
- 第一版不依赖复杂 URP 特性

Input:
- 使用 UGUI 虚拟摇杆
- 暂不引入复杂输入系统

Data:
- 使用 ScriptableObject 管理配置
- 第一版不接 Excel 导表
- 第一版不使用 Addressables

Architecture:
- 简单模块化架构
- GameSystemRunner 作为游戏入口
- 各系统职责单一
- 不使用 ECS
- 不使用复杂依赖注入框架

Performance:
- 敌人、僵尸、特效使用对象池
- 避免频繁 Instantiate/Destroy
- 第一版目标是 100~200 个单位内流畅运行

Save:
- 第一版使用 PlayerPrefs 或简单 JSON

Ads:
- 第一版只预留广告接口
- 不接真实广告 SDK

Code Style:
- 私有字段使用 m_ 前缀
- Inspector 字段使用 [SerializeField] private
- 关键逻辑添加中文注释
- 不要过度工程化