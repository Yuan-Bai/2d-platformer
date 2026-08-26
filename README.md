# 2d-platformer

2D 横板平台跳跃小作品（个人学习项目，纯平台跳跃、无战斗）。正式游戏名待关卡主题确定后另起。

## 技术栈

- Unity **2022.3.62f3c1**（URP 2D）
- Cinemachine 2.9.7、Input System 1.7.0（新输入系统）
- 分层 C# 状态机 + 纯 C# 运动核心（`PlayerMotor` 深模块，可 EditMode 单测）

## 控制

| 输入 | 行为 |
|---|---|
| A / D 或 ← / → | 移动 |
| 空格 | 跳跃（按住跳更高；土狼时间 + 跳跃缓冲） |
| S / ↓ | 从单向平台下落穿过 |

## 代码结构

```
Assets/Scripts/
├── Motor/        纯 C# 运动核心（重力/土狼/缓冲/可变跳高/跳切/终端速度/空中控制/冲量）
├── States/       分层状态机（Idle/Run/Jump/Fall），只做决策不碰物理
├── Player/       Unity 适配层（PlayerBody 唯一写 Rigidbody2D.velocity；InputReader 输入）
├── Mechanics/    机关（单向平台/弹簧/移动平台/尖刺/重生点）
├── Camera/       Cinemachine 相机封装
└── Editor/       Tools/Platformer/Build Test Room 一键生成测试房
Assets/Tests/     EditMode 19 + PlayMode 6（Test Runner 运行）
```

## 素材与许可

- **美术**：Ansimuz（Luis Zuno）Sunny Land —— **CC0**（`Assets/Art/SunnyLand/public-license.pdf`），个人/商用/修改/再分发无限制
  - https://ansimuz.itch.io/sunny-land-pixel-game-art
- **音乐**：Sunny Land Music 两包 —— 个人/商用均可、可修改、可再分发，署名不强制但欢迎（`Assets/Audio/Music/` 内 public-license.txt）
- Credit：Artwork & Music by Luis Zuno (@ansimuz)

## 里程碑

- [x] M0 初始化（2022 LTS 2D URP、装包、素材导入、CC0 许可核实）
- [x] M1 手感核心（运动深模块 + 状态机 + 19 测试 + 两个物理根因修复）
- [x] M2 关卡机制（单向平台/弹簧/移动平台/尖刺+重生）
- [ ] M3 关卡内容（教学关 + 3~4 正式关）
- [ ] M4 收尾（主菜单/音效/打包）
