# Leaf Share / v1

两个平台使用同一 HTTP 协议。Windows 服务实现位于 `ShareServer.cs`，Windows 客户端位于 `Sources.cs`，Android 客户端位于 `android/src/org/leafreader/Api.java`。

## 身份、授权和边界

服务实例首次配置产生持久化 `serverId`；每个明确选定的共享目录产生持久化 `rootId`。书籍和文件 ID 为 rootId 与相对名称组合的 SHA-256，不接受客户端文件路径。文件版本来自文件长度、最后写入时间和压缩包条目名称；压缩包修改会更新所有条目的版本。

所有 ID 是不透明字符串。文件更新不会改变同路径文件 ID；移动 / 改名文件或删除并重新添加共享根会改变相应身份。客户端进度保存文件 ID，不只保存数组下标。各客户端自己保存进度，不同步进度。

`GET /v1/info` 与 `POST /v1/pair` 无需 Bearer。其他接口需要 `Authorization: Bearer <token>`。配对码 8 位数字、10 分钟有效，每个地址最多 5 次 / 分钟尝试。成功配对返回独立的 256 bit 随机令牌。服务只保存令牌哈希；Windows 客户端使用 DPAPI CurrentUser，Android 使用 Keystore AES-GCM 保存凭据。令牌持续有效直至服务端重置配对凭据。客户端移除服务器只清除该客户端的配置，服务端重置会撤销全部旧令牌。

服务默认关闭，只有明确点击开启才监听。监听 IPv4，端口默认 8765。接受回环和私有地址连接，拒绝公有来源；不会建立 UPnP、NAT-PMP 或公网端口映射。HTTP 不提供加密，只用于可信局域网 / 私有 VPN。跨网络使用 VPN 或带有效证书的 HTTPS 反向代理，反向代理将 `/v1/*` 转发到该服务根地址，并保留 Authorization。客户端拒绝 HTTP 公有服务地址，不关闭证书校验，不跟随携带凭据的 HTTP 重定向。

仅提供读取。禁止选择整块磁盘。扫描不进入子目录重解析点；打开文件时逐级拒绝符号链接 / 联接点，并检查已经打开的文件句柄的实际路径仍位于授权根内。压缩包条目只通过 ZipArchive / ZipFile 读取，不向文件系统解压条目路径。

## 接口

| 方法与路径 | 结果 |
|---|---|
| GET `/v1/info` | `{protocol:1,serverId,name,formats,preview,readOnly:true}` |
| POST `/v1/pair` | 请求 `{code,client}`；响应 `{serverId,token}` |
| GET `/v1/roots` | `{roots:[{id,name}]}`，不泄露磁盘绝对路径 |
| GET `/v1/directories?rootId=...` | `{directories:[{id,rootId,parentId,name}]}`；省略 rootId 返回全部授权树节点 |
| GET `/v1/books` | `{books:[{id,rootId,rootName,relativePath,name,createdTicks,pageCount,cover}]}`，自然排序 |
| GET `/v1/books/{id}/pages` | `{files:[FileMetadata...]}`，自然排序 |
| GET `/v1/files/{id}/meta` | FileMetadata |
| GET `/v1/files/{id}/thumbnail?w=256&version=...` | 已按 EXIF 方向处理的 JPEG；宽度最多 512 |
| GET `/v1/files/{id}/preview?w=1600&version=...` | 已按 EXIF 方向处理的 JPEG；最多 8192 宽，目标最多 8 MP |
| GET `/v1/files/{id}/original?version=...` | 原始图片字节；CBZ / ZIP 返回单个图片条目，不返回整个压缩包 |

`FileMetadata` 为 `{id,name,version,width,height,format}`。宽高为 EXIF 方向处理后的逻辑尺寸。`createdTicks` 是十进制字符串形式的 .NET UTC ticks，Android 使用 `(ticks - 621355968000000000) / 10000` 转成 Unix 毫秒。

版本查询参数可选；客户端图片请求总是携带元数据中的版本。发现文件更新时返回 409，客户端刷新后再打开。`/books` 会更新现有封面的版本；新增文件、书籍和目录后，需要在电脑共享设置中点击“刷新共享目录”。这是有意的快照目录模式，首版不实现文件系统监视。

`/directories` 返回目录树的平面表示，可按 parentId 遍历。现有两个客户端以直接包含图片的文件夹或压缩包为书籍展示。Android 的“浏览授权目录”按 rootId 筛选；不是远程系统文件管理器。

接口只接受 GET 和配对 POST；请求体最多 8192 字节，头部最多 32768 字节。服务最多 4 个并发连接，不支持上传、删除、修改、分块请求体、HTTP Range、多路复用或 CORS。桌面与安卓均使用原生 HTTP 客户端。

## 错误

错误统一返回 JSON：

```json
{"error":{"code":"AUTH_REQUIRED","message":"授权失效，请重新配对"}}
```

| HTTP | code | 恢复方法 |
|---|---|---|
| 400 | BAD_REQUEST | 检查协议 / 地址 |
| 401 | AUTH_REQUIRED / PAIR_FAILED / PAIR_EXPIRED | 重配对 / 核对配对码 / 重新生成配对码 |
| 403 | OUTSIDE_ROOT | 资源不在授权边界，不能访问 |
| 404 | NOT_FOUND | 文件 / 目录已删，刷新来源 |
| 405 | READ_ONLY | 不支持写操作 |
| 409 | STALE_VERSION | 文件更新，刷新后重新打开 |
| 415 | UNSUPPORTED_FORMAT | 安装兼容系统解码器或转换图片 |
| 429 | PAIR_RATE_LIMIT | 等待一分钟 |
| 500 | READ_FAILED | 检查文件、共享状态或重连 |

连接拒绝、超时和休眠没有 HTTP 响应；客户端提示检查开启状态、地址 / 端口、网络及电脑休眠。已缓存图片可继续在当前阅读页显示；重新打开远程书籍仍需要服务端元数据连接。缓存不保证离线下载完整书籍。

## 缓存与图像

缓存键包含 serverId、fileId、version、variant 和请求宽度。不同服务器的同名文件不会共用缓存。桌面远程磁盘缓存 256 MiB；安卓持久图片缓存 256 MiB，内存 Bitmap 缓存 48 MiB，另有当前可见位图与最多两个区域解码任务。清理入口在各客户端的服务器管理中。

目录只请求缩略图。Windows 按阅读尺寸请求不同分辨率的预览，服务端最大 8 MP；桌面原有目标位图限制仍为 16 MP。Android 优先使用最多 2 MP 预览；缩放或超长图时只缓存当前图片原始文件，再通过 BitmapRegionDecoder 解码当前可见区域，单块目标最多 1.5 MP。不复制整个目录。原图下载上限 200 MiB，下载中有临时文件，因此缓存目标不等于整个进程内存或瞬时磁盘峰值。

JPEG / PNG 等能区域解码的格式支持该细节流程；其他格式可能仅使用兼容预览。Android 本地 GIF 为首帧，本地 TIFF 不支持；远程 TIFF 可由 Windows WIC 转为 JPEG 预览。Windows WebP 依赖 WIC 解码器，Android 使用系统 BitmapFactory 支持的静态格式。
