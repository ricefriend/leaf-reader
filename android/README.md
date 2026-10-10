# Android 构建与测试

原生 Java，最低 Android 8.0 / API 26，目标 API 35。没有 WebView 或 Gradle 插件依赖。运行只需系统图像解码器和标准 Android API，扫码依赖内置 ZXing core 3.5.3。

## 构建测试 APK

安装 JDK 17 与 Android SDK，SDK 包含 `platforms;android-35`、`build-tools;35.0.0`、`platform-tools`。官方入口：[JDK](https://adoptium.net/temurin/releases/?version=17)、[Android 命令行工具](https://developer.android.com/studio#command-tools)。使用 SDK Manager 安装组件：

```powershell
sdkmanager --sdk_root=C:\Android\Sdk "platforms;android-35" "build-tools;35.0.0" "platform-tools"
```

在本目录执行：

```powershell
& .\build.ps1 -SdkRoot C:\Android\Sdk -JdkHome C:\Java\jdk-17
```

也可设置 `ANDROID_HOME` 与 `JAVA_HOME` 后直接执行 `build.ps1`。构建产物在整个源码目录的上级：`LeafReader-Android-2.3-migration-debug.apk`。构建步骤为 javac → D8 → aapt2 → zipalign → apksigner，脚本验证 v2 / v3 签名。

脚本使用本地已有的 `debug.keystore`，不存在时生成标准开发证书（密码 android）；公开源码不包含任何签名密钥。该证书只适合测试，不能作为正式发布密钥。请保留开发证书以便后续 APK 覆盖安装保留应用数据。正式发行应换成自己管理的签名密钥。

## JVM 规则与 QR 测试

`tests/ModelTests.java` 验证自然排序、630 组跨页组合和进度 / 缓存身份；使用真实 org.json 的 JVM 测试 JAR，不能直接用 Android SDK stub 在 JVM 上运行 JSON。

```powershell
javac --release 8 -encoding UTF-8 -cp json-20240303.jar -d qa src\org\leafreader\Models.java src\org\leafreader\L10n.java tests\ModelTests.java
java -cp "qa;json-20240303.jar" org.leafreader.ModelTests
```

`json-20240303.jar` 从 [Maven Central](https://repo.maven.apache.org/maven2/org/json/json/20240303/json-20240303.jar) 下载，仅供测试，不打进 APK。

`tests/QrRoundTrip.java` 用 APK 相同的 ZXing core 解码 Windows 生成的二维码，验证完整配对链接一致。

## Android 设备测试

构建仪器测试版本：

```powershell
& .\build.ps1 -SdkRoot C:\Android\Sdk -JdkHome C:\Java\jdk-17 -WithTests
adb install -r build\LeafReader-instrumented.apk
```

`-WithTests` 才会添加 Instrumentation、调试标志和受 MANAGE_DOCUMENTS 权限保护的测试 DocumentsProvider。普通 APK 没有这些入口。设备测试会在本项目应用内产生测试目录、图片、CBZ 和临时服务器配置，应在专用模拟器或测试设备运行，不在有个人阅读数据的安装上运行。

在 Windows 源码目录编译桌面端，启动自动生成真实长图的测试服务：

```powershell
Start-Process ..\LeafReader.exe -ArgumentList '--fixture-server','C:\LeafQA\fixture' -WindowStyle Hidden
```

服务默认端口 8876，仅共享 `C:\LeafQA\fixture\library`，将短期配对信息写入 `fixture.json`。Android Emulator 通常用 10.0.2.2 访问主机；真机使用电脑局域网 IP。

```powershell
$fixture = Get-Content C:\LeafQA\fixture\fixture.json -Raw | ConvertFrom-Json
adb shell am instrument -w -e address "http://10.0.2.2:8876" -e code $fixture.code org.leafreader/.DeviceTests
```

向 `C:\LeafQA\fixture\stop` 写入任意文本会停止测试服务。二维码短码 10 分钟有效，超时后重启测试服务。设备测试验证实际 API、原生阅读界面、手势、旋转、Keystore、SAF 内容流、CBZ、区域解码和缓存。读取的样例图片是真实图片文件，测试服务使用正式服务和 API 实现。

本次已在 MuMu Android 12 模拟器执行。真机系统目录授权选择器、相机扫码与 Wi‑Fi 防火墙仍需手工验收。

## Android 2.1 阅读手势

- 新书默认纵向连续阅读；升级保留已保存的模式。旧书可在“模式”选择“纵向连续 · 默认”。
- 上下拖动后松手按速度惯性滚动，轻划可跨多页；再次触碰立即停止。采用原生 OverScroller，低摩擦衰减，每帧只绘制视口图片。UI 自动隐藏会等运动结束，避免改变视口时中断惯性。
- 单页、左右双页跟随手指滑动；超过 20% 屏宽或快速甩动时以 240ms 减速动画进入相邻页，不足阈值回弹。首尾页不能越界。双指操作和放大拖动不会切页。
- “拼前页 / 拼后页”临时按等高比例无缝并排显示两张相邻图片，不受封面 / 横图独页设置限制。右至左模式相应交换位置。可缩放拖动；“结束拼接”或系统返回恢复原阅读模式、页内位置及缩放。首尾边界有提示。拼接不改变持久阅读进度。
- 快速运动期间暂停高清区域请求，继续使用预览，停止后按需解码区域；预加载附近两页，仍沿用原内存 / 磁盘限额和过时请求取消。

2.1 在 Android 12 模拟器通过 42 项设备测试，新增默认模式兼容、动画中间帧、跨多页惯性、触碰停止、前后拼接 / 返回恢复及边界验证。惯性测试复用真实远程 JPEG 构造长列表，测试整体滚动流程；未把模拟器结果作为真实手机帧率保证。真机连接已由使用者确认，更新手势仍需真机体验验收。

## Android 2.2 书架与界面

统一深色 / 浅绿配色、圆角封面和按钮、搜索框及文字层级。ShelfLayout 根据窗口宽度计算列数与统一封面尺寸，封面保持 1:1.4 比例，填满可用宽度，不使用原固定列宽留白。右侧只保留快速滚动条所需空间。

书架仍是统一封面连续网格，不分页。右侧快速滚动条支持拖动或点击轨道跳转；拖动显示对应书名和序号，可直接到最后一本。没有可滚动内容时不绘制滚动条。支持无障碍前后滚动操作。旋转会调整列数并保留当前搜索结果。

本次以 33 个实际共享图片目录验证，在 Android 12 模拟器通过 53 项设备检查，含底端 / 顶部定位、真实滚动条触摸、释放、宽度填充、搜索空态、横竖屏及阅读手势回归。普通 APK 覆盖安装与冷启动成功。仍沿用原开发签名；真机外观和触控体验需安装后确认。FixtureHost 已增加 32 个程序生成的封面目录供复现测试，桌面日常功能未变。
