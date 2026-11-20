param([ValidateSet('qoder','qoder-cn')][string]$Edition = 'qoder')

$ErrorActionPreference = 'Stop'
$npm = (Get-Command npm.cmd -ErrorAction Stop).Source
$package = if ($Edition -eq 'qoder-cn') { '@qodercn-ai/qodercn-agent-sdk@1.0.60' } else { '@qoder-ai/qoder-agent-sdk@1.0.60' }
$directory = Join-Path $PSScriptRoot $Edition
New-Item -ItemType Directory -Path $directory -Force | Out-Null
& $npm install --prefix $directory --no-audit --no-fund --save-exact $package 'zod@4'
if ($LASTEXITCODE -ne 0) { throw 'Qoder SDK installation failed.' }
Write-Output 'Select the matching Qoder quota source in Orbitlet. The Qoder CLI must be signed in or have a personal access token in its environment.'
