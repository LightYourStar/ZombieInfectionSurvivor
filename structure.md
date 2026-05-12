# Project Structure

Assets/Game/Scripts/Core
游戏入口、状态机、全局管理类。

Assets/Game/Scripts/Config
ScriptableObject 配置类。

Assets/Game/Scripts/Gameplay/Player
玩家移动、玩家属性、玩家输入。

Assets/Game/Scripts/Gameplay/Enemy
人类敌人、士兵、精英怪等。

Assets/Game/Scripts/Gameplay/Zombie
僵尸同伴逻辑。

Assets/Game/Scripts/Gameplay/Infection
感染判定、感染转换逻辑。

Assets/Game/Scripts/Gameplay/Wave
刷怪、波次、Boss 刷新。

Assets/Game/Scripts/Gameplay/Skill
局内升级、技能效果。

Assets/Game/Scripts/UI
HUD、升级选择、结算界面。

Assets/Game/Scripts/Utility
对象池、数学工具、通用工具。

Rules:
1. 不允许把所有逻辑写在一个 MonoBehaviour 中。
2. 不允许在业务逻辑中大量使用 GameObject.Find。
3. 配置数据不能硬编码在逻辑代码里。
4. 第一版优先实现可运行 Demo。
5. 代码应便于后续接入 AI 生成资源。