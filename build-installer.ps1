param(
    [Parameter(Mandatory=$true)][string]$PayloadDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][string]$ReleaseZip,
    [switch]$SkipDownloadSite,
    [string]$CertificateThumbprint
)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$payload=(Resolve-Path -LiteralPath $PayloadDirectory).Path
$out=(Resolve-Path -LiteralPath $OutputDirectory).Path
$iscc=Join-Path $root 'tools\inno-setup\ISCC.exe'
if(-not(Test-Path -LiteralPath $iscc)){throw 'Inno Setup compiler missing. Run scripts/prepare-installer.ps1 first.'}
$assembly=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $payload 'dogeplay.exe')))
$version=$assembly.GetType('LumaKeys.UpdateClient').GetField('CurrentVersion').GetRawConstantValue()
if(Test-Path -LiteralPath (Join-Path $payload 'data')){throw 'Installer payload must not contain user data.'}
$license=Join-Path $out 'installer-license.txt'
[IO.File]::WriteAllText($license,[IO.File]::ReadAllText((Join-Path $payload 'LICENSE')),[Text.UTF8Encoding]::new($true))
& $iscc /Qp ('/DPayloadDir='+$payload) ('/DReleaseVersion='+$version) ('/DSetupOutput='+$out) ('/DSetupLicense='+$license) (Join-Path $root 'installer\dogeplay.iss')
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed.'}
$setup=Join-Path $out ('dogeplay-'+$version+'-setup-win-x64.exe')
if(-not(Test-Path -LiteralPath $setup)){throw 'Installer output missing.'}
if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    & (Join-Path $root 'scripts\sign-authenticode.ps1') -FilePath $setup -CertificateThumbprint $CertificateThumbprint
}
$hash=(Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($setup+'.sha256'),$hash+'  '+[IO.Path]::GetFileName($setup)+"`r`n",[Text.Encoding]::ASCII)
if (-not $SkipDownloadSite) { & (Join-Path $root 'prepare-download.ps1') -ReleaseZip $ReleaseZip -SetupExe $setup }
Write-Output ('Installer ready: '+$setup)
