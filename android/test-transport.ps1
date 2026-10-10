param([string]$JdkHome = $env:JAVA_HOME, [Parameter(Mandatory=$true)][string]$JsonJar)
$ErrorActionPreference = 'Stop'
if (!$JdkHome -or !(Test-Path -LiteralPath $JsonJar)) { throw '需要 JDK 17 与 org.json 测试 JAR。' }
$classes=Join-Path $PSScriptRoot 'build/transport-tests'
New-Item -ItemType Directory -Force $classes | Out-Null
$files=@('tests/jvm-stubs/android/net/Uri.java','src/org/leafreader/Api.java','src/org/leafreader/Models.java','src/org/leafreader/L10n.java','tests/ApiTransportTests.java') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& (Join-Path $JdkHome 'bin/javac.exe') --release 8 -encoding UTF-8 -cp $JsonJar -d $classes @files
if ($LASTEXITCODE -ne 0) { throw 'HTTP 传输测试编译失败' }
& (Join-Path $JdkHome 'bin/java.exe') -cp ($classes + ';' + [IO.Path]::GetFullPath($JsonJar)) org.leafreader.ApiTransportTests
if ($LASTEXITCODE -ne 0) { throw 'HTTP 传输测试失败' }
