# 一键构建：生成可直接分发的自包含单文件
# 用法：.\build.ps1            -> dist\DSH-BossKey.exe
#       .\build.ps1 -SelfContained:$false  -> 精简版（需目标机器装 .NET 运行时）
param(
    [string]$Out = "dist",
    [bool]$SelfContained = $true
)

$ErrorActionPreference = "Stop"

$args = @(
    "publish", "-c", "Release", "-r", "win-x64",
    "-p:SelfContained=$($SelfContained.ToString().ToLower())",
    "-p:PublishSingleFile=true",
    "-p:DebugType=none",
    "-o", $Out
)

if ($SelfContained) {
    $args += "-p:EnableCompressionInSingleFile=true"
}

Write-Host "dotnet $($args -join ' ')" -ForegroundColor DarkGray
& dotnet @args

if ($LASTEXITCODE -ne 0) { throw "构建失败（exit $LASTEXITCODE）" }

$exe = Join-Path $Out "DSH-BossKey.exe"
if (Test-Path $exe) {
    $mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "`n完成：$exe（$mb MB）" -ForegroundColor Green
}
