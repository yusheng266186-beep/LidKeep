# 合盖保活 LidKeep

[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6)](#)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.0%2B-512BD4)](#)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

需要跑长时间任务（编译、下载、训练、远程会话）时，Windows 一合盖就睡眠、任务直接断掉。
**LidKeep 临时把「合盖时采取的操作」改成「不采取任何操作」，退出或到时后自动把原设置还原。**

单文件绿色程序，不写注册表、不装服务、不申请管理员权限。

---

## 特性

- **一键保活**：把合盖动作改成「不采取任何操作」，并调用 `SetThreadExecutionState` 申请系统保持唤醒。
- **保持屏幕常亮（可选，默认开）**：加上 `ES_DISPLAY_REQUIRED` 让显示器也不关闭，从而避免合盖后被锁屏。取消勾选则只保系统不睡眠，屏幕仍会熄灭并锁屏。
- **自动还原**：停止保活、退出程序、注销 / 关机时都会把原来的合盖动作写回去。
- **限时保活**：不限时 / 30 分钟 / 1 小时 / 2 小时 / 4 小时，到时自动恢复，卡片上有倒计时进度条。
- **状态文件兜底**：原设置记在 `%TEMP%\lidkeep-state.txt`，进程被强杀后下次启动仍能还原。
- **不覆盖手动改动**：如果保活期间你自己去电源选项改了合盖动作，程序不会把它改回去。
- **托盘常驻**：关闭窗口只是收回托盘，不会中断保活。
- **命令行模式**：`--keep 1800` 可以无界面后台保活，方便脚本 / 计划任务调用。
- **自绘深色界面**：无系统边框的圆角卡片，支持高 DPI 缩放。

## 锁屏与「保持屏幕常亮」

这是最容易误解的一点，先说清楚：

| 请求的标志 | 系统睡眠 | 显示器 | 结果 |
| --- | --- | --- | --- |
| `ES_SYSTEM_REQUIRED` | 阻止 | **不管** | 任务不中断，但屏幕会熄灭 → 合盖后进入锁屏界面 |
| `+ ES_DISPLAY_REQUIRED` | 阻止 | 阻止 | 屏幕保持点亮，不会因为「显示器关闭」而锁屏 |

`ES_SYSTEM_REQUIRED` **不等于**「屏幕保持点亮」，它只保证系统不睡。
原始版本只申请了前者，所以合盖后屏幕照常熄灭，Windows 随即锁屏 —— 任务其实一直在跑，
只是界面锁了。勾选「保活时保持屏幕常亮」后补上 `ES_DISPLAY_REQUIRED` 即可避免。

> **注意**：`SetThreadExecutionState` 只能压制**空闲**触发的睡眠与息屏。合盖、按电源键
> 这类硬件事件由固件 / 驱动上报，应用层无法拦截（参见
> [系统睡眠条件](https://learn.microsoft.com/zh-cn/windows/win32/power/system-sleep-criteria)）。
> 本程序回避睡眠靠的是改合盖动作，`SetThreadExecutionState` 只是兜底。
> 因此「屏幕常亮能否 100% 阻止合盖锁屏」取决于机型的驱动行为，建议实测一次。

## 截图

> 主界面为 400 × 400（96 DPI）的深色圆角卡片：顶部标题栏，中间状态卡片（呼吸绿灯 + 状态文案 + 倒计时进度条），下方「保活时长」胶囊选择器、主按钮，以及「保活时保持屏幕常亮」勾选项。

## 快速开始

### 直接使用

1. 下载 / 编译得到 `LidKeep.exe`，双击运行。
2. 选一个保活时长，按需勾选 / 取消「保活时保持屏幕常亮」。
3. 点「开启保活」，然后就可以合盖了。
4. 用完点「停止保活并恢复」，或从托盘菜单退出，合盖设置会自动还原。

### 命令行

```text
LidKeep.exe                        打开图形界面（双击同理）
LidKeep.exe --keep 1800            后台保活 1800 秒后自动恢复（Ctrl+C 提前结束）
LidKeep.exe --gui --keep 1800      打开界面并立即开始限时保活
LidKeep.exe --status               查看当前合盖动作设置
LidKeep.exe --restore              恢复保活前的合盖动作
LidKeep.exe --version / --help

屏幕选项（仅本次运行生效，不写回配置）:
LidKeep.exe --keep 1800 --display      保活时保持屏幕常亮（默认）
LidKeep.exe --keep 1800 --no-display   只保系统不睡眠，允许屏幕熄灭并锁屏
```

`--status` 输出示例：

```text
LidAction: AC=1 (睡眠), DC=1 (睡眠)
SavedOriginal: AC=1, DC=1
KeepActive: yes
```

## 工作原理

合盖动作存放在当前电源方案的「按钮和盖子」子组下：

| 项 | 值 |
| --- | --- |
| 子组 GUID | `4f971e89-eebd-4455-a8de-9e59040e7347` |
| 设置 GUID | `5ca83367-6e45-459f-a27b-476b1d01c936` |
| 0 | 不采取任何操作 |
| 1 | 睡眠 |
| 2 | 休眠 |
| 3 | 关机 |

读取：

```bat
powercfg -q SCHEME_CURRENT 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936
```

输出里的最后两个 `0x…` 分别是「交流」和「直流」下的取值。

写入：

```bat
powercfg -setacvalueindex SCHEME_CURRENT <子组GUID> <设置GUID> 0
powercfg -setdcvalueindex SCHEME_CURRENT <子组GUID> <设置GUID> 0
powercfg -setactive SCHEME_CURRENT
```

> 第三条 `-setactive` 不能省，否则改动不会生效。

在此基础上再调用 `SetThreadExecutionState`，让系统在这段时间内保持唤醒：

```
ES_CONTINUOUS | ES_SYSTEM_REQUIRED                          只保系统不睡眠
ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED    连屏幕一起保持点亮
```

请求**每 5 秒重申一次**（见 `LidPower.ReassertExecutionState`）。虽然带了 `ES_CONTINUOUS`，
但会话切换、快速用户切换、驱动重置之后系统仍可能丢掉这个请求，周期性重申才能保证
「程序启用期间一直不休眠、不息屏」是可靠的。

### 会不会休眠 / 锁屏：逐条对照

| 触发路径 | 靠什么挡住 |
| --- | --- |
| 合盖 | 合盖动作被改成「不采取任何操作」 |
| 空闲超时 | 睡眠超时本就是「从不」；另有 `ES_SYSTEM_REQUIRED` 兜底 |
| 息屏 | 关闭显示器超时本就是「从不」；另有 `ES_DISPLAY_REQUIRED` 兜底 |
| 无人参与睡眠超时（默认 120 秒） | `ES_SYSTEM_REQUIRED` 会覆盖它 |
| 屏保 / 屏保恢复要密码 | 不启用屏保就不会走这条路 |
| 息屏触发的锁屏 | 屏幕不灭，就不会触发 |
| **电源按钮 / 睡眠按钮** | ❌ **不接管**，按了仍会休眠（特意的，保留手动强制休眠的能力） |
| 合盖动作被改成 0/0 之外的其它值 | ❌ 用户手动改电源选项的话，`StopKeep` 会尊重你的修改而不还原 |

> 本程序**不能**拦截合盖、电源键这类硬件事件本身，它改变的是这些事件的**处理结果**。
> 若某机型在固件层直接触发锁屏（与息屏无关），应用层无法阻止，只能调整系统锁屏策略。

## 原值保护机制

保活会把合盖动作改成 `0/0`，退出时再还原成原值。**「原值记在哪里」是这类工具最容易出问题的地方**，
因为一旦原值丢了，用户真正的电源设置就被永久覆盖了。本程序用两个文件分开存：

| 文件 | 作用 | 何时写入 | 何时删除 |
| --- | --- | --- | --- |
| `%TEMP%\lidkeep-state.txt` | 本次保活的状态记录 | 每次开始保活 | 停止保活 / 恢复成功 |
| `%TEMP%\lidkeep-original.txt` | **受保护的原值备份** | 只在拿到有效原值时，且已有有效备份则不覆盖 | 停止保活 / 恢复成功 |

核心规则：**`0/0` 永远不被视为有效原值** —— 「不采取任何操作」只可能是保活自己写进去的。
因此它既不会被接受为原值，也不会覆盖已有的备份。

原值的确定顺序（GUI 与命令行一致）：

1. 受保护备份里有有效原值 → 直接沿用，**绝不覆盖**
2. 会话状态里有合法原值（1/2/3）→ 提升为备份
3. 现场读到的不是 `0/0` → 那就是真正的原始设置，记入备份
4. 都不成立 → 回落到「睡眠」，并且**不写备份**，避免把无效值固化

> 启动时还会做一次自检：如果会话状态显示「正在保活」却拿不到有效原值
> （说明上一轮被强杀且原值已丢失），会提示并清理残留记录。

用 `--status` 可以看到受保护的原值：

```text
LidAction: AC=0 (不采取任何操作), DC=0 (不采取任何操作)
KeepActive: no
ProtectedOriginal: AC=1, DC=1  (C:\Users\...\Temp\lidkeep-original.txt)
```

## 构建

### 环境要求

- Windows 10 / 11
- .NET Framework 4.8（系统自带）或 .NET Framework 4.0 + 对应 Targeting Pack
- .NET SDK 6/8（SDK 风格项目）**或** Visual Studio 2022 / Build Tools

### 命令行

```bat
cd src
dotnet build -c Release
```

产物：`src\bin\Release\net48\LidKeep.exe`

### 关于目标框架

原始程序集基于 **.NET Framework 4.0** 构建。仓库里的 `LidKeep.csproj` 默认目标为 `net48`
（Win10/11 自带，开箱可编译）。若要还原成 4.0：

```xml
<TargetFramework>net40</TargetFramework>
```

然后使用 classic MSBuild 并安装 .NET 4.0 Targeting Pack。

## 项目结构

```text
lidkeep/
├─ README.md
├─ LICENSE
├─ docs/
│  └─ DECOMPILATION.md          还原说明（来源、工具、验证方式）
└─ src/
   ├─ LidKeep.csproj
   ├─ app.ico
   ├─ Properties/AssemblyInfo.cs
   ├─ Program.cs                入口：命令行解析、控制台模式、单实例
   ├─ MainForm.cs               主窗口：布局、托盘、拖动、窗口事件
   ├─ MainForm.Paint.cs         主窗口：全部自绘内容
   ├─ MainForm.KeepAlive.cs     保活逻辑：开启 / 停止 / 状态刷新
   ├─ LidPower.cs               powercfg 读写 + SetThreadExecutionState
   ├─ AppState.cs               原值与会话状态（双文件保护）
   ├─ Settings.cs               用户偏好（是否保持屏幕常亮）
   ├─ Ui.cs                     配色与绘图辅助
   ├─ FlatBtn.cs                圆角按钮
   ├─ CaptionBtn.cs             自绘标题栏按钮
   ├─ CheckBoxFlat.cs           自绘勾选框
   ├─ PillBar.cs                胶囊形分段选择器
   └─ DarkMenuColors.cs         深色托盘菜单配色
```

## 已知行为与注意事项

- **勾选「保持屏幕常亮」时屏幕不熄灭**，不会锁屏；代价是屏幕一直亮着（更耗电、发热）。
- **取消勾选时合盖后屏幕会熄灭并进入锁屏界面**，但任务不会中断 —— 这是
  `ES_SYSTEM_REQUIRED` 的语义决定的，不是故障。
- **电池供电时会持续耗电发热**，界面里会给出橙色提示。建议插电使用。
- 需要修改电源方案，普通用户通常可以；若被组策略限制，`powercfg` 会报错并原样显示在界面上。
- 本程序**不能**阻止合盖 / 电源键这类硬件事件，它只是把合盖动作改成「不采取任何操作」来回避睡眠。
- 保活期间被强杀（任务管理器结束进程 / 断电 / 蓝屏）**不会丢失原值** ——
  原值存在独立的受保护备份里，下次启动或 `--restore` 仍能正确还原。
  只有当备份文件本身也被删除、且原值从未被成功记录过时，才会回落到「睡眠」。
  详见[原值保护机制](#原值保护机制)。

## 开源说明

本仓库的源代码由原始 `LidKeep.exe` 反编译还原并整理而成，
详见 [docs/DECOMPILATION.md](docs/DECOMPILATION.md)。

## 许可证

[MIT](LICENSE)

## English

**LidKeep** temporarily sets the Windows lid-close action to *Do nothing* so long-running
tasks survive a closed lid, then restores the original setting on exit. See
[docs/DECOMPILATION.md](docs/DECOMPILATION.md) for how this source was recovered from the
original binary. Build with `dotnet build -c Release` inside `src/`.
