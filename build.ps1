param([string]$OutputDirectory = '..\app')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs = @('System.dll','System.Core.dll','System.Xml.dll','System.Xml.Linq.dll','System.Web.Extensions.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Xaml.dll','System.Drawing.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
$refs += '/r:' + (Join-Path $root 'assets\vendor\BrotliSharpLib.dll')
$refs += @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.Wpf.dll') | ForEach-Object { '/r:' + (Join-Path "$root\assets\vendor" $_) }
$refs += @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll') | ForEach-Object { '/r:' + (Join-Path "$framework\WPF" $_) }
$output = if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))}
$engineSource = Join-Path $root 'tools\audiveris\Audiveris'
if(-not (Test-Path -LiteralPath (Join-Path $engineSource 'Audiveris.exe'))){$engineSource=[IO.Path]::GetFullPath((Join-Path $root '..\app\tools\audiveris\Audiveris'))}
foreach($required in @('Audiveris.exe','app\Audiveris.cfg','app\audiveris.jar','runtime\bin\server\jvm.dll')){
    if(-not(Test-Path -LiteralPath (Join-Path $engineSource $required))){throw 'Bundled staff recognition engine missing. Run scripts/prepare-engine.ps1 before building.'}
}
New-Item -ItemType Directory -Force -Path $output | Out-Null
$sources = Get-ChildItem "$root\src\*.cs" | ForEach-Object FullName
$resourceStage = Join-Path $root ('.resource-build-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $resourceStage | Out-Null
try {
    $aes = [Security.Cryptography.Aes]::Create()
    try {
        $aes.GenerateKey(); $aes.GenerateIV()
        $keySource = 'namespace LumaKeys { internal static class EmbeddedResourceKey { internal const string Key="' + [Convert]::ToBase64String($aes.Key) + '"; internal const string IV="' + [Convert]::ToBase64String($aes.IV) + '"; } }'
        $generated = Join-Path $resourceStage 'EmbeddedResourceKey.cs'
        [IO.File]::WriteAllText($generated,$keySource,[Text.UTF8Encoding]::new($false))
        $xaml = [IO.File]::ReadAllText((Join-Path $root 'src\MainWindow.xaml'))
        $payload = [Text.Encoding]::UTF8.GetBytes($xaml)
        $encryptor = $aes.CreateEncryptor()
        try { $encrypted = $encryptor.TransformFinalBlock($payload,0,$payload.Length) } finally { $encryptor.Dispose() }
        $resource = Join-Path $resourceStage 'interface.bin'
        [IO.File]::WriteAllBytes($resource,$encrypted)
    } finally { $aes.Dispose() }
    & "$framework\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ "/win32icon:$root\assets\dogeplay.ico" /codepage:65001 "/win32manifest:$root\app.manifest" "/resource:$resource,LumaKeys.Interface" "/out:$output\dogeplay.exe" $refs $sources $generated
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
} finally {
    $resolvedStage = [IO.Path]::GetFullPath($resourceStage)
    if(-not $resolvedStage.StartsWith([IO.Path]::GetFullPath($root) + '\',[StringComparison]::OrdinalIgnoreCase)){throw 'Resource build cleanup escaped project'}
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}
if(Test-Path -LiteralPath (Join-Path $output 'MainWindow.xaml')){Remove-Item -LiteralPath (Join-Path $output 'MainWindow.xaml') -Force}
Copy-Item "$root\scripts" $output -Recurse -Force
Get-ChildItem "$output\scripts\*.ps1" | ForEach-Object {
    $content = [IO.File]::ReadAllText($_.FullName, [Text.Encoding]::UTF8)
    [IO.File]::WriteAllText($_.FullName, $content, [Text.UTF8Encoding]::new($true))
}
Copy-Item "$root\assets" $output -Recurse -Force
New-Item -ItemType Directory -Force -Path (Join-Path $output 'assets\release') | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $output 'assets\release\LICENSE') -Force
Copy-Item -LiteralPath (Join-Path $root 'RELEASE_README.md') -Destination (Join-Path $output 'assets\release\README.md') -Force
Copy-Item -LiteralPath (Join-Path $root 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $output 'assets\release\THIRD_PARTY_NOTICES.md') -Force
Copy-Item "$root\assets\vendor\*.dll" $output -Force
Copy-Item "$root\update-config.json" "$output\update-config.json" -Force
$engineDestination=Join-Path $output 'tools\audiveris\Audiveris'
if(-not [IO.Path]::GetFullPath($engineSource).Equals([IO.Path]::GetFullPath($engineDestination),[StringComparison]::OrdinalIgnoreCase)){
    New-Item -ItemType Directory -Path $engineDestination -Force | Out-Null
    Get-ChildItem -LiteralPath $engineSource -Force | Copy-Item -Destination $engineDestination -Recurse -Force
}
if(Test-Path -LiteralPath (Join-Path $output 'LumaKeys.exe')){Remove-Item -LiteralPath (Join-Path $output 'LumaKeys.exe') -Force}
Write-Output "Built: $output\dogeplay.exe"
