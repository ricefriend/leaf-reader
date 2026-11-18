# Leaf Reader 2.4.1 验证记录

日期：2026-11-18  
范围：修复打开软件后向已导入书库新增文件夹必须退出重进才能更新的问题；将文件夹选择框升级为支持地址栏导航与搜索的原生 Explorer 目录选择器；无线共享默认带入已导入软件的目录并支持删除记忆；重新构建 Windows EXE、Android APK 与交付归档。

## 核心改动

### Windows 桌面端（`MainWindow.cs`、`FolderPicker.cs`、`ShareServer.cs`、`NetworkingUi.cs`、`Program.cs`、`SettingsUi.cs`）
- **实时目录监听与防抖自动刷新**：在 `MainWindow` 中通过 `FileSystemWatcher` 监听 `State.Roots` 中的本地目录与压缩包来源（含子目录新增、删除、重命名及图片/压缩包变更事件），经 700ms 防抖后执行 `RefreshLocal()` 自动扫描并更新书架。
- **不打断阅读与弹窗**：若文件系统变更发生在阅读器打开期间（`Reader.Book != null`）、正在忙于扫描/打开（`busy`）或存在模态弹窗（`OwnedWindows.Count > 0`），自动挂起 `pendingLocalRefresh`，待返回书架（`Back()`）或操作结束后自动执行刷新。
- **降级回退与并发合并**：当某些目录无法挂载监听器或发生缓冲区异常（`watcherDegraded`）时，窗口重新激活（`Activated`）自动静默核对本地书库；对并发的 `Scan()` 请求通过 `pendingFullScan` 合并排队，不再静默丢弃。
- **可见刷新入口与 `F5` 修复**：在书架主工具栏（`rightTools`）新增始终可见的“刷新”按钮（无论宽屏或窄屏均可直接点击），并将 `F5` 快捷键提前到文本框过滤前处理，使焦点位于搜索框内时也可按 `F5` 刷新。
- **原生 Explorer 目录选择器（`FolderPicker.cs`）**：使用 `IFileOpenDialog`（`FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST`）替换“导入文件夹”与“添加共享目录”中的旧版 `FolderBrowserDialog` 纯目录树弹窗，支持地址栏输入/粘贴路径导航及右上角搜索（异常时安全回退）。
- **无线共享默认目录与删除记忆（`ShareServer.cs`、`NetworkingUi.cs`）**：打开“共享”窗口时（未处于运行中），调用 `ShareConfig.SyncDefaultRoots(State.Roots)` 自动带入软件已导入的有效本地文件夹（自动跳过压缩包、整盘根目录、缺失或符号链接目录）；点击“移除所选”通过 `RemoveRoot` 记录至 `dismissedRoots` 持久化，确保后续重开窗口或重启软件不会再把已删除的默认项加回，而新导入软件的目录仍会自动带入、手动“添加共享目录”也可重新恢复。
- **版本更新**：`AssemblyVersion` 更新为 `2.4.1.0`，设置窗口版本标注更新为 `Leaf Reader 2.4.1 preview · ricefriend · MIT`。

### Android 端（`MainActivity.java`、`android/build.ps1`、`android/test-transport.ps1`）
- **后台切回静默刷新**：记录从前台离开（`onStop`），切回书架（`onResume`）时静默扫描本地授权目录；仅在本地书籍列表实际变化时更新书架与状态栏，不打断正在阅读的书籍，也不在无变化时重连服务器或清空封面缓存。
- **构建脚本与版本**：`versionCode` 更新为 `20401`，`versionName` 更新为 `2.4.1`；将 `android/test-transport.ps1` 规范化为 UTF-8 with BOM 以兼容 Windows PowerShell 5.1。

## 自动化验证

### Windows
- 编译 `.\build.ps1`：退出码 0，生成 `LeafReader.exe`。
- `--tree-test`：退出码 0（`RESULT: ALL TESTS PASSED`）。
- `--photo-cache-test`：退出码 0。
- `--self-test`：退出码 0（`RESULT: ALL TESTS PASSED`），新增覆盖：
  - 宽屏（1220px）与窄屏（960px）下主工具栏刷新按钮均保持可见；
  - 搜索框聚焦时按 `F5` 触发重新扫描并发现新增文件夹；
  - 原生 `FolderPicker` COM 实例初始化及 `FOS_PICKFOLDERS` 标志校验；
  - 无线共享 `SyncDefaultRoots` 默认带入有效已导入目录并跳过压缩包/整盘、`RemoveRoot` 持久删除、重开不复活且新增导入仍自动带入、`AddRoot` 手动恢复；
  - 打开软件并启用监听后，在库目录新增文件夹可自动防抖刷新并出现在书架；
  - 阅读过程中在后台新增文件夹不打断当前阅读，按 `Esc` 返回书架后延迟刷新立即生效。
- `--protocol-test`（正常权限 Windows 进程）：退出码 0（`ALL PROTOCOL TESTS PASSED`）。

### Android
- `ModelTests`：14 项模型测试全部通过（`ALL 14 MODEL TESTS PASSED`）。
- `ApiTransportTests`（`android/test-transport.ps1`）：16 项本机 HTTP 传输测试全部通过（`ALL 16 HTTP TRANSPORT TESTS PASSED`）。
- 生产 APK 构建（JDK 17、Android SDK 35 / build-tools 35.0.0，使用原维护者私密签名证书）：v2 / v3 签名验证通过，无 instrumentation / debuggable 标记。
- 测试 APK（`-WithTests`）编译与签名通过。
- 注：当前环境未连接真机或模拟器，未执行设备端 `DeviceTests` 仪器测试。
