<#
PURPOSE:
- Removes the Scheduled Task and runner cmd file.

UNINSTALL:
  .\scripts\uninstall-windows-agent-task.ps1
#>

param(
  [string]$TaskName = "EmployeeTrackerAgent"
)

$programDataDir = Join-Path $env:ProgramData "EmployeeTracker"
$runnerPath = Join-Path $programDataDir "agent-windows-runner.cmd"

if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
  try { Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue } catch {}
  Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false | Out-Null
  Write-Host "Removed scheduled task: $TaskName"
} else {
  Write-Host "Scheduled task not found: $TaskName"
}

if (Test-Path $runnerPath) {
  Remove-Item $runnerPath -Force
  Write-Host "Removed runner: $runnerPath"
}
