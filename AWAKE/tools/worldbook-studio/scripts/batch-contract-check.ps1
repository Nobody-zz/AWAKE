param(
    [string]$ContractPath = (Join-Path (Split-Path -Parent $PSScriptRoot) '..\..\docs\superpowers\specs\2026-08-25-worldbook-studio-batch-authoring-contracts-r14.json')
)
$ErrorActionPreference = 'Stop'
$checks = 0
$failures = [System.Collections.Generic.List[string]]::new()
function Check([bool]$Condition, [string]$Label) {
    $script:checks++
    if (-not $Condition) { $script:failures.Add($Label) }
}
function Has-Property($Object, [string]$Name) {
    return $null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]
}
$raw = Get-Content -LiteralPath $ContractPath -Raw -Encoding UTF8
$contract = $raw | ConvertFrom-Json
Check ($contract.revision -eq 14) 'contract revision is 14'
Check ($contract.schema_version -eq 'awake.worldbook.batch-authoring.contracts.v2') 'contract schema version'
Check ($contract.test_entrypoints.wired_now -eq $true) 'release wiring is enabled'
Check ($contract.test_entrypoints.release_must_fail_when_wired_now_is_false -eq $true) 'unwired release remains fail-closed'
$schemaKeys = @($contract.schemas.PSObject.Properties.Name)
$schemaIds = @($contract.schemas.PSObject.Properties | ForEach-Object { [string]$_.Value.schema_id })
Check ($schemaKeys.Count -gt 0) 'schemas exist'
Check (($schemaIds | Sort-Object -Unique).Count -eq $schemaIds.Count) 'schema ids unique'
$commonKeys = @($contract.common_fields.PSObject.Properties.Name)
$refs = [regex]::Matches($raw, '"\$ref"\s*:\s*"([^"]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
foreach ($ref in $refs) { Check (($ref -like 'common.*' -and ($commonKeys -contains $ref.Substring(7))) -or ($schemaKeys -contains $ref) -or ($schemaIds -contains $ref)) "ref resolves: $ref" }
foreach ($entry in $contract.schemas.PSObject.Properties) {
    $schema = $entry.Value
    $fields = @($schema.fields.PSObject.Properties.Name)
    foreach ($required in @($schema.required)) { Check ($fields -contains [string]$required) "$($entry.Name) required field: $required" }
    foreach ($condition in @($schema.conditional_required.PSObject.Properties)) { foreach ($required in @($condition.Value)) { Check ($fields -contains [string]$required) "$($entry.Name) conditional field: $($condition.Name)/$required" } }
    if ($null -ne $schema.conditional_forbidden) { foreach ($condition in @($schema.conditional_forbidden.PSObject.Properties)) { foreach ($forbidden in @($condition.Value)) { Check ($fields -contains [string]$forbidden) "$($entry.Name) forbidden field: $($condition.Name)/$forbidden" } } }
}
foreach ($routeEntry in $contract.api.routes.PSObject.Properties) {
    $route = $routeEntry.Value
    Check (($schemaKeys -contains [string]$route.request_ref) -or ($schemaIds -contains [string]$route.request_ref)) "route request ref: $($routeEntry.Name)"
    Check (($schemaKeys -contains [string]$route.response_ref) -or ($schemaIds -contains [string]$route.response_ref)) "route response ref: $($routeEntry.Name)"
    foreach ($routeError in @($route.errors)) {
        Check (Has-Property $contract.error_definitions ([string]$routeError.code)) "route error definition: $($routeError.code)"
        if (Has-Property $contract.error_definitions ([string]$routeError.code)) {
            $definition = $contract.error_definitions.PSObject.Properties[[string]$routeError.code].Value
            Check ([int]$definition.http_status -eq [int]$routeError.http_status) "route HTTP status: $($routeError.code)"
            Check ([bool]$definition.retryable -eq [bool]$routeError.retryable) "route retryable: $($routeError.code)"
            Check ([string]$definition.error_ref -eq [string]$routeError.error_ref) "route error ref: $($routeError.code)"
        }
    }
}
$item = $contract.schemas.item
Check (@($item.required) -contains 'active_attempt_stage') 'item active attempt stage required'
Check ([string]$item.conditional_constraints.'metadata_pending.active_attempt_stage' -eq 'none') 'metadata pending waits with no active stage'
Check ([string]$item.conditional_constraints.'metadata_pending.result_stage' -eq 'facts') 'metadata pending committed result stage'
Check ([string]$item.conditional_constraints.'metadata_running.active_attempt_stage' -eq 'metadata') 'metadata running active stage'
Check ([string]$item.conditional_constraints.'metadata_running.result_stage' -eq 'facts') 'metadata running committed result stage'
Check (@($item.conditional_required.metadata_running) -contains 'lease') 'metadata running lease required'
Check (@($item.required) -contains 'claim_generation' -or @($contract.schemas.manifest.required) -contains 'claim_generation') 'claim generation persisted'
$attempt = $contract.schemas.attempt
Check ([string]$attempt.validation_rules.stage_must_match_item_active_attempt_stage -eq 'True') 'attempt stage binds active stage'
Check (@($attempt.conditional_required.'stage=metadata') -contains 'source_fact_set_hash') 'metadata attempt source fact set required'
$metadata = $contract.schemas.metadata_result
Check ([bool]$metadata.validation_rules.source_fact_set_hash_must_equal_server_recomputed_accepted_fact_set_hash) 'metadata source fact set recompute'
Check ([bool]$metadata.validation_rules.attempt_source_fact_set_hash_must_equal_result_source_fact_set_hash) 'metadata attempt/result source set binding'
Check ([bool]$metadata.validation_rules.current_accepted_facts_are_rechecked_before_metadata_commit) 'metadata current facts recheck'
Check (-not (@($contract.schemas.cache_metadata_payload.required) -contains 'source_fact_set_hash')) 'cache metadata excludes batch source fact set'
Check (@($contract.schemas.cache_materialization.conditional_required.'stage=metadata') -contains 'source_fact_set_hash') 'cache materialization source fact set'
$publicItem = $contract.schemas.public_item
Check (-not (Has-Property $publicItem.fields 'document_path')) 'public item hides document path'
$publicUnit = $contract.schemas.public_source_unit
Check (@($publicUnit.conditional_forbidden.'source_scope=prebatch') -contains 'batch_id') 'prebatch public unit hides batch id'
Check (-not (Has-Property $contract.schemas.batch_create_result_item.fields 'document_path')) 'create result hides document path'
Check (-not (Has-Property $contract.schemas.batch_create_result_item.fields 'reservation_id')) 'create result hides reservation id'
Check (Has-Property $contract.schemas.public_metadata_result.fields 'title') 'public metadata title'
Check (Has-Property $contract.schemas.public_metadata_result.fields 'summary') 'public metadata summary'
Check (@($contract.schemas.public_review_projection.required) -contains 'metadata_results') 'item detail exposes metadata candidates'
Check ([string]$contract.schemas.'api.response.report'.fields.data.'$ref' -eq 'awake.worldbook.batch-report-public.v2') 'report uses public projection'
Check ([string]$contract.schemas.public_manifest.fields.report_summary.'$ref' -eq 'awake.worldbook.batch-report-counts.v2') 'manifest report summary is closed'
Check (-not (Has-Property $contract.schemas.public_item.fields 'last_error')) 'public item hides raw last error'
Check (Has-Property $contract.schemas.public_item.fields 'last_error_code') 'public item exposes error code'
Check (Has-Property $contract.schemas.public_item.fields 'last_error_message') 'public item exposes sanitized error message'
$reportDoc = $contract.schemas.report_created_document
Check (-not (@($reportDoc.required) -contains 'document_path')) 'report hides document path'
Check (@($reportDoc.conditional_required.'status=failed') -contains 'error_code') 'failed report error code'
Check (@($reportDoc.conditional_required.'status=failed') -contains 'message') 'failed report message'
Check (-not (@($reportDoc.required) -contains 'document_id')) 'failed report does not require document id'
$errorSchema = $contract.schemas.error
Check (-not (Has-Property $errorSchema.fields 'reservation_ref')) 'error hides reservation path'
Check (Has-Property $errorSchema.fields 'reservation_id') 'error exposes opaque reservation id'
Check ([bool]$errorSchema.fields.details.additional_properties -eq $false) 'error details whitelist'
Check (@($errorSchema.conditional_required.'code=WB-BATCH-IDEMPOTENCY-INFLIGHT-409') -contains 'reservation_id') 'inflight reservation id'
Check (Has-Property $contract.error_definitions 'WB-BATCH-PROMOTION-409') 'promotion failure error definition'
Check (Has-Property $contract.error_definitions 'WB-BATCH-PROMOTION-RECONCILE-409') 'promotion reconcile error definition'
$createRoute = $contract.api.routes.create
Check (@($createRoute.errors | ForEach-Object { $_.code }) -contains 'WB-BATCH-PROMOTION-409') 'create promotion failure route'
Check (@($createRoute.errors | ForEach-Object { $_.code }) -contains 'WB-BATCH-PROMOTION-RECONCILE-409') 'create promotion reconcile route'
$reservation = $contract.schemas.create_reservation
Check (@($reservation.required) -contains 'reservation_id') 'reservation id required'
Check (@($reservation.required) -contains 'fence_token') 'reservation fence required'
Check (@($reservation.required) -contains 'claim_expires_at') 'reservation claim expiry required'
Check ([bool]$reservation.validation_rules.reserved_state_never_allocates_a_second_batch_for_the_same_key) 'reservation single allocation'
Check (@($reservation.required) -contains 'allocation_id') 'allocation id required'
Check (@($reservation.fields.state.values) -contains 'allocation_pending') 'allocation pending state'
Check ([bool]$reservation.validation_rules.allocation_pending_recovery_replays_same_allocation_id) 'allocation pending recovery anchor'
Check ([string]$contract.promotion_contract.promotion_claim_rebind.old_fence_behavior -match 'WB-BATCH-CLAIM-409') 'promotion old fence rejected'
Check ([bool]$contract.promotion_contract.journal_state_to_reservation.aborted.batch_id_retained) 'aborted reservation mapping'
Check ([string]$contract.promotion_contract.journal_state_to_reservation.quarantined.reservation_state -eq 'needs_reconcile') 'quarantined reservation mapping'
Check ([string]$contract.stage_contract.facts_review_to_metadata.facts_only_consent_path.next_entry -match 'facts_and_metadata') 'facts-only does not start metadata'
Check ([string]$contract.stage_contract.facts_review_to_metadata.facts_and_metadata_consent_path.next_entry -match 'start.stage=metadata') 'metadata stage entry'
Check (@($contract.schemas.'api.retry.request'.required) -contains 'stage') 'retry stage required'
Check ([string]$contract.schemas.'api.retry.request'.fields.stage.type -eq 'enum') 'retry stage enum'
Check ([bool]$contract.schemas.'api.retry.request'.validation_rules.stage_metadata_transitions_to_metadata_pending_with_active_stage_none) 'metadata retry requeues pending'
Check ([bool]$contract.schemas.'api.retry.request'.validation_rules.provider_is_called_only_by_follow_up_start_stage_metadata) 'retry does not call provider'
Check ([string]$contract.stage_contract.facts_review_to_metadata.metadata_start.transition -match 'metadata_pending -> metadata_running') 'metadata start running transition'
Check ([string]$contract.stage_contract.facts_review_to_metadata.metadata_failure.retry_entry -like '*retry-item(stage=metadata*') 'metadata failure retry entry'
Check (@($contract.schemas.consent.required) -contains 'authorized_item_ids') 'consent authorized item ids'
Check (@($contract.schemas.'api.consent.request'.required) -contains 'authorized_item_ids') 'consent request authorized item ids'
Check (@($contract.schemas.'api.start.request'.required) -contains 'target_item_ids') 'start target item ids'
Check ([string]$contract.api.routes.start.cas.target_item_field -eq 'target_item_ids') 'start route target item CAS'
Check ([string]$contract.formulas.consent_binding_hash -like '*authorized_item_ids*') 'consent binding includes authorized item ids'
Check ([string]$contract.stage_contract.facts_review_to_metadata.batch_start.atomicity -like '*no target item is transitioned*') 'batch start atomic selection'
Check (@($contract.schemas.item.conditional_required.metadata_pending) -contains 'accepted_fact_ids') 'metadata pending accepted facts required'
Check (@($contract.schemas.item.conditional_required.metadata_running) -contains 'accepted_fact_ids') 'metadata running accepted facts required'
Check ([string]$contract.stage_contract.consent_scope_matrix.metadata_retry -like '*newly issued facts_and_metadata consent*') 'metadata retry consent reissue'
Check ([bool]$contract.schemas.consent.validation_rules.metadata_consent_consumed_at_metadata_target_schedule_commit_before_provider) 'metadata consent schedule timing'
Check (-not (Has-Property $contract.schemas.consent.validation_rules 'facts_and_metadata_consent_is_consumed_after_metadata_stage_commit')) 'no metadata result-time consent rule'
Check ([bool]$contract.schemas.'api.start.request'.validation_rules.metadata_consent_is_consumed_after_target_schedule_journal_commit_before_provider) 'start consent before provider'
Check ([string]$contract.consent_consumption_contract.facts_and_metadata_metadata_stage.order[3] -eq 'CAS consent state issued -> consumed') 'consent order CAS'
Check ([string]$contract.consent_consumption_contract.facts_and_metadata_metadata_stage.order[4] -eq 'only then invoke Provider') 'provider after consent'
Check ([string]$contract.stage_contract.facts_review_to_metadata.metadata_start.consent -like '*before any Provider call*') 'metadata start consent before provider'
Check ([string]$contract.stage_contract.facts_review_to_metadata.metadata_commit -like '*already consumed at the metadata target scheduling journal commit before Provider invocation*') 'metadata result does not consume consent'
Check ([string]$contract.stage_contract.facts_review_to_metadata.metadata_commit -notlike '*consent becomes consumed') 'no stale result-time consent wording'
Check ([string]$contract.promotion_contract.journal_authority_note -like '*separate contracts*') 'promotion journal vocabulary separated'
Check ([string]$contract.consent_consumption_contract.facts_and_metadata_metadata_stage.order[3] -eq 'CAS consent state issued -> consumed') 'metadata consent CAS remains fourth step'
$promotion = $contract.promotion_contract
Check ([string]$promotion.reservation_commit_cas.from -eq 'promoting') 'promotion reservation CAS source'
Check ([string]$promotion.reservation_commit_cas.to -eq 'committed') 'promotion reservation CAS target'
Check ([string]$promotion.reservation_commit_cas.must_occur_after -eq 'promotion_journal.state=committed') 'promotion CAS order'
Check ([string]$promotion.reservation_commit_cas.must_occur_before -eq 'scan.promotion.state projection update') 'promotion projection order'
Check ([string]$promotion.reservation_state_mapping.failed.error_code -eq 'WB-BATCH-PROMOTION-409') 'failed reservation API mapping'
Check ([string]$promotion.reservation_state_mapping.needs_reconcile.error_code -eq 'WB-BATCH-PROMOTION-RECONCILE-409') 'reconcile reservation API mapping'
Check (@($contract.prebatch_layout.promotion_crash_windows.PSObject.Properties.Name) -contains 'after_journal_commit_before_reservation_commit') 'promotion crash window journal/CAS'
Check (@($contract.prebatch_layout.promotion_crash_windows.PSObject.Properties.Name) -contains 'after_reservation_commit_before_scan_projection') 'promotion crash window projection'
$formula = [string]$contract.formulas.fact_id
Check ($formula -eq 'fact.<SHA-256(CanonicalJson({batch_id,item_id,cache_fact_fingerprint}))>') 'fixed fact id formula'
Check ([int]$contract.formulas.fact_id_length -eq 69) 'fixed fact id length'
Check ([string]$contract.schemas.scan.fields.prebatch_path.pattern -notmatch '/\\?$') 'scan path has no trailing slash'
Check ([string]$contract.schemas.snapshot.fields.source_text_path.pattern -notmatch '\[A-Za-z0-9._/-\]') 'snapshot path does not use broad regex'
Check (@($contract.cache_commit_protocol.recovery_table | ForEach-Object { $_.observed }) -contains 'marker-only orphan with no entry or payload') 'cache marker-only recovery'
Check (@($contract.cache_commit_protocol.recovery_table | ForEach-Object { $_.observed }) -contains 'published marker but entry missing') 'cache published missing entry recovery'
Check (@($contract.cache_commit_protocol.recovery_table | ForEach-Object { $_.observed }) -contains 'published marker but payload missing') 'cache published missing payload recovery'
Check (@($contract.cache_commit_protocol.recovery_table | ForEach-Object { $_.observed }) -contains 'marker.state=needs_reconcile') 'cache needs reconcile recovery'
if ($failures.Count -gt 0) { Write-Output ("BATCH CONTRACT CHECK: FAIL ($($checks - $failures.Count)/$checks)"); $failures | ForEach-Object { Write-Output ("- $_") }; exit 1 }
Write-Output ("BATCH CONTRACT CHECK: PASS ($checks/$checks)")
