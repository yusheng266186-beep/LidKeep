# 源代码还原说明

本目录下的代码是从已编译的 `LidKeep.exe` 反编译还原并人工整理而来。这里说明来源、
使用的方法，以及做了哪些改动，便于审计与追溯。

## 原始文件

| 项 | 值 |
| --- | --- |
| 文件名 | `LidKeep.exe` |
| 大小 | 406,016 字节 |
| SHA-256 | `355EF779C77F3073662BA7D83896EBB9617CACF03ED21E390F28C5EC3CD6DC52` |
| 最后修改 | 2026-09-19 13:47:43 |
| 程序集 | `LidKeep, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null` |
| PE 架构 | x86（32 位），Subsystem = Windows GUI |
| 运行时 | .NET Framework 4.0（SDK 风格 WindowsDesktop 项目） |

> 哈希值请在本地用 `Get-FileHash .\LidKeep.exe -Algorithm SHA256` 重新计算核对。

## 使用的工具

| 工具 | 版本 | 用途 |
| --- | --- | --- |
| [ILSpy / ilspycmd](https://github.com/icsharpcode/ILSpy) | 9.1.0.7988 | C# 反编译 |
| .NET Runtime | 8.0.31 | 运行 ilspycmd |
| PowerShell 5.1 | — | 提取嵌入资源、校验 |
| MSBuild | 17.14 | 编译验证 |

反编译命令：

```bat
ilspycmd --project --outputdir .\out LidKeep.exe
```

## 还原出的文件

原始程序集共 10 个类型，全部还原为 C# 源文件：

| 类型 | 文件 | 说明 |
| --- | --- | --- |
| `LidKeep.Program` | `Program.cs` | 入口 |
| `LidKeep.MainForm` | `MainForm*.cs` | 主窗口（拆成 3 个 partial 文件） |
| `LidKeep.LidPower` | `LidPower.cs` | 电源设置读写 |
| `LidKeep.AppState` | `AppState.cs` | 状态文件 |
| `LidKeep.Ui` | `Ui.cs` | 配色与绘图 |
| `LidKeep.FlatBtn` | `FlatBtn.cs` | 自绘按钮 |
| `LidKeep.CaptionBtn` | `CaptionBtn.cs` | 标题栏按钮 |
| `LidKeep.PillBar` | `PillBar.cs` | 分段选择器 |
| `LidKeep.DarkMenuColors` | `DarkMenuColors.cs` | 菜单配色 |
| — | `Properties/AssemblyInfo.cs` | 程序集信息 |

以下两个类型是**本仓库新增**的（原版没有），用于修复息屏 / 锁屏问题，
详见下文「与原版不一致的地方」：

| 类型 | 文件 | 说明 |
| --- | --- | --- |
| `LidKeep.Settings` | `Settings.cs` | 持久化「保持屏幕常亮」开关 |
| `LidKeep.CheckBoxFlat` | `CheckBoxFlat.cs` | 自绘勾选框 |

程序集内**不含**任何嵌入资源（`GetManifestResourceNames()` 返回空），
界面全部由代码自绘，因此没有 `.resx` / Designer 文件需要还原。
唯一的外部资源是 `app.ico`（图标，372,526 字节），已原样提取。

## 做了哪些整理

反编译产物不能直接当作可读源码，整理时遵守「不改变行为」的原则：

1. **补回标识符名称**。ILSpy 生成的是 `text`、`text2`、`num`、`flag` 这类名字，
   已按用途重命名为 `output`、`origAc`、`deadline`、`_hover` 等。
2. **合并重复表达式**。例如 `powercfg` 的三条命令行里的 GUID 常量，
   已还原为 `SubButtonsGuid` / `LidActionGuid` 常量引用。
3. **拆分大文件**。`MainForm` 原本约 700 行，按职责拆成
   `MainForm.cs`（结构 / 托盘）、`MainForm.Paint.cs`（绘制）、
   `MainForm.KeepAlive.cs`（保活逻辑）三个 partial 文件。
4. **补注释**。对 `powercfg` 的取值含义、`SetThreadExecutionState` 的标志位、
   兜底状态文件的设计意图等加了说明。
5. **补程序集元数据**。原 `AssemblyInfo.cs` 中缺少 `AssemblyTitle`、
   `AssemblyCompany` 等字段，已按原程序集的实际值补全。

### 刻意保留的原样行为

以下几处看起来「可以更好」，但为保持与原程序集一致而没有改动：

- `RunHeadless` 中 `ac` / `dc` 输出参数与当前值的变量复用写法。
  （已改为等价的显式写法 `origAc` / `origDc`，逻辑分支完全一致。）
- `AppState.Load` 在文件缺失时把 `ac` / `dc` 置为 `1`（睡眠）的默认值。
- `StopKeep` 只在当前动作仍为 `0/0` 时才执行还原，避免覆盖用户的手动修改。
- 托盘图标文案截断到 62 字符（`NotifyIcon.Text` 的 63 字符上限）。

### ⚠️ 与原版**不一致**的地方：修复了息屏 / 锁屏缺陷

这是本仓库**唯一一处有意改变行为**的改动，单独列出以免误解为原版行为。

原版 `LidPower.KeepAwake` 只申请了一个标志：

```csharp
private const uint EsSystemRequired = 0x00000001; // 原注释写“连屏幕一起保持点亮”
SetThreadExecutionState(on ? 0x80000000 | EsSystemRequired : 0x80000000);
```

问题有两层：

1. **注释是错的**。`ES_SYSTEM_REQUIRED` 只阻止*系统*进入睡眠，**不阻止显示器关闭**。
   阻止显示器关闭需要 `ES_DISPLAY_REQUIRED`（`0x00000002`）。
2. **因此有个真实缺陷**：保活期间合盖，显示器照常熄灭，Windows 随即进入锁屏界面。
   任务本身不会中断（这点已用系统日志确认：保活期间没有任何 Kernel-Power 睡眠事件），
   但界面被锁 —— 与「保活」这个功能名给人的预期不符。

本仓库的修法：

- `LidPower.KeepAwake(bool on, bool keepDisplayOn = false)`，
  在 `keepDisplayOn` 为真时补上 `ES_DISPLAY_REQUIRED`。
- 新增 `Settings.cs` 持久化该开关，界面上对应
  「保活时保持屏幕常亮（防止合盖锁屏）」勾选框，默认**开启**。
- 命令行加 `--display` / `--no-display` 临时覆盖。

> 需要说明的是，`SetThreadExecutionState` 只能压制**空闲**触发的睡眠与息屏；
> 合盖、电源键这类硬件事件由固件 / 驱动上报，应用层无法拦截
> （见 [系统睡眠条件](https://learn.microsoft.com/zh-cn/windows/win32/power/system-sleep-criteria)）。
> 本程序回避睡眠本来就靠改合盖动作，`SetThreadExecutionState` 只是兜底。
> 因此「保持屏幕常亮能否在所有机型上 100% 阻止合盖锁屏」取决于驱动行为，未能穷举验证。

## 验证方式

1. **反编译完整性**：`--project` 模式输出全部 10 个类型，无报错、无 `throw new NotSupportedException` 占位。
2. **行为对照**：用原 `LidKeep.exe` 执行 `--version` / `--status` / `--restore`，
   记录输出格式（含中文文案）与状态文件变化，作为整理时的对照基准。
3. **编译验证**：整理后的源码用 MSBuild / `dotnet build` 编译通过，无警告级错误。
4. **保活有效性实测**：本机开启保活后合盖，查系统日志确认无 Kernel-Power
   Event ID 42（进入睡眠），即任务未被中断。
5. **标志位验证**：对编译产物用 ilspycmd 反查 `LidPower`，确认
   `EsContinuous = 2147483648u`、`EsSystemRequired = 1u`、`EsDisplayRequired = 2u`
   三个常量均存在于 IL 中。

## 免责声明

本仓库仅用于学习与备份目的恢复原作者自己的程序源码。代码按原样提供，使用前请自行审阅。
