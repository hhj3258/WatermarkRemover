param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    # 각 항목: "수정 내용|수정 이유" 형식으로 입력
    # 예) "WM_SETTINGCHANGE 추가|PaintDesktopVersion 변경 후 Explorer가 즉시 반영 못 해서"
    [Parameter(Mandatory=$true)]
    [string[]]$Changes
)

$root     = $PSScriptRoot
$project  = "$root\WatermarkRemover\WatermarkRemover.csproj"
$binPath  = "$root\WatermarkRemover\bin\Release\net8.0-windows\win-x64\publish\WatermarkRemover.exe"
$pubDir   = "$root\publish\ver_$Version"
$pubExe   = "$pubDir\WatermarkRemover.exe"
$taskName = "WatermarkRemover"

# 1) 퍼블리시 (단일 exe)
Write-Host "[1/4] 퍼블리시 중..."
dotnet publish $project -c Release -r win-x64 --self-contained false --nologo
if ($LASTEXITCODE -ne 0) { Write-Error "퍼블리시 실패"; exit 1 }

# 2) 버전 폴더 생성, exe + CHANGES.md 복사
Write-Host "[2/4] publish\ver_$Version 생성..."
if (Test-Path $pubDir) {
    Write-Error "이미 존재하는 버전입니다: ver_$Version"
    exit 1
}
New-Item -Path $pubDir -ItemType Directory -Force | Out-Null
Copy-Item $binPath $pubExe -Force

$date = Get-Date -Format "yyyy-MM-dd"
$lines = @("# ver_$Version ($date)", "")
foreach ($c in $Changes) {
    $parts = $c -split '\|', 2
    $lines += "- $($parts[0].Trim())"
    if ($parts.Length -eq 2) {
        $lines += "  - 이유: $($parts[1].Trim())"
    }
}
$lines | Set-Content "$pubDir\CHANGES.md" -Encoding UTF8
Write-Host "       -> $pubExe"
Write-Host "       -> $pubDir\CHANGES.md"

# 3) Task Scheduler 업데이트 (관리자 권한 필요)
Write-Host "[3/4] Task Scheduler 업데이트..."
$taskScript = @"
`$action    = New-ScheduledTaskAction -Execute '$pubExe'
`$trigger   = New-ScheduledTaskTrigger -AtLogOn -User '$env:USERNAME'
`$settings  = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0 -MultipleInstances IgnoreNew
`$principal = New-ScheduledTaskPrincipal -UserId '$env:USERNAME' -RunLevel Highest -LogonType Interactive
Register-ScheduledTask -TaskName '$taskName' -Action `$action -Trigger `$trigger -Settings `$settings -Principal `$principal -Force
Write-Host 'Task updated'
"@
$tmpScript = "$env:TEMP\wmr_task_update.ps1"
$taskScript | Set-Content $tmpScript -Encoding UTF8
Start-Process powershell -Verb RunAs -WindowStyle Hidden -ArgumentList "-ExecutionPolicy Bypass -File `"$tmpScript`"" -Wait

# 4) 완료
Write-Host "[4/4] 완료!"
Write-Host ""
Write-Host "  버전  : ver_$Version"
Write-Host "  경로  : $pubExe"
Write-Host "  스케줄러: $taskName -> ver_$Version"
