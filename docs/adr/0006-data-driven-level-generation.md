# ADR-0006：关卡用单文件 JSON 定义、Editor 生成器一键转场景（M3）

M3 决定：关卡源 = 单文件 JSON（`Assets/Levels/*.json`：结构化元数据 + `map` 字符串行数组 = ASCII 地形，1 字符 = 1m），由 Editor 工具（`TestRoomBuilder` 模式的升级版 `LevelBuilder`）一键生成 `.unity` 场景。生成后的场景归用户自由编辑；若要重新生成，把场景改动迁回 JSON 数据。

**Considered Options**

- 手摆场景（用户或我在编辑器里逐块摆放）：视觉反馈即时，但 `.unity` YAML 无法 diff/评审/测试，我作为 agent 无法目视摆放，合并冲突风险高——否决为唯一来源。
- 文本头迷你语法 + ASCII 地图（txt 混合格式，本 ADR 初稿）：地形可读性同等，但元数据（路径点/速度/文案）是自造小语言，需手写解析器、每加字段都要改——被 JSON 取代。
- 单文件 JSON + `map` 行数组：元数据用 Unity 内置 `JsonUtility` 反序列化（零外部依赖、向后兼容扩展），地形保留 ASCII 画的肉眼可读性；关卡可 diff、可代码评审、可写完整性校验测试——采纳。
- 预制体乐高块 + 手摆：作为生成后的「微调手段」保留（混合分工的一部分），但不作为唯一来源。

**Consequences**

- 关卡度量基准锚定：16px 图块按 PPU 16 切片 → 1 图块 = 1 世界单位，JSON 的 `map` 行 1 字符 = 1m。
- JSON 不支持注释：字段语义以 `docs/design/m3-levels.md` 第 3 节为准，结构校验测试兜住解析错误。
- 关卡数据的 schema 变更（加新机关符号/新字段）会要求生成器同步升级——两者版本号绑定，见 `docs/design/m3-levels.md` 第 3 节。
- 手工搭关路径（M3 阶段 3.5 决议）：`LevelKit` 提供预制体（Build Prefabs）与空场景脚手架（New Level Scaffold），与 JSON 管线共用同一批工厂装配——手工场景不经 LevelValidator，质量靠试玩。
