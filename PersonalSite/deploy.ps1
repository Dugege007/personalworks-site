Set-Location -LiteralPath $PSScriptRoot
$Host.UI.RawUI.WindowTitle = "PersonalSite 一键发布"
try { chcp 65001 | Out-Null } catch { }

Write-Host ""
Write-Host "  ========================================"
Write-Host "   PersonalSite  一键发布"
Write-Host "  ========================================"
Write-Host "  配置来自本目录 .env.deploy"
Write-Host "  将构建站点、按需同步 COS、覆盖服务器网站目录并重载 Nginx"
Write-Host ""

if (-not (Test-Path -LiteralPath ".env.deploy")) {
  Write-Host "  [失败] 缺少 .env.deploy"
  Write-Host "  请复制 .env.deploy.example 为 .env.deploy 并填写后再运行。"
  Write-Host ""
  cmd /c pause
  exit 1
}

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
  Write-Host "  [失败] 未找到 node。请先安装 Node.js，并确保已加入 PATH。"
  Write-Host ""
  cmd /c pause
  exit 1
}

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
  Write-Host "  [失败] 未找到 npm。请重新安装 Node.js 并勾选 Add to PATH。"
  Write-Host ""
  cmd /c pause
  exit 1
}

$confirm = Read-Host "输入 Y 后回车开始发布，直接回车取消"
if ($confirm -ne "Y" -and $confirm -ne "y") {
  Write-Host "  已取消。"
  Write-Host ""
  cmd /c pause
  exit 0
}

Write-Host ""
cmd /c "npm run deploy"
$err = $LASTEXITCODE
Write-Host ""
if ($err -ne 0) {
  Write-Host "  [失败] 发布未完成，退出码 $err"
} else {
  Write-Host "  [完成] 发布结束"
}
Write-Host ""
cmd /c pause
exit $err