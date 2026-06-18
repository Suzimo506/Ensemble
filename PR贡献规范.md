# MDEN 客户端 Pull Request 贡献规范

本文档面向准备向 MDEN 客户端提交 Pull Request 的贡献者。这里已经归纳了本地开发规范中的关键要求，贡献者只需要按本文档自检，不需要额外阅读仓库未公开的内部规范文档。

---

## 一、项目边界

MDEN（Ensemble）是 Muse Dash 的 MelonLoader 联机模组。客户端主要由以下层组成：

| 目录/模块 | 职责 | 禁止事项 |
|---|---|---|
| `Network/` | TCP 连接、封包拆包、JSON 序列化、Request/Response 配对、心跳、断线通知和重连调度。 | 禁止写 UI、房间、玩家、聊天、对战等业务逻辑。 |
| `Managers/` | 业务状态缓存、请求组装、调用网络层、订阅 Push、生成 UI 可读状态。 | 禁止直接操作按钮、窗口、游戏对象、TCP 流或裸 JSON。 |
| `UI/` | 创建界面、展示状态、绑定用户输入、调用 Manager。 | 禁止直接调用 `NetworkClient`，禁止自己跨层计算业务规则。 |
| `Patches/` | 挂载入口按钮、接入原版游戏流程或场景事件。 | 禁止承载大厅、聊天、对战等完整业务流程。 |
| `MDEN.Protocol/` | 双端共享的 OpCode、Envelope、Request、Response、Notify、Push、枚举和 DTO。 | 禁止复制一份客户端私有协议模型。 |

PR 必须保持这些边界。能通过扩展现有 Manager、PushDispatcher、UI 生命周期或协议模型解决的问题，不要新建平行体系。

---

## 二、网络与协议规则

MDEN 使用 TCP 长连接，所有数据通过 `4 字节 Little-Endian 长度头 + UTF-8 JSON Payload` 传输。新增网络能力时必须遵守：

- Request 必须带 `ReqId`，Response 必须带回相同 `ReqId`。
- Request 与 Response 使用相邻 OpCode，例如 `0x0300` 与 `0x0301`。
- Notify 和 Push 不携带 `ReqId`；需要结果的操作必须设计为 Request/Response。
- `Success=false` 时必须提供玩家或调用方能理解的失败原因。
- 所有消息体、枚举和共享 DTO 必须放在 `MDEN.Protocol/`。
- JSON 序列化必须使用项目统一协议配置，不手写 JSON 字符串。
- OpCode 必须使用 `OpCodes` 常量，不写魔法数字。
- 客户端和服务端必须引用同一个协议定义；改协议时必须说明双端是否已同步。

网络层只负责收发和派发。收到 Push 后应交给 `PushDispatcher` 和对应 Manager，不能在 `NetworkClient` 的接收循环里直接刷新 UI 或改房间业务状态。

---

## 三、Manager 业务规则

Manager 是 UI 与 Network 之间的业务中转。新增或修改业务时应遵守：

- UI 点击事件调用 Manager 方法，例如 `JoinLobbyAsync`、`SetReadyAsync`、`SendChatAsync`。
- Manager 负责判断是否已登录、是否在房间、当前玩家是否房主、按钮是否可点击。
- Manager 负责组装 Request、调用 `NetworkClient`、处理 Response、订阅 Push。
- Manager 可以维护状态缓存和 ViewModel，但不直接操作具体窗口、按钮、文本、贴图。
- Push 回调不保证在 Unity 主线程；通知 UI 刷新前必须切回主线程。
- 异步结果必须检查房间 ID、窗口版本或状态版本，过期结果必须丢弃。

如果一个规则会被多个 UI 使用，放到 Manager；如果只是展示样式，留在 UI。

---

## 四、UI 生命周期与防错

涉及 `UI/` 的 PR 必须确认：

- 主窗口、错误弹窗、确认框、遮罩和关闭流程使用现有 UI 调度方式。
- 同一时间活跃主窗口的切换必须可控，不能让旧窗口回调影响新窗口。
- 所有按钮监听、Manager 事件和临时订阅都要能在窗口关闭时解绑。
- 会发网络请求的按钮必须在发请求前加锁，并在 `finally` 中释放。
- 全局锁和局部锁都不能替代版本检查；旧 Response 或旧 Push 不能覆盖新窗口状态。
- 高频刷新场景尽量局部刷新，避免丢输入框内容、滚动位置或选中状态。
- 资源加载失败必须有可见 fallback，不能让窗口创建流程直接崩溃。

常见数据流应保持为：

```text
玩家点击 UI
-> UI 加锁并调用 Manager
-> Manager 组装协议模型并调用 NetworkClient
-> NetworkClient 收到 Response 或 PushDispatcher 派发 Push
-> Manager 更新状态或 ViewModel
-> UI 在 Unity 主线程刷新
-> finally 释放 UI 锁
```

---

## 五、富文本与玩家输入

MDEN 同时使用 PopupLib 窗口文本和游戏原生 `UnityEngine.UI.Text`，颜色格式不能混用：

- `UI/Windows/` 下 PopupLib 窗口文本使用 `<color=fff700ff>文本</color>`。
- `UI/Core/`、`UI/Displays/` 等原生 UI 文本使用 `<color=#fff700ff>文本</color>`。
- 玩家输入、玩家昵称、聊天内容、服务端下发文本进入富文本前必须转义或校验。
- 玩家自定义颜色必须先归一化为 6 位或 8 位十六进制字符串，再按 UI 类型决定是否加 `#`。

禁止跨 UI 体系批量替换富文本颜色格式。

---

## 六、代码风格

- 不新增 XML 文档注释，尤其不要新增 `<summary>`。
- 注释只解释“为什么”，不要复述代码“做了什么”。
- 使用强类型模型，不用散乱字符串、魔法数组或魔法数字表达业务状态。
- 不复制粘贴已有逻辑；公共逻辑下沉到 Manager、协议模型或局部 helper。
- 不提交 `bin/`、`obj/`、本地配置、发布产物、临时调试文件。
- 不把无关格式化、批量重命名、换行调整混进业务 PR。
- 不保留过期 TODO。必须写 TODO 时，要写清触发条件或后续动作。

---

## 七、PR 描述要求

PR 描述必须写清楚：

- 本次完成了什么。
- 本次没有做什么。
- 修改涉及哪些层：UI、Manager、Network、Protocol、Patch。
- 是否影响服务端或 Web 服务。
- 是否新增配置、资源、协议字段或玩家可见行为。
- 构建结果、成功路径、失败路径和回归影响。

推荐格式：

```text
本次完成：
- ...

边界说明：
- UI：...
- Manager：...
- Network：...
- Protocol：...

验证：
- 构建：通过 / 未运行，原因是 ...
- 成功路径：...
- 失败路径：...
- 回归影响：...
```

---

## 八、提交前自检

提交 PR 前至少检查：

- 项目能否构建；如果不能构建，PR 描述中说明原因。
- UI 是否没有直接调用 `NetworkClient`。
- Network 层是否没有业务逻辑和 UI 逻辑。
- Manager 是否是 UI 与 Network 的业务中转。
- Push 注册是否清晰，是否避免重复注册或无法注销。
- 异步请求是否有异常处理、超时语义和 UI 解锁。
- 新协议是否在客户端和服务端同步。
- 新文案是否符合富文本颜色格式。
- 新增文件是否确实应该提交。

---

## 九、不建议合并的 PR

以下类型的 PR 通常会被要求拆分或重做：

- 把 UI、网络和业务逻辑写在同一个类里。
- 为临时功能新增一套平行网络或 UI 框架。
- 只改客户端协议，不同步服务端。
- 大量无关格式化掩盖真实业务改动。
- 无法说明成功路径、失败路径和回归影响。
- 引入无法解绑的事件监听或异步竞态。

保持改动小而清楚，会让 review 更轻，也更容易被合并。
