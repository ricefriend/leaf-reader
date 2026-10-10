param([string]$SdkRoot = $env:ANDROID_HOME, [string]$JdkHome = $env:JAVA_HOME, [switch]$WithTests, [string]$SigningKeystore, [string]$SigningAlias = 'leafreader', [string]$OutputPath)
$ErrorActionPreference = 'Stop'
if (!$SdkRoot -or !$JdkHome) { throw '请设置 ANDROID_HOME（含 android-35 和 build-tools 35.0.0）及 JAVA_HOME（JDK 17）。' }
$base = $PSScriptRoot
$build = Join-Path $base 'build'
New-Item -ItemType Directory -Force -Path $build,(Join-Path $build 'classes'),(Join-Path $build 'dex') | Out-Null
foreach ($child in @('classes','dex')) { $clean = [IO.Path]::GetFullPath((Join-Path $build $child)); if (!$clean.StartsWith([IO.Path]::GetFullPath($build) + [IO.Path]::DirectorySeparatorChar)) { throw '构建清理路径无效' }; Remove-Item -LiteralPath $clean -Recurse -Force; New-Item -ItemType Directory -Force -Path $clean | Out-Null }
$platform = Join-Path $SdkRoot 'platforms\android-35\android.jar'
$tools = Join-Path $SdkRoot 'build-tools\35.0.0'
$java = Join-Path $JdkHome 'bin\java.exe'
$jar = Join-Path $JdkHome 'bin\jar.exe'
$zxing = Join-Path $base 'zxing-core-3.5.3.jar'
$sources = Get-ChildItem (Join-Path $base 'src') -Recurse -Filter '*.java' | ForEach-Object FullName
if ($WithTests) { $sources += Join-Path $base 'tests\DeviceTests.java'; $sources += Join-Path $base 'tests\TestDocuments.java' }
& (Join-Path $JdkHome 'bin\javac.exe') -encoding UTF-8 --release 8 -classpath ($platform + ';' + $zxing) -d (Join-Path $build 'classes') @sources
if ($LASTEXITCODE -ne 0) { throw 'Java 编译失败' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$appJar = Join-Path $build 'app.jar'
if (Test-Path -LiteralPath $appJar) { Remove-Item -LiteralPath $appJar }
[IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $build 'classes'),$appJar)
& $java -cp (Join-Path $tools 'lib\d8.jar') com.android.tools.r8.D8 --min-api 26 --lib $platform --output (Join-Path $build 'dex') (Join-Path $build 'app.jar') $zxing
if ($LASTEXITCODE -ne 0) { throw 'DEX 编译失败' }
$manifest = Join-Path $build 'AndroidManifest.xml'
$manifestText = [IO.File]::ReadAllText((Join-Path $base 'AndroidManifest.xml'))
if ($WithTests) { $manifestText = $manifestText.Replace('<application ','<application android:debuggable="true" ').Replace('</application>','<provider android:name=".TestDocuments" android:authorities="org.leafreader.test.documents" android:exported="true" android:permission="android.permission.MANAGE_DOCUMENTS" android:grantUriPermissions="true"/></application>').Replace('</manifest>','<instrumentation android:name=".DeviceTests" android:targetPackage="org.leafreader" android:functionalTest="true"/></manifest>') }
[IO.File]::WriteAllText($manifest,$manifestText,[Text.UTF8Encoding]::new($false))
& (Join-Path $tools 'aapt2.exe') compile --dir (Join-Path $base 'res') -o (Join-Path $build 'resources.zip')
if ($LASTEXITCODE -ne 0) { throw 'APK 资源编译失败' }
& (Join-Path $tools 'aapt2.exe') link -I $platform --manifest $manifest (Join-Path $build 'resources.zip') --version-code 20301 --version-name 2.3.1 --min-sdk-version 26 --target-sdk-version 35 -o (Join-Path $build 'unsigned.apk')
if ($LASTEXITCODE -ne 0) { throw 'APK 资源打包失败' }
$apkZip = [IO.Compression.ZipFile]::Open((Join-Path $build 'unsigned.apk'),[IO.Compression.ZipArchiveMode]::Update)
try { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($apkZip,(Join-Path $build 'dex\classes.dex'),'classes.dex') | Out-Null } finally { $apkZip.Dispose() }
& (Join-Path $tools 'zipalign.exe') -f 4 (Join-Path $build 'unsigned.apk') (Join-Path $build 'aligned.apk')
if ($LASTEXITCODE -ne 0) { throw 'APK 对齐失败' }
$keystore = Join-Path $base 'debug.keystore'
if ($SigningKeystore) { if (!(Test-Path -LiteralPath $SigningKeystore) -or !$env:LEAF_SIGNING_PASSWORD) { throw '发布签名需要现有 SigningKeystore 和 LEAF_SIGNING_PASSWORD 环境变量。' }; $keystore = $SigningKeystore }
elseif (!(Test-Path -LiteralPath $keystore)) { & (Join-Path $JdkHome 'bin\keytool.exe') -genkeypair -keystore $keystore -storepass android -keypass android -alias androiddebugkey -keyalg RSA -keysize 2048 -validity 10000 -dname 'CN=Leaf Reader Debug,O=Leaf Reader,C=CN'; if ($LASTEXITCODE -ne 0) { throw '开发密钥生成失败' } }
$output = Join-Path (Split-Path (Split-Path $base -Parent) -Parent) 'LeafReader-Android-2.3-migration-debug.apk'
if ($WithTests) { $output = Join-Path $build 'LeafReader-instrumented.apk' }
if ($OutputPath) { $output = [IO.Path]::GetFullPath($OutputPath) }
if ($SigningKeystore) { & $java -jar (Join-Path $tools 'lib\apksigner.jar') sign --ks $keystore --ks-key-alias $SigningAlias --ks-pass env:LEAF_SIGNING_PASSWORD --key-pass env:LEAF_SIGNING_PASSWORD --out $output (Join-Path $build 'aligned.apk') }
else { & $java -jar (Join-Path $tools 'lib\apksigner.jar') sign --ks $keystore --ks-key-alias androiddebugkey --ks-pass pass:android --key-pass pass:android --out $output (Join-Path $build 'aligned.apk') }
if ($LASTEXITCODE -ne 0) { throw 'APK 签名失败' }
& $java -jar (Join-Path $tools 'lib\apksigner.jar') verify --verbose $output
if ($LASTEXITCODE -ne 0) { throw 'APK 签名验证失败' }
Write-Output ('生成 APK: ' + $output)
