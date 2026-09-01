# M4 设计：收尾——跨场景常驻重构 + 主菜单 + 音频 + 打包

> 前置 ADR：0009（常驻架构）。本文档是 M4 的实施蓝图，验收标准与拆解顺序。

## 1. 锚定决策（grilling 两轮，用户全部接受推荐）

| # | 决策 |
|---|---|
| Q1 | 跨场景常驻架构 = **方案 A**（Bootstrap 常驻 + 关卡 Additive） |
| Q2 | 主菜单 = 开始游戏 + 退出 + 音乐音量滑条；无关卡选择、无读档 |
| Q3 | 音效素材 = 下载 Kenney CC0（Interface Sounds + Impact Sounds），走代理、核实许可 |
| Q4 | 音乐选曲：菜单 `exploration`、关卡循环 `happywalking`、通关 `Going Up`（可试听换） |
| Q5 | 打包图标 = Foxy idle 第一帧放大（现成素材，像素风） |
| Q6 | 验收：主菜单可用 / 全程通关玩家不重建音乐不中断 / 死亡重生语义不变 / 测试全绿 / Windows x64 出包试玩 / 文档更新 |
| Q7 | 主菜单与 Bootstrap **合并**为一个场景 `00-Bootstrap` |
| Q8 | 05-GameClear 场景退役，通关画面 = 常驻 Canvas 面板（樱桃累计 + 回主菜单按钮） |
| Q9 | 编辑器工具 Play From Bootstrap + 直开关卡场景警告 |
| Q10 | 打包 1280×720 窗口模式、Alt+Enter 全屏、Windows x64 release |

## 2. 场景与组件清单变化

**新增**
- `Assets/Scenes/00-Bootstrap.unity`（Build index 0）：GameBootstrap（帧率）+ AudioManager + 常驻 Canvas（MenuPanel / HUD / HintBar / GameClearPanel）+ Player（Prefab）+ CameraRig（Prefab）+ GameFlowController。

**退役**
- `Assets/Scenes/Levels/05-GameClear.unity` 及 `GameClearScreen.cs`（移入常驻 Canvas 的 GameClearPanel）。

**关卡场景（01~04，Build All Levels 生成，内容裁剪）**
- 不再生成：Player、CameraRig、HUD、LevelManager。
- 改为生成：`SpawnPoint`（P 字符处，空对象标记）、`LevelConfig`（totalCherries 等关卡数据）。
- 保留：地形 tilemap + Composite、五机关、樱桃、路牌、门、CameraBounds、视差背景。

**代码增删**
- 删：`Levels/LevelManager.cs`、`Levels/GameClearScreen.cs`。
- 改：`Collectible`/`LevelExit` 接缝 → `GameFlowController.Instance`；`PlayerBody` 新增公开重置入口 `RespawnAt(Vector2 spawnPoint)`（内部复用 Respawn 逻辑 + 置 RespawnPosition）；`LevelBuilder` 裁剪关卡场景生成内容；`PlayerCameraRig` 补 Confiner 边界重绑接口（复用现有 Configure 内部逻辑）。
- 新：`GameFlowController.cs`（流程编排深模块）、`SpawnPoint.cs`、`LevelConfig.cs`、`UI/GameClearPanel.cs`、`UI/MainMenuPanel.cs`（含音量滑条）、`Audio/AudioManager.cs`、Editor 的 Play From Bootstrap 入口。

## 3. GameFlowController 深模块（窄接口、大行为）

```csharp
public sealed class GameFlowController : MonoBehaviour
{
    public static GameFlowController Instance { get; }
    public FlowState State { get; }          // Menu / Playing / LevelClear / GameClear
    public int CollectedInLevel { get; }     // 本关樱桃（HUD 同步用）
    public int TotalCollected { get; }       // 跨关累计（原 LevelManager.TotalCollected static → 常驻实例字段）

    // —— 玩家/菜单入口 ——
    public void StartGame();      // 累计清零 + Additive 加载第一关 → Playing
    public void ReturnToMenu();   // 卸载当前关卡场景 → Menu
    public void QuitGame();       // Application.Quit

    // —— 关卡内事件（机关层调用，取代原 LevelManager 接口）——
    public void RegisterCherry(); // 樱桃计数 + 累计 + HUD 同步
    public void CompleteLevel();  // 终点门：切关编排（见 §4）
}
```

**内部行为（调用方不可见）**
- 关卡有序列表（01→02→03→04）：Build Settings 顺序即列表；"下一关" = 列表下一项；列表末项 → GameClear。
- 切关编排全流程（§4）；关卡加载后从场景取 `SpawnPoint` 重置玩家、取 `LevelConfig` 取樱桃总数、重绑相机边界。
- 编辑器回退：场景未进 Build Settings 时（直开单场景）给可诊断警告。

## 4. 切关时序（CompleteLevel 后）

1. 冻结输入（InputReader.enabled=false），状态 → LevelClear；
2. HintBar 提示「本关樱桃 x/y」，时间戳计时（不协程，ADR-0005 纪律）；
3. 到期 → `SceneManager.UnloadScene(旧关)` + `SceneManager.LoadScene(新关, Additive)`（关卡体量小，同步加载，音乐不中断）；
4. 玩家 `RespawnAt(新关 SpawnPoint)`：位置/速度/重生点/输入缓冲全清；
5. 相机 Confiner 重绑新关 CameraBounds；
6. 解冻输入，状态 → Playing；
7. 若已无下一关 → GameClearPanel（樱桃累计 + 回主菜单按钮），状态 → GameClear。

**死亡重生不变**：Hazard→PlayerBody.Die 自治，重生点 = RespawnPosition（出生点/最近 Checkpoint）；Checkpoint 随关卡场景卸载销毁，天然不复用旧关记录。

## 5. 拆解与顺序

| 阶段 | 内容 | 验收 |
|---|---|---|
| **M4a 常驻架构** | GameFlowController + 00-Bootstrap 场景 + LevelManager 删除 + 机关接缝改造 + LevelBuilder 裁剪 + SpawnPoint/LevelConfig + 切关流程 + Play From Bootstrap 工具 + 测试适配 | 从 00-Bootstrap 全程通关（玩家不重建）；EditMode/PlayMode 全绿 |
| **M4b 主菜单** | MenuPanel（开始/退出/音量滑条）+ 标题视觉；ReturnToMenu 闭环 | 菜单↔游戏往返稳定 |
| **M4c 音频** | AudioManager（音乐循环/音量、场景切换不中断）+ 3 首选曲 + Kenney SFX 下载接入（跳跃/落地/樱桃/死亡/UI 点击） | 音乐连续；SFX 触发正确 |
| **M4d 打包** | icon（Foxy idle 帧放大）+ 1280×720 窗口 + Windows x64 release + 试玩验收 | 出包试玩通过；README/ADR/CONTEXT 更新 |

## 6. 测试影响

- **EditMode**：不受场景结构影响；GameFlowController 的纯逻辑（计数/状态迁移）如可抽纯 C# 则补 EditMode 测试。
- **PlayMode**：现有引用 `LevelManager` 的测试改为装配 `GameFlowController` 的适配；新增：切关（加载→CompleteLevel→卸载/加载断言）、ReturnToMenu 回菜单、最后一关触发 GameClearPanel。
- 测试与 Bootstrap 无关：测试自行在临时场景装配对象（现有模式），不依赖 Build Settings。

## 7. 遗留确认点

- ⚠️ **暂停菜单**：未锚定。默认建议做：Esc 暂停（Time.timeScale=0 + 暂停面板：继续/音量/回主菜单）。**用户确认是否进 M4 及范围。**
- Kenney SFX 具体曲目（哪几首配哪个事件）：下载后按试听定，M4c 时给清单确认。
