param([string]$Frontend = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Frontend'))
$ErrorActionPreference = 'Stop'
$taskProject = Split-Path -Parent $PSScriptRoot
$taskBuild = Join-Path $taskProject 'Builds\CE883'
if (-not (Test-Path -LiteralPath (Join-Path $taskBuild 'Build\CE883.loader.js'))) { throw 'CE883 WebGL 빌드가 없습니다. Unity에서 먼저 빌드하세요.' }
$taskDestination = Join-Path $Frontend 'public\ce883-build'
New-Item -ItemType Directory -Force -Path $taskDestination | Out-Null
Copy-Item -Path (Join-Path $taskBuild '*') -Destination $taskDestination -Recurse -Force
$taskHashes = @('CE883.loader.js','CE883.framework.js','CE883.wasm','CE883.data') | ForEach-Object {
    (Get-FileHash -LiteralPath (Join-Path $taskDestination "Build\$_") -Algorithm SHA256).Hash
}
$taskRevision = $taskHashes -join '-'
@{ revision = $taskRevision } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskDestination 'build-version.json') -Encoding utf8
Write-Output "로컬 복사 완료: $taskDestination"
