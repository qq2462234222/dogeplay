param()
$ErrorActionPreference='Stop'
# Keep only the three newest patch archives in the private sibling app folder.
$folder=[IO.Path]::GetFullPath((Join-Path (Join-Path $PSScriptRoot '..\app') 'update-server\files'))
if(-not(Test-Path -LiteralPath $folder)){return}
$patches=@(Get-ChildItem -LiteralPath $folder -File | Where-Object {$_.Name -match '^(dogeplay|LumaKeys)-(\d+\.\d+\.\d+\.\d+)-patch\.zip$'} | Sort-Object @{Expression={ [version]([regex]::Match($_.Name,'\d+\.\d+\.\d+\.\d+').Value) };Descending=$true})
foreach($item in @($patches | Select-Object -Skip 3)) {
    if($item.DirectoryName -ne $folder){throw 'Update cleanup path escaped intended directory.'}
    Remove-Item -LiteralPath $item.FullName -Force
    Write-Output ('Removed old patch: '+$item.Name)
}
