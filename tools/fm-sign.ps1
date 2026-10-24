# Signs FileMind.* binaries with the local dev cert so Smart App Control
# (CodeIntegrity 3077) lets them load on this dev box. Cert: a self-signed
# code-signing cert imported into CurrentUser\Root + CurrentUser\TrustedPublisher
# (see docs/dev/SIGNING.md if it ever needs recreating — generated once with
# New-SelfSignedCertificate; thumbprint lives only on this machine).
# Usage: powershell tools/fm-sign.ps1 <dir-with-built-binaries>
param([Parameter(Mandatory = $true)][string]$Dir)

$pfx = "C:\tmp\fm-dev-sign.pfx"
$signtool = Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools" `
    -Recurse -Filter signtool.exe | Where-Object FullName -like "*x64*" |
    Sort-Object FullName | Select-Object -Last 1
if (-not (Test-Path $pfx)) { Write-Error "dev signing cert missing: $pfx"; exit 2 }
if (-not $signtool) { Write-Error "signtool.exe not found in NuGet cache"; exit 2 }

Get-ChildItem $Dir -Filter "FileMind*.dll" + (Get-ChildItem $Dir -Filter "FileMind*.exe") |
ForEach-Object {
    & $signtool.FullName sign /fd sha256 /f $pfx /p fmdev $_.FullName | Out-Null
    if ($LASTEXITCODE -eq 0) { Write-Output "signed $($_.Name)" }
    else { Write-Error "FAILED to sign $($_.Name)"; exit 1 }
}
