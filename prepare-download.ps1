param([string]$ReleaseZip,[string]$SetupExe)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
if([string]::IsNullOrWhiteSpace($ReleaseZip)) {
    $candidate=Get-ChildItem -LiteralPath (Join-Path (Join-Path $root '..\app') 'releases') -Recurse -File -Filter '*-win-x64.zip' | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if($null -eq $candidate){throw 'No full client release ZIP found. Run package-release.ps1 first.'}
    $ReleaseZip=$candidate.FullName
}
$archivePath=(Resolve-Path -LiteralPath $ReleaseZip).Path
$checksumPath=$archivePath+'.sha256'
if(-not (Test-Path -LiteralPath $checksumPath)){throw 'Release checksum missing; only use audited full release archives.'}
$digest=(Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
$expected=([IO.File]::ReadAllText($checksumPath).Trim() -split '\s+')[0]
if($digest -ne $expected){throw 'Release checksum mismatch.'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $executables=@($archive.Entries | Where-Object { $_.FullName.Replace('\','/') -match '^[^/]+/dogeplay\.exe$' })
    if($executables.Count -ne 1){throw 'This is not a complete client release.'}
    foreach($entry in $archive.Entries) {
        $relative=$entry.FullName.Replace('\','/')
        if($relative -match '(?i)(^|/)(data|source|src|downloads)(/|$)|\.(cs|pdb|bat|cmd|log)$|MainWindow\.xaml$'){throw ('Unexpected file in download release: '+$relative)}
    }
    $bytes=[IO.MemoryStream]::new();$stream=$executables[0].Open()
    try{$stream.CopyTo($bytes);$compiled=[Reflection.Assembly]::Load($bytes.ToArray())}finally{$stream.Dispose();$bytes.Dispose()}
    $version=$compiled.GetType('LumaKeys.UpdateClient').GetField('CurrentVersion').GetRawConstantValue()
}finally{$archive.Dispose()}
$metadata=[ordered]@{app='dogeplay';version=$version;packageUrl='files/dogeplay-latest-win-x64.zip';size=(Get-Item -LiteralPath $archivePath).Length;sha256=$digest.ToLowerInvariant()}
$setupPath=$null
if(-not [string]::IsNullOrWhiteSpace($SetupExe)) {
    $setupPath=(Resolve-Path -LiteralPath $SetupExe).Path
    $setupHash=(Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash
    if(-not(Test-Path -LiteralPath ($setupPath+'.sha256')) -or ([IO.File]::ReadAllText($setupPath+'.sha256').Trim() -split '\s+')[0] -ne $setupHash){throw 'Installer checksum mismatch.'}
    $metadata['portableUrl']=$metadata.packageUrl;$metadata['portableSize']=$metadata.size;$metadata['portableSha256']=$metadata.sha256
    $metadata.packageUrl='files/dogeplay-setup.exe';$metadata.size=(Get-Item -LiteralPath $setupPath).Length;$metadata.sha256=$setupHash.ToLowerInvariant()
}
foreach($destination in @((Join-Path (Join-Path $root '..\app') 'update-server'))){
    New-Item -ItemType Directory -Path (Join-Path $destination 'files'),(Join-Path $destination 'download') -Force | Out-Null
    $downloadPath=Join-Path $destination 'files\dogeplay-latest-win-x64.zip'
    Copy-Item -LiteralPath $archivePath -Destination $downloadPath -Force
    if((Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash -ne $digest){throw 'Download copy checksum mismatch.'}
    if($setupPath){Copy-Item -LiteralPath $setupPath -Destination (Join-Path $destination 'files\dogeplay-setup.exe') -Force;if((Get-FileHash -LiteralPath (Join-Path $destination 'files\dogeplay-setup.exe') -Algorithm SHA256).Hash -ne $setupHash){throw 'Installer copy checksum mismatch.'}}
    Copy-Item -LiteralPath (Join-Path $root 'download-page.html') -Destination (Join-Path $destination 'download\index.html') -Force
    [IO.File]::WriteAllText((Join-Path $destination 'latest.json'),($metadata|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
}
Write-Output ('Download ready: '+(Join-Path (Join-Path $root '..\app') 'update-server'))
Write-Output ('Full client version: '+$version)
