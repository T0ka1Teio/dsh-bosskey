<p align="center">
  <img src="docs/icon.png" width="96" alt="DSH 老板键">
</p>

<h1 align="center">DSH 老板键</h1>

<p align="center">
  按一下热键，DeepSeek Harness 的窗口连同任务栏图标一起消失；再按一下，原样回来。
</p>

<p align="center">
  <img alt="platform" src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4">
  <img alt="dotnet" src="https://img.shields.io/badge/.NET-10-512BD4">
  <img alt="license" src="https://img.shields.io/badge/license-MIT-2ea44f">
  <img alt="size" src="https://img.shields.io/badge/download-220%20KB-lightgrey">
</p>

---

## 这是什么

一个 **Windows 小工具**，只干一件事：把 DeepSeek Harness 的窗口一键收起来。

不是最小化——是**彻底从屏幕上消失**，任务栏上的图标也一起消失。再按一次热键，窗口原样回来，位置、大小、内容都不变。

录屏、直播、共享屏幕，或者有人走过来的时候，都用得上。

- 单文件，免安装，不写系统目录
- 常驻系统托盘，占用可以忽略
- 不注入、不 hook、不修改 DeepSeek Harness 的任何文件

## 快速开始

1. 到 [**Releases**](../../releases/latest) 下载 `DSH-BossKey.exe`（单文件，220 KB）
2. 放到任意文件夹就能用（**免安装**，不写系统目录）
3. 双击 `DSH-BossKey.exe`
4. 看屏幕右下角的**系统托盘**——出现一个蓝色圆形图标，就说明它已经在正常运行了
   （Windows 11 默认把托盘图标折叠起来，点一下任务栏上的 `^` 就能看到）
5. 按 `Ctrl + Alt + Q` 试试

> **运行环境**：需要 [**.NET 10 桌面运行时**](https://dotnet.microsoft.com/download/dotnet/10.0)（Windows 一般没自带）。
> 如果双击后提示缺少 .NET 或一闪而过，就是这个原因；装上桌面运行时（Desktop Runtime）再双击即可。
> 不想装运行时的话，可以自己构建自包含版，见下方「从源码构建」。

> **第一次运行时 Windows 可能拦一下**
> 会提示「Windows 已保护你的电脑」。这是因为程序没有购买数字签名证书，属于未签名软件的常规提示，不是报错。
> 点 **「更多信息」→「仍要运行」** 即可，之后不会再问。

## 热键

| 热键 | 作用 |
| :--- | :--- |
| `Ctrl` + `Alt` + `Q` | **老板键**：隐藏 / 显示窗口 |
| `Ctrl` + `Alt` + `W` | 只摘掉任务栏图标（窗口保持可见），再按装回 |

热键是**全局**的：不管当前在用什么软件、焦点在哪个窗口，按下去都生效。

## 系统托盘

右键托盘里的蓝色图标，可以做这些事：

- 隐藏 / 显示窗口
- 隐藏 / 恢复任务栏图标
- **开机自启**（勾选后每次登录自动运行）
- 打开配置文件
- 退出（退出前会自动把窗口恢复，不会留下看不见的窗口）

## 常见问题

**按了热键没反应？**
按顺序排查：

1. 托盘里还有没有那个蓝色图标？没有就是程序没在运行，重新双击打开。
2. DeepSeek Harness 开着吗？程序只对正在运行的窗口生效。
3. 热键是不是被别的软件占了？被占用时程序启动会弹一个提示气球，换一组键即可（见下面的「配置」）。

**双击没反应 / 提示缺少 .NET？**
装 [.NET 10 桌面运行时](https://dotnet.microsoft.com/download/dotnet/10.0)（要选 **Desktop Runtime**，不是 ASP.NET Core 那个），装完直接双击。
注意：它是一瞬间就退出的，不会弹错误框，所以"没反应"往往就是缺运行时。

**会不会把我正在跑的任务弄停？**
不会。它只改变窗口的「可见性」，不关闭、不结束进程，后台任务照常跑。

**能隐藏别的程序吗？**
能。改配置里的 `TargetProcess` 就行，比如 `Weixin`、`Notepad`。

**Windows 10 能用吗？**
可以，Windows 10 / 11 的 64 位版本都支持。

**为什么隐藏后录屏里什么都没有？**
因为窗口不是被最小化、也不是被移到屏幕外，而是从桌面合成里整个撤掉了。屏幕上没有的东西，录屏自然也拿不到——连黑块都不会有。

**会被杀毒软件报毒吗？**
有可能。它需要调用 Windows 的窗口 API 去操作别的程序的窗口，这个行为特征在启发式扫描里容易被误判。加入信任即可，源码就在这个仓库里，可以自己审。

**支持多显示器和缩放吗？**
支持。窗口在哪块屏幕上都能隐藏，不受缩放比例影响。

## 配置

配置文件在 `%APPDATA%\DshBossKey\config.json`，第一次运行会自动生成：

```json
{
  "TargetProcess": "DeepSeek Harness",
  "HideHotkey": "Ctrl+Alt+Q",
  "TabHotkey": "Ctrl+Alt+W",
  "StartWithWindows": false
}
```

| 字段 | 说明 |
| :--- | :--- |
| `TargetProcess` | 要隐藏的进程名，**不带 `.exe`** |
| `HideHotkey` | 老板键，支持 `Ctrl` / `Alt` / `Shift` / `Win` + 字母或 `F1`~`F12` |
| `TabHotkey` | 任务栏图标开关的热键 |
| `StartWithWindows` | 是否开机自启（托盘菜单里勾选也会同步这里） |

改完重启程序生效。

## 命令行

除了托盘模式，也支持直接调用（适合放进脚本或快捷方式）：

```bat
DSH-BossKey.exe --status                    :: 看看当前找到哪些窗口
DSH-BossKey.exe --hide                      :: 隐藏
DSH-BossKey.exe --show                      :: 显示
DSH-BossKey.exe --toggle                    :: 切换
DSH-BossKey.exe --tab-off / --tab-on        :: 摘掉 / 装回任务栏图标
DSH-BossKey.exe --target Notepad --toggle   :: 临时指定别的进程
```

## 实现原理

全部使用 Windows 公开 API，没有逆向、没有注入：

| 能力 | 用到的 API |
| :--- | :--- |
| 隐藏 / 显示窗口 | `ShowWindow`（只能操作顶层窗口，所以是按进程枚举后逐个处理） |
| 摘 / 装任务栏按钮 | `ITaskbarList::DeleteTab` / `AddTab` |
| 全局热键 | `RegisterHotKey` + 一个隐藏的消息窗口 |
| 托盘 | WinForms `NotifyIcon` |

从源码到可执行文件大约 500 行 C#，读完大概十分钟。

## 已知限制

- 只处理**顶层窗口**。把界面寄宿在系统进程里的应用（部分 UWP）可能无效。
- 隐藏的是窗口本身。如果目标程序自己还有一个**系统托盘小图标**，那个图标不受影响。
- 它不是 DRM：挡不住 HDMI 采集卡，也挡不住拿手机拍屏幕。
- 只建议用在你自己的设备和你自己有权操作的程序上。

## 从源码构建

需要 [.NET 10 SDK](https://dotnet.microsoft.com/download)：

```powershell
git clone https://github.com/<your-name>/dsh-bosskey.git
cd dsh-bosskey
.\build.ps1
```

或者手动指定：

```powershell
# 精简版：约 220 KB，需要目标机器装 .NET 10 桌面运行时（Release 里发的就是这个）
dotnet publish -c Release -r win-x64 -p:SelfContained=false -p:PublishSingleFile=true -o dist

# 自包含版：约 47 MB，内置运行时，对方什么都不用装（适合发给别人）
dotnet publish -c Release -r win-x64 -p:SelfContained=true -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o dist\selfcontained
```

`build.ps1` 默认构建自包含版；要精简版就加 `-SelfContained:$false`。
每次 push 后 GitHub Actions 会在 Actions 页生成构建产物。

## 目录结构

```
BossKey.csproj      # net10.0-windows + WinForms
Program.cs          # 全部逻辑：窗口枚举、隐藏/显示、任务栏、托盘、热键、配置
icon.ico            # 16/24/32/48/64/128/256 多尺寸图标
make-icon.ps1       # 图标生成脚本
build.ps1           # 一键构建
```

## 许可

[MIT](LICENSE) —— 随便用、随便改、随便分发。
