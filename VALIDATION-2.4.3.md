# Leaf Reader 2.4.3 验证记录

日期：2026-11-19  
范围：优化删除文件夹/自动刷新后的书架滚动位置保持，以及文件夹穿透换本的边界缓冲停顿与提示横幅；重新构建 Windows EXE / Android APK 2.4.3 并更新 GitHub 发布。

## 核心改动

### 1. 删除文件夹与自动刷新保持书库位置（`ShelfGrid.cs`、`MainWindow.cs`）
- `ShelfGrid.SetBooks(items, reset)` 在 `reset == false` 时不再因为列表长度或元素变化就强制 `ScrollToVerticalOffset(0)`，而是在重新布局前记录当前视口首个保留卡片（`anchorId`）及其垂直偏移（`anchorDelta`），布局后按新位置精确恢复并限制在有效滚动范围内。
- `DeleteTargetCore` 与 `RefreshLocal` 不再清空整个封面缓存；`RefreshLocal` 复用了未改动书籍首张封面的已读取尺寸与 `Version`，避免封面闪烁。

### 2. 文件夹穿透边界缓冲与换本提示（`MainWindow.cs`、`localization/strings.json`、`L10n.cs`、`L10n.java`）
- 在阅读区底部加入浮动提示横幅（`readerNotice` + `noticeTimer`），支持文本提示与可选快捷动作按钮。
- `HandleBoundaryTurn` 在首次触达某本书的末尾或首页边界时先锁定一次（`boundaryArmedDirection` / `boundaryArmedBookId` / `boundaryArmedUtc`），弹出“已到本册末尾 · 再次翻页进入下一本：...”或“已是本册第 1 页 · 再次翻页返回上一本：...”，并内置 250ms 冷却时间防止快速连滑一次直接跳过；若在书内翻回其他页或返回书架会自动取消锁定。
- 再次翻页（或点击提示条按钮 / 底部“上一本 / 下一本”）切换成功后，展示 2.2 秒的“已进入下一本 · ...” / “已进入上一本 · ...”提示横幅。

## 自动化验证

### Windows
- 编译 `.\build.ps1`：退出码 0，生成 `LeafReader.exe`（2.4.3.0）。
- `--tree-test`：退出码 0；`--photo-cache-test`：退出码 0。
- `--self-test`：退出码 0（`RESULT: ALL TESTS PASSED`），新增及更新验证：
  - 书架向下滚动后删除中间文件夹，验证滚动位置保持不变（不再跳回顶部 0）；
  - 末页首次向右翻页停在当前本并弹出下一本提示、250ms 冷却期内连按不误跳、停顿后再次翻页穿透到下一本并展示进入提示横幅；
  - 跨目录边界、向前穿透回上一本末页、Webtoon 底部穿透与关闭穿透开关全部通过。
- `--protocol-test`（正常 Windows 权限）：退出码 0（`ALL PROTOCOL TESTS PASSED`）。

### 本地化与 Android
- `python tools/test-localization.py`：15 项本地化校验通过（422 条目）。
- Android 2.4.3 构建（versionCode 20403）及 JVM `ModelTests` / `ApiTransportTests` 校验。
