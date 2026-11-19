# Leaf Reader 2.4.2 验证记录

日期：2026-11-19  
范围：在 Windows 桌面端新增书库右键删除文件夹与文件夹穿透连续阅读功能，同步多语言条目并完成 Windows EXE / Android APK 2.4.2 构建、交付打包与 GitHub 发布。

## 核心改动

### 1. 书库界面右键删除文件夹
- **书架卡片右键菜单（`ShelfGrid.cs`、`Theme.xaml`）**：为本地文件夹卡（`GroupId != null`）、本地书籍文件夹卡及本地压缩包卡片增加主题适配的右键 `ContextMenu`，提供“打开文件夹 / 开始阅读”、“平铺查看”（适用时）、“在资源管理器中打开”以及“删除文件夹… / 删除压缩包…”。
- **左侧目录树右键菜单（`ShelfSidebar.cs`）**：为本地来源节点和子文件夹行增加右键“打开 / 删除文件夹…”菜单。远程服务器条目保持只读，不提供删除入口。
- **安全删除与状态同步（`MainWindow.cs`、`build.ps1`）**：确认弹窗展示目标名称、内含本数及完整路径，默认按钮为“否”；确认后通过 `Microsoft.VisualBasic.FileIO.FileSystem`（`RecycleOption.SendToRecycleBin`）移入系统回收站（`testMode` 下直接删除测试目录以免污染用户回收站），同步清理 `State.Roots` 中受影响的根路径、刷新 `FileSystemWatcher`、更新书架并重绘。

### 2. 文件夹穿透连续阅读
- **阅读边界穿透（`ReaderView.cs`、`MainWindow.cs`）**：
  - 单页 / 双页模式（含左至右、右至左）在最后一页继续往后翻页，或 Webtoon 模式滚动到底部继续向下翻页 / 滚轮向下滑过阈值时，通过 `BoundaryTurn(1)` 自动穿透到下一本或下一个子文件夹的第一页（`BookOpenPosition.Start`）。
  - 在第一页（或 Webtoon 顶部）继续往前翻页 / 滚轮向上滑过阈值时，通过 `BoundaryTurn(-1)` 自动穿透回上一本的最后一页 / 底部（`BookOpenPosition.End`）。
  - `Up` / `Down` 方向键在 Webtoon 模式下经 `ReaderView.ScrollStep` 同样支持顶/底边界穿透。
- **穿透序列与控制（`ShelfView.cs`、`MainWindow.cs`、`Core.cs`、`SettingsUi.cs`）**：
  - 新增 `ShelfView.OrderedBooks`，按当前视图与排序规则（支持同文件夹及跨同来源嵌套子目录的深度优先顺序）构建阅读穿透序列。
  - 若后台监听在阅读期间发现新文件夹（`pendingLocalRefresh`），穿透切换时自动补充扫描并纳入序列。
  - 在 `Settings` 新增 `FolderPenetration`（默认开启），在阅读器顶部选项栏新增“文件夹穿透”复选框（并在设置弹窗同步提供开关），底部导航栏新增“上一本 / 下一本”按钮及状态提示。
- **双语词条（`localization/strings.json`、`L10n.cs`、`L10n.java`）**：同步生成 416 条中英文对照条目。

## 自动化验证

### Windows
- 编译 `.\build.ps1`：退出码 0，生成 `LeafReader.exe`。
- `--tree-test`：退出码 0（`RESULT: ALL TESTS PASSED`）。
- `--photo-cache-test`：退出码 0。
- `--self-test`：退出码 0（`RESULT: ALL TESTS PASSED`），新增覆盖：
  - 书卡与目录卡右键菜单均正确包含“删除文件夹…”；
  - 单页模式下翻过末页穿透至同目录下一本第 1 页；
  - 翻过当前子目录最后一本后跨子目录穿透至兄弟文件夹下一本；
  - 首页往前翻页穿透回上一本末页；
  - Webtoon 模式滚到底部继续翻页穿透至下一本；
  - 关闭 `FolderPenetration` 后末页翻页保持停留在当前本；
  - 删除目录卡和叶子书文件夹后磁盘文件、书架列表与视图状态均同步移除。
- `--protocol-test`（正常权限 Windows 进程）：退出码 0（`ALL PROTOCOL TESTS PASSED`）。

### 本地化与 Android
- `python tools/test-localization.py`：15 项本地化校验通过。
- `ModelTests`：14 项模型测试全部通过（`ALL 14 MODEL TESTS PASSED`）。
- `ApiTransportTests`（`android/test-transport.ps1`）：16 项本机 HTTP 传输测试全部通过（`ALL 16 HTTP TRANSPORT TESTS PASSED`）。
- Android 生产 APK（JDK 17、SDK 35 / build-tools 35.0.0，版本 2.4.2 / versionCode 20402，原维护者证书签名）：v2 / v3 签名验证通过。
- 注：当前环境未连接 Android 真机或模拟器，未执行设备端 `DeviceTests` 仪器测试。
