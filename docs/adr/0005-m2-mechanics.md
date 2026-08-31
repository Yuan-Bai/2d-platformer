# ADR-0005：M2 五个机关的关键物理方案

M2 落地五个机关，每个都踩过并否决了一个「更显然」的备选：

- **单向平台**：`PlatformEffector2D` + OneWayPlatform 层实现只挡上方；主动下穿用 per-collider `Physics2D.IgnoreCollision` 而非层矩阵（实测 IgnoreLayerCollision 对既有接触不生效，角色被托住）。玩家设独立 Player 层支撑下穿切换。
- **弹簧**：显式冲量入口 `PlayerMotor.Bounce`——不经过跳跃判定、不消耗跳跃缓冲、不受跳切管辖、不依赖地面。
- **移动平台**：kinematic 刚体 + `rb.MovePosition` 驱动；携带用**速度补偿**（平台本帧位移 ÷ fixedDeltaTime 并入角色速度）而非位置传送（实测传送吞掉角色自身速度的物理积分 → 站上平台无法移动/跳跃）。
- **尖刺**：一击死亡、无血量；死亡流程用**时间戳计时**而非协程（协程被禁用/打断会永久冻结 `_dead`）。
- **重生点**：Trigger 接触即更新 `PlayerBody.RespawnPosition`，默认出生点自动记录。

**Consequences**

- 详见 `docs/lessons/2026-08-moving-platform-carry-postmortem.md`（M2 排障复盘，含 UNITY-1~4 物理教训）。
- M3 关卡生成器直接复用这五个机关组件，不新增机关类型。

> 补录于 M3 阶段 0：本决策在 M2 已生效于代码（五个组件与 `M2MechanicsTests` 注释引用 ADR-0005），此前未落盘。
