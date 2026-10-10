# 相似项目调研与 GitHub 开源准备

调研时间：2026-10-10。依据各项目官方仓库与文档，不代表逐项安装验收，也不能证明不存在其他类似项目。

## 结论

有能覆盖主要使用场景的开源项目 / 组合，不宜宣传“首个”或“没有替代品”。Komga + Mihon 是明确的全开源近似方案；YACReader 的电脑书库服务与手机扫码流程最接近，但官方 GitHub 仓库明确仅为桌面源码，未确认 Android 端公开许可 / 源码。

| 项目 | 重合功能 | 与当前项目的主要差异 / 未确认点 |
|---|---|---|
| [Komga + Mihon](https://komga.org/docs/guides/mihon/) | 自建电脑漫画服务；Android 客户端；多个服务器；浏览阅读与进度同步 | 服务端与手机阅读器是两个项目，使用扩展连接；不是现有 Windows 轻量阅读器中一键启用的同套客户端 / 共享服务 |
| [YACReader](https://android.yacreader.com/user-guide/) | 桌面阅读 / 书库服务；Android 远程按需阅读；二维码 / 地址连接；多种方向、连续阅读、双页 | [公开仓库](https://github.com/YACReader/yacreader)仅声明桌面源码，GPL-3.0；Android 开源状态未确认，不把整个生态列为全部开源 |
| [OpenComic](https://github.com/ollm/OpenComic) | 桌面母目录、漫画 / Webtoon / 双页、书签、滑动；SMB / WebDAV 等远程来源 | Electron 桌面项目，官方构建列表为 Windows / macOS / Linux；未找到配套 Android 原生客户端或与本项目相同的内置配对服务 |
| [LANraragi + 外部客户端](https://github.com/Difegue/LANraragi/blob/dev/tools/Documentation/advanced-usage/external-readers.md) | 自建漫画服务、API、逐页流式读取、Android / 桌面客户端、多个服务器 | 以压缩包归档 / 元数据管理为中心，服务与客户端是分别部署的项目；不是简单目录分享型一体软件 |
| [Koharia](https://github.com/Mister-album/Koharia) | 原生 Android、自建服务与本地库、多种服务器、阅读进度和缓存 | 主要作为现有服务的客户端；不提供我们这套 Windows 本地阅读器兼共享服务的实现。功能仍在迭代，以官方文档 / Releases 为准 |

“拼前页 / 拼后页退出恢复原位置”在以上所查文档中未找到完全相同的操作描述；只能标记未确认，不能据此断言没有。

## 建议的项目定位

“栞 / Leaf Reader：面向本地图片目录的轻量 Windows 和 Android 漫画阅读器，电脑一键只读共享，手机扫码按需阅读。”

核心卖点可以是：无云账号、现有文件夹直接使用、原生 WPF / Java、轻量安装包、临时跨页拼接、电脑也可作为远程客户端。优先完善易用性与稳定性，不为区分竞品引入无关功能。

真实限制须保留：桌面目前仅 Windows；协议目前是 Leaf Share v1，不支持 Komga / OPDS / SMB 互通；进度各端独立；手机最新手势已在模拟器验证，真实手机连接由使用者确认，尚无完整真机性能基准；软件仍为测试版本。

## 已完成的开源基础准备

- 加强 .gitignore，排除签名密钥、构建产物、用户状态与临时配对信息。
- 添加 THIRD-PARTY-NOTICES.md；保留 QRCoder MIT、ZXing Apache-2.0 文本，并补充 ZXing 3.5.3 上游 NOTICE。
- 添加贡献说明、安全反馈说明、Issue / PR 模板。
- 添加 prepare-public-source.ps1，以独立 ZIP 生成不含密钥、用户数据与构建目录的源码候选包，保留当前工作目录与本地开发密钥。
- 从候选 ZIP 解压到独立目录，Windows EXE 和 Android APK 都重新构建成功；Android 在没有现有密钥时生成新测试签名，v2 / v3 签名校验通过。未对新签名包重复设备测试。
- 检查源码和说明中的常见凭据字段及机器路径；此次未发现固化在应用源码中的真实访问令牌、配对码或个人账号。该静态检查不是全面安全审计。

## 维护者决策与当前状态

名称 栞 · Leaf Reader、仓库 leaf-reader、作者 ricefriend、MIT、公开测试版源码与两端安装包均已确认。Issues / PR 尽力维护，不承诺时限。2.3 已导入外部 AI 译文、增加设置与 Ko-fi，并完成加密备份 / 新签名一次迁移验证。

最新待办见 RELEASE_CHECKLIST.md，验证边界见 VALIDATION-2.3.md。公开源码候选包已排除签名密钥及运行状态，仍需维护者独立保存签名密码与证书并做实机验收。维护者已完成验收并授权发布至 ricefriend/leaf-reader；不要上传整个 workspace。
