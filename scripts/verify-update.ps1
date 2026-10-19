$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Web.Extensions,System.IO.Compression.FileSystem
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $project 'app\dogeplay.exe'))
$type=$assembly.GetType('LumaKeys.UpdateClient')
$info=[LumaKeys.UpdateClient]::FetchAsync().GetAwaiter().GetResult()
if($info.App -ne 'LumaKeys' -or $null -eq $info.Announcement -or [string]::IsNullOrWhiteSpace($info.Announcement.Content)){throw 'Server manifest or announcement invalid'}
Write-Output ('PASS: compiled client reads server version '+$info.Version+' and announcement')
if(-not [LumaKeys.UpdateClient]::IsNewer('2026.10.11.2','2026.10.11.1')){throw 'Version comparison failed'}
if([LumaKeys.UpdateClient]::IsNewer('2026.10.10.2','2026.10.11.1')){throw 'Downgrade detected as update'}
$safe=$type.GetMethod('SafeRelative',[Reflection.BindingFlags]'NonPublic,Static')
foreach($bad in @('../dogeplay.exe','data/library.json','assets/../../data/settings.json','assets/foo:stream.dll','C:\temp\x.dll')){if($safe.Invoke($null,@($bad))){throw ('Invalid path accepted: '+$bad)}}
Write-Output 'PASS: version comparisons and protected update paths'
$sandbox=Join-Path ([IO.Path]::GetTempPath()) ('LumaKeys-updater-check-'+[guid]::NewGuid().ToString('N'))
$stage=Join-Path $sandbox 'stage';$target=Join-Path $sandbox 'target'
try {
    foreach($relative in @('stage\scripts','target\scripts','target\data')){[IO.Directory]::CreateDirectory((Join-Path $sandbox $relative))|Out-Null}
    [IO.File]::WriteAllText((Join-Path $stage 'MainWindow.xaml'),'new-ui')
    [IO.File]::WriteAllText((Join-Path $stage 'scripts\number-ocr.ps1'),'new-script')
    [IO.File]::WriteAllText((Join-Path $target 'MainWindow.xaml'),'old-ui')
    [IO.File]::WriteAllText((Join-Path $target 'scripts\number-ocr.ps1'),'old-script')
    [IO.File]::WriteAllText((Join-Path $target 'data\library.json'),'user-library')
    $install=$type.GetMethod('InstallFiles',[Reflection.BindingFlags]'NonPublic,Static')
    $install.Invoke($null,[object[]]@([string]$stage,[string]$target))|Out-Null
    if([IO.File]::ReadAllText((Join-Path $target 'MainWindow.xaml')) -ne 'new-ui' -or [IO.File]::ReadAllText((Join-Path $target 'scripts\number-ocr.ps1')) -ne 'new-script'){throw 'Nested file replacement failed'}
    if([IO.File]::ReadAllText((Join-Path $target 'data\library.json')) -ne 'user-library'){throw 'Library modified'}
    if([IO.File]::ReadAllText((Join-Path $stage 'backup\scripts\number-ocr.ps1')) -ne 'old-script'){throw 'Nested backup failed'}
    Write-Output 'PASS: nested file replacement and backup, user data preserved'
    # A directory at a file destination causes a later copy to fail, after the UI copy succeeds.
    $stage2=Join-Path $sandbox 'rollback';[IO.Directory]::CreateDirectory($stage2)|Out-Null
    [IO.File]::WriteAllText((Join-Path $stage2 'MainWindow.xaml'),'should-rollback')
    [IO.File]::WriteAllText((Join-Path $stage2 'z.dll'),'blocked-file')
    [IO.Directory]::CreateDirectory((Join-Path $target 'z.dll'))|Out-Null
    $failed=$false;try{$install.Invoke($null,[object[]]@([string]$stage2,[string]$target))|Out-Null}catch{$failed=$true}
    if(-not $failed -or [IO.File]::ReadAllText((Join-Path $target 'MainWindow.xaml')) -ne 'new-ui'){throw 'Partial update rollback failed'}
    Write-Output 'PASS: copy failure restores previously replaced files'
} finally {if([IO.Directory]::Exists($sandbox)){[IO.Directory]::Delete($sandbox,$true)}}
