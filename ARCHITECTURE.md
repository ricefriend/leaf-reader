# Leaf Reader 2.0 架构与实施记录

## 现有项目检查

本次基于同一目录中的 1.3 源码扩展，没有另建独立演示程序。原项目是 Windows WPF / .NET Framework 4.8 的 C# 桌面应用，没有 Git 元数据、解决方案或 .NET SDK 工程；由 `build.ps1` 调用系统 csc 编译。原窗口、书架、渲染器、配对逻辑和既有自测保留。

原 `Core.cs` 混合文件来源、BitmapImage 解码、数据持久化和翻页。`ReaderView.cs` 负责自绘单页 / 双页 / Webtoon、滚轮、鼠标和缓存；`ShelfGrid.cs` 负责固定封面的连续网格。WPF、Windows DPAPI、文件句柄 API 不能直接用在 Android；自然排序、双页锚点和稳定阅读身份规则可移植。

## 选型

- Windows 继续 WPF / C#，避免重写成熟桌面功能。
- Android 为原生 Java / Canvas / GridView / BitmapFactory / BitmapRegionDecoder，API 26+，目标 API 35。没有 WebView、云账户或第三方存储。
- 自然排序和双页配对算法在 Java 中等价移植，使用相同 630 组边界场景验证。不是复用 WPF 二进制。
- 平台之间复用版本化 HTTP 协议、文件身份与缓存规则。Windows `IImageSource` 和 Android `Images.Source` 将来源访问与阅读界面分开。

## 目录与责任

```text
MangaReader-Source/
  Core.cs                    图片 / 书籍 / 稳定进度 / 自然排序 / 配对
  Sources.cs                 IImageSource、本地 / 远程来源、HTTP 客户端、DPAPI、磁盘缓存
  ShareServer.cs             配置、只读 HTTP 服务、授权、目录边界与图像预览
  NetworkingUi.cs            原 WPF 窗口的共享与服务器管理扩展
  MainWindow.cs              原桌面窗口与书架 / 阅读流程
  ReaderView.cs              原渲染器；增加版本缓存键与取消请求
  ShelfGrid.cs               原固定尺寸连续网格；封面缓存包含版本
  SelfTests.cs               原桌面回归
  ProtocolTests.cs           真实 HTTP、凭据、边界、失效与重连测试
  FixtureHost.cs             仅测试入口，生成真实临时图片服务
  protocol/leaf-share-v1.md   两端共用协议规范
  android/
    AndroidManifest.xml      SAF、网络、可选相机；不申请全盘文件权限
    build.ps1                javac → D8 → aapt2 → zipalign → apksigner
    src/org/leafreader/
      Models.java            自然排序、配对、稳定身份和进度
      Storage.java           SAF 树、CBZ、Keystore、持久化
      Api.java               协议、私有 HTTP 地址、标准证书校验、取消
      Images.java            统一来源、有限缓存、预览与区域解码
      ReaderCanvas.java      四种模式、触摸、缩放、拖动、预加载
      MainActivity.java      本地 / 远程书架、服务器管理、生命周期
      ScannerActivity.java   ZXing 相机扫码
    tests/                   JVM 规则测试、真实 Android 仪器测试与仅测试的 DocumentsProvider
  third-party/               二维码依赖、许可和对应版本的依赖源码
```

## 实施顺序及结果

1. 抽象 Windows 图片来源接口，让 Page 和已有渲染流程处理本地或远程来源。
2. 实现显式开启的服务、根授权、持久标识、配对令牌、兼容图像接口和 Windows 客户端；用实际 TCP / HTTP 测试。
3. 接入原生 Android SAF 目录、单张与 CBZ、原生远程客户端及同一套阅读规则。
4. 加入触摸冲突处理、旧请求取消、相邻预加载、有限缓存、分块读取和生命周期保存。
5. 构建 EXE 与签名 APK，执行 Windows 旧功能回归、协议测试、JVM 规则测试、二维码互通，以及 Android 12 模拟器的实际应用测试。

服务组件与客户端组件不相互依赖。停止共享只关闭监听和共享请求，书架及本地阅读继续工作。通常二者在同一 Windows 程序内，关闭整个程序会停止共享。Android 本次只实现客户端，未实现 Android 共享服务器。

## 验证范围

测试结果随发行包保存。设备测试确实安装并运行 Android 12 模拟器 APK，通过同一模拟器的网络连接 Windows 服务，包含实际配对按钮、书架打开、自然排序、内容 URI 读取、CBZ、手势、旋转、进度、区域解码和清理。

SAF 测试使用系统 DocumentsContract 与受权限保护的测试 DocumentsProvider 读取实际生成的图片；未用真实手机上的系统选择器手工授权。扫码数据由桌面真实二维码生成器与 Android ZXing core 互相验证；未验证真实手机相机取景扫码。

桌面间验证是两个独立服务实例和客户端在同一 Windows 主机上通信，不等于已验证两台物理电脑的防火墙 / Wi-Fi 环境。原图长图测试为 1200×24000 JPEG，预览 / 区域解码阶段采样 PSS 约 86 MiB；这是采样点，不是完整操作的峰值。Bitmap 分配和区域尺寸上限另有断言。

没有宣称 macOS / Linux 桌面版兼容。Android 8–15 的其他系统版本、不同厂商文件提供器、真实 Wi-Fi 断线 / 电脑休眠、HTTPS 代理和 VPN、真实相机以及长时间连续使用仍需实机验收。

## 首版限制

没有自动发现、进度同步、后台下载、跨目录自动续集、远程写接口或公网映射。共享目录目录表是手动刷新快照，新添加 / 删除书籍后在电脑共享设置刷新。原图无断点续传；Android 本地 SAF 压缩包需要缓存单个压缩包以获得随机条目访问，限制 200 MiB。某些提供器缺失修改时间时，版本只靠其可用元数据，手工清理缓存可强制重读。

解码预算不包含系统编解码器的所有临时分配。Android 同时最多两个图像工作线程；大型 / 不支持区域解码的格式可能仅显示预览。Windows 原有极长图以有限分辨率显示，Android JPEG 长图有实际区域解码验证。

参考：[Android SAF 文档](https://developer.android.com/training/data-storage/shared/documents-files)、[BitmapRegionDecoder](https://developer.android.com/reference/android/graphics/BitmapRegionDecoder)、[Android 命令行构建](https://developer.android.com/build/building-cmdline)、[APK 签名](https://developer.android.com/tools/apksigner)。

## 2.3 设置、语言与迁移

两个原生前端复用 localization/strings.json，由 Python 生成静态 C# / Java 目录，运行时无 Python 或网络翻译依赖。语言切换重建控件并复用当前阅读视图。SettingsUi.cs / SettingsActivity.java 管理偏好和仅由用户触发的赞助外链。MigrationBackup.java 实现密码派生和加密信封；Storage 校验并在单次提交中合并状态，访问令牌恢复后重新进入新安装的 Android Keystore。授权目录权限不随备份迁移。
