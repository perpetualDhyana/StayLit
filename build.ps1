param([switch]$Test)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $taskCompiler)) {
    $taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $taskCompiler)) {
    throw 'This build requires the Windows .NET Framework C# compiler.'
}
$taskBuild = Join-Path $taskRoot 'build'
$taskDist = Join-Path $taskRoot 'dist'
$taskAssets = Join-Path $taskRoot 'assets'
New-Item -ItemType Directory -Force -Path $taskBuild,$taskDist,$taskAssets | Out-Null
$taskSources = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
$taskIcon = Join-Path $taskAssets 'StayLit.ico'
$taskIconBuilder = Join-Path $taskBuild 'MakeIcon.exe'
& $taskCompiler /nologo /utf8output /optimize+ /target:exe "/out:$taskIconBuilder" /r:System.Drawing.dll (Join-Path $taskRoot 'src\Branding.cs') (Join-Path $taskRoot 'tools\MakeIcon.cs')
if ($LASTEXITCODE -ne 0) { throw 'Icon compiler failed.' }
& $taskIconBuilder $taskIcon
if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed.' }
$taskApp = Join-Path $taskDist 'StayLit-1.4.2.exe'
$taskManifest = Join-Path $taskRoot 'src\app.manifest'
$taskCommon = @('/nologo', '/utf8output', '/optimize+', '/platform:anycpu', '/codepage:65001', '/warn:4',
    '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Xml.dll',
    "/win32manifest:$taskManifest", "/win32icon:$taskIcon", "/resource:$taskIcon,StayLit.ico")
& $taskCompiler @taskCommon /target:winexe "/out:$taskApp" @taskSources
if ($LASTEXITCODE -ne 0) { throw 'Application compiler failed.' }

if ($Test) {
    $taskTests = Join-Path $taskBuild 'StayLit.Tests.exe'
    & $taskCompiler @taskCommon /target:exe /main:StayLit.Tests.TestRunner "/out:$taskTests" @taskSources (Join-Path $taskRoot 'tests\TestRunner.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compiler failed.' }
    & $taskTests (Join-Path $taskBuild 'test-report.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
Copy-Item -LiteralPath (Join-Path $taskRoot 'USAGE.md') -Destination (Join-Path $taskDist '使用说明.md') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'LICENSE') -Destination (Join-Path $taskDist 'LICENSE') -Force
$taskArchive = Join-Path $taskDist 'StayLit-1.4.2-win.zip'
Compress-Archive -LiteralPath $taskApp,(Join-Path $taskDist '使用说明.md'),(Join-Path $taskDist 'LICENSE') -DestinationPath $taskArchive -Force
$taskChecksums = @($taskApp, $taskArchive) | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetFileName($_)
}
[IO.File]::WriteAllLines((Join-Path $taskDist 'SHA256SUMS.txt'), $taskChecksums, [Text.Encoding]::ASCII)
[pscustomobject]@{
    Application = $taskApp
    Archive = $taskArchive
    Bytes = (Get-Item -LiteralPath $taskApp).Length
    SHA256 = (Get-FileHash -LiteralPath $taskApp -Algorithm SHA256).Hash
} | ConvertTo-Json
