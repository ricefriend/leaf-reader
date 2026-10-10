# 书架来源分组 / Bookshelf source grouping

Windows：书架搜索行选择“平铺书架”或“按来源分组”。Android：点击“书架展示”。默认继续平铺，选择自动保存。

分组层使用按名称自然排序的第一本书的封面，并显示书籍数量；点击进入该来源。“‹ 全部来源”、电脑 Esc 或 Android 系统返回可回到上层。从阅读器返回时保留当前来源。搜索在当前层生效，在来源层可用书名筛选来源。

本地来源按导入母文件夹识别，远程按服务器稳定 ID + 授权目录 ID 识别，同名来源不会合并。Windows 重叠导入只展示每本书一次，归入最具体的导入位置；Android 使用已授权的 SAF 树 URI，不申请额外磁盘权限。压缩包跟随其导入来源。

来源层按创建时间排序使用该来源内最新一本书的时间；子书架仍按每本书本身的时间排序。最近阅读／未读筛选使用子书籍的记录，不为来源封面创建虚假的阅读记录。空目录和不可用来源没有可展示书籍时不产生封面，可在来源管理查看／重新连接。

The source view uses the naturally first book cover and opens the existing reader. Source identities distinguish local directory grants and remote server/root IDs. No server protocol change or file copy is required. Source creation sorting uses the latest child book timestamp.

验证：Windows 现有界面测试增加来源归属、重叠目录、同名远程目录、搜索、进入／返回、切回平铺及保存偏好检查。Android JVM 模型测试增加 SAF 和远程来源隔离及封面选择检查；可选 DeviceTests 增加真实来源点击和返回检查。没有连接安卓设备时，APK 编译和 JVM 检查不等于手机界面已验收。
