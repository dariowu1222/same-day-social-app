# 同頻 Today — 實機測試一鍵啟動（Windows）
# 由 device-test.bat 呼叫，請勿直接 commit .env.device
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

Write-Host '=== 同頻 Today 實機測試一鍵啟動 ===' -ForegroundColor Cyan

# 1. 偵測電腦的區網 IPv4
$ipconf = Get-NetIPConfiguration |
  Where-Object { $_.IPv4DefaultGateway -and $_.NetAdapter.Status -eq 'Up' } |
  Select-Object -First 1
if (-not $ipconf) {
  Write-Host '找不到可用網路連線，請確認已連上 Wi-Fi / 網路後再執行。' -ForegroundColor Red
  exit 1
}
$ip = ($ipconf.IPv4Address | Select-Object -First 1).IPAddress
Write-Host "1/5 電腦區網 IP：$ip" -ForegroundColor Green

# 2. 寫入 frontend/.env.device
Set-Content -Path (Join-Path $repo 'frontend\.env.device') -Value "VITE_API_BASE_URL=http://${ip}:5000" -Encoding ASCII
Write-Host "2/5 已寫入 frontend\.env.device → http://${ip}:5000" -ForegroundColor Green

# 3. 防火牆放行 5000 埠（需要管理員權限，只會在第一次跳 UAC）
$rule = Get-NetFirewallRule -DisplayName 'SameDay Backend 5000' -ErrorAction SilentlyContinue
if ($rule) {
  Write-Host '3/5 防火牆規則已存在，略過' -ForegroundColor Green
} else {
  Write-Host '3/5 建立防火牆規則（會跳出系統管理員確認視窗，請按「是」）...' -ForegroundColor Yellow
  Start-Process powershell -Verb RunAs -Wait -ArgumentList @(
    '-NoProfile', '-Command',
    "New-NetFirewallRule -DisplayName 'SameDay Backend 5000' -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow | Out-Null"
  )
  Write-Host '    防火牆已放行 TCP 5000' -ForegroundColor Green
}

# 4. 啟動後端（新視窗，0.0.0.0:5000，手機可連）
Write-Host '4/5 啟動後端（另開視窗）...' -ForegroundColor Green
Start-Process cmd -WorkingDirectory (Join-Path $repo 'backend') -ArgumentList '/k', 'dotnet run --launch-profile device'

# 5. 前端打包 + 同步 + 開 Android Studio
Write-Host '5/5 前端打包中（第一次會先 npm install，需要幾分鐘）...' -ForegroundColor Green
Set-Location (Join-Path $repo 'frontend')
if (-not (Test-Path 'node_modules')) { npm install }
npm run cap:device:android

Write-Host ''
Write-Host '=== 完成 ===' -ForegroundColor Cyan
Write-Host "下一步：" -ForegroundColor Yellow
Write-Host "  1. 手機連到跟電腦同一個 Wi-Fi"
Write-Host "  2. 手機瀏覽器先開 http://${ip}:5000 確認看得到「同頻 Today 正在啟動」"
Write-Host "  3. 在 Android Studio 上方選你的手機 → 按綠色 Run"
Write-Host "（手機需開啟 USB 偵錯，詳見 docs/device-build.md）"
