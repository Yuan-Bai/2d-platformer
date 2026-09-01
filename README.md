# 狐跃林间（Foxy's Forest）

2D 横板平台跳跃小作品（个人学习项目，纯平台跳跃、无战斗）。森林主题，主角 Foxy；正式命名于 M3：「狐跃林间 / Foxy's Forest」。

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
| Esc | 暂停 / 继续（暂停菜单：继续、音量、回到主菜单） |

## 关卡编辑（两条产线）

| 产线 | 做法 | 适用 |
|---|---|---|
| JSON 管线 | 编辑 `Assets/Levels/*.json`（`map` 字符串 + 机关元数据）→ 菜单 `Tools/Platformer/Build All Levels` 重新生成 | 结构性改关（地形/机关布局） |
| 手工管线 | Tile Palette 刷 Ground(Tilemap) + 拖 `Assets/Prefabs/` 预制体（`New Level Scaffold` 建脚手架） | 微调/实验关 |

字符对照：`#`=地形、`=`=单向平台、`M`=移动平台、`B`=弹簧、`^`=尖刺、`C`=重生点、`o`=樱桃、`D`=门、`S`=路牌、`P`=出生点。1 字符 = 1 米。

注意：**手工改场景对象会在下次 Build All Levels 时被覆盖**；共享对象（Player/CameraRig）改参数请改 `Assets/Prefabs/` 下对应预制体。

## 代码结构

```
Assets/Scripts/
├── Motor/        纯 C# 运动核心（重力/土狼/缓冲/跳切/终端速度/空中控制/冲量）
├── States/       分层状态机（Idle/Run/Jump/Fall），只做决策不碰物理
├── Player/       Unity 适配层（PlayerBody 唯一写 Rigidbody2D.velocity；InputReader 输入；
│                 PlayerVisuals + PlayerAnimSelector 帧动画）
├── Mechanics/    机关（单向平台/弹簧/移动平台/尖刺/重生点/樱桃/门/路牌）
├── Camera/       PlayerCameraRig（Cinemachine 相机封装）
├── Levels/       关卡数据（LevelData 解析 + LevelValidator 校验；SpawnPoint/LevelConfig）
├── UI/           HUD（樱桃计数/提示栏）+ 面板（主菜单/暂停/通关）
├── GameFlowController.cs   常驻流程控制器（Menu/Playing/LevelClear/GameClear，Additive 切关）
├── AudioManager.cs         常驻音频（音乐随流程切曲 + 6 语义音效）
└── Editor/       LevelKit（组件装配单一事实源）、LevelBuilder（JSON→场景）、
                  BootstrapSceneBuilder（00-Bootstrap 场景/主菜单/暂停面板）、
                  TestRoomBuilder、SunnyLandArtTools（素材批处理）
Assets/Tests/     EditMode 43 + PlayMode 21（Test Runner 运行）
```

## 运行与打包

- 编辑器：菜单 `Tools/Platformer/Play From Bootstrap`（或直接打开 `Assets/Scenes/00-Bootstrap.unity` 点 Play）
- 打包：菜单 File > Build Settings 或 `Tools` 流程；Windows x64 release 输出到 `Builds/Windows64/`（已 gitignore）
- 窗口：1280×720 窗口化，Alt+Enter 切全屏

## 素材与许可

- **美术**：Ansimuz（Luis Zuno）Sunny Land —— **CC0**（`Assets/Art/SunnyLand/public-license.pdf`），个人/商用/修改/再分发无限制
  - https://ansimuz.itch.io/sunny-land-pixel-game-art
- **音乐**：Sunny Land Music 两包 —— 个人/商用均可、可修改、可再分发，署名不强制但欢迎（`Assets/Audio/Music/` 内 public-license.txt）
- **音效**：Kenney（Retro Sounds / Impact Sounds / Interface Sounds）—— **CC0**（`Assets/Audio/SFX/` 内 License-Kenney.txt、License-RetroSounds.txt）
- Credit：Artwork & Music by Luis Zuno (@ansimuz)、SFX by Kenney

## 里程碑

- [x] M0 初始化（2022 LTS 2D URP、装包、素材导入、CC0 许可核实）
- [x] M1 手感核心（运动深模块 + 状态机 + 测试 + 物理根因修复）
- [x] M2 关卡机制（单向平台/弹簧/移动平台/尖刺+重生）
- [x] M3 关卡内容（教学关 + 3 正式关 + 通关画面；Player/CameraRig 预制体化；碰撞几何生成修复）
- [x] M4 收尾（跨场景常驻重构、主菜单/暂停/音效/打包）
- [ ] M5 动作扩展（冲刺/滑墙/蹬墙跳/二段跳——2D 沙盒验证，为 3D 项目打基础）
