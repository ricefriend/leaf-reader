# 2.3.2 preview: changes and validation / 更新与验证

## Changes

- Windows large-photo reading: fix prefetch resolution and keep useful in-flight neighbour requests. Adapt preloading to average file size, pixel dimensions and measured load time. The 192 MiB decoded-image cache limit does not bound total process memory. [Details](DESKTOP-PHOTO-CACHE.md).
- Windows / Android bookshelves: flat view or source folders, saved view preference, search, natural ordering and source navigation. Local sources and remote server/root identities remain distinct. [Details](SHELF-GROUPS.md).
- Android remote reading: keep repeated taps on the same open request, cancel obsolete work and covers, allow a separate bounded directory read timeout, show loading stages / elapsed time, and tap bottom failures to retry. Current-page loading and errors stay visible when toolbars hide.
- Windows sharing: cache image metadata by file version; re-read changed files and keep directory-boundary checks. Return a readable `503 SERVER_BUSY` when all four workers are occupied. Clients retry only eligible temporary directory failures with a fixed attempt limit. [Details and limits](REMOTE-OPEN-FIX.md).

## Verified locally

- 116 Windows UI / core checks passed on the 2.3.2 build, including source navigation, scrolling, reading directions, spreads, progress and settings.
- 40 real sharing-protocol checks passed, including authentication, path traversal, an actual NTFS junction, metadata reuse / invalidation, worker saturation and recovery, and Windows-to-Windows reading.
- 16 Android API HTTP transport checks passed against a local HTTP fixture: timeouts, bounded busy retries, authentication failure without retry, cancellation, late responses and connection ownership. Only Android URI encoding is substituted for JVM execution; these checks do not establish Android device UI or network-stack behavior.
- 15 localization importer validation checks passed.
- Android production APK and the expanded instrumentation suite compiled successfully. Production v2/v3 signatures verified and match the official 2.3 signing certificate; version code is 20302.
- The earlier photo-cache implementation passed 27 camera-sized JPEG integration checks at 2.3.1. GitHub CI also runs this suite on release commits.

## Not verified on a device in this update

No Android device was attached during this update. New loading-status visibility, retry taps, repeated taps, background transitions and source navigation have compiled instrumentation checks but have not been executed on a phone for this release. The user's intermittent original failure was not captured in a device log. Identified code defects are fixed; do not treat the transport checks or APK build as proof that every instance of that symptom is resolved.

The user previously confirmed real phone-to-PC connectivity. This update does not add cross-device progress sync, internet port mapping, cloud storage, or verification of non-Windows sharing hosts.

## 中文摘要

本版合并大照片缓存、来源分组和远程打开修复。Windows 116 项、真实共享协议 40 项、Android 本机 HTTP 传输 16 项与翻译导入校验 15 项检查通过。新增安卓仪器测试已编译，当前没有连接手机，因此手机界面、后台恢复和原始间歇失败仍待真机验收。推荐更新两端；官方同证书 APK 可覆盖安装，历史开发 APK 需按 [迁移说明](MIGRATION.md) 操作。
