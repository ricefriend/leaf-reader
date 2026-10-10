# 构建、运行和验收

## Windows

Windows 10 / 11，.NET Framework 4.8+。桌面 UI、文件句柄边界验证和 DPAPI 是 Windows 专用实现。没有验证 macOS 或 Linux。

```powershell
& .\build.ps1
```

EXE 生成在源码目录上级。QRCoder DLL 嵌入 EXE，运行无需额外 DLL、Java、Android SDK 或 .NET SDK。

自测必须使用专用目录，会生成 / 覆盖同名样例文件，不使用用户实际阅读状态：

```powershell
$app = Join-Path (Split-Path $PWD -Parent) 'LeafReader.exe'
$qa = 'C:\LeafQA\desktop'
$p = Start-Process $app -ArgumentList '--self-test', $qa -WindowStyle Hidden -PassThru
$p.WaitForExit()
Get-Content (Join-Path $qa 'test-results.txt')

$qa = 'C:\LeafQA\protocol'
$p = Start-Process $app -ArgumentList '--protocol-test', $qa -WindowStyle Hidden -PassThru
$p.WaitForExit()
Get-Content (Join-Path $qa 'protocol-results.txt')
```

对于目录越界真实联接点测试，在专用协议 QA 目录中建立 `authorized 中文 # & 空格\junction` 指向同 QA 目录中的 `private`，测试会验证拒绝该链接。不要把测试路径指向个人目录。

测试返回码 0 才表示通过。文件句柄检查使用 GetFinalPathNameByHandle，受限沙盒可能拒绝该 API；本次真实验证在正常权限的 Windows 进程中运行，未绕过最终路径检查。

## Android

详见 [android/README.md](android/README.md)。本次生成的是开发签名、可安装测试 APK。Android 原生客户端的依赖和源码都在现有源码树的 android 目录中。

## 第三方依赖

| 依赖 | 版本 | 用途 | 许可 |
|---|---|---|---|
| QRCoder | 1.4.3 | Windows 生成二维码 | MIT |
| ZXing core | 3.5.3 | Android 扫码与 QR 互通验证 | Apache-2.0 |

依赖 DLL / JAR、许可和对应源码包已提供。官方来源：[QRCoder 1.4.3](https://github.com/codebude/QRCoder/tree/v1.4.3)、[ZXing 3.5.3](https://github.com/zxing/zxing/tree/zxing-3.5.3)。SDK / JDK 位于开发工作目录，不包含在源码发行包，运行应用也不需要它们。

## 手工实机验收清单

1. 在电脑上点击“共享”，只选择具体图片目录，开启服务。
2. 电脑和手机连接同一可信 Wi‑Fi，安卓安装测试 APK。
3. 手机选择“服务器 → 扫码配对”，或输入电脑显示的局域网地址和短码。
4. 浏览授权根，打开书籍，用左右方向 / Webtoon 阅读，缩放查看细节。
5. 返回书架、关闭应用再打开，确认恢复文件及页内位置。
6. 让电脑停止共享或休眠，确认错误提示；恢复共享后重试连接。
7. 用另一台 Windows 电脑在“服务器”中配对同一服务并阅读。
8. 更新文件后刷新，确认缓存版本改变；重置配对凭据，确认旧客户端必须重配对。
9. 用真机系统目录选择器授权手机漫画目录，确认横竖屏、返回和 CBZ 阅读。
10. 如果使用 VPN / HTTPS，检查证书和代理、保持只读授权边界，不开放公网裸 HTTP。

当前自动化结果不能替代不同厂商真机、相机和真实网络的这份验收。

## 2.3 双语与发布签名

UI 文本的单一来源是 localization/strings.json，tools/sync-localization.py 生成 L10n.cs 和 android/src/org/leafreader/L10n.java。常规构建无需 Python。新增或修改文本后运行同步脚本；外部翻译批次先用 tools/import-localization.py 校验，再加 --apply 导入。完整导出任务见 localization/translation-request.json。

安卓私密签名构建：先在当前进程安全地设置 LEAF_SIGNING_PASSWORD 环境变量，再执行：

```powershell
& .\android\build.ps1 -SdkRoot C:\Android\Sdk -JdkHome C:\Java\jdk-17 -SigningKeystore C:\Private\leaf-reader-release.p12 -SigningAlias leafreader -OutputPath C:\Release\LeafReader-Android-2.3-preview.apk
```

签名密码由 apksigner 从环境读取，不写入构建脚本或命令参数。完成后清除该进程环境变量。密钥及其密码必须独立离线备份，永不提交仓库。不要在正式构建中加 -WithTests；该开关加入测试 instrumentation、测试目录提供器与 debuggable 属性。公开预览 APK 不含这些测试组件。

2.3 的实际检查结果见 VALIDATION-2.3.md，换签名操作见 MIGRATION.md。默认无 SigningKeystore 的构建是开发签名；从公开源码首次生成的开发密钥无法覆盖维护者历史安装。

翻译导入器回归：python tools/test-localization.py（15 项专用校验 fixture，不改写应用目录）。

