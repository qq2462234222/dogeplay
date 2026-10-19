param(
    [Parameter(Mandatory=$true)][string]$Version,
    [string]$Notes = '关键文件增量更新',
    [string]$BuildDirectory = '..\app'
)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
if($Version -notmatch '^\d+\.\d+\.\d+\.\d+$'){throw 'Version must use four numeric parts, for example 2026.10.10.3'}
$app=if([IO.Path]::IsPathRooted($BuildDirectory)){[IO.Path]::GetFullPath($BuildDirectory)}else{[IO.Path]::GetFullPath((Join-Path $root $BuildDirectory))}
$rootFull=[IO.Path]::GetFullPath($root).TrimEnd([IO.Path]::DirectorySeparatorChar)
$allowedApp=[IO.Path]::GetFullPath((Join-Path $root '..\app'))
if(-not ($app.Equals($allowedApp,[StringComparison]::OrdinalIgnoreCase) -or $app.StartsWith($rootFull+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase))){throw 'Build directory must be the sibling app folder or stay inside the source project.'}
$runtimeRoot=[IO.Path]::GetFullPath((Join-Path $root '..\app'))
$server=Join-Path $runtimeRoot 'update-server'
$stage=Join-Path $root 'update-stage'
if(-not (Test-Path (Join-Path $app 'dogeplay.exe'))){throw 'Build app first.'}
$compiled=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $app 'dogeplay.exe')))
$compiledVersion=$compiled.GetType('LumaKeys.UpdateClient').GetField('CurrentVersion').GetRawConstantValue()
if($compiledVersion -ne $Version){throw ('Compiled client version is '+$compiledVersion+'. Set CurrentVersion in src/UpdateClient.cs, rebuild app, then create the matching update package.')}
if(Test-Path $stage){Remove-Item $stage -Recurse -Force}
New-Item -ItemType Directory -Path (Join-Path $server 'files'),(Join-Path $stage 'scripts') -Force | Out-Null
# Earlier clients accept only this legacy executable name. It migrates itself
# to dogeplay.exe after restarting, while retaining the installation's data.
$files=@('LumaKeys.exe','update-config.json','BrotliSharpLib.dll','Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.Wpf.dll','WebView2Loader.dll')
Copy-Item (Join-Path $app 'dogeplay.exe') (Join-Path $stage 'LumaKeys.exe')
foreach($name in $files | Select-Object -Skip 1){Copy-Item (Join-Path $app $name) (Join-Path $stage $name)}
Copy-Item (Join-Path $app 'scripts\number-ocr.ps1') (Join-Path $stage 'scripts\number-ocr.ps1')
New-Item -ItemType Directory -Path (Join-Path $stage 'assets\release') -Force | Out-Null
Copy-Item (Join-Path $app 'assets\dogeplay.ico') (Join-Path $stage 'assets\dogeplay.ico')
foreach($doc in @('LICENSE','README.md','THIRD_PARTY_NOTICES.md')){Copy-Item -LiteralPath (Join-Path $app ('assets\release\'+$doc)) -Destination (Join-Path $stage ('assets\release\'+$doc))}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipName='dogeplay-'+$Version+'-patch.zip';$zipPath=Join-Path $server ('files\'+$zipName)
if(Test-Path $zipPath){Remove-Item $zipPath -Force}
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$zipPath,[IO.Compression.CompressionLevel]::Optimal,$false)
$hash=(Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant();$size=(Get-Item $zipPath).Length
$announcement=$null
$oldManifestPath=Join-Path $server 'manifest.json'
$announcementPath=Join-Path $server 'announcement.json'
if(Test-Path $announcementPath){try{$announcement=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($announcementPath))}catch{}}
if($null -eq $announcement -and (Test-Path $oldManifestPath)){try{$old=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($oldManifestPath));if($null -ne $old -and $old.PSObject.Properties.Name -contains 'announcement'){$announcement=$old.announcement}}catch{}}
$manifest=@{app='LumaKeys';product='dogeplay';version=$Version;notes=$Notes;packageUrl=('files/'+$zipName);sha256=$hash;size=$size;files=@($files+@('scripts/number-ocr.ps1','assets/dogeplay.ico','assets/release/LICENSE','assets/release/README.md','assets/release/THIRD_PARTY_NOTICES.md'))}
if($null -ne $announcement){$manifest.announcement=$announcement}
[IO.File]::WriteAllText((Join-Path $server 'manifest.json'),($manifest|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
& (Join-Path $root 'prune-updates.ps1')
Remove-Item $stage -Recurse -Force
Write-Output ('Update package: '+$zipPath)
Write-Output ('SHA-256: '+$hash)
