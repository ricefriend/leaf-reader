param([string]$Output = (Join-Path (Split-Path $PSScriptRoot -Parent) 'LeafReader-GitHub-Source-Candidate.zip'))
$ErrorActionPreference = 'Stop'
$sourceRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$archivePath = [IO.Path]::GetFullPath($Output)
if ($archivePath.StartsWith($sourceRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw '公开源码 ZIP 必须放在源码目录之外。' }
$allowed = @('.cs','.java','.xml','.xaml','.ps1','.md','.txt','.dll','.jar','.zip','.ico','.manifest','.py','.yml','.yaml')
$excludedNames = @('build','.git','.vs','.idea','bin','obj')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$tempArchive = $archivePath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
$zip = [IO.Compression.ZipFile]::Open($tempArchive,[IO.Compression.ZipArchiveMode]::Create)
$count = 0
try {
    foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Force) {
        $relative = $file.FullName.Substring($sourceRoot.Length + 1)
        $parts = $relative.Split([IO.Path]::DirectorySeparatorChar)
        if (@($parts | Where-Object { $_ -in $excludedNames -or $_ -like 'qa*' }).Count -gt 0) { continue }
        $documentationImage = $file.Extension -eq '.png' -and $relative.StartsWith('docs\images\',[StringComparison]::OrdinalIgnoreCase)
        if ($file.Extension -notin $allowed -and $file.Name -notin @('.gitignore','.gitattributes','LICENSE','NOTICE','strings.json','translation-request.json') -and !$documentationImage) { continue }
        # Only these vendored distribution archives belong in the public source.
        if ($file.Extension -in @('.zip','.jar','.dll') -and !$relative.StartsWith('third-party\') -and $relative -ne 'android\zxing-core-3.5.3.jar') { continue }
        if ($file.Name -in @('qr-payload.txt') -or $file.Name -like '*results*') { continue }
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,('LeafReader/' + $relative.Replace('\','/')),[IO.Compression.CompressionLevel]::Optimal) | Out-Null
        $count++
    }
} finally { $zip.Dispose() }
Move-Item -LiteralPath $tempArchive -Destination $archivePath -Force
Write-Output ('公开源码候选包：' + $archivePath + '；文件数：' + $count)
Write-Output '不包含签名密钥。MIT © 2026 ricefriend；发布前仍需维护者保存签名离线备份，并完成 GitHub 发布设置。'
