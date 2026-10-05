param([string]$Frontend = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Frontend'))
$ErrorActionPreference = 'Stop'
$taskUrl = 'http://127.0.0.1:5173/react-twin/?viewer=ce883'
$taskProject = Split-Path -Parent $PSScriptRoot
$taskBDatabase = Join-Path $taskProject 'Data\local-b\history.sqlite'
if (Test-Path -LiteralPath $taskBDatabase) {
    $taskPython = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
    if (-not (Test-Path -LiteralPath $taskPython)) { $taskPython = (Get-Command python -ErrorAction Stop).Source }
    $taskBListener = Get-NetTCPConnection -LocalPort 8143 -State Listen -ErrorAction SilentlyContinue
    if (-not $taskBListener) {
        $taskBLogDir = Join-Path $taskProject 'Logs'
        New-Item -ItemType Directory -Path $taskBLogDir -Force | Out-Null
        $taskBServer = Start-Process -FilePath $taskPython -ArgumentList @('"'+(Join-Path $PSScriptRoot 'local_b_data.py')+'"','--serve') -WorkingDirectory $taskProject -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskBLogDir 'local-b-api.log') -RedirectStandardError (Join-Path $taskBLogDir 'local-b-api-error.log') -PassThru
        Write-Output "B history API PID: $($taskBServer.Id)"
        Start-Sleep -Milliseconds 700
    }
    $taskBHealth = Invoke-RestMethod 'http://127.0.0.1:8143/api/local-b/health'
    if ($taskBHealth.service -ne 'nextro-local-b') { throw '8143 포트가 다른 서비스에 사용 중입니다. 해당 서비스를 종료하지 않았습니다.' }
}
$taskNode = (Get-Command node -ErrorAction SilentlyContinue).Source
if (-not $taskNode) {
    $taskNode = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
}
if (-not (Test-Path -LiteralPath $taskNode)) { throw 'Node.js가 필요합니다. Node 설치 후 다시 실행하세요.' }
if (-not (Test-Path -LiteralPath (Join-Path $Frontend 'node_modules\vite\bin\vite.js'))) { throw '프론트 폴더에서 npm install을 먼저 실행하세요.' }
if (-not (Test-Path -LiteralPath (Join-Path $Frontend 'public\ce883-build\Build\CE883.loader.js'))) { throw 'Publish-Ce883Local.ps1로 WebGL 빌드를 먼저 복사하세요.' }
$taskListener = Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalAddress -in @('127.0.0.1', '0.0.0.0', '::') }
if (-not $taskListener) {
    $taskLogDir = Join-Path $taskProject 'Logs'
    New-Item -ItemType Directory -Path $taskLogDir -Force | Out-Null
    $taskServer = Start-Process -FilePath $taskNode -ArgumentList @('node_modules/vite/bin/vite.js','--host','127.0.0.1','--port','5173','--strictPort') -WorkingDirectory $Frontend -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskLogDir 'ce883-vite.log') -RedirectStandardError (Join-Path $taskLogDir 'ce883-vite-error.log') -PassThru
    Write-Output "Vite PID: $($taskServer.Id)"
}
Write-Output "검토 화면: $taskUrl"
