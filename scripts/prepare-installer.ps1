param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $root 'tools\inno-setup'
if (-not (Test-Path -LiteralPath (Join-Path $destination 'ISCC.exe'))) {
$cache = [IO.Path]::GetFullPath((Join-Path $root '..\app\maintenance\publishing-tools'))
New-Item -ItemType Directory -Path $cache -Force | Out-Null
$setup = Join-Path $cache 'innosetup-6.7.3.exe'
if (-not (Test-Path -LiteralPath $setup)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $setup -UseBasicParsing
}
$expected = '9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732'
if ((Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash -ne $expected) { throw 'Installer compiler checksum mismatch.' }
if ((Get-AuthenticodeSignature -LiteralPath $setup).Status -ne 'Valid') { throw 'Installer compiler signature verification failed.' }
$process = Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS',('/DIR="' + $destination + '"'),'/TASKS=') -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $destination 'ISCC.exe'))) { throw 'Installer compiler setup failed.' }
}
$language = Join-Path $destination 'Languages\ChineseSimplified.isl'
$languageHash = '7D544B9BB1D142CFA11F2E5D3CC8ABE2E55F8E066C5124E3772675AA236E1278'
if (-not (Test-Path -LiteralPath $language)) {
    Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/jrsoftware/issrc/is-6_7_3/Files/Languages/Unofficial/ChineseSimplified.isl' -OutFile $language -UseBasicParsing
}
if ((Get-FileHash -LiteralPath $language -Algorithm SHA256).Hash -ne $languageHash) { throw 'Installer language checksum mismatch.' }
Write-Output 'Installer compiler is ready in tools/inno-setup.'
