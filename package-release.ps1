param([switch]$SkipDownloadSite)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$releaseId = 'dogeplay-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6) + '-win-x64'
$runtimeRoot = [IO.Path]::GetFullPath((Join-Path $root '..\app'))
$out = Join-Path $runtimeRoot ('releases\' + $releaseId)
$buildRelative = '.release-build-' + [Guid]::NewGuid().ToString('N')
$build = Join-Path $root $buildRelative
$package = Join-Path $out $releaseId
New-Item -ItemType Directory -Path $package -Force | Out-Null

# Build into a new directory. Never package an installation that has been run.
& (Join-Path $root 'build.ps1') -OutputDirectory $buildRelative
if (-not (Test-Path -LiteralPath (Join-Path $build 'dogeplay.exe'))) { throw 'Release build missing.' }

$files = [ordered]@{
    'dogeplay.exe' = 'dogeplay.exe'
    'BrotliSharpLib.dll' = 'BrotliSharpLib.dll'
    'Microsoft.Web.WebView2.Core.dll' = 'Microsoft.Web.WebView2.Core.dll'
    'Microsoft.Web.WebView2.Wpf.dll' = 'Microsoft.Web.WebView2.Wpf.dll'
    'WebView2Loader.dll' = 'WebView2Loader.dll'
    'update-config.json' = 'update-config.json'
    'assets\dogeplay.ico' = 'assets\dogeplay.ico'
    'assets\release\LICENSE' = 'assets\release\LICENSE'
    'assets\release\README.md' = 'assets\release\README.md'
    'assets\release\THIRD_PARTY_NOTICES.md' = 'assets\release\THIRD_PARTY_NOTICES.md'
    'scripts\number-ocr.ps1' = 'scripts\number-ocr.ps1'
    'assets\openscore\Just_for_Today.mxl' = 'assets\openscore\Just_for_Today.mxl'
    'assets\openscore\LICENSE.txt' = 'assets\openscore\LICENSE.txt'
    'assets\openscore\catalog.json' = 'assets\openscore\catalog.json'
    'assets\josh\catalog.json' = 'assets\josh\catalog.json'
    'assets\smap\catalog.json' = 'assets\smap\catalog.json'
    'assets\sky\catalog.json' = 'assets\sky\catalog.json'
    'assets\vendor\BrotliSharpLib-LICENSE.txt' = 'assets\vendor\BrotliSharpLib-LICENSE.txt'
    'assets\vendor\WebView2-LICENSE.txt' = 'assets\vendor\WebView2-LICENSE.txt'
    'assets\vendor\WebView2-NOTICE.txt' = 'assets\vendor\WebView2-NOTICE.txt'
    'assets\AUDIVERIS-LICENSE.txt' = 'assets\AUDIVERIS-LICENSE.txt'
}
$allowed = New-Object 'System.Collections.Generic.List[string]'
function Copy-ReleaseFile([string]$source,[string]$relative) {
    $item = Get-Item -LiteralPath $source
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw ('Linked release input is not allowed: ' + $relative) }
    $target = Join-Path $package $relative
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
    $allowed.Add($relative.Replace('\','/'))
}
function Copy-ReleaseTree([string]$sourceRoot,[string]$relativeRoot) {
    $rootItem=Get-Item -LiteralPath $sourceRoot
    if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw ('Linked release input is not allowed: ' + $relativeRoot) }
    foreach ($item in Get-ChildItem -LiteralPath $sourceRoot -Recurse -Force) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw ('Linked release input is not allowed: ' + $item.FullName) }
        if (-not $item.PSIsContainer) {
            $inside=$item.FullName.Substring($sourceRoot.Length+1).Replace('\','/')
            Copy-ReleaseFile $item.FullName (($relativeRoot.TrimEnd('\','/'))+'/'+$inside)
        }
    }
}
foreach ($entry in $files.GetEnumerator()) { Copy-ReleaseFile (Join-Path $build $entry.Key) $entry.Value }
Copy-ReleaseTree (Join-Path $build 'tools\audiveris\Audiveris') 'tools\audiveris\Audiveris'
Copy-ReleaseFile (Join-Path $root 'LICENSE') 'LICENSE'
Copy-ReleaseFile (Join-Path $root 'RELEASE_README.md') 'README.md'

$notices = [IO.File]::ReadAllText((Join-Path $root 'THIRD_PARTY_NOTICES.md'))
$notices = $notices.Replace('`app/tools/audiveris/Audiveris`', '`tools/audiveris/Audiveris`')
[IO.File]::WriteAllText((Join-Path $package 'THIRD_PARTY_NOTICES.md'),$notices,[Text.UTF8Encoding]::new($false))
$allowed.Add('THIRD_PARTY_NOTICES.md')


# Inspect all final payload files, including UTF-16 strings in assemblies.
# Report filenames only; never print any suspected credential value.
$rules = @(
    '(?i)-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',
    '(?i)(?:access_token|refresh_token|authorization|cookie|password|api[_-]?key)\s*["'']?\s*[:=]\s*["''][^"''\r\n]{12,}["'']',
    '\beyJ[A-Za-z0-9_-]{16,}\.[A-Za-z0-9_-]{16,}\.[A-Za-z0-9_-]{16,}\b',
    '(?i)[?&](?:access_token|refresh_token|auth_token|token|sessionid)=[^\s"''&<>]{8,}'
)
$personalPaths = @($root,[Environment]::GetFolderPath('UserProfile')) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$manifest = @()
foreach ($item in Get-ChildItem -LiteralPath $package -Recurse -File) {
    $relative = $item.FullName.Substring($package.Length+1).Replace('\','/')
    if (-not $allowed.Contains($relative)) { throw ('Unexpected release file: ' + $relative) }
    if ($relative -match '(?i)(^|/)(data|downloads|app-backups|PiaStudyWebView|EBWebView|User Data|Network)(/|$)|(^|/)(Cookies|Login Data|Local State|settings\.json|library\.json|.*\.log|.*\.pdb)$') { throw ('Private-data filename in release: ' + $relative) }
    $bytes = [IO.File]::ReadAllBytes($item.FullName)
    $texts = @([Text.Encoding]::UTF8.GetString($bytes),[Text.Encoding]::Unicode.GetString($bytes))
    foreach ($content in $texts) {
        foreach ($rule in $rules) { if ($content -match $rule) { throw ('Potential credential pattern found; inspect locally: ' + $relative) } }
        foreach ($privatePath in $personalPaths) {
            foreach ($pathForm in @($privatePath,$privatePath.Replace('\','/'),$privatePath.Replace('\','\\'))) {
                if ($content.IndexOf($pathForm,[StringComparison]::OrdinalIgnoreCase) -ge 0) { throw ('Personal absolute path found: ' + $relative) }
            }
        }
    }
    $manifest += [ordered]@{path=$relative;bytes=$item.Length;sha256=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash}
}
[IO.File]::WriteAllText((Join-Path $package 'manifest-sha256.json'),(ConvertTo-Json -InputObject @($manifest) -Depth 4),[Text.UTF8Encoding]::new($false))
$allowed.Add('manifest-sha256.json')

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipPath = Join-Path $out ($releaseId + '.zip')
[IO.Compression.ZipFile]::CreateFromDirectory($package,$zipPath,[IO.Compression.CompressionLevel]::Optimal,$true)
$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $entries = @($archive.Entries | Where-Object { $_.Name.Length -gt 0 })
    $payloadCount=@(Get-ChildItem -LiteralPath $package -Recurse -File).Count
    if ($entries.Count -ne $payloadCount) { throw 'ZIP entry count mismatch.' }
    foreach ($entry in $entries) {
        $prefix = $releaseId + '/'
        $entryPath=$entry.FullName.Replace('\','/')
        if (-not $entryPath.StartsWith($prefix)) { throw 'Unexpected ZIP root.' }
        $relative=$entryPath.Substring($prefix.Length)
        if (-not $allowed.Contains($relative)) { throw ('Unexpected ZIP member: ' + $relative) }
        $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
        try { $digest=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') } finally { $stream.Dispose();$sha.Dispose() }
        if ($digest -ne (Get-FileHash -LiteralPath (Join-Path $package $relative) -Algorithm SHA256).Hash) { throw ('ZIP content mismatch: ' + $relative) }
    }
} finally { $archive.Dispose() }
$zipHash=(Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
[IO.File]::WriteAllText(($zipPath+'.sha256'),$zipHash+'  '+[IO.Path]::GetFileName($zipPath)+"`r`n",[Text.Encoding]::ASCII)

$report = @"
# 发布包检查结果

- 构建来源：本次源码编译，使用全新输出目录。
- 文件来源：明确列举的运行文件、公开索引、CC0 示例、说明和许可证。
- ZIP 文件数量：$($payloadCount)；逐个核对路径、数量和 SHA-256，全部一致。
- 未包含 data、用户曲库、设置、下载缓存、日志、备份、浏览器配置、Cookie 数据库、登录数据库或调试符号。
- 最终文件已扫描常见凭据格式、JWT、带认证参数的 URL 和本机个人绝对路径，未发现匹配项。扫描不等同于对任意格式秘密的绝对保证。
- 网站登录配置目录在本机用户目录，不作为打包输入；没有读取 Cookie 或 Token 的内容。
- 已包含 Audiveris 5.11.0 五线谱识别引擎及其运行时；WebView2 Runtime 仍使用 Windows 上已安装的系统组件。
- 未运行游戏实测，未登录任何账号，未上传或发布到外部平台。
- ZIP SHA-256：$zipHash
- 发布包不含自有源码或源码 ZIP；界面资源嵌入 EXE。第三方组件仍保留原许可。
"@
[IO.File]::WriteAllText((Join-Path $out '发布检查报告.md'),$report,[Text.UTF8Encoding]::new($false))
& (Join-Path $root 'build-installer.ps1') -PayloadDirectory $package -OutputDirectory $out -ReleaseZip $zipPath -SkipDownloadSite:$SkipDownloadSite
if(Test-Path -LiteralPath $build){Remove-Item -LiteralPath $build -Recurse -Force}
Write-Output ('Release ZIP: ' + $zipPath)

Write-Output ('Audit report: ' + (Join-Path $out '发布检查报告.md'))
Write-Output ('Files: ' + $payloadCount + '; SHA-256: ' + $zipHash)
