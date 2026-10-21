param(
    [Parameter(Mandatory=$true)][string]$FilePath,
    [string]$CertificateThumbprint,
    [string]$TimestampUrl = 'https://timestamp.digicert.com'
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) { throw 'Provide the thumbprint of a trusted code-signing certificate installed in the current user certificate store.' }
$thumbprint = $CertificateThumbprint -replace '\s',''
$certificate = Get-Item -LiteralPath ('Cert:\CurrentUser\My\' + $thumbprint) -ErrorAction Stop
if (-not $certificate.HasPrivateKey) { throw 'The selected code-signing certificate does not have its private key.' }
if ($certificate.NotBefore -gt [DateTime]::Now -or $certificate.NotAfter -lt [DateTime]::Now) { throw 'The selected code-signing certificate is not currently valid.' }
if (@($certificate.EnhancedKeyUsageList | Where-Object { $_.ObjectId.Value -eq '1.3.6.1.5.5.7.3.3' }).Count -eq 0) { throw 'The selected certificate is not valid for code signing.' }
$signtool = Get-ChildItem -Path @(
    (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin\*\x64\signtool.exe'),
    (Join-Path $env:ProgramFiles 'Windows Kits\10\bin\*\x64\signtool.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Microsoft SDKs\Windows\*\bin\x64\signtool.exe')
) -File -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
if ($null -eq $signtool) { throw 'signtool.exe was not found. Install the Windows 10/11 SDK signing tools.' }
$file = (Resolve-Path -LiteralPath $FilePath).Path
$arguments = @('sign','/fd','SHA256','/td','SHA256','/tr',$TimestampUrl)
$arguments += @('/sha1',$thumbprint)
$arguments += $file
& $signtool.FullName @arguments
if ($LASTEXITCODE -ne 0) { throw ('Authenticode signing failed for ' + $file) }
& $signtool.FullName verify /pa /all $file
if ($LASTEXITCODE -ne 0) { throw ('Authenticode verification failed for ' + $file) }
Write-Output ('Signed and verified: ' + $file)
