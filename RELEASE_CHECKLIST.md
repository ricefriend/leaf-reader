# 首次公开发布清单

版本：2.3 preview。维护者已完成本次验收并授权公开发布；仓库 ricefriend/leaf-reader。

## 已确认并落实

- [x] 名称 栞 · Leaf Reader，仓库名 leaf-reader，作者 ricefriend。
- [x] MIT LICENSE，完整源码、Windows EXE、Android APK，公开测试版定位。
- [x] 接受 Issues / PR，尽力维护，不承诺响应时限。
- [x] 第三方许可与来源记录，公开源码排除私密签名、用户状态与漫画。
- [x] 352 条外部译文导入，两端语言选择及设置页。
- [x] Ko-fi gdbkf；爱发电入口暂不展示。
- [x] Android 加密备份与真实换签名迁移验证；保留旧签名迁移包及新私密证书预览包。
- [x] 本地回归、构建及发布包校验，详见 VALIDATION-2.3.md。

## 外部发布前

- [ ] 维护者将私密发布证书及其密码备份到自己控制的离线介质 / 密码管理器；当前 Windows DPAPI 保护文件只能由本机同账户解密，不代替独立离线备份。
- [x] 维护者已确认验收完成；自动化测试的覆盖边界仍见 VALIDATION-2.3.md。
- [ ] 建立 ricefriend/leaf-reader，启用私密漏洞报告，并确定公开发布授权。
- [ ] 执行 GitHub CI / 新机器构建检查；本地通过不等于 CI 通过。
- [ ] 发布时仅上传源码候选包中的文件；EXE / APK / 使用说明 / SHA256 放 Release。不要上传整个开发 workspace。

签名密码、GitHub 密码、访问令牌与个人漫画无需发到对话中。当前仍保留未验收事项，不把本地候选包称为已发布。
