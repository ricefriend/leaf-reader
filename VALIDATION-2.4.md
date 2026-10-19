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

- 1220px 左侧触发区可用、侧栏默认收起，折叠时左侧 28×96 强调色“目录”把手可见；`RevealSidebar` 后 190ms 滑入并停靠；`CollapseSidebar` 后移出屏幕。
- 960px 触发区隐藏，跨阈值往返不丢失状态。
- 阅读时侧栏和书架工具栏隐藏；阅读 UI 自动隐藏/唤出使用淡入淡出与轻微位移。
- 来源 → 系列 → 02 夜行列车逐级下钻，面包屑和返回状态；系列下的叶子目录直接显示为书卡，不再多包一层。
- 两层结构（来源下只有直接含图片的文件夹）只显示“来源 → 书”，叶子目录不生成目录卡。
- 混合目录同时显示“本目录”书卡和直接摊平的叶子书卡。
- `Esc` 从目录逐级回到来源和全部来源。
- 未知目录键回退到全部书籍。
- 当前目录内搜索及清空。
- 混合目录同时显示“本目录”书卡和子目录卡。
- “继续阅读”选择当前范围内最近阅读的书。
- 多页本地文件夹书卡显示 `▦` 按钮；平铺窗口按页加载 16 张缩略图并正确显示页码。
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
| `13-tree-wide.png` | 宽屏目录树（自动展开状态）+ 作品A 混合目录 | 侧栏、面包屑、目录卡、“本目录”书卡均正确 |
| `11-light.png` | 浅色 240 本 | 搜索框、分段控件、卡片、侧栏均为浅色 |
| `10-english.png` | 英文界面 | 侧栏、面包屑、按钮、搜索占位符均英文 |
| `09-continuous-grid-narrow.png` | 960px 窄屏 | 侧栏隐藏，列数自适应 |
| `12-empty.png` | 空书架 | 空状态与导入按钮正常 |
| `14-page-grid.png` | 平铺图片网格 | 16 张缩略图、页码、加载占位均正常 |

README 截图已更新为 `docs/images/windows-bookshelf.png`（`13-tree-wide.png`）。

## 手工检查

- 1220px / 960px / 2200px 三种宽度下书架列数、侧栏触发区、面包屑返回。
- 鼠标移到左侧触发区、移出侧栏后的延时收起与滑入/滑出动画（代码路径与最终位移断言；真机手感仍需实际操作确认）。
- 深色/浅色切换后搜索框、分段控件、侧栏、卡片颜色。
- 中英文切换后书架与阅读页布局。
- 阅读器打开时侧栏和书架工具栏隐藏，返回书架恢复当前目录。

## Android 源码同步

本次同步了 Android 源码，但本机没有 JDK / Android SDK，**没有编译 APK，也没有跑模拟器或真机测试**：

- `Models.java`：`Book.relativePath`、`ShelfNode`、`tree/find/path/descendants/pseudo`，`groups` 改为基于树。
- `Storage.java`：SAF document id 相对来源根计算 `relativePath`，压缩包记录所在目录。
- `Api.java`：远程书读取 `relativePath`。
- `MainActivity.java`：`shelfSource + shelfPath` 状态、面包屑、逐级返回、叶子目录直接作为书、目录卡下钻；系统返回键先退一层目录。
- `AndroidManifest.xml` + `res/mipmap-anydpi-v26/ic_launcher.xml`：自适应图标，背景满幅，去掉旧版白边；同时提供 monochrome 图层。
- `android/build.ps1`：版本改为 2.4.0 / 20400；因为本机没有 SDK，未执行。

这些改动需要后续在有 Android SDK/JDK 的环境编译并做真机验收。**平铺按钮暂未加到安卓**，先按讨论确定入口。

## 限制

- 当前目录和侧栏展开状态不持久化；侧栏默认收起，选中目录后仍可继续浏览，移开鼠标延迟收起。重新扫描后保留仍存在的目录，否则退回最近祖先。
- 窄屏阈值为 1120px；窗口更窄时侧栏隐藏，用面包屑导航。
- 英文复数仍沿用简单拼接（例如 `1 pages`），不影响功能。
- 平铺按钮只对本地、多页、非压缩包文件夹显示；单页、CBZ 和远程书卡暂不提供该入口。缩略图网格一次性创建当前书的全部图片控件，超大单本（数百页以上）仍建议直接在阅读器内翻页。
- Android 目录树源码已同步，但当前环境没有 Android SDK/JDK，未构建 APK、未跑模拟器/真机测试；平铺按钮暂未加到安卓，等待 UI 方案确认。
