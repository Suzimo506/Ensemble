# MDEN / Ensemble

MDEN（Muse Dash Ensemble）是一个基于 MelonLoader 的 Muse Dash 联机模组。它为游戏加入房间大厅、多人准备、曲目列表、房间聊天、实时对战数据同步、结算展示、玩家资料与服务器选择等联机体验，让玩家可以在同一个房间里一起选歌、准备、游玩并查看对战结果。

模组 DLL 在 MelonLoader 控制台与 Mod 列表中显示为 `Ensemble`，作者显示为 `MDENTeam`。

---

## 安装教程

请将 `Ensemble.dll` 放入 `Mods`，安装前置模组 `PopupLib.dll`（作者：`PBalint817`），并将 `MDEN.Protocol.dll` 放入 `UserLibs`。

---

## 模组使用教程

详情请见 [mden.top](https://mden.top/)。

---

## 功能概览

- 多人房间：创建、加入、离开、锁房、踢人、准备与开始。
- 曲目列表：房主或成员按房间规则添加、移除和继续播放曲目。
- 对战同步：游戏内展示其他玩家分数、准确率、连击与存活状态。
- 聊天与系统消息：房间内聊天、系统提示和玩家操作广播。
- 玩家资料：昵称、签名、头像、聊天颜色与当前 Muse Dash 选择状态。
- 结算展示：房间游玩统计、曲目列表与多维度结算结果。
- 自定义服务器：支持官方服务器列表与玩家自定义服务器配置。

---

## 项目结构

| 路径 | 说明 |
|---|---|
| `Main.cs` | MelonLoader 模组入口，负责初始化管理器、UI 控制器与场景钩子。 |
| `Constants.cs` | 模组常量、颜色、资源路径与关于页面鸣谢文本。 |
| `Network/` | TCP 连接、封包拆包、请求响应配对、Push 派发与重连。 |
| `Managers/` | 房间、玩家、聊天、对战、配置等业务状态与网络调用。 |
| `UI/` | PopupLib 窗口、游戏内 HUD、房间显示、结算显示与 UI 生命周期控制。 |
| `Patches/` | Harmony Patch，负责把入口和游戏流程接入原版 Muse Dash。 |
| `MDEN.Protocol/` | 客户端和服务端共享的 OpCode、Envelope、DTO 与协议模型。 |
| `Assets/` | 模组内嵌或运行时加载的图片资源。 |

---

## 构建要求

- .NET 6 SDK
- Muse Dash + MelonLoader net6 环境
- `PopupLib.dll`、`LocalizeLib.dll`、`CustomAlbums.dll` 等项目引用中声明的运行时依赖
- 环境变量或 MSBuild 属性 `MD_NET6_DIRECTORY` 指向 Muse Dash 的 MelonLoader net6 根目录

常用构建命令：

```powershell
dotnet build .\MDEN.csproj
```

构建完成后，项目会尝试把 `Ensemble.dll` 复制到 `$(MD_NET6_DIRECTORY)\Mods`，并把 `MDEN.Protocol.dll` 复制到 `$(MD_NET6_DIRECTORY)\UserLibs`。

---

## 贡献

欢迎通过 Pull Request 改进 MDEN。提交前请先阅读 `PR贡献规范.md`，并确认改动符合客户端分层、协议同步、富文本颜色格式、异步 UI 锁和验收要求。

---

## 许可

本项目使用 GNU General Public License v3.0。详见 `LICENSE`。

---

## 赞助官方服务器

如果你愿意支持 MDEN 官方服务器，可以通过 [爱发电](https://afdian.com/a/szm520) 赞助。

如果你需要私人开服工具，请进入喵斯兔交流群联系群主或者管理员。
