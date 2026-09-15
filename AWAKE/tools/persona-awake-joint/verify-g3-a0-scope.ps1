param([Parameter(Mandatory=$true)][string]$ReportPath)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'persona-awake-joint.ps1')
$root=$script:JointAwakeRoot
$checks=[Collections.Generic.List[object]]::new()
function C([string]$id,[bool]$ok,[string]$detail){$checks.Add([ordered]@{id=$id;passed=$ok;detail=$detail})}
function J([string]$p){(Read-JointJsonFile (Assert-JointReadablePath $p $root $p) $p).Value}
try {
  $out=Assert-JointOutputPath $ReportPath 'ReportPath'
  $scopePath=Join-Path $root 'docs/persona-awake-joint-g3-a0-scope.v1.json'
  $scope=J $scopePath; $approval=J (Join-Path $root $scope.authority.approvalRecord); $lease=J (Join-Path $root $scope.authority.leaseRecord)
  $scopeHash=(Get-FileHash $scopePath -Algorithm SHA256).Hash
  C 'a0_scope_hash' ([string]$approval.scopeSha256 -eq $scopeHash -and [string]$lease.scopeSha256 -eq $scopeHash) 'Approval and lease bind current A0 scope.'
  C 'a0_approval' ([string]$approval.recordStatus -eq 'approved' -and [bool]$approval.userSignoff.confirmed) 'A0 approval and user signoff are present.'
  $scopeSet=@($scope.writeSet|ForEach-Object{([string]$_).Replace('\\','/').ToLowerInvariant()}); $leaseSet=@($lease.writeSet|ForEach-Object{([string]$_).Replace('\\','/').ToLowerInvariant()})
  C 'a0_lease_exact' ([string]$lease.status -eq 'active' -and ((@($scopeSet|Sort-Object)-join "`n") -eq (@($leaseSet|Sort-Object)-join "`n"))) 'A0 active lease and exact write set match.'
  $completion=J (Join-Path $root $scope.predecessor.completionPath)
  $completionHash=(Get-FileHash (Join-Path $root $scope.predecessor.completionPath) -Algorithm SHA256).Hash
  C 'g0c_completion' ([string]$completion.recordStatus -eq 'complete' -and [string]$scope.predecessor.completionSha256 -eq [string]$completion.recordSha256 -and [string]$completion.recordSha256 -eq 'CBD830A9D9260F74034107C392EB67C279FE286C88DBBFD1F6288D8174E8FE9A') 'G0C completion record is bound.'
  $matrix=J (Join-Path $root $scope.checkMatrixPath); $matrixHash=(Get-FileHash (Join-Path $root $scope.checkMatrixPath) -Algorithm SHA256).Hash
  C 'matrix_hash' ([string]$scope.checkMatrixSha256 -eq $matrixHash) 'A0 matrix hash matches.'
  $staticReportPath=Join-Path $root 'tools/persona-awake-joint/artifacts/g3-a0-static-report.json'
  $null=& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'verify-runtime-bridge-static.ps1') -ReportPath $staticReportPath
  $static=J $staticReportPath
  $expected=@($matrix.checks|Where-Object{([string]$_.layer -eq 'G3-A') -or ([string]$_.expectedDisposition -eq 'legacy_tool_false_negative')}|ForEach-Object{[string]$_.staticCheckId})
  $actual=@($static.checks|Where-Object{ -not [bool]$_.passed -and $expected -contains [string]$_.id}|ForEach-Object{[string]$_.id})
  $allFailures=@($static.checks|Where-Object{ -not [bool]$_.passed}|ForEach-Object{[string]$_.id})
  C 'static_report_status' ([string]$static.status -eq 'reject' -and [int]$static.exitCode -eq 10) 'Static bridge remains reject/10 before runtime implementation.'
  C 'static_report_exact_matrix' ((@($actual|Sort-Object)-join "`n") -eq (@($expected|Sort-Object)-join "`n") -and $allFailures.Count -eq 9) 'Static failures map exactly to A0 matrix; G3-B is excluded from blocking.'
  $failed=@($checks|Where-Object{-not $_.passed}); $report=[ordered]@{schemaVersion='awake.persona.g3-a0-scope-report.v1';status=if($failed.Count -eq 0){'pass'}else{'reject'};exitCode=if($failed.Count -eq 0){0}else{10};checks=@($checks)}; Write-JointReport $report $out; Remove-Item -LiteralPath $staticReportPath -Force; exit $report.exitCode
} catch { throw }
