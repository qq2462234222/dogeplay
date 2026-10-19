$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
& (Join-Path $root 'build.ps1')
foreach($mode in @('--self-test','--playback-test')) {
    $p=Start-Process -FilePath (Join-Path $root 'app\dogeplay.exe') -ArgumentList $mode -WindowStyle Hidden -Wait -PassThru
    if($p.ExitCode -ne 0){throw "Failed: $mode. See test-output."}
}
Add-Type -AssemblyName System.Drawing
$directory=Join-Path $root 'test-output'
[IO.Directory]::CreateDirectory($directory) | Out-Null
$image=New-Object Drawing.Bitmap 1400,420
$graphics=[Drawing.Graphics]::FromImage($image)
$font=New-Object Drawing.Font 'Arial',52
try {
    $graphics.Clear([Drawing.Color]::White)
    $graphics.DrawString('1  1  5  5  6  6  5',$font,[Drawing.Brushes]::Black,70,65)
    $graphics.DrawString('4  4  3  3  2  2  1',$font,[Drawing.Brushes]::Black,70,230)
    $image.Save((Join-Path $directory 'number-score.png'))
} finally {$graphics.Dispose();$font.Dispose();$image.Dispose()}
& (Join-Path $root 'app\scripts\number-ocr.ps1') -ImagePath (Join-Path $directory 'number-score.png') -OutputPath (Join-Path $directory 'ocr-result.json')
$ocr=Get-Content (Join-Path $directory 'ocr-result.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if(($ocr.Text -replace '[|\s]','') -ne '11556654433221'){throw ('OCR mismatch: '+$ocr.Text)}
$image=New-Object Drawing.Bitmap 800,360
$graphics=[Drawing.Graphics]::FromImage($image)
$font=New-Object Drawing.Font 'Arial',52
try {
    $graphics.Clear([Drawing.Color]::White)
    $graphics.DrawString('1',$font,[Drawing.Brushes]::Black,100,120)
    $graphics.DrawString('2',$font,[Drawing.Brushes]::Black,250,120)
    $graphics.DrawString('3',$font,[Drawing.Brushes]::Black,400,120)
    $graphics.FillEllipse([Drawing.Brushes]::Black,124,106,8,8)
    $graphics.FillRectangle([Drawing.Brushes]::Black,262,201,36,3)
    $graphics.FillEllipse([Drawing.Brushes]::Black,424,214,8,8)
    $image.Save((Join-Path $directory 'number-decorated.png'))
} finally {$graphics.Dispose();$font.Dispose();$image.Dispose()}
& (Join-Path $root 'app\scripts\number-ocr.ps1') -ImagePath (Join-Path $directory 'number-decorated.png') -OutputPath (Join-Path $directory 'ocr-decorated-result.json')
$ocr=Get-Content (Join-Path $directory 'ocr-decorated-result.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($ocr.Text.Trim() -ne "1' 2:0.5 3,"){throw ('Decorated OCR mismatch: '+$ocr.Text)}
$coreCount=@(Get-Content (Join-Path $directory 'self-tests.txt') | Where-Object {$_ -like 'PASS:*'}).Count
$playbackCount=@(Get-Content (Join-Path $directory 'playback-tests.txt') | Where-Object {$_ -like 'PASS:*'}).Count
"PASS: $coreCount core checks, $playbackCount playback checks, 2 image recognition checks." | Set-Content (Join-Path $directory 'verification-summary.txt')
Get-Content (Join-Path $directory 'verification-summary.txt')
