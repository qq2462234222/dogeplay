# dogeplay

**作者：doge · 免费使用，不收取软件下载、激活或使用费用。**

**仅供学习交流使用。** 本项目自有代码以 MIT License 开源，作者为 doge。软件不收费；第三方服务、组件、曲谱和资源遵守各自条款。

Windows 游戏键盘乐器演奏工具，使用 C# / WPF。支持本地曲库、歌单、图片识谱、MIDI / MusicXML 导入，以及同一曲谱切换多个游戏和乐器方案。

## 获取与运行

- GitHub 源码与发行版：[qq2462234222/dogeplay](https://github.com/qq2462234222/dogeplay) · [下载最新发行版](https://github.com/qq2462234222/dogeplay/releases/latest)。
- 直接下载正式安装包：[dogeplay 安装包](http://47.113.189.245:88/download/)。此地址由作者维护，可能与仓库发行版更新时间不同。
- Windows 安全提示：发布包只有在使用受信任 CA 的代码签名证书签名后，才可能显示已验证发布者。GitHub/Gitee 本身不会替代 Authenticode 签名；自签名证书不能消除 SmartScreen 警告。

## Code signing policy

Free code signing provided by SignPath.io, certificate by SignPath Foundation
(application pending). Signed releases are built from this public repository
and published on the GitHub Releases page. The project maintainer is the
author, committer, reviewer and release approver for the current single-
maintainer project.

Privacy policy: [PRIVACY.md](PRIVACY.md). The application does not transfer
information to other networked systems unless specifically requested by the
user or the person installing or operating it.

- 使用者：推荐下载 EXE 安装包，首次默认安装到 `D:\dogeplay`，可更改位置；没有 D 盘时使用系统盘。也可下载 `win-x64.zip`，完整解压到可写文件夹，双击根目录的 `dogeplay.exe`。
- 维护者：源码、构建脚本和许可证可公开查看；发布包仍不包含本地曲库、设置、Cookie 或其他个人数据。
- Windows 10/11 x64，要求 .NET Framework 4.8。无需安装 Python、Node.js 或 .NET SDK。
- 运行程序会按清单请求管理员权限，以便向以管理员权限运行的游戏发送键盘输入；Windows 会在首次启动或每次启动时显示 UAC 确认。
- 软件不注册、不监听 Esc；Esc 始终留给当前游戏或其他前台应用。F8 开始、F9 暂停、F10 停止；悬浮学习谱打开时，F7 锁定 / 解锁鼠标穿透；演奏时方向键和小键盘方向键用于调整进度。
- 每次启动自动检查更新并刷新右侧公告，顶部“检查更新”可手动检查。更新来自 `update-config.json` 指向的公开 `manifest.json`，使用 SHA-256 校验的关键文件增量包，保留 `data` 曲库和设置。
- 不需要激活码。请勿将任何第三方收费下载页面当成本项目官方渠道。

## 维护者本地构建

在仓库根目录用 Windows PowerShell 执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

默认输出位于源码目录上一级的 `app`，直接运行 `..\app\dogeplay.exe`。重新构建前请关闭旧程序；构建会保留 `..\app\data` 内的个人数据。

`assets/vendor` 包含构建所需的 BrotliSharpLib 和 WebView2 SDK 依赖及许可；它们是第三方依赖，不是本项目的旧版程序。Windows 系统组件不随仓库分发。

## 主要功能

- 数字简谱图片使用 Windows OCR，本机识别后校对；五线谱图片 / PDF 使用包内 Audiveris（含 Java 运行时）。
- 导入 MIDI、MusicXML、MXL、dogeplay JSON、SkyStudio 曲谱；可多选文件导入。
- 收藏、歌单、搜索、排序、最近播放、批量导出 / 删除和可调整顺序的播放队列。
- 音乐标题、来源、BPM、时长均可点击列标题排序，再次点击切换倒序。
- 置顶学习谱显示简谱与游戏按键，保留和弦，支持完整长曲、同步高亮和独立练习走谱。窗口可拖动、缩放、调整透明度及鼠标穿透；独立练习不发送游戏按键。无边框窗口或窗口模式适合使用，不能保证覆盖独占全屏。
- 试听、暂停 / 继续、进度跳转、上一曲 / 下一曲、顺序 / 循环 / 随机播放。
- 游戏和乐器分别选择。每个乐器独立保存键位、音域、按键时长、倒计时和演奏偏好。
- 在右侧“演奏设置”顶部可设置北京时间（UTC+8）目标时刻，让多台设备按同一目标时间开始；精准程度取决于各设备系统时间同步和 Windows 输入调度，不能保证绝对零误差。
- 可按自然音阶配置，也可逐键指定 MIDI 音高以支持半音或不连续音域；支持 1～47 个独立字母、主键盘数字和符号键，不支持修饰键组合、鼠标乐器和自动鼓点编配。
- 同一曲谱切换乐器无需再次导入；游戏音域适配只影响游戏输出，不改写原谱。试听音色为系统 MIDI 近似音色，游戏内乐器须手动选择。
- 在线查找 OpenScore、社区曲谱、Josh 动漫曲谱和 PiaStudy；个人登录由网站界面处理，不从用户浏览器提取 Token。

## 可选组件

**五线谱识别：** 运行包固定附带 Audiveris 5.11.0 及 Java 运行时，使用相对位置 `tools\audiveris\Audiveris\Audiveris.exe`。不依赖系统安装目录或当前工作目录。正常时位置锁定，只有引擎缺失、不完整或运行失败时，“导入文件”窗口才显示修复入口。整包移动后无需修改引擎位置。MusicXML / MIDI 导入不需要识别引擎。

源码不保存第三方运行工具目录。首次构建前运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\prepare-engine.ps1`，它从 Audiveris 官方发布页下载并校验 5.11.0，解压到上一级 `app/tools/audiveris/Audiveris`。构建和发布脚本会把完整引擎复制到输出包；缺少引擎时停止构建，避免发出不完整的包。

**网站登录窗口：** 需要 [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)。已安装该运行时的电脑不必重复安装；网站的账号和下载权限由网站自己管理。

## 游戏演奏与限制

游戏列表提供独立的 **永劫无间大师模式**，默认乐器为“古筝 · 大师 36 键”。覆盖 C3～B5（MIDI 48～83），共 21 个自然音和 15 个半音。旧用户启动新版时会自动补入此方案，保留原有方案、选项和曲谱；请自行在游戏中解锁并切换大师模式。

下表按音高递增排列；数字为主键盘上排数字，符号为对应物理键，无需按 Shift：

| 音域 | C | C♯ | D | D♯ | E | F | F♯ | G | G♯ | A | A♯ | B |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| C3～B3 | Z | S | X | D | C | V | G | B | H | N | J | M |
| C4～B4 | , | L | . | ; | / | Q | 2 | W | 3 | E | 4 | R |
| C5～B5 | T | 6 | Y | 7 | U | I | 9 | O | 0 | P | - | [ |

键位依据：[《永劫无间古筝键位》](https://www.bilibili.com/video/BV1pA4m1L7L8/) 的公开音名对应表（2024 年发布，2026-10-08 查阅），并参考 [大师模式与 EOP 改键教程](https://www.bilibili.com/video/BV1LM4m1f79N/)。这是古筝大师预设，非所有乐器通用的官方保证；自定义改键或后续版本变化可在乐器编辑器修正。未在游戏中验证本次新增方案。

键位编辑器中逗号、分号须分别填写 `Comma`、`Semicolon`，避免被当作旧配置的分隔符；其他符号也提供 `Period`、`Slash`、`Minus`、`LeftBracket` 等名称。程序将符号转换成 Windows 对应按键后再发送，不能直接把符号字符编码当作按键编号。

1. 在游戏里进入对应乐器界面。
2. 在软件中选择游戏、乐器方案和目标窗口，核对实际键位与音高。
3. 先试听检查曲谱，再开始游戏演奏。F8 开始，F9 暂停 / 继续，F10 停止。
4. 前台模式切出游戏会暂停，切回后按 F9 继续。游戏以管理员运行时，软件需要匹配运行权限。

后台窗口消息模式仅对接受这类消息的游戏有效。永劫无间的用户实测未能实现可靠后台演奏；发送成功不代表游戏收到，不能承诺边操作其他应用边自动演奏。

程序按曲谱 BPM 演奏，同拍和弦同步输出。游戏缺失音高、音域和输入采样限制会影响还原度；自动适配不能无损还原任意钢琴曲。图片 OCR 需要人工校对，MusicXML 暂不展开反复记号及速度变化。

## 本地项目目录

| 路径 | 内容 |
|---|---|
| `src/` | 程序源码、XAML 界面和现有检查代码 |
| `scripts/` | 本机 OCR 脚本与开发检查脚本 |
| `assets/` | 指定的公开曲谱索引、CC0 示例、第三方依赖与许可证 |
| `build.ps1` | 源码构建 |
| `package-release.ps1` | 生成干净运行包 |
| `make-update.ps1` | 从上一级 `app` 制作关键文件增量更新包 |
| `update-server/` | 可直接上传宝塔站点的静态更新目录 |
| `RELEASE_README.md` | 面向最终使用者的说明 |
| `THIRD_PARTY_NOTICES.md` | 第三方来源及许可说明 |

上一级的 `app`、`app-update`、`releases`、下载、缓存、测试输出和个人备份均不应作为源码或个人数据对外分享。

## 生成发布包

首次打包先准备识谱引擎和安装包编译器，工具版本及下载校验固定在脚本中：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\prepare-engine.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\prepare-installer.ps1
```

获得受信任的代码签名证书并将其安装到当前 Windows 用户的“个人”证书存储后，可用证书指纹给程序和安装器签名。私钥不作为命令行参数传递：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\package-release.ps1 -CertificateThumbprint <证书指纹>
```

签名前需要安装 Windows 10/11 SDK 的 Signing Tools（提供 `signtool.exe`）。证书必须包含 Code Signing 用途和私钥；普通 HTTPS 网站证书、免费自签名证书都不能让 Edge 信任下载包。

只生成仓库发行版、不改动本地宝塔下载站内容时，给下方命令追加 `-SkipDownloadSite`。`tools/` 和运行数据均不进入 Git 仓库。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\package-release.ps1
```

带受信任证书发布时，将上面的命令改为 `-CertificateThumbprint <证书指纹>`。不要把 PFX、私钥或密码提交到仓库。

脚本从全新目录编译，按明确的文件清单生成运行包，不生成源码包。ZIP 逐项核对内容与 SHA-256；输出到上一级 `app/releases`，附检查报告和校验文件。所有文件均扫描常见凭据格式及本机个人路径，发现可疑项会停止；这不能替代对任意未知格式秘密的人工审查。

发布时选择最新生成的 ZIP。请勿直接压缩使用过的整个工作目录，也不要将个人曲库或网页登录状态提交到公开仓库。

### 配置客户端更新

将 `update-config.json` 中的 `manifestUrl` 改成宝塔站点的公开地址，例如 `https://你的域名/manifest.json`，再生成发布包。更新脚本会把宝塔上传目录和增量包放在上一级 `app/update-server`；部署说明见源码目录的 `update-server/README_BAOTA.md`。只修改上一级 `app/update-server/manifest.json` 的 `announcement` 字段就能发布公告，不会触发文件更新。维护者制作下一次更新时可运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\make-update.ps1 -Version 2026.10.10.3 -Notes "更新说明"
```

## 个人数据

- 使用后的曲库、设置、歌单、历史和下载缓存位于上一级 `app/data`，只留在本机。
- 网站 Cookie 和站点登录数据位于当前 Windows 用户的 `%LOCALAPPDATA%\dogeplay\PiaStudyWebView`，不作为打包输入。同一 Windows 用户运行多个副本可能复用这个登录状态。
- `.gitignore` 排除个人数据、日志、构建输出、浏览器配置和常见凭据文件。它不是对已提交文件的自动清除工具。
- 源码包从明确列举的文件生成，不携带 Git 历史、个人数据或旧构建产物。

## 开发检查

按需运行 `scripts/verify.ps1` 可执行现有核心、播放逻辑和 OCR 检查。真实游戏兼容性需要使用者自己验证。发布打包本身只编译、检查文件与生成 ZIP，不会启动游戏或代替使用者登录网站。

## 许可与资源

当前版本自有代码采用 LICENSE 中的 MIT License，保留作者 doge 署名。第三方依赖和曲谱不自动继承本项目许可证，详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。仅附带明确授权的 CC0 示例；公开曲谱索引不意味着任何歌曲均可自由再分发。

永劫普通模式和大师模式均提供古筝与曲笛方案。合奏时，古筝继续演奏原曲的和弦/伴奏，曲笛从完整原谱抽取旋律声部；谱面标出“旋律 / Melody / Lead”等声部时优先采用该声部。只有钢琴和弦谱时，会按连贯的高音走向提取曲笛旋律，而不是直接取第一个声部或每拍最高音。曲笛单音长按，持续到原谱时值或下一个音符起点。原谱和 BPM 不变。按键与音域沿用对应模式模板，请在游戏里核对当前乐器和键位；游戏更新或自定义改键可能需要校准。合奏需要游戏中另一个古筝演奏者/角色承担伴奏，单个游戏乐器不能同时发出两种音色。
