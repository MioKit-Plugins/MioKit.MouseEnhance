# 鼠标增强（Mouse Enhance）

Windows 鼠标手势增强插件，运行于 [MioKit](https://github.com/MioKit) 宿主。

| 功能 | 说明 |
| --- | --- |
| **修饰键 + 滚轮** | 调节系统音量 |
| **修饰键 + 拖动窗口** | 任意位置左键移动、右键缩放（不必瞄准标题栏） |
| **双击桌面** | 隐藏 / 显示桌面图标 |
| **双击任务栏** | 显示 / 隐藏桌面 |
| **寻找鼠标** | 双击 Ctrl 或摇晃鼠标，在所有显示器上显示跟随指针的聚光灯 |
| **光标环绕** | 从显示器外缘进入对侧，可通过启动和停止节点控制 |

窗口拖动和光标环绕分别参考 PowerToys Grab And Move 与 CursorWrap（MIT），见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。本仓库**不是** Microsoft / PowerToys 官方组件。

## 快速开始

1. 在 MioKit 中安装并启用本插件。
2. 搜索「鼠标增强」并回车，打开配置窗口。
3. 在左侧选择对应功能，按需开启和配置。

默认开启「滚轮调音量」与「拖动窗口」；桌面图标与任务栏手势默认关闭。

在「寻找鼠标」页可启用全局手势、调整聚光灯外观或预览效果。寻找鼠标默认关闭，「预览效果」可直接使用。配置窗口加载本地 Vue 资源，需要 Microsoft Edge WebView2 Runtime 和支持 SDK 1.3.1 的 MioKit 宿主。

「光标环绕」默认关闭。设置页可调整环绕方向、触发条件、拖动及单屏行为。若要使用快捷键，可在「启动光标环绕」和「停止光标环绕」各自的搜索结果菜单中选择「全局快捷键设置」。

## 搜索命令

| 命令 | 说明 |
| --- | --- |
| **鼠标增强** | 打开配置窗口（主入口） |
| **启用窗口拖动** | 仅启用窗口拖动手势 |
| **停用窗口拖动** | 仅停用窗口拖动手势 |
| **启动光标环绕** | 启用并激活光标环绕 |
| **停止光标环绕** | 暂停本次运行的光标环绕 |

可为上述结果设置别名或热键。

## 窗口拖动

| 操作 | 条件 | 效果 |
| --- | --- | --- |
| 修饰键 + 左键拖动 | 「拖动窗口」已启用 | 整体移动窗口 |
| 修饰键 + 右键拖动 | 「右键调整大小」开启 | 按离指针最近的边或角缩放 |
| 松开修饰键 | — | 结束当前拖移 / 缩放 |

激活修饰键可选 `Alt`（默认）或 `Windows`。排除列表用于跳过特定进程或把修饰键交还前台应用。

## 注意事项

- 部分系统窗口、以管理员身份运行且高于 MioKit 权限的程序，可能无法被拖移或缩放。
- 全屏游戏或重度使用 `Alt` / `Win` 的应用，建议加入「排除修饰键」或「排除窗口」。
- 双击桌面仅在空白处切换图标：Hook 返回后使用 MSAA `IAccessible.accHitTest` 检测，再向 `SHELLDLL_DefView` 异步发送 `WM_COMMAND 0x7402`。检测失败时不切换；图标隐藏时允许再次双击显示。
- 不可直接把本进程的 `LVHITTESTINFO` 指针通过 `LVM_HITTEST` 发送给 Explorer：`WM_USER` 及以上消息不自动封送指针，会造成 Explorer 访问无效内存、黑屏和重启，移出 Hook 或延时不能修复。参见 [SendMessage 文档](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendmessagea) 和 [MSAA 命中测试文档](https://learn.microsoft.com/en-us/windows/win32/api/oleacc/nf-oleacc-iaccessible-acchittest)。
- 双击任务栏同样延后调用 Shell `ToggleDesktop`。

## 开发与打包

```powershell
pnpm --dir plugin/vue-ui install --frozen-lockfile
pnpm --dir plugin/vue-ui build
dotnet build plugin/MioKit.MouseEnhance.csproj -c Release
```

设置页可通过 `pnpm --dir plugin/vue-ui dev` 在浏览器预览，桌面聚光灯可通过 `preview` 项目预览。

光标环绕和滚轮音量的无头回归使用合成显示器布局和输入：

```powershell
dotnet run --project tests/CursorWrap.Regression -c Release
dotnet run --project tests/VolumeScroll.Regression -c Release
```

Windows 跨进程命中测试回归（需要 .NET 10 SDK，无额外 NuGet 依赖）：

```text
dotnet run --project tests/DesktopIconHitTest.Regression -c Release
```

测试在独立进程的原生 `SysListView32` 上验证图标、文字、空白、越界、失效窗口以及 MTA 线程上的 100 组连续命中测试，不操作真实桌面。宿主内仍需验证双击空白处隐藏/显示、双击图标正常打开，以及多显示器和 Explorer 重建后的行为。

本地打包：

```powershell
.\pack.ps1 -PackageVersion 0.0.1-beta.1
```

输出：`artifacts/MioKit.MouseEnhance.<version>.nupkg`。推送 `v*` 标签后，GitHub Actions 会打包并挂到 GitHub Release。

几何 Overlay、Game Mode 抑制等后续能力说明见 [docs/future-overlay-and-game-mode.md](docs/future-overlay-and-game-mode.md)。
