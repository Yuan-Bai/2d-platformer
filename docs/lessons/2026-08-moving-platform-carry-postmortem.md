# 复盘：PlayMode "五红" 与移动平台卡死 Bug（Agent 学习档案）

日期：2026-08 · 项目：2d-platformer（Unity 2022.3.62 + InputSystem 1.7.0）

## 一句话摘要

同一次排障里处理了两轮问题：**第一轮 5 个红测试全部是"测试代码自身"的问题**（断言写错 + 测试中断不清理引发的跨测试污染雪球）；**第二轮才是真正的运行时 bug**（移动平台用 `MovePosition` 位置传送做携带，吞掉了角色自身速度的物理积分，导致站在平台上无法移动/跳跃）。

---

## 第一轮：五个红测试的真相（无一是运行时功能 bug）

### 现象

`Bumper_LaunchesPlayerUpward` 断言失败（vy = -5.72 而非 > 0）；`OneWayPlatform_HoldingDown_DropsThrough` 前置断言失败（y = +0.44 而非 < -0.5）；另外 4 个测试死于 InputSystem 的未处理日志错误 `Cached unprocessed value unexpectedly became outdated`。

### 根因 1：Bumper 测试断言时机错误（测试 bug）

- 玩家出生点 (0, -0.8)，盒子 1×1，**底部 -1.3 与弹簧触发器（顶 -1.0）一开始就重叠** → 第 0~1 个物理帧就触发 `Bounce(14)`。
- 测试却在第 30 帧断言瞬时 `vy > 0`。弹起发生在第 1 帧，到第 30 帧已回落 0.58s：
  **vy = 14 − 34(FallGravity) × 0.58 = −5.72**，与实测 `-5.72000122` 完全一致。
- **这个数字恰恰证明弹起机制工作正常**，只是断言在错误的时间点取样。
- 修复：出生点移到触发器上方（先下落再触发）+ 等待期间记录**峰值 vy** 断言 `maxVy > 5`。

### 根因 2：OneWayPlatform 前置断言几何上写反（测试 bug）

- 平台顶 y = −0.75，玩家半高 0.5 → **站在平台上时玩家中心 y ≈ −0.25**。
- 旧断言要求 `y < −0.5` 作为"站在平台上"的判据——**几何上永远不可能成立**；即使自由落体 6 帧也只有 y = −0.445，同样 > −0.5。这个前置条件无论机制好坏恒红。
- 实测到的 +0.44（向上飞）是**跨测试污染**：Bumper 测试断言失败中止协程 → 未清理 → 被弹起、仍在空中飞行的玩家泄漏进后续测试场景，与新玩家发生物理碰撞/顶推（+0.44 与 −0.445 幅值镜像，是污染而非真实机制的重要线索）。
- 修复：前置改为 `y ∈ (−0.5, 0)` + `PlayerBody.Grounded == true` 三重校验，等待 10 帧。

### 根因 3：泄漏雪球引发 "Cached unprocessed value"（测试基础设施 bug，链条完整）

1. **断言失败或未处理日志错误会中止 `[UnityTest]` 协程** → 测试末尾的 `Cleanup()` 永远不执行 → 场景对象泄漏（玩家、机关、触发器）。
2. 泄漏的 `InputReader` 持有上一个测试的孤儿 `Keyboard`。`InputSystem.Restore()` 后设备的 `m_DeviceIndex` **不会复位**（`added` 仍为 true，原有的 `!_kb.added` 防御失效）。
3. 下一个测试 `AddDevice<Keyboard>()` 拿到**同一个缓冲区 index 0** → 泄漏的 reader 每帧轮询的其实是**新测试键盘的状态**，而它自己的控件缓存仍停留在旧状态——状态在缓存失效机制之外被改写。
4. InputSystem 1.7 的 `InputTestFixture` 默认开启 `PARANOID_READ_VALUE_CACHING_CHECKS` 自检（`InputTestFixture.cs:140-142`）→ 每帧 `LogError` → 当前测试因"未处理日志"失败 → 又中断、又泄漏 → 雪球。
5. **可对账证据**：错误重复次数 = 泄漏 reader 数量（Hazard 报 1 次、MovingPlatform 报 2 次、HoldingRight 报 4 次）；报错行号随"哪个键状态变了"而变（按 D 报 `InputReader.cs:29` 的 dKey 行，按空格报 `:31` 的 spaceKey 行）。
- 修复：三个测试类全部加 `[TearDown]` 兜底（`DestroyImmediate` 全部 `_spawned` + 守卫式 `RemoveDevice`）；`InputReader.Update` 防御升级为 `InputSystem.GetDeviceById(_kb.deviceId) != _kb` 时直接返回（`GetDeviceById` 对未知 id 返回 null、id 撞新设备返回新对象，两种都会拦住）。

### 为什么之前的"测试修复尝试"会失败

之前修测试时一直在**假设运行时坏了**（还加了 `[DEBUG-down]` 探针追下穿逻辑），但真正的问题是**测试断言本身永远无法成立**：

1. 旧 OneWayPlatform 只等 1 帧 → 接触未建立 → 测的是"空中下落穿过"而非"站上后下穿"；改成等 6 帧后**仍然红**，因为 `y < -0.5` 这个判据本身就写反了——等待帧数怎么调都没用。
2. 旧 Bumper 断言固定第 30 帧瞬时速度——弹起发生在前 1 帧、回落需要 0.58s，这个断言**在任何等待量下都大概率红**，看起来像"弹簧坏了"。
3. 红测试越多、泄漏越多，后续测试全是受害者，**掩盖了"第一个真 bug 是哪个"**。追"五红"时必须先找出第一个红，并把后面的红视为污染嫌疑，而不是逐一当独立 bug 修。

---

## 第二轮：移动平台卡死（真正的运行时 bug）

### 现象

玩家跳到移动平台上后被正常携带（随平台移动），但**相对平台完全静止**：不能左右移动、不能跳跃。

### 诊断链（每一轮数据都缩小假设空间）

1. 写复现测试 `MovingPlatform_PlayerCanWalkAndJumpOnIt`：红。**相对位移 0.0027（≈0）**——玩家绝对速度恒等于平台速度，自身运动完全无效。
2. 探针一轮：`rb.velocity.x` 采样 **[0.9, 6.0]（速度写入成功且在求解器之后仍然活着）**、`IsSleeping() = false`（醒着）、位置却不动。三个事实排除"接触吞速度"与"刚体入睡"。
3. A/B 对照：**静止 kinematic 平台绿、移动平台红** → 毒药锁定为"平台移动 + 玩家每帧 `MovePosition` 位置传送"的组合。
4. 结论：`MovePosition(rb.position + delta)` 的位置传送与 `rb.velocity` 直写的速度积分在同一个刚体上互斥——传送把速度积分产生的位移覆盖/无效化。玩家只通过传送移动（所以携带正常），自身速度永不积分（所以走不动、跳不起）。

### 修复（速度补偿）

`PlayerBody.FixedUpdate`：不再 `MovePosition`，而是把平台本帧位移折算成速度并入最终写入：

```csharp
Vector2 carryVelocity = _pendingPlatformDelta / Time.fixedDeltaTime; // 折算
...
_rb.velocity = cmd.Velocity + carryVelocity;   // 同一积分路径
if (_dead) _rb.velocity = carryVelocity;       // 死亡冻结期仍随平台走
```

携带与角色运动共用同一条速度积分路径，互不覆盖。回归测试转绿，TestRoom 实机验证通过。

---

## Agent 可复用教训（按 ID 检索）

### 测试类（TEST-*）

- **[TEST-1]** `[UnityTest]` 协程被断言异常/未处理日志错误中止时，测试方法末尾的清理代码**不会执行**。清理必须放在 `[TearDown]`（用 `DestroyImmediate` 同步销毁 + 守卫式清理单例/设备），测试内清理只能当常规路径。
- **[TEST-2]** 泄漏的 MonoBehaviour 会在后续测试里继续运行。其持有的 InputDevice 在 `InputSystem.Restore()` 后 `added` 不复位、缓冲区索引会别名到新设备 → 触发 InputSystem 1.7 测试夹具的 paranoid 缓存 `LogError`。**错误重复次数 = 泄漏 reader 数量**，报错行号随变化的键而变，可以用这两点对账验证污染理论。
- **[TEST-3]** 写物理断言前先算账：静止位置、某时刻的速度/位移都可以手算。手算值与实测值精确相等时，结论是"机制正常、断言错了"，而不是反过来。
- **[TEST-4]** 断言要与时间解耦：用峰值/累计量（maxVy、相对位移、区间最值）代替固定帧的瞬时状态；等待帧数只给余量，不承载判定语义。
- **[TEST-5]** 从未绿过的测试，其"后半段"等于从未被验证（OneWayPlatform 的下穿机制直到前置断言修好后才第一次真正被测）。
- **[TEST-6]** 多个红测试要追"第一个红"：后续的红可能只是第一个红的泄漏受害者。修完第一个红再重跑，往往后面的红自行消失。
- **[TEST-7]** 判据本身错误时（几何写反、方向写反、时机写死），调等待帧数、加探针都不会变绿——先验证判据的算术/几何正确性。

### Unity 物理类（UNITY-*）

- **[UNITY-1]** 同一个 `Rigidbody2D` 上"每帧 `MovePosition` 位置传送"与"`velocity` 直写"互斥：传送会吞掉速度积分的位移。**携带移动平台应做速度补偿**（平台位移 ÷ fixedDeltaTime 并入角色 velocity），不要做位置传送。
- **[UNITY-2]** 速度补偿的一帧延迟是恒定的（角色落后平台一个物理步），不会累积漂移；平台折返时只有一帧瞬态。
- **[UNITY-3]** 诊断"角色不动"时依次采样：`rb.velocity`（写入是否存活到求解后）、`IsSleeping()`（是否入睡）、`rb.position` 位移（物理位置是否真没动，绕过 Transform 同步）；再用"静止平台 vs 移动平台"A/B 对照锁定变量。
- **[UNITY-4]** kinematic 平台 + 全零摩擦的组合下，接触求解器不会吞掉切向速度——"速度活着但位置不动"应优先怀疑写入路径（传送/睡眠），而不是摩擦。

### 排障方法论（DIAG-*）

- **[DIAG-1]** 反馈回路优先于读代码：先写一个能复现症状的测试（红），再逐轮加判别性探针；每一轮数据必须能排除至少一个假设。
- **[DIAG-2]** 每个假设都要预测一个具体数值，并与实测对账（"若弹起发生在第 1 帧，第 30 帧 vy = −5.72"）。
- **[DIAG-3]** 用户描述里的参考系信息可以直接转成断言（"以平台为参考系静止" → 断言相对位移 ≈ 0）。
- **[DIAG-4]** 同一幅值、相反符号的异常（+0.4439 vs 自由落体 −0.4448）是"外部污染"而不是"机制错误"的典型指纹。
- **[DIAG-5]** 编辑器占用项目锁时无法起第二个 Unity 实例跑批处理测试，可让用户跑"单一测试"作为反馈回路（选中 → Run Selected），探针数据放进断言消息里一并回传。

---

## 修复对照表

| 位置 | 改动 |
| --- | --- |
| `Assets/Tests/PlayMode/M2MechanicsTests.cs` | Bumper 出生点上移 + 峰值 vy 断言；OneWayPlatform 前置三重校验；`[TearDown]` 兜底清理；新增 `MovingPlatform_PlayerCanWalkAndJumpOnIt` 与 `KinematicPlatform_Stationary_PlayerCanWalk` 两个回归测试 |
| `Assets/Tests/PlayMode/PlayerGroundMovementTests.cs`、`PlayerJumpTests.cs` | `_spawned` 跟踪 + `[TearDown]` 兜底清理 |
| `Assets/Scripts/Player/InputReader.cs` | 防御升级：`GetDeviceById(_kb.deviceId) != _kb` 时停止查询 |
| `Assets/Scripts/Player/PlayerBody.cs` | 移动平台携带改为速度补偿；删除 `[DEBUG-down]` 临时探针 |
| `Assets/Scripts/Mechanics/MovingPlatform.cs` | 仅注释更新（位移补偿 → 速度补偿），逻辑未动 |

## 遗留观察（非阻塞）

- `MovingPlatform` 仍用 `transform.position` 直写驱动 kinematic 刚体（依赖 `AutoSyncTransforms=0` 下的模拟步同步）；规范做法是 kinematic 体用 `rb.MovePosition`。当前可工作，属可选改进。
- 死亡重生（`DeathSequence`）依赖 `WaitForSecondsRealtime` 协程跑完才能复位 `_dead`；若协程被禁用/销毁打断会永久冻结——目前无触发路径，但值得留意。
