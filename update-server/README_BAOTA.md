# dogeplay 宝塔更新服务

此源码目录只包含部署示例。*.example.json 是结构说明，不能直接作为有效更新清单。真实包、哈希与清单由根目录的 make-update.ps1、prepare-download.ps1 生成到上一级 app/update-server；先完成构建及打包，再上传该运行目录下生成的文件。

## 第一次部署

1. 在宝塔新建一个站点，建议启用 HTTPS。
2. 把本目录中的 `manifest.json`、`announcement.json`、`files/` 和本说明文件上传到站点根目录。`announcement.json` 是维护者制作下一次补丁时保留公告的工作文件，也可以一起放在网站上。
3. 确认浏览器可以直接打开：

   ```text
   https://你的域名/manifest.json
   ```

4. 把这个地址填入客户端目录中的 `update-config.json` 的 `manifestUrl` 字段，然后重新发布一次客户端：

   ```json
   {
     "manifestUrl": "https://你的域名/manifest.json"
   }
   ```

宝塔不需要安装 PHP，也不需要数据库。只要站点可以提供 JSON 和 ZIP 静态文件即可。若使用 Nginx，请确认没有把 `.json` 或 `.zip` 拦截；若使用 CDN，请关闭更新文件的旧缓存或刷新缓存。

如果 ZIP 下载被拦截，可把 `BAOTA_NGINX.conf` 中的两个 `location` 片段复制到宝塔“网站 → 设置 → 配置文件”的 `server { ... }` 内，保存并重载 Nginx。当前站点使用 88 端口，服务器和阿里云安全组需放行 TCP 88；正式使用建议绑定域名并启用 HTTPS，原始 IP 的 HTTP 地址适合先做连通性测试。

## 发布下一次增量更新

每次客户端改动后，重新编译 `app`，制作一个只包含需要替换的文件的 ZIP。ZIP 内必须直接放相对路径，例如：

```text
LumaKeys.exe
BrotliSharpLib.dll
Microsoft.Web.WebView2.Core.dll
Microsoft.Web.WebView2.Wpf.dll
WebView2Loader.dll
scripts/number-ocr.ps1
```

不要放入 `data/`、用户曲库、下载缓存、Cookie、Token 或日志。

然后计算 ZIP 的 SHA-256，并更新 `manifest.json`：

```json
{
  "app": "LumaKeys",
  "version": "2026.10.13.2",
  "notes": "本次更新说明",
  "packageUrl": "files/LumaKeys-2026.10.13.2-patch.zip",
  "sha256": "这里填写 ZIP 的 64 位十六进制 SHA-256",
  "size": 123456,
  "announcement": {
    "id": "2026-10-10-notice-1",
    "title": "维护公告标题",
    "content": "这里填写公告正文。客户端点击‘公告 / 更新’即可看到。",
    "publishedUtc": "2026-10-10T00:00:00Z"
  },
  "files": [
    "LumaKeys.exe",
    "MainWindow.xaml"
  ]
}
```

公告可以单独更新：只修改 `manifest.json` 里的 `announcement`，保持 `version`、`sha256`、`size` 和 `packageUrl` 不变，客户端就会显示新公告而不会下载补丁。每次公告使用新的 `id`，方便以后区分公告版本。

客户端会先校验 SHA-256，再关闭当前程序，由临时更新进程替换关键文件并重新启动。更新过程不会覆盖 `app/data`，也不会读取或上传本地曲库、浏览器 Cookie 或登录 Token。

版本号使用四段数字，例如 `2026.10.13.2`。只有服务器版本高于客户端版本时才会提示更新。

制作补丁前先修改 `src/UpdateClient.cs`（仓库中为 `src/UpdateClient.cs`）里的 `CurrentVersion`，编译后再调用 `make-update.ps1`；脚本会核对编译版本与清单版本一致，避免更新后仍重复提示更新。

## 访问检查

在浏览器中分别检查：

```text
https://你的域名/manifest.json
https://你的域名/files/LumaKeys-2026.10.13.2-patch.zip
```

第二个地址应当能够下载 ZIP。客户端更新地址建议使用 HTTPS；正式发布时不要使用带登录 Cookie、Token 或后台管理路径的地址。

客户端每次启动都会自动检查更新并刷新公告，公告显示在右侧作者下方。点击“查看全文 / 刷新”可查看完整公告并重新读取服务器内容；顶部“检查更新”可手动检查新版。首次使用会打开可一键跳过的教程，顶部“使用指南”可重新查看。

从 2026.10.13.2 起，界面加密嵌入 EXE，不再更新外部 MainWindow.xaml，也不附带应用源码。补丁含 assets/release 的当前授权及说明，新客户端会同步到根目录。旧版本已发出的源码与授权不能通过本次更新收回。

## 固定下载链接

完整客户端下载入口：http://47.113.189.245:88/download
直接文件地址：http://47.113.189.245:88/files/LumaKeys-latest-win-x64.zip

首次添加下载：将 download 文件夹、files/LumaKeys-latest-win-x64.zip 和 latest.json 上传到现有站点根目录；files 文件夹已有增量补丁时保留它们，不要删除整个目录。download/index.html 自动转到完整客户端，不需要 PHP 或数据库。浏览器将 /download 转到 /download/ 属于正常目录处理。

如需 /download 直接返回 ZIP，可在宝塔“网站 → 设置 → 配置文件”的现有 server 块中加入 BAOTA_NGINX.conf 末尾两个下载 location，保存并重载。这个配置是可选项，不要用整个配置片段覆盖站点原配置。

今后 package-release.ps1 完成打包和检查后会自动调用 prepare-download.ps1，将最新完整客户端下载文件准备到“宝塔直接上传”。也可以单独运行 prepare-download.ps1 使用现有的最新发布包，不需要重新编译。

上传新版时，先上传并覆盖 files/LumaKeys-latest-win-x64.zip，再上传 latest.json；如果同时发布客户端增量更新，先上传版本补丁，再覆盖 manifest.json。固定下载链接始终不变。服务器仅返回已上传的完整包，不会从增量补丁自动生成完整客户端。

latest.json 记录完整包版本、大小和 SHA-256，独立于用于增量更新和公告的 manifest.json。不要拿约 800 KB 的增量补丁作为首次安装包；完整包包括识谱引擎和其他依赖。
## 五线谱识别故障排查

客户端的 Audiveris 单次识别最长等待 15 分钟。多页、高清、复杂钢琴谱可能需要较长时间；程序会保持界面响应，并把标准输出和错误输出保存到 `app/data/omr/时间-随机目录/recognition.log`。如果仍然失败，请让客户提供：原始五线谱图片或 PDF、软件版本、界面显示的完整错误文字，以及最近一次 `recognition.log`。如果文件包含个人信息，可先裁掉标题、姓名和路径；不要提供账号、Cookie、Token 或整个 `data` 文件夹。

不同电脑识别结果不同，优先检查：ZIP 是否完整解压（必须包含 `tools/audiveris/Audiveris/Audiveris.exe`）、软件是否放在可写文件夹、图片是否为清晰的每页谱面、以及客户是否在 15 分钟内手动关闭了导入窗口。五线谱的“5 分钟歌曲长度”本身不是曲谱时长限制，实际耗时主要取决于页数、图片分辨率、谱面复杂度和电脑性能。

## dogeplay 2026.10.15.2

软件已改名 dogeplay。默认下载入口现在提供 files/dogeplay-setup.exe 安装包，免安装版为 files/dogeplay-latest-win-x64.zip。首次默认安装到 D:\dogeplay，可自行更改。旧版增量更新仍使用兼容的协议标识和过渡文件名，启动后会改名为 dogeplay.exe 并保留同目录曲库。

如果以前配置了 /download 直连旧 ZIP 的 Nginx location，请按 BAOTA_NGINX.conf 把对应 try_files 路径改成 /files/dogeplay-setup.exe，同时修改 Content-Disposition 文件名和 MIME 类型。没有自定义 location 时覆盖 download/index.html 即可。
