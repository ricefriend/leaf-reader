# Leaf Reader 2.4.0 验证记录

日期：2026-10-19  
范围：Windows 桌面端目录树书架与 UI 重做。Android 应用代码未在本次修改，未构建 APK。

## 自动化

构建：

```powershell
cd D:\AI\leaf-reader
& .\build.ps1
```

结果：`已生成：D:\AI\LeafReader.exe`，退出码 0。构建脚本已改为 UTF-8 with BOM，Windows PowerShell 5.1 下可直接执行。

快速树测试：

```powershell
D:\AI\LeafReader.exe --tree-test D:\LeafQA\2.4-tree
```

结果：`RESULT: ALL TESTS PASSED`，退出码 0。覆盖相对路径、压缩包、大小写合并、混合目录、深层路径、自然排序、后代计数、封面选择和未知节点。

Windows 自测：

```powershell
D:\AI\LeafReader.exe --self-test D:\LeafQA\2.4-desktop
```

结果：`RESULT: ALL TESTS PASSED`，退出码 0。新增覆盖：

- 1220px 显示侧栏、960px 隐藏侧栏，跨阈值往返不丢失状态。
- 来源 → 系列 → 02 夜行列车逐级下钻，面包屑和返回状态。
- `Esc` 从目录逐级回到来源和全部来源。
- 未知目录键回退到全部书籍。
- 当前目录内搜索及清空。
- 混合目录同时显示“本目录”书卡和子目录卡。
- “继续阅读”选择当前范围内最近阅读的书。
- 卡片尺寸 164×284，行距 302，连续网格、滚动保持、窄宽度列数。
- 浅色主题、英文界面、空书架截图。

协议回归：

```powershell
D:\AI\LeafReader.exe --protocol-test D:\LeafQA\2.4-protocol
```

结果：`ALL PROTOCOL TESTS PASSED`，退出码 0。

## 截图检查

使用自测生成并逐张检查：

| 文件 | 内容 | 结论 |
| --- | --- | --- |
| `13-tree-wide.png` | 宽屏目录树 + 作品A 混合目录 | 侧栏、面包屑、目录卡、“本目录”书卡均正确 |
| `11-light.png` | 浅色 240 本 | 搜索框、分段控件、卡片、侧栏均为浅色 |
| `10-english.png` | 英文界面 | 侧栏、面包屑、按钮、搜索占位符均英文 |
| `09-continuous-grid-narrow.png` | 960px 窄屏 | 侧栏隐藏，列数自适应 |
| `12-empty.png` | 空书架 | 空状态与导入按钮正常 |

README 截图已更新为 `docs/images/windows-bookshelf.png`（`13-tree-wide.png`）。

## 手工检查

- 1220px / 960px / 2200px 三种宽度下书架列数、侧栏可见性、面包屑返回。
- 深色/浅色切换后搜索框、分段控件、侧栏、卡片颜色。
- 中英文切换后书架与阅读页布局。
- 阅读器打开时侧栏和书架工具栏隐藏，返回书架恢复当前目录。

## 限制

- 当前目录和侧栏展开状态不持久化；重新扫描后保留仍存在的目录，否则退回最近祖先。
- 窄屏阈值为 1120px；窗口更窄时侧栏隐藏，用面包屑导航。
- 英文复数仍沿用简单拼接（例如 `1 pages`），不影响功能。
- Android 仅同步了由 `localization/strings.json` 生成的 `L10n.java`；目录树、面包屑和目录卡尚未落实到安卓，本机没有 Android SDK/JDK，未构建 APK。
