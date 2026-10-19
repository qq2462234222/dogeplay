param([Parameter(Mandatory=$true)][string]$ImagePath,[Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.Encoding]::UTF8
Add-Type -AssemblyName System.Runtime.WindowsRuntime
Add-Type -AssemblyName System.Drawing
$null=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot '..\LumaKeys.exe'))
$null=[Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime]
$null=[Windows.Storage.Streams.IRandomAccessStream,Windows.Storage.Streams,ContentType=WindowsRuntime]
$null=[Windows.Graphics.Imaging.BitmapDecoder,Windows.Graphics.Imaging,ContentType=WindowsRuntime]
$null=[Windows.Graphics.Imaging.SoftwareBitmap,Windows.Graphics.Imaging,ContentType=WindowsRuntime]
$null=[Windows.Media.Ocr.OcrEngine,Windows.Foundation,ContentType=WindowsRuntime]
$null=[Windows.Media.Ocr.OcrResult,Windows.Foundation,ContentType=WindowsRuntime]
$null=[Windows.Globalization.Language,Windows.Globalization,ContentType=WindowsRuntime]
$asyncMethod=[System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {$_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.IsGenericMethod -and $_.GetGenericArguments().Count -eq 1} | Select-Object -First 1
function Await-WinRT($operation,[Type]$resultType) {
    $task=$asyncMethod.MakeGenericMethod($resultType).Invoke($null,@($operation))
    $task.Wait()
    return $task.Result
}
$sizeCheck=[Drawing.Image]::FromFile($ImagePath)
try {if($sizeCheck.Width -gt [Windows.Media.Ocr.OcrEngine]::MaxImageDimension -or $sizeCheck.Height -gt [Windows.Media.Ocr.OcrEngine]::MaxImageDimension){throw 'Image too large. Crop or resize it to less than 2500 pixels.'}} finally {$sizeCheck.Dispose()}
$geometry=New-Object LumaKeys.NumberGeometry $ImagePath
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($OutputPath)) | Out-Null
$file=Await-WinRT ([Windows.Storage.StorageFile]::GetFileFromPathAsync([IO.Path]::GetFullPath($ImagePath))) ([Windows.Storage.StorageFile])
$stream=Await-WinRT ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
try {
    $decoder=Await-WinRT ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
    $bitmap=Await-WinRT ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
    try {
        if($bitmap.PixelWidth -gt [Windows.Media.Ocr.OcrEngine]::MaxImageDimension -or $bitmap.PixelHeight -gt [Windows.Media.Ocr.OcrEngine]::MaxImageDimension) {throw 'Image exceeds Windows OCR size limit. Crop or resize the image to less than 2500 pixels.'}
        $language=New-Object Windows.Globalization.Language 'en-US'
        $engine=[Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($language)
        if($null -eq $engine){$engine=[Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()}
        if($null -eq $engine) {throw 'Windows OCR language pack is missing. Install English or Chinese language OCR in Windows Settings.'}
        $result=Await-WinRT ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
        $lines=New-Object 'System.Collections.Generic.List[string]'
        foreach($line in $result.Lines) {
            $tokens=New-Object 'System.Collections.Generic.List[string]'
            foreach($word in $line.Words) {
                $text=$word.Text.Normalize([Text.NormalizationForm]::FormKC)
                # Only accept musical tokens, never extract digits out of titles, dates or BPM labels.
                if($text -notmatch "^[0-7\s.,'\u2019\u00b7#b|\-\u2014_]+$") {continue}
                foreach($m in [regex]::Matches($text,"[#b]?[0-7]['\u2019.,]*|[-\u2014]")) {
                    $value=$m.Value.Replace([char]0x2019,[char]39).Replace([char]0x2014,[char]45)
                    if($value -match '^[1-7]$') {
                        $box=$word.BoundingRect
                        $charWidth=$box.Width / [Math]::Max(1,$text.Length)
                        $value=$geometry.Enrich($value,($box.X+$m.Index*$charWidth),$box.Y,$charWidth,$box.Height)
                    }
                    $tokens.Add($value)
                }
            }
            if($tokens.Count -gt 0) {$lines.Add(($tokens -join ' '))}
        }
        $draft=$lines -join " | `r`n"
        if([string]::IsNullOrWhiteSpace($draft)){$draft=$geometry.FallbackDraft()}
        $payload=@{Text=$draft;Raw=$result.Text;Warning='OCR produces a draft. Verify octave dots, underline durations, rests, chords and time signatures against the original image.'}
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($OutputPath)) | Out-Null
        [IO.File]::WriteAllText($OutputPath,($payload | ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
    } finally {$bitmap.Dispose()}
} finally {$stream.Dispose()}
