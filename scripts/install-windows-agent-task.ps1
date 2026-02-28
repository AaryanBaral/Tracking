<#
PURPOSE:
- Installs Agent.Windows as a Scheduled Task that runs at user logon (in the user session).
- Hidden task, restart on failure.
- Writes %ProgramData%\EmployeeTracker\agent-windows-runner.cmd to set env vars and run the agent.

PUBLISH:
  dotnet publish .\Agent.Windows\Agent.Windows.csproj -c Release -r win-x64 --self-contained true

INSTALL (example):
  .\scripts\install-windows-agent-task.ps1 `
    -ExePath "C:\EmployeeTracker\Agent.Windows.exe" `
    -Token "YOUR_TOKEN" `
    -LocalApiUrl "http://127.0.0.1:43121" `
    -PollSeconds 1 `
    -FailureExitSeconds 60
#>

param(
  [Parameter(Mandatory = $true)]
  [string]$ExePath,

  [Parameter(Mandatory = $true)]
  [string]$Token,

  [string]$LocalApiUrl = "http://127.0.0.1:43121",
  [int]$PollSeconds = 1,
  [int]$FailureExitSeconds = 60,

  [string]$TaskName = "EmployeeTrackerAgent"
)

if (-not (Test-Path $ExePath)) {
  throw "ExePath not found: $ExePath"
}

$programDataDir = Join-Path $env:ProgramData "EmployeeTracker"
New-Item -ItemType Directory -Force -Path $programDataDir | Out-Null

$runnerPath = Join-Path $programDataDir "agent-windows-runner.cmd"

$cmd = @"
@echo off
setlocal
set "AGENT_LOCAL_API_URL=$LocalApiUrl"
set "AGENT_LOCAL_API_TOKEN=$Token"
set "AGENT_POLL_SECONDS=$PollSeconds"
set "AGENT_FAILURE_EXIT_SECONDS=$FailureExitSeconds"
"$ExePath"
"@

Set-Content -Path $runnerPath -Value $cmd -Encoding ASCII
Write-Host "Wrote runner: $runnerPath"

$action = New-ScheduledTaskAction -Execute "cmd.exe" -Argument "/c `"$runnerPath`""

$currentUser = "$env:UserDomain\$env:UserName"
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $currentUser

$settings = New-ScheduledTaskSettingsSet `
  -AllowStartIfOnBatteries `
  -DontStopIfGoingOnBatteries `
  -StartWhenAvailable `
  -MultipleInstances IgnoreNew `
  -RestartCount 999 `
  -RestartInterval (New-TimeSpan -Minutes 1) `
  -Hidden

$principal = New-ScheduledTaskPrincipal -UserId $currentUser -LogonType Interactive -RunLevel Limited

$task = New-ScheduledTask -Action $action -Trigger $trigger -Settings $settings -Principal $principal

Register-ScheduledTask -TaskName $TaskName -InputObject $task -Force | Out-Null
Write-Host "Installed scheduled task: $TaskName"

Start-ScheduledTask -TaskName $TaskName
Write-Host "Started scheduled task: $TaskName"
