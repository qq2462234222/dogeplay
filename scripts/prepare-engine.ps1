param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$runtime=[IO.Path]::GetFullPath((Join-Path $root '..\app'))
$destination=Join-Path $runtime 'tools\audiveris\Audiveris'
$required=@('Audiveris.exe','app\Audiveris.cfg','app\audiveris.jar','runtime\bin\server\jvm.dll')
if(@($required | Where-Object {-not(Test-Path -LiteralPath (Join-Path $destination $_))}).Count -eq 0){Write-Output 'Bundled recognition engine is ready.';return}
$cache=Join-Path $runtime 'maintenance\engine-download'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
$installer=Join-Path $cache 'Audiveris-5.11.0.msi'
$hash='5F1B4E96A12C53C7DA426814B76E599363C4181E291855996E0A6878DDA95F71'
if(-not(Test-Path -LiteralPath $installer)){
    [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri 'https://github.com/Audiveris/audiveris/releases/download/5.11.0/Audiveris-5.11.0-windowsConsole-x86_64.msi' -OutFile $installer -UseBasicParsing
}
if((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash -ne $hash){throw 'Audiveris download checksum mismatch. Remove maintenance/engine-download/Audiveris-5.11.0.msi and retry.'}
$extract=Join-Path $cache ('extract-'+[Guid]::NewGuid().ToString('N'))
$log=Join-Path $cache 'extract.log'
New-Item -ItemType Directory -Path $extract -Force | Out-Null
# Administrative extraction only: do not install or register anything system-wide.
$process=Start-Process -FilePath (Join-Path $env:WINDIR 'System32\msiexec.exe') -ArgumentList @('/a',('"'+$installer+'"'),'/qn',('TARGETDIR="'+$extract+'"'),'/L*v',('"'+$log+'"')) -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -notin @(0,3010)){throw 'Engine extraction failed. See app/maintenance/engine-download/extract.log.'}
$exe=Get-ChildItem -LiteralPath $extract -Recurse -File -Filter Audiveris.exe | Select-Object -First 1
if($null -eq $exe){throw 'Extracted package does not contain Audiveris.exe.'}
foreach($file in $required){if(-not(Test-Path -LiteralPath (Join-Path $exe.DirectoryName $file))){throw ('Incomplete extracted engine: '+$file)}}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Get-ChildItem -LiteralPath $exe.DirectoryName -Force | Copy-Item -Destination $destination -Recurse -Force
Write-Output 'Bundled recognition engine is ready in app/tools/audiveris/Audiveris.'
