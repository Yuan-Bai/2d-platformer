# ADR-0009：M4 跨场景常驻架构——Bootstrap 常驻场景 + 关卡 Additive 加载

M4 决定以「常驻场景 + 关卡场景 Additive 加载」取代 ADR-0007 的「整场景 LoadScene 场景链」：新建 `00-Bootstrap` 常驻场景承载 Player/CameraRig/常驻 Canvas（HUD/提示/菜单/通关面板）/AudioManager/GameFlowController，启动即主菜单；关卡场景（01~04）只装关卡内容（地形/机关/相机边界/出生点/关卡数据），由 GameFlowController 动态加载卸载。切关全程玩家对象与音频不销毁不重建，樱桃累计、输入状态、音乐天然连续。

**Considered Options**

- A. Bootstrap 常驻 + 关卡 Additive（采纳）：状态连续、切关不重建玩家、音频不中断；关卡场景保持"只装关卡内容"的干净接缝，与 JSON 生成管线兼容（关卡场景仍由 Build All Levels 生成，只是不再含玩家/相机/HUD）。代价：LevelManager 删除并入 GameFlowController；编辑器直开关卡场景无玩家，需 Play From Bootstrap 工具 + 警告。
- B. DontDestroyOnLoad 跨场景：改动最小，但场景内序列化引用（LevelManager→Player 等）切场景后断链需运行时重绑、编辑器残留对象、调试脏，社区公认反模式——否决。
- C. 单场景 + UI 切换：推翻 JSON→场景管线，改动面最大——否决。
- D. 维持 ADR-0007、仅把场景 0 换成主菜单（ADR-0007 原预期）：每关仍重建 Player/相机/HUD，音乐必然中断或需额外 hack；「全程玩家不重建、音乐不中断」的 M4 验收标准不成立——否决。

**Consequences**

- 场景清单变化：`00-Bootstrap`（新，Build index 0）承载常驻层与主菜单；`05-GameClear` 退役删除（通关画面改为常驻 Canvas 面板）；Build Settings 变为 `[00-Bootstrap, 01, 02, 03, 04]`。
- `LevelManager` 类删除：职责（樱桃计数、过关流转、跨关累计 static）并入常驻的 `GameFlowController`；`Collectible`/`LevelExit` 的接缝从 `FindObjectOfType<LevelManager>()` 改为 `GameFlowController.Instance`（项目既有 Instance 惯例）；跨关樱桃累计从 static 字段降级为常驻实例字段（不再需要 static）。
- 关卡场景新增两个轻量组件：`SpawnPoint`（出生点标记，玩家切关重置锚点）、`LevelConfig`（关卡数据：樱桃总数；兼作"编辑器直开场景"警告的挂点）。
- 死亡重生语义不变：PlayerBody 自治（时间戳冻结→传送 RespawnPosition）；重生点记录随关卡场景卸载自然失效，切关时由 GameFlowController 重置为新关出生点。
- 切关语义：终点门 → 冻结输入 → 提示 → 延时 → 卸载旧关场景 + Additive 加载新关场景 → 玩家重置（位置/速度/重生点）→ 相机 Confiner 重绑新关 CameraBounds → 解冻输入；最后一关改为触发常驻通关面板。
- 编辑器工作流：新增 Tools/Platformer/Play From Bootstrap 一键进 Play；直开关卡场景时 LevelConfig 在编辑器下警告。
- 主菜单为 `00-Bootstrap` 的 UI 面板（开始/退出/音乐音量），无独立菜单场景。
