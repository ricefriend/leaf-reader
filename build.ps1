$ErrorActionPreference = 'Stop'
$base = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '需要 Windows .NET Framework 4.x 编译器。' }
$target = Join-Path (Split-Path $base -Parent) 'LeafReader.exe'
$refs = @('System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.Xaml.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Web.dll','System.Web.Extensions.dll','System.Security.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$refs += @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path (Join-Path $framework 'WPF') $_) }
$sources = Get-ChildItem -LiteralPath $base -Filter '*.cs' | ForEach-Object FullName
$argsBuild = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/codepage:65001',('/out:' + $target),('/win32manifest:' + (Join-Path $base 'app.manifest')),('/resource:' + (Join-Path $base 'Theme.xaml') + ',LeafReader.Theme.xaml'))
$qr = Join-Path $base 'third-party\QRCoder.dll'
if (Test-Path -LiteralPath $qr) { $refs += '/reference:' + $qr; $argsBuild += '/resource:' + $qr + ',QRCoder.dll' }
if (Test-Path -LiteralPath (Join-Path $base 'app.ico')) { $argsBuild += '/win32icon:' + (Join-Path $base 'app.ico') }
& $compiler @argsBuild @refs @sources
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
Write-Output ('已生成：' + $target)
