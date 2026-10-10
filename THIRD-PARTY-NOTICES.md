# 第三方组件

项目自己的许可证已选定 MIT；以下组件始终适用各自的许可，不由项目许可证替代。

| 组件 | 版本 / 用途 | 原始来源 | 许可与署名文件 |
|---|---|---|---|
| QRCoder | 1.4.3；Windows 二维码生成 | https://github.com/codebude/QRCoder/tree/v1.4.3 | MIT；third-party/QRCoder-LICENSE.txt |
| ZXing core | 3.5.3；Android 扫码 | https://github.com/zxing/zxing/tree/zxing-3.5.3 | Apache-2.0；third-party/ZXing-LICENSE.txt、third-party/ZXing-NOTICE.txt |

随仓库保留依赖 DLL / JAR、对应源码包及完整许可证。ZXing NOTICE 按 3.5.3 上游根目录原样保留，涵盖上游发行版的声明；不表示本应用使用了其所有模块。

JVM 测试另需 org.json:json:20240303；构建说明提供官方 Maven 下载地址，该测试 JAR 不随源码包或 APK 分发。Android 使用系统 org.json。JDK、Android SDK、.NET Framework 是外部构建或运行环境，不打进仓库。

发布 EXE / APK 时同时提供本文件和 third-party 下的许可证 / NOTICE，不能只发布裸二进制而丢掉分发声明。

此次未从 EhViewer、Mihon、OpenComic、Komga 或 YACReader 引入代码；相似项目调研不表示代码继承关系。后续如复用第三方代码，须记录具体来源、版本、修改及其许可。
