# 栞 · Leaf Reader（Windows / Android 2.3 预览版）

[English](README.en.md) · [下载 Releases](https://github.com/ricefriend/leaf-reader/releases) · [迁移说明](MIGRATION.md) · [构建说明](BUILDING.md)

**公开测试版，MIT © 2026 ricefriend。** 中英界面与设置页已接入，安卓支持加密备份和一次换签名迁移。安装包见下方 Releases，旧安卓测试版请先完成备份迁移。 相似项目与开源准备见 [OPEN_SOURCE_PREPARATION.md](OPEN_SOURCE_PREPARATION.md)，依赖声明见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。本地签名密钥不进入公开源码包。

基于原有 Windows WPF / C# 阅读器扩展，新增原生 Android Java 客户端与 Leaf Share v1 只读远程协议。Windows 目标运行时为 .NET Framework 4.8+；Android 最低 API 26，目标 API 35。二维码依赖直接提供 DLL / JAR 和源码，不需要在线 NuGet / Gradle 恢复。

完整构建与测试见 [BUILDING.md](BUILDING.md)，架构检查与实施记录见 [ARCHITECTURE.md](ARCHITECTURE.md)，协议见 [protocol/leaf-share-v1.md](protocol/leaf-share-v1.md)，Android 构建见 [android/README.md](android/README.md)。

原有本地阅读器的界面和核心功能保留，本地 / 远程来源都进入原书架和阅读流程。没有 WebView、云服务或账号系统。当前桌面实现仍为 Windows 专用。


## 下载与开始使用

在 [Releases](https://github.com/ricefriend/leaf-reader/releases) 下载：

- Windows：`LeafReader-2.3-preview.exe`，直接运行，无需安装器。
- Android 首次安装：`LeafReader-Android-2.3-preview.apk`（Android 8.0+）。
- 已装历史测试 APK：先用 `LeafReader-Android-2.3-migration-debug.apk` 覆盖旧版并导出、验证加密备份，再卸载换装 preview。不要先卸载；完整步骤见 [迁移说明](MIGRATION.md)。

导入本地图片母目录即可浏览书架。远程阅读时，电脑在“共享”中选择目录并开启服务，手机或另一台电脑扫码 / 输入地址及配对码连接。共享默认关闭，图片按需读取。

设置中可选简体中文 / English。支持开发：[Ko-fi](https://ko-fi.com/gdbkf)，阅读功能始终免费。

## 构建

在 Windows PowerShell 中运行：

```powershell
& .\build.ps1
```

构建脚本使用系统 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，在源码目录的上级生成 `LeafReader.exe`。主题和图标内嵌在 EXE 中，运行无需源码目录。编译器采用 C# 5 兼容语法。

## 自测

指定一个新的、可写的测试目录：

```powershell
$qa = Join-Path $PWD 'qa'
$app = Join-Path (Split-Path $PWD -Parent) 'LeafReader.exe'
$process = Start-Process -FilePath $app -ArgumentList '--self-test', ('"' + $qa + '"') -WindowStyle Hidden -PassThru
$process.WaitForExit()
Get-Content (Join-Path $qa 'test-results.txt')
```

返回码 0 表示通过，1 表示失败。自测会在指定目录生成样例图片、压缩包、测试记录和 UI 渲染图，阅读状态也重定向至该目录，不改动正常用户的阅读记录。请使用专用目录；再次运行会覆盖同名测试文件。全屏测试可能短暂显示测试窗口。

## 文件

- `Core.cs`：自然排序、文件夹 / 压缩包扫描、EXIF 处理、数据持久化、配对算法。
- `ReaderView.cs`：可视区域绘制、连续纵向排版、异步图像缓存、滚轮 / 点击 / 拖动。
- `MainWindow.cs`：书架、工具栏、导入、搜索、键盘、书签、全屏、设置。
- `ShelfGrid.cs`：固定尺寸连续网格、可视区域卡片创建、封面缓存与滚动锚点。
- `Theme.xaml`：内嵌 WPF 样式。
- `Program.cs`：入口、单实例、异常记录、自测入口。
- `SelfTests.cs`：功能回归与实际 WPF 视图渲染测试。
- `app.manifest`、`app.ico`：Windows 清单和图标。

## 配对原则

图片序号从 0 开始。`Progress.PairAnchor` 保存用户跳页 / 手动错位后的配对起点。组边界从书首按封面、横图和锚点规则确定；反向翻页重建同一组边界，避免用简单的 `page - 2` 造成横图附近配对漂移。下一页到达最后一组后停留，不重复显示末组第二张。

## 1.1 界面行为

`Book.CreatedUtc` 在扫描时读取文件系统的 UTC 创建时间，排序使用该时间，显示时转换为本地时间。文件夹书籍读取文件夹本身；压缩包读取文件本身。`Settings.ShelfSort` 保存书架排序选项。

阅读进度条使用 `Slider.IsDirectionReversed` 跟随右至左模式，保持真实页码数值不变。其他模式恢复普通横向方向。

自动隐藏使用 3 秒 `DispatcherTimer`，菜单、模态窗口、鼠标捕获、进度拖动及工具栏悬停期间暂停隐藏。隐藏后仅鼠标进入上下 32 DIP 边缘或按 Tab 等明确操作恢复界面；普通翻页保持隐藏。全屏与窗口阅读共用同一套显示状态，返回书架恢复全部控件。视口尺寸改变通过 ReaderView 的现有页内锚点恢复逻辑保持 Webtoon 位置。

## 性能与范围

书架不分页，使用完整书目对应的轻量矩形记录计算网格布局；仅创建可视区域上下约 400 DIP 内的卡片，顺序后台解码封面。卡片为 180×296 DIP，其中封面为 164×220 DIP，以 UniformToFill 等比填满并裁切。卡片按排序顺序逐行排列，间距 18 DIP。图片解码完成时不重排；窗口宽度改变时调整列数，并保留可见书目的滚动锚点。

普通封面缓存目标为 72 项，大视口时优先保留全部可见封面，避免反复淘汰加载。单张封面缩略图的目标像素约为 30 万。

阅读区只有一个自绘视图。Webtoon 使用图片尺寸与累计偏移定位当前页，按需解码附近图片；阅读图片缓存以 LRU 淘汰，最多 16 项 / 192 MiB。最多两个并发阅读解码任务，每次最多排队 8 个请求。

打开书籍时后台读取整本书的图片尺寸，特别大的文件夹或压缩包会需要更长准备时间。解码器的瞬时内存占用不受缓存预算严格约束。

支持范围及用户操作详见发行包中的《使用说明》。

## Android 2.1

手机阅读手势已更新：纵向惯性滚动默认、横向跟手与切页动画、临时前后页拼接。Windows 继续使用 2.0。构建与操作详见 android/README.md。

## Android 2.2

书架改为自适应列宽与统一比例圆角封面，新增右侧可拖动快速滚动条，调整控件配色和文字层级。53 项设备测试通过，详见 android/README.md。

## 2.3 设置与迁移

两端可在设置中选择简体中文 / English，赞助入口使用 ricefriend 的 Ko-fi。安卓迁移操作见 [MIGRATION.md](MIGRATION.md)，本次测试与限制见 [VALIDATION-2.3.md](VALIDATION-2.3.md)。旧版本用户应先使用迁移 APK 导出并验证加密备份，之后才能卸载换签名。
