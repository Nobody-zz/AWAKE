Set-StrictMode -Version Latest

$script:JointToolRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$script:JointAwakeRoot = [IO.Path]::GetFullPath((Join-Path $script:JointToolRoot '..\..'))
$script:JointWorkspaceRoot = [IO.Path]::GetFullPath((Join-Path $script:JointAwakeRoot '..'))
$script:JointUtf8 = [Text.UTF8Encoding]::new($false)
$script:JointStatusCodes = [ordered]@{
    pass = 0
    reject = 10
    blocked = 20
    not_attempted = 30
    error = 40
}
$script:JointFixtureInputSchema = 'awake.persona.fixture-input.v1'
$script:JointFixtureReportSchema = 'awake.persona.fixture-report.v1'
$script:JointAuthoringSchema = 'awake.persona.authoring.v2'
$script:JointExportSchema = 'awake.persona.export.v1'
$script:JointDefinitionSchema = 'awake.persona.definition.v1'
$script:JointRegistrySchema = 'awake.persona.tags.v1'
$script:JointSelectionSchema = 'awake.persona.selection.v1'

if (-not ('AwakePersonaJointCanonicalJson' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

public static class AwakePersonaJointCanonicalJson
{
    public static string Canonicalize(byte[] bytes)
    {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        ValidateDuplicates(bytes);
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 100
        });
        StringBuilder builder = new StringBuilder();
        Write(builder, document.RootElement);
        builder.Append('\n');
        return builder.ToString();
    }

    private static void ValidateDuplicates(byte[] bytes)
    {
        Utf8JsonReader reader = new Utf8JsonReader(bytes, new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 100
        });
        Stack<HashSet<string>> scopes = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                scopes.Push(new HashSet<string>(StringComparer.Ordinal));
            }
            else if (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (scopes.Count == 0) throw new JsonException("property outside object");
                string name = reader.GetString() ?? string.Empty;
                if (!scopes.Peek().Add(name)) throw new JsonException("duplicate property: " + name);
            }
            else if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (scopes.Count == 0) throw new JsonException("unexpected object end");
                scopes.Pop();
            }
        }
        if (scopes.Count != 0) throw new JsonException("unclosed object");
    }

    private static void Write(StringBuilder builder, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                bool firstProperty = true;
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    if (!firstProperty) builder.Append(',');
                    firstProperty = false;
                    WriteString(builder, property.Name);
                    builder.Append(':');
                    Write(builder, property.Value);
                }
                builder.Append('}');
                return;
            case JsonValueKind.Array:
                builder.Append('[');
                bool firstValue = true;
                foreach (JsonElement child in value.EnumerateArray())
                {
                    if (!firstValue) builder.Append(',');
                    firstValue = false;
                    Write(builder, child);
                }
                builder.Append(']');
                return;
            case JsonValueKind.String:
                WriteString(builder, (value.GetString() ?? string.Empty).Normalize(NormalizationForm.FormC));
                return;
            case JsonValueKind.Number:
                builder.Append(FormatNumber(value));
                return;
            case JsonValueKind.True:
                builder.Append("true");
                return;
            case JsonValueKind.False:
                builder.Append("false");
                return;
            case JsonValueKind.Null:
                builder.Append("null");
                return;
            default:
                throw new JsonException("unsupported JSON value");
        }
    }

    private static string FormatNumber(JsonElement value)
    {
        if (value.TryGetInt64(out long integer)) return integer.ToString(CultureInfo.InvariantCulture);
        if (value.TryGetDecimal(out decimal decimalValue)) return decimalValue.ToString("G29", CultureInfo.InvariantCulture);
        if (value.TryGetDouble(out double doubleValue)) return doubleValue.ToString("R", CultureInfo.InvariantCulture);
        return value.GetRawText();
    }

    private static void WriteString(StringBuilder builder, string value)
    {
        builder.Append(JsonSerializer.Serialize(value));
    }
}
'@ -Language CSharp
}

function Get-JointStatusCode([string]$Status) {
    if (-not $script:JointStatusCodes.Contains($Status)) { return $script:JointStatusCodes.error }
    return [int]$script:JointStatusCodes[$Status]
}

function Resolve-JointLegacyPath([string]$Path) {
    $normalized = $Path.Replace([char]47, [char]92)
    $awakePrefix = '_houkai_merge\AWAKE\'
    $testsPrefix = '_houkai_merge\AWAKE.Tests\'
    if ($normalized.StartsWith($awakePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        return Join-Path $script:JointAwakeRoot $normalized.Substring($awakePrefix.Length)
    }
    if ($normalized.StartsWith($testsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        return Join-Path (Join-Path $script:JointWorkspaceRoot 'AWAKE.Tests') $normalized.Substring($testsPrefix.Length)
    }
    return $null
}

function Get-JointFullPath([string]$Path, [string]$BasePath = $script:JointWorkspaceRoot) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw [ArgumentException]::new('Path is required.') }
    $legacyCandidate = if ([IO.Path]::IsPathRooted($Path)) { $null } else { Resolve-JointLegacyPath $Path }
    $candidate = if ($legacyCandidate) { $legacyCandidate } elseif ([IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path $BasePath $Path }
    return [IO.Path]::GetFullPath($candidate)
}

function Get-JointRelativePath([string]$Path, [string]$BasePath) {
    $fullPath = Get-JointFullPath $Path
    $baseFull = (Get-JointFullPath $BasePath).TrimEnd([char]92, [char]47) + [char]92
    if (-not $fullPath.StartsWith($baseFull, [StringComparison]::OrdinalIgnoreCase)) {
        return $null
    }
    return $fullPath.Substring($baseFull.Length).Replace([char]92, [char]47)
}

function Test-JointPathUnder([string]$Path, [string]$Root, [switch]$AllowEqual) {
    $fullPath = Get-JointFullPath $Path
    $rootFull = Get-JointFullPath $Root
    if ($AllowEqual -and [StringComparer]::OrdinalIgnoreCase.Equals($fullPath, $rootFull)) { return $true }
    $prefix = $rootFull.TrimEnd([char]92, [char]47) + [char]92
    return $fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
}

function Test-JointReparsePath([string]$Path) {
    $fullPath = Get-JointFullPath $Path
    $current = $fullPath
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { return $true }
        }
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or [StringComparer]::OrdinalIgnoreCase.Equals($parent, $current)) { break }
        $current = $parent
    }
    return $false
}

function Assert-JointOutputPath([string]$Path, [string]$Label = 'Output') {
    $fullPath = Get-JointFullPath $Path
    if (-not (Test-JointPathUnder $fullPath $script:JointToolRoot -AllowEqual:$false)) {
        Throw-JointReject 'persona.path_protected' 'path' $Label 'Output must remain under tools/persona-awake-joint.'
    }
    $protectedRoots = @(
        (Join-Path $script:JointAwakeRoot 'src'),
        (Join-Path $script:JointAwakeRoot 'ModuleData'),
        (Join-Path $script:JointAwakeRoot 'dist'),
        (Join-Path $script:JointAwakeRoot 'docs'),
        (Join-Path $script:JointAwakeRoot 'runtime'),
        'D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE'
    )
    foreach ($root in $protectedRoots) {
        if (Test-JointPathUnder $fullPath $root -AllowEqual) {
            Throw-JointReject 'persona.path_protected' 'path' $Label 'Output intersects a protected root.'
        }
        if (Test-JointPathUnder $root $fullPath -AllowEqual) {
            Throw-JointReject 'persona.path_protected' 'path' $Label 'Output is an ancestor of a protected root.'
        }
    }
    if (Test-JointReparsePath $fullPath) {
        Throw-JointReject 'persona.path_protected' 'path' $Label 'Output traverses a reparse point.'
    }
    $parent = Split-Path -Parent $fullPath
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    if (Test-JointReparsePath $parent) {
        Throw-JointReject 'persona.path_protected' 'path' $Label 'Output parent traverses a reparse point.'
    }
    return $fullPath
}

function Write-JointUtf8Atomic([string]$Path, [string]$Text) {
    $fullPath = Assert-JointOutputPath $Path
    $parent = Split-Path -Parent $fullPath
    $temporary = Join-Path $parent ('.' + [IO.Path]::GetFileName($fullPath) + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [IO.File]::WriteAllBytes($temporary, $script:JointUtf8.GetBytes($Text))
        Move-Item -LiteralPath $temporary -Destination $fullPath -Force
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    }
    return $fullPath
}

function Get-JointHashBytes([byte[]]$Bytes) {
    return ([BitConverter]::ToString([Security.Cryptography.SHA256]::HashData($Bytes))).Replace('-', '').ToUpperInvariant()
}

function Get-JointHashText([string]$Text) {
    return Get-JointHashBytes $script:JointUtf8.GetBytes($Text)
}

function Get-JointHashFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Get-JointCanonicalTextFromBytes([byte[]]$Bytes) {
    try { return [AwakePersonaJointCanonicalJson]::Canonicalize($Bytes) }
    catch { throw [IO.InvalidDataException]::new($_.Exception.Message, $_.Exception) }
}

function Get-JointCanonicalText([object]$Value) {
    $json = ConvertTo-Json -InputObject $Value -Depth 100 -Compress
    return Get-JointCanonicalTextFromBytes $script:JointUtf8.GetBytes($json)
}

function Get-JointCanonicalHash([object]$Value) {
    return Get-JointHashText (Get-JointCanonicalText $Value)
}

function Read-JointJsonText([string]$Text, [string]$Label = 'JSON') {
    if ($null -eq $Text) { Throw-JointReject 'persona.schema_invalid' 'parse' $Label 'JSON text is null.' }
    try {
        $bytes = $script:JointUtf8.GetBytes($Text)
        $canonical = Get-JointCanonicalTextFromBytes $bytes
        $value = ConvertFrom-Json -InputObject $Text -AsHashtable -Depth 100 -DateKind String -ErrorAction Stop
        return [pscustomobject]@{ Value = $value; RawBytes = $bytes; RawSha256 = Get-JointHashBytes $bytes; CanonicalText = $canonical; CanonicalSha256 = Get-JointHashText $canonical }
    } catch {
        if ($_.Exception.Message -like '__JOINT_REJECT__*') { throw }
        Throw-JointReject 'persona.schema_invalid' 'parse' $Label $_.Exception.Message
    }
}

function Read-JointJsonFile([string]$Path, [string]$Label = 'JSON file') {
    $fullPath = Get-JointFullPath $Path
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { throw [System.IO.FileNotFoundException]::new($Label + ' is missing.', $fullPath) }
    if (Test-JointReparsePath $fullPath) { Throw-JointReject 'persona.path_protected' 'path' $Label 'Input traverses a reparse point.' }
    $bytes = [IO.File]::ReadAllBytes($fullPath)
    $text = $script:JointUtf8.GetString($bytes)
    $parsed = Read-JointJsonText $text $Label
    $parsed | Add-Member -NotePropertyName FullPath -NotePropertyValue $fullPath
    return $parsed
}

function Get-JointRequiredSha256([object]$Value, [string]$Name, [string]$Path) {
    $sha256 = (Get-JointRequiredString $Value $Name $Path).ToUpperInvariant()
    if ($sha256 -notmatch '^[A-F0-9]{64}$') { Throw-JointReject 'persona.schema_invalid' 'schema' ($Path + '.' + $Name) 'Expected an uppercase SHA-256 digest.' }
    return $sha256
}

function Assert-JointReadablePath([string]$Path, [string]$Root, [string]$Label) {
    $fullPath = Get-JointFullPath $Path $Root
    if (-not (Test-JointPathUnder $fullPath $Root -AllowEqual:$false)) { Throw-JointReject 'persona.path_protected' 'path' $Label 'Input path escapes its allowed root.' }
    if (Test-JointReparsePath $fullPath) { Throw-JointReject 'persona.path_protected' 'path' $Label 'Input path traverses a reparse point.' }
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { throw [System.IO.FileNotFoundException]::new($Label + ' is missing.', $fullPath) }
    return $fullPath
}

function Get-JointCrosswalkBundle() {
    $crosswalkPath = Join-Path $script:JointAwakeRoot 'docs\persona-contract\persona-workbench-to-awake.crosswalk.v1.json'
    $crosswalkPath = Assert-JointReadablePath $crosswalkPath $script:JointAwakeRoot 'crosswalk'
    $crosswalk = Read-JointJsonFile $crosswalkPath 'persona-workbench-to-awake.crosswalk.v1'
    Assert-JointExactFields $crosswalk.Value @('$schema','$id','schemaVersion','sourceSchema','targetAuthoringSchema','targetRuntimeSchema','targetRegistry','sourceVocabulary','valueEncodings','rows') @('$schema','$id','schemaVersion','sourceSchema','targetAuthoringSchema','targetRuntimeSchema','targetRegistry','sourceVocabulary','valueEncodings','rows') '$.crosswalk'
    if ((Get-JointRequiredString $crosswalk.Value 'schemaVersion' '$.crosswalk') -ne 'persona-workbench-to-awake.crosswalk.v1') { Throw-JointReject 'persona.schema_version_unsupported' 'mapping' '$.crosswalk.schemaVersion' 'Crosswalk schema is unsupported.' }
    if ((Get-JointRequiredString $crosswalk.Value 'sourceSchema' '$.crosswalk') -ne 'persona-workbench.character.v1') { Throw-JointReject 'persona.schema_version_unsupported' 'mapping' '$.crosswalk.sourceSchema' 'Crosswalk source schema is unsupported.' }
    if ((Get-JointRequiredString $crosswalk.Value 'targetAuthoringSchema' '$.crosswalk') -ne $script:JointAuthoringSchema) { Throw-JointReject 'persona.schema_version_unsupported' 'mapping' '$.crosswalk.targetAuthoringSchema' 'Crosswalk authoring target is unsupported.' }
    if ((Get-JointRequiredString $crosswalk.Value 'targetRuntimeSchema' '$.crosswalk') -ne $script:JointDefinitionSchema) { Throw-JointReject 'persona.schema_version_unsupported' 'mapping' '$.crosswalk.targetRuntimeSchema' 'Crosswalk runtime target is unsupported.' }
    $target = Get-JointJsonProperty $crosswalk.Value 'targetRegistry'
    Assert-JointExactFields $target @('schemaVersion','path','sha256','digestMode') @('schemaVersion','path','sha256','digestMode') '$.crosswalk.targetRegistry'
    if ((Get-JointRequiredString $target 'schemaVersion' '$.crosswalk.targetRegistry') -ne $script:JointRegistrySchema) { Throw-JointReject 'persona.schema_version_unsupported' 'registry' '$.crosswalk.targetRegistry.schemaVersion' 'Crosswalk registry schema is unsupported.' }
    if ((Get-JointRequiredString $target 'digestMode' '$.crosswalk.targetRegistry') -ne 'raw_utf8_bytes') { Throw-JointReject 'persona.registry_digest_mismatch' 'registry' '$.crosswalk.targetRegistry.digestMode' 'Crosswalk registry digest mode is unsupported.' }
    $targetRegistryRelativePath = Get-JointRequiredString $target 'path' '$.crosswalk.targetRegistry'
    $targetRegistryPath = Assert-JointReadablePath (Get-JointFullPath $targetRegistryRelativePath $script:JointAwakeRoot) $script:JointAwakeRoot 'target registry'
    $targetRegistry = Read-JointJsonFile $targetRegistryPath 'AWAKE target registry'
    $crosswalkRegistrySha256 = Get-JointRequiredSha256 $target 'sha256' '$.crosswalk.targetRegistry'
    if ($targetRegistry.RawSha256 -ne $crosswalkRegistrySha256) { Throw-JointReject 'persona.registry_digest_mismatch' 'registry' '$.crosswalk.targetRegistry.sha256' 'Crosswalk target registry digest does not match the current target registry bytes.' }
    $targetRegistryIds = Assert-JointRegistry $targetRegistry.Value '$.crosswalk.targetRegistry'
    $rows = Get-JointJsonProperty $crosswalk.Value 'rows'
    if (-not (Test-JointJsonArray $rows) -or $rows.Count -eq 0) { Throw-JointReject 'persona.mapping_loss' 'mapping' '$.crosswalk.rows' 'Crosswalk rows are missing.' }
    $rowMap = @{}
    $rowIndex = 0
    foreach ($row in $rows) {
        Assert-JointExactFields $row @('sourceId','sourceKind','targetField','targetId','mappingKind','valueEncoding','lossPolicy','provenancePath','registrySha256','status') @('sourceId','sourceKind','targetField','targetId','mappingKind','valueEncoding','lossPolicy','provenancePath','registrySha256','status') ('$.crosswalk.rows[' + $rowIndex + ']')
        $sourceId = Get-JointRequiredString $row 'sourceId' ('$.crosswalk.rows[' + $rowIndex + ']')
        if ($rowMap.ContainsKey($sourceId)) { Throw-JointReject 'persona.duplicate_id' 'mapping' ('$.crosswalk.rows[' + $rowIndex + '].sourceId') 'Crosswalk source ID is duplicated.' }
        $rowRegistrySha256 = Get-JointRequiredSha256 $row 'registrySha256' ('$.crosswalk.rows[' + $rowIndex + ']')
        if ($rowRegistrySha256 -ne $crosswalkRegistrySha256) { Throw-JointReject 'persona.registry_digest_mismatch' 'mapping' ('$.crosswalk.rows[' + $rowIndex + '].registrySha256') 'Crosswalk row is pinned to a different registry digest.' }
        $status = Get-JointRequiredString $row 'status' ('$.crosswalk.rows[' + $rowIndex + ']')
        $lossPolicy = Get-JointRequiredString $row 'lossPolicy' ('$.crosswalk.rows[' + $rowIndex + ']')
        if ($status -notin @('mapped','unmapped') -or $lossPolicy -notin @('reject','preserve_only','drop_non_runtime_metadata')) { Throw-JointReject 'persona.mapping_loss' 'mapping' ('$.crosswalk.rows[' + $rowIndex + ']') 'Crosswalk row status or loss policy is unsupported.' }
        $sourceKind = Get-JointRequiredString $row 'sourceKind' ('$.crosswalk.rows[' + $rowIndex + ']')
        $targetId = Get-JointJsonProperty $row 'targetId'
        if ($status -eq 'mapped' -and $null -ne $targetId -and $sourceKind -in @('tag','facet_strength','axis','flag')) {
            if ($targetId -isnot [string] -or [string]::IsNullOrWhiteSpace([string]$targetId)) { Throw-JointReject 'persona.mapping_loss' 'mapping' ('$.crosswalk.rows[' + $rowIndex + '].targetId') 'Mapped crosswalk row requires a target ID.' }
            if (-not $targetRegistryIds.Contains([string]$targetId)) { Throw-JointReject 'persona.unknown_tag' 'mapping' ('$.crosswalk.rows[' + $rowIndex + '].targetId') ('Crosswalk target tag is absent from the current target registry: ' + [string]$targetId) }
        }
        $rowMap[$sourceId] = $row
        $rowIndex++
    }
    return [pscustomobject]@{
        Crosswalk = $crosswalk.Value
        CrosswalkSha256 = $crosswalk.RawSha256
        CrosswalkRows = $rowMap
        TargetRegistry = $targetRegistry.Value
        TargetRegistrySha256 = $targetRegistry.RawSha256
        TargetRegistryIds = $targetRegistryIds
    }
}

function Get-JointWorkbenchSource([object]$FixtureInput, [string]$FixtureId, [string]$FixtureDirectory) {
    $hasSource = Test-JointJsonProperty $FixtureInput 'source'
    $hasLegacyWorkbench = Test-JointJsonProperty $FixtureInput 'workbench'
    if ($hasSource) {
        $sourceEnvelope = Get-JointJsonProperty $FixtureInput 'source'
        Assert-JointExactFields $sourceEnvelope @('document','sourceRevision','sourceSha256','file') @('document','sourceRevision','sourceSha256') '$.source'
        $document = Get-JointJsonProperty $sourceEnvelope 'document'
        $sourcePath = '$.source.document'
        $sourceRevision = Get-JointRequiredInteger $sourceEnvelope 'sourceRevision' '$.source' 1
        $declaredSha256 = Get-JointRequiredSha256 $sourceEnvelope 'sourceSha256' '$.source'
        if (-not (Test-JointJsonObject $document)) { Throw-JointReject 'persona.schema_invalid' 'schema' $sourcePath 'Workbench source document must be an object.' }
        $sourceFilePath = $null
        $rawBytes = $null
        $sourceSha256 = $declaredSha256
        if (Test-JointJsonProperty $sourceEnvelope 'file') {
            $sourceFileName = Get-JointRequiredString $sourceEnvelope 'file' '$.source'
            if ([string]::IsNullOrWhiteSpace($FixtureDirectory)) { Throw-JointReject 'persona.source_revision_required' 'read' '$.source.file' 'Fixture directory is required to verify source.file.' }
            $sourceFilePath = Assert-JointReadablePath $sourceFileName $FixtureDirectory '$.source.file'
            $sourceFileRead = Read-JointJsonFile $sourceFilePath ('fixture ' + $FixtureId + ' source file')
            $documentCanonical = Get-JointCanonicalText $document
            if (-not [StringComparer]::Ordinal.Equals($sourceFileRead.CanonicalText, $documentCanonical)) { Throw-JointReject 'persona.source_revision_required' 'digest' '$.source.file' 'source.file canonical JSON does not match source.document.' }
            if ($sourceFileRead.RawSha256 -ne $declaredSha256) { Throw-JointReject 'persona.source_revision_required' 'digest' '$.source.sourceSha256' 'sourceSha256 does not match source.file raw bytes.' }
            $rawBytes = $sourceFileRead.RawBytes
            $sourceSha256 = $sourceFileRead.RawSha256
        }
        if ($hasLegacyWorkbench -and (Get-JointCanonicalHash (Get-JointJsonProperty $FixtureInput 'workbench')) -ne (Get-JointCanonicalHash $document)) { Throw-JointReject 'persona.schema_invalid' 'schema' '$.workbench' 'Legacy workbench alias conflicts with source.document.' }
        return [pscustomobject]@{ Document = $document; Path = $sourcePath; SourceRevision = [int]$sourceRevision; SourceSha256 = $sourceSha256; RawBytes = $rawBytes; FilePath = $sourceFilePath; LegacyAlias = $false }
    }
    if (-not $hasLegacyWorkbench) { Throw-JointReject 'persona.schema_required_field' 'schema' '$.source.document' 'Workbench source document is missing.' }
    $document = Get-JointJsonProperty $FixtureInput 'workbench'
    $sourcePath = '$.workbench'
    $sourceRevision = if (Test-JointJsonProperty $FixtureInput 'sourceRevision') { Get-JointRequiredInteger $FixtureInput 'sourceRevision' '$' 1 } elseif (Test-JointJsonProperty $FixtureInput 'selection') { Get-JointRequiredInteger (Get-JointJsonProperty $FixtureInput 'selection') 'sourceRevision' '$.selection' 1 } else { 1 }
    $rawBytes = $null
    if (Test-JointJsonProperty $FixtureInput 'workbenchRaw') {
        $rawText = Get-JointRequiredString $FixtureInput 'workbenchRaw' '$'
        $raw = Read-JointJsonText $rawText '$.workbenchRaw'
        if ((Get-JointCanonicalHash $raw.Value) -ne (Get-JointCanonicalHash $document)) { Throw-JointReject 'persona.source_revision_required' 'digest' '$.workbenchRaw' 'workbenchRaw content does not match workbench.' }
        $rawBytes = $raw.RawBytes
    }
    $declaredSha256 = if (Test-JointJsonProperty $FixtureInput 'sourceSha256') { Get-JointRequiredSha256 $FixtureInput 'sourceSha256' '$' } elseif ($null -ne $rawBytes) { Get-JointHashBytes $rawBytes } else { Get-JointHashBytes ([byte[]]$script:JointUtf8.GetBytes((Get-JointCanonicalText $document))) }
    if (Test-JointJsonProperty $FixtureInput 'workbenchSha256') {
        $expectedSha256 = Get-JointRequiredSha256 $FixtureInput 'workbenchSha256' '$'
        if ($expectedSha256 -ne $declaredSha256) { Throw-JointReject 'persona.source_revision_required' 'digest' '$.workbenchSha256' 'workbenchSha256 does not match the source bytes.' }
    }
    return [pscustomobject]@{ Document = $document; Path = $sourcePath; SourceRevision = [int]$sourceRevision; SourceSha256 = $declaredSha256; RawBytes = $rawBytes; FilePath = $null; LegacyAlias = $true }
}

function Get-JointJsonProperties([object]$Value) {
    if ($null -eq $Value) { return ,([object[]]@()) }
    if ($Value -is [Collections.IDictionary]) { return @($Value.Keys | ForEach-Object { [string]$_ }) }
    return @($Value.PSObject.Properties | ForEach-Object { $_.Name })
}

function Get-JointJsonProperty([object]$Value, [string]$Name) {
    $result = $null
    if ($null -eq $Value) { return $null }
    if ($Value -is [Collections.IDictionary]) {
        $result = $Value[$Name]
    } else {
        $property = $Value.PSObject.Properties[$Name]
        if ($null -eq $property) { return $null }
        $result = $property.Value
    }
    if ($result -is [Collections.IList] -and $result -isnot [string]) { return ,$result }
    return $result
}

function Test-JointJsonProperty([object]$Value, [string]$Name) {
    if ($null -eq $Value) { return $false }
    if ($Value -is [Collections.IDictionary]) { return $Value.Contains($Name) }
    return $null -ne $Value.PSObject.Properties[$Name]
}

function Test-JointJsonObject([object]$Value) {
    return $null -ne $Value -and $Value -is [Collections.IDictionary]
}

function Test-JointJsonArray([object]$Value) {
    return $null -ne $Value -and $Value -is [Collections.IList] -and $Value -isnot [string]
}

function Throw-JointReject([string]$Code, [string]$Stage, [string]$Path, [string]$Detail) {
    throw [InvalidOperationException]::new('__JOINT_REJECT__|' + $Code + '|' + $Stage + '|' + $Path + '|' + $Detail)
}

function Throw-JointBlocked([string]$Code, [string]$Stage, [string]$Path, [string]$Detail) {
    throw [InvalidOperationException]::new('__JOINT_BLOCKED__|' + $Code + '|' + $Stage + '|' + $Path + '|' + $Detail)
}

function Throw-JointNotAttempted([string]$Code, [string]$Stage, [string]$Path, [string]$Detail) {
    throw [InvalidOperationException]::new('__JOINT_NOT_ATTEMPTED__|' + $Code + '|' + $Stage + '|' + $Path + '|' + $Detail)
}

function Assert-JointExactFields([object]$Value, [string[]]$Allowed, [string[]]$Required, [string]$Path) {
    if (-not (Test-JointJsonObject $Value)) { Throw-JointReject 'persona.schema_invalid' 'schema' $Path 'Expected an object.' }
    $allowedSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($name in $Allowed) { [void]$allowedSet.Add($name) }
    foreach ($name in (Get-JointJsonProperties $Value)) {
        if (-not $allowedSet.Contains($name)) { Throw-JointReject 'persona.schema_unknown_field' 'schema' ($Path + '.' + $name) 'Unknown field.' }
    }
    foreach ($name in $Required) {
        if (-not (Test-JointJsonProperty $Value $name)) { Throw-JointReject 'persona.schema_required_field' 'schema' ($Path + '.' + $name) 'Required field is missing.' }
    }
}

function Get-JointRequiredString([object]$Value, [string]$Name, [string]$Path, [switch]$AllowEmpty) {
    if (-not (Test-JointJsonProperty $Value $Name)) { Throw-JointReject 'persona.schema_required_field' 'schema' ($Path + '.' + $Name) 'Required string is missing.' }
    $result = Get-JointJsonProperty $Value $Name
    if ($result -isnot [string]) { Throw-JointReject 'persona.schema_invalid' 'schema' ($Path + '.' + $Name) 'Expected a string.' }
    if (-not $AllowEmpty -and [string]::IsNullOrWhiteSpace($result)) { Throw-JointReject 'persona.schema_invalid' 'schema' ($Path + '.' + $Name) 'String cannot be empty.' }
    return ([string]$result).Normalize([Text.NormalizationForm]::FormC)
}

function Get-JointOptionalString([object]$Value, [string]$Name, [string]$Path) {
    if (-not (Test-JointJsonProperty $Value $Name)) { return '' }
    $result = Get-JointJsonProperty $Value $Name
    if ($null -eq $result) { return '' }
    if ($result -isnot [string]) { Throw-JointReject 'persona.schema_invalid' 'schema' ($Path + '.' + $Name) 'Expected a string.' }
    return ([string]$result).Normalize([Text.NormalizationForm]::FormC)
}

function Get-JointRequiredInteger([object]$Value, [string]$Name, [string]$Path, [int64]$Minimum = [int64]::MinValue, [int64]$Maximum = [int64]::MaxValue) {
    if (-not (Test-JointJsonProperty $Value $Name)) { Throw-JointReject 'persona.schema_required_field' 'schema' ($Path + '.' + $Name) 'Required integer is missing.' }
    $result = Get-JointJsonProperty $Value $Name
    if ($result -isnot [int] -and $result -isnot [long] -and $result -isnot [short]) { Throw-JointReject 'persona.schema_invalid' 'schema' ($Path + '.' + $Name) 'Expected an integer.' }
    $number = [int64]$result
    if ($number -lt $Minimum -or $number -gt $Maximum) { Throw-JointReject 'persona.schema_invalid' 'schema' ($Path + '.' + $Name) 'Integer is outside the allowed range.' }
    return $number
}

function Get-JointOptionalInteger([object]$Value, [string]$Name, [string]$Path, [int64]$Minimum = [int64]::MinValue, [int64]$Maximum = [int64]::MaxValue) {
    if (-not (Test-JointJsonProperty $Value $Name)) { return $null }
    $result = Get-JointJsonProperty $Value $Name
    if ($null -eq $result) { return $null }
    if ($result -isnot [int] -and $result -isnot [long] -and $result -isnot [short]) { Throw-JointReject 'persona.schema_invalid' 'schema' ($Path + '.' + $Name) 'Expected an integer or null.' }
    $number = [int64]$result
    if ($number -lt $Minimum -or $number -gt $Maximum) { Throw-JointReject 'persona.schema_invalid' 'schema' ($Path + '.' + $Name) 'Integer is outside the allowed range.' }
    return $number
}

function Get-JointRequiredStringArray([object]$Value, [string]$Name, [string]$Path) {
    if (-not (Test-JointJsonProperty $Value $Name)) { Throw-JointReject 'persona.schema_required_field' 'schema' ($Path + '.' + $Name) 'Required array is missing.' }
    $array = Get-JointJsonProperty $Value $Name
    if (-not (Test-JointJsonArray $array)) { Throw-JointReject 'persona.schema_invalid' 'schema' ($Path + '.' + $Name) 'Expected an array.' }
    $result = [Collections.Generic.List[string]]::new()
    $index = 0
    foreach ($item in $array) {
        if ($item -isnot [string]) { Throw-JointReject 'persona.schema_invalid' 'schema' ($Path + '.' + $Name + '[' + $index + ']') 'Expected a string.' }
        $result.Add(([string]$item).Normalize([Text.NormalizationForm]::FormC))
        $index++
    }
    return @($result.ToArray())
}

function Assert-JointStableId([string]$Id, [string]$Path) {
    if ($Id -notmatch '^[a-z0-9_]+(?:\.[a-z0-9_]+)*$') { Throw-JointReject 'persona.selection_invalid' 'selection' $Path 'Stable ID grammar is invalid.' }
}

function ConvertTo-JointEntityId([string]$Kind, [string]$Value, [string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Kind) -or [string]::IsNullOrWhiteSpace($Value)) { Throw-JointReject 'persona.selection_invalid' 'selection' $Path 'Entity kind and value are required.' }
    $normalizedKind = $Kind.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_')
    $normalizedValue = $Value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_')
    $prefixes = @(
        ('awake:entity:' + $normalizedKind + ':'),
        ('awake:' + $normalizedKind + ':'),
        ('calradia:' + $normalizedKind + ':'),
        ('entity.' + $normalizedKind + '.'),
        ($normalizedKind + ':')
    )
    foreach ($prefix in $prefixes) {
        if ($normalizedValue.StartsWith($prefix, [StringComparison]::Ordinal)) {
            $normalizedValue = $normalizedValue.Substring($prefix.Length)
            break
        }
    }
    if ([string]::IsNullOrWhiteSpace($normalizedValue) -or $normalizedValue.StartsWith('entity.', [StringComparison]::Ordinal)) { Throw-JointReject 'persona.selection_invalid' 'selection' $Path 'Entity value canonicalization is empty.' }
    return 'awake:' + $normalizedKind + ':' + $normalizedValue
}

function Get-JointStableHashId([string]$Prefix, [string]$Text) {
    return $Prefix + '.' + (Get-JointHashText $Text).Substring(0, 24).ToLowerInvariant()
}

function Get-JointStringArrayOrEmpty([object]$Value, [string]$Path) {
    if ($null -eq $Value) { return ,([object[]]@()) }
    if (-not (Test-JointJsonArray $Value)) { Throw-JointReject 'persona.schema_invalid' 'schema' $Path 'Expected an array.' }
    $result = [Collections.Generic.List[string]]::new()
    $index = 0
    foreach ($item in $Value) {
        if ($item -isnot [string]) { Throw-JointReject 'persona.schema_invalid' 'schema' ($Path + '[' + $index + ']') 'Expected a string.' }
        $result.Add(([string]$item).Normalize([Text.NormalizationForm]::FormC))
        $index++
    }
    return @($result.ToArray())
}

function Assert-JointWorkbenchDocument([object]$Document, [string]$Path = '$.workbench') {
    $allowed = @('schemaVersion','id','displayName','core','identityFacts','summary','sourcePackId','templateVersion','status','sourceDescription','publicDescription','privateDescription','contradictionDescription','selfClaimRules','realSelfBehaviors','selfClaimExamples','tags','facetStrengths','traitProfile','expressionProfile','behaviorProfile','reactionProfile','commitmentProfile')
    $required = @('schemaVersion','id','displayName','core','identityFacts','summary','sourcePackId','templateVersion','status','sourceDescription','publicDescription','privateDescription','contradictionDescription','selfClaimRules','realSelfBehaviors','selfClaimExamples','tags')
    Assert-JointExactFields $Document $allowed $required $Path
    $schema = Get-JointRequiredString $Document 'schemaVersion' $Path
    if ($schema -ne 'persona-workbench.character.v1') { Throw-JointReject 'persona.schema_version_unsupported' 'migration' ($Path + '.schemaVersion') 'Workbench schema must be persona-workbench.character.v1.' }
    $id = Get-JointRequiredString $Document 'id' $Path
    Assert-JointStableId $id ($Path + '.id')
    [void](Get-JointRequiredString $Document 'displayName' $Path -AllowEmpty)
    foreach ($name in @('core','identityFacts','summary','sourcePackId','templateVersion','status','sourceDescription','publicDescription','privateDescription','contradictionDescription')) { [void](Get-JointRequiredString $Document $name $Path -AllowEmpty) }
    $status = Get-JointRequiredString $Document 'status' $Path
    if ($status -notin @('draft','approved')) { Throw-JointReject 'persona.review_status_invalid' 'migration' ($Path + '.status') 'Workbench status must be draft or approved.' }
    foreach ($name in @('selfClaimRules','realSelfBehaviors','selfClaimExamples','tags')) { [void](Get-JointRequiredStringArray $Document $name $Path) }
    if (Test-JointJsonProperty $Document 'facetStrengths') {
        $facets = Get-JointJsonProperty $Document 'facetStrengths'
        if (-not (Test-JointJsonObject $facets)) { Throw-JointReject 'persona.schema_invalid' 'migration' ($Path + '.facetStrengths') 'FacetStrengths must be an object.' }
        foreach ($key in (Get-JointJsonProperties $facets)) {
            $value = Get-JointJsonProperty $facets $key
            if ($value -isnot [int] -and $value -isnot [long]) { Throw-JointReject 'persona.schema_invalid' 'migration' ($Path + '.facetStrengths.' + $key) 'Facet strength must be an integer.' }
            if ([int64]$value -lt 1 -or [int64]$value -gt 4) { Throw-JointReject 'persona.facet_strength_invalid' 'migration' ($Path + '.facetStrengths.' + $key) 'Facet strength must be 1..4.' }
        }
    }
    $profiles = [ordered]@{
        traitProfile = @('caution','ambition','pride','pragmatism','inGroupLoyalty','tradition')
        expressionProfile = @('restraint','directness','formality','playfulness','warmth')
        behaviorProfile = @('conditionality','deliberation','trustTesting','leverage','inGroupPriority','leadership')
        reactionProfile = @('confrontation','expression','timing','resentment','supportSeeking','sensitiveConditions','conditionalResponses')
        commitmentProfile = @('promiseCaution','promisePersistence','valueTradeability','priorityOrder','protectedValues','applicableScope','exceptionCost','breachResponse')
    }
    foreach ($profileName in $profiles.Keys) {
        if (-not (Test-JointJsonProperty $Document $profileName)) { continue }
        $profile = Get-JointJsonProperty $Document $profileName
        Assert-JointExactFields $profile $profiles[$profileName] $profiles[$profileName] ($Path + '.' + $profileName)
        foreach ($field in $profiles[$profileName]) {
            if ($field -in @('sensitiveConditions','conditionalResponses','priorityOrder','protectedValues','applicableScope','exceptionCost','breachResponse')) {
                [void](Get-JointRequiredString $profile $field ($Path + '.' + $profileName) -AllowEmpty)
            } else {
                [void](Get-JointOptionalInteger $profile $field ($Path + '.' + $profileName) -Minimum -2 -Maximum 2)
            }
        }
    }
    return $id
}

function Assert-JointRegistry([object]$Registry, [string]$Path = '$.registry') {
    Assert-JointExactFields $Registry @('schemaVersion','tags','bundles') @('schemaVersion','tags','bundles') $Path
    if ((Get-JointRequiredString $Registry 'schemaVersion' $Path) -ne $script:JointRegistrySchema) { Throw-JointReject 'persona.registry_invalid' 'registry' ($Path + '.schemaVersion') 'Registry schema is unsupported.' }
    $tagIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $tags = Get-JointJsonProperty $Registry 'tags'
    if (-not (Test-JointJsonArray $tags)) { Throw-JointReject 'persona.registry_invalid' 'registry' ($Path + '.tags') 'Registry tags must be an array.' }
    $tagIndex = 0
    foreach ($tag in $tags) {
        Assert-JointExactFields $tag @('id','category','displayName','meaning','promptText') @('id','category','displayName','meaning','promptText') ($Path + '.tags[' + $tagIndex + ']')
        $tagId = Get-JointRequiredString $tag 'id' ($Path + '.tags[' + $tagIndex + ']')
        if (-not $tagIds.Add($tagId)) { Throw-JointReject 'persona.duplicate_id' 'registry' ($Path + '.tags[' + $tagIndex + '].id') 'Duplicate registry tag ID.' }
        $category = Get-JointRequiredString $tag 'category' ($Path + '.tags[' + $tagIndex + ']')
        if ($category -notin @('trait','expression','behavior','trigger','boundary')) { Throw-JointReject 'persona.registry_invalid' 'registry' ($Path + '.tags[' + $tagIndex + '].category') 'Unknown tag category.' }
        foreach ($field in @('displayName','meaning','promptText')) { [void](Get-JointRequiredString $tag $field ($Path + '.tags[' + $tagIndex + ']') -AllowEmpty) }
        $tagIndex++
    }
    $bundleIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $bundles = Get-JointJsonProperty $Registry 'bundles'
    if (-not (Test-JointJsonArray $bundles)) { Throw-JointReject 'persona.registry_invalid' 'registry' ($Path + '.bundles') 'Registry bundles must be an array.' }
    $bundleIndex = 0
    foreach ($bundle in $bundles) {
        Assert-JointExactFields $bundle @('id','displayName','tags') @('id','displayName','tags') ($Path + '.bundles[' + $bundleIndex + ']')
        $bundleId = Get-JointRequiredString $bundle 'id' ($Path + '.bundles[' + $bundleIndex + ']')
        if (-not $bundleIds.Add($bundleId)) { Throw-JointReject 'persona.duplicate_id' 'registry' ($Path + '.bundles[' + $bundleIndex + '].id') 'Duplicate registry bundle ID.' }
        [void](Get-JointRequiredString $bundle 'displayName' ($Path + '.bundles[' + $bundleIndex + ']') -AllowEmpty)
        foreach ($bundleTag in (Get-JointRequiredStringArray $bundle 'tags' ($Path + '.bundles[' + $bundleIndex + ']'))) {
            if (-not $tagIds.Contains($bundleTag)) { Throw-JointReject 'persona.unknown_tag' 'registry' ($Path + '.bundles[' + $bundleIndex + '].tags') ('Bundle references unknown tag: ' + $bundleTag) }
        }
        $bundleIndex++
    }
    return $tagIds
}

function Assert-JointSelection([object]$Selection, [string]$Path = '$.selection') {
    Assert-JointExactFields $Selection @('schemaVersion','characterId','identityId','role','scope','priority','sourceRevision','selectionRevision','suppliedBy','suppliedAt') @('schemaVersion','characterId','identityId','role','scope','priority','sourceRevision','selectionRevision','suppliedBy','suppliedAt') $Path
    if ((Get-JointRequiredString $Selection 'schemaVersion' $Path) -ne $script:JointSelectionSchema) { Throw-JointReject 'persona.selection_invalid' 'selection' ($Path + '.schemaVersion') 'Selection schema is unsupported.' }
    $characterId = ConvertTo-JointEntityId 'character' (Get-JointRequiredString $Selection 'characterId' $Path) ($Path + '.characterId')
    $identityId = ConvertTo-JointEntityId 'identity' (Get-JointRequiredString $Selection 'identityId' $Path) ($Path + '.identityId')
    $role = (Get-JointRequiredString $Selection 'role' $Path).Trim().ToLowerInvariant()
    if ($role -match '[\p{Cc}]' -or [string]::IsNullOrWhiteSpace($role)) { Throw-JointReject 'persona.selection_invalid' 'selection' ($Path + '.role') 'Role is invalid.' }
    $scope = Get-JointRequiredString $Selection 'scope' $Path
    if ($scope -notin @('character','identity','role','culture','global')) { Throw-JointReject 'persona.selection_invalid' 'selection' ($Path + '.scope') 'Selection scope is invalid.' }
    $priority = Get-JointRequiredInteger $Selection 'priority' $Path 0 100000
    $sourceRevision = Get-JointRequiredInteger $Selection 'sourceRevision' $Path 1
    $selectionRevision = Get-JointRequiredInteger $Selection 'selectionRevision' $Path 1
    $suppliedBy = Get-JointRequiredString $Selection 'suppliedBy' $Path
    $suppliedAt = Get-JointRequiredString $Selection 'suppliedAt' $Path
    Assert-JointStableId $suppliedBy ($Path + '.suppliedBy')
    try { [DateTimeOffset]::Parse($suppliedAt, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AssumeUniversal) | Out-Null } catch { Throw-JointReject 'persona.selection_invalid' 'selection' ($Path + '.suppliedAt') 'suppliedAt must be RFC3339-compatible.' }
    return [ordered]@{
        schemaVersion = $script:JointSelectionSchema
        characterId = $characterId
        identityId = $identityId
        role = $role
        scope = $scope
        priority = [int]$priority
        sourceRevision = [int]$sourceRevision
        selectionRevision = [int]$selectionRevision
        suppliedBy = $suppliedBy
        suppliedAt = $suppliedAt
    }
}

function Assert-JointFixtureMappingIssues([object]$FixtureInput) {
    if (-not (Test-JointJsonProperty $FixtureInput 'mappingIssues')) { return }
    $issues = Get-JointJsonProperty $FixtureInput 'mappingIssues'
    if (-not (Test-JointJsonArray $issues)) { Throw-JointReject 'persona.schema_invalid' 'mapping' '$.mappingIssues' 'mappingIssues must be an array.' }
    $index = 0
    foreach ($issue in $issues) {
        $path = '$.mappingIssues[' + $index + ']'
        if (-not (Test-JointJsonObject $issue)) { Throw-JointReject 'persona.schema_invalid' 'mapping' $path 'Mapping issue must be an object.' }
        Assert-JointExactFields $issue @('sourceId','lossPolicy','result','provenancePath','targetId') @('sourceId','lossPolicy','result','provenancePath','targetId') $path
        $sourceId = Get-JointRequiredString $issue 'sourceId' $path
        $lossPolicy = Get-JointRequiredString $issue 'lossPolicy' $path
        $result = Get-JointRequiredString $issue 'result' $path
        if ($lossPolicy -eq 'reject' -and $result -eq 'rejected') {
            Throw-JointReject 'persona.mapping_loss' 'mapping' $path ('Mapping issue requires rejection: ' + $sourceId)
        }
        $index++
    }
}
function Get-JointReviewMap([object]$FixtureInput, [string]$Path = '$.observationReview') {
    $map = @{}
    if (Test-JointJsonProperty $FixtureInput 'observationReview') {
        $review = Get-JointJsonProperty $FixtureInput 'observationReview'
    } elseif (Test-JointJsonProperty $FixtureInput 'migrationReview') {
        $migrationReview = Get-JointJsonProperty $FixtureInput 'migrationReview'
        if (-not (Test-JointJsonProperty $migrationReview 'observationDecisions')) { return $map }
        $review = Get-JointJsonProperty $migrationReview 'observationDecisions'
        $Path = '$.migrationReview.observationDecisions'
    } else {
        return $map
    }
    if (Test-JointJsonObject $review) {
        foreach ($key in (Get-JointJsonProperties $review)) {
            $item = Get-JointJsonProperty $review $key
            Assert-JointExactFields $item @('reviewState','evidence','enabled','provenance') @('reviewState','evidence','enabled','provenance') ($Path + '.' + $key)
            $state = Get-JointRequiredString $item 'reviewState' ($Path + '.' + $key)
            if ($state -notin @('accepted','needs_review','rejected')) { Throw-JointReject 'persona.authoring_observation_unreviewed' 'migration' ($Path + '.' + $key + '.reviewState') 'Invalid observation review state.' }
            $evidence = Get-JointRequiredString $item 'evidence' ($Path + '.' + $key) -AllowEmpty
            $enabledValue = Get-JointJsonProperty $item 'enabled'
            if ($enabledValue -isnot [bool]) { Throw-JointReject 'persona.schema_invalid' 'migration' ($Path + '.' + $key + '.enabled') 'enabled must be boolean.' }
            $provenance = Get-JointRequiredString $item 'provenance' ($Path + '.' + $key)
            $map[$key] = [ordered]@{ reviewState = $state; evidence = $evidence; enabled = [bool]$enabledValue; provenance = $provenance }
        }
        return $map
    }
    if (-not (Test-JointJsonArray $review)) { Throw-JointReject 'persona.schema_invalid' 'migration' $Path 'observationReview must be an object or array.' }
    $index = 0
    foreach ($item in $review) {
        Assert-JointExactFields $item @('selectorId','value','reviewState','evidence','enabled','provenance') @('selectorId','reviewState','evidence','enabled') ($Path + '[' + $index + ']')
        $selectorId = Get-JointRequiredString $item 'selectorId' ($Path + '[' + $index + ']')
        if ($map.ContainsKey($selectorId)) { Throw-JointReject 'persona.duplicate_id' 'migration' ($Path + '[' + $index + '].selectorId') 'Duplicate observation review selector.' }
        $state = Get-JointRequiredString $item 'reviewState' ($Path + '[' + $index + ']')
        if ($state -notin @('accepted','needs_review','rejected')) { Throw-JointReject 'persona.authoring_observation_unreviewed' 'migration' ($Path + '[' + $index + '].reviewState') 'Invalid observation review state.' }
        $evidence = Get-JointRequiredString $item 'evidence' ($Path + '[' + $index + ']') -AllowEmpty
        $enabledValue = Get-JointJsonProperty $item 'enabled'
        if ($enabledValue -isnot [bool]) { Throw-JointReject 'persona.schema_invalid' 'migration' ($Path + '[' + $index + '].enabled') 'enabled must be boolean.' }
        $provenance = if (Test-JointJsonProperty $item 'provenance') { Get-JointRequiredString $item 'provenance' ($Path + '[' + $index + ']') } elseif ($selectorId -notmatch '^axis\.') { 'workbench.tag' } else { 'workbench.v1' }
        $map[$selectorId] = [ordered]@{ reviewState = $state; evidence = $evidence; enabled = [bool]$enabledValue; provenance = $provenance }
        $index++
    }
    return $map
}

function Get-JointPinnedValues([object]$FixtureInput, [object]$Registry, [string]$Path = '$') {
    $pinned = if (Test-JointJsonProperty $FixtureInput 'pinned') { Get-JointJsonProperty $FixtureInput 'pinned' } else { $FixtureInput }
    if (Test-JointJsonProperty $FixtureInput 'pinned') {
        Assert-JointExactFields $pinned @('registryVersion','instructionVersion','compilerVersion') @('registryVersion','instructionVersion','compilerVersion') ($Path + '.pinned')
        return [ordered]@{
            registryVersion = Get-JointRequiredString $pinned 'registryVersion' ($Path + '.pinned')
            instructionVersion = Get-JointRequiredString $pinned 'instructionVersion' ($Path + '.pinned')
            compilerVersion = Get-JointRequiredString $pinned 'compilerVersion' ($Path + '.pinned')
        }
    }
    foreach ($name in @('registryVersion','instructionVersion','compilerVersion')) {
        if (-not (Test-JointJsonProperty $FixtureInput $name)) { Throw-JointReject 'persona.migration_required' 'migration' ($Path + '.' + $name) 'Pinned migration asset is missing.' }
    }
    return [ordered]@{
        registryVersion = Get-JointRequiredString $FixtureInput 'registryVersion' $Path
        instructionVersion = Get-JointRequiredString $FixtureInput 'instructionVersion' $Path
        compilerVersion = Get-JointRequiredString $FixtureInput 'compilerVersion' $Path
    }
}

function Get-JointCrosswalkRow([object]$CrosswalkBundle, [string]$SourceId, [string]$Path) {
    if (-not $CrosswalkBundle.CrosswalkRows.ContainsKey($SourceId)) {
        if ($SourceId.StartsWith('tags.', [StringComparison]::Ordinal)) { Throw-JointReject 'persona.unknown_tag' 'mapping' $Path ('Source tag is absent from the pinned crosswalk: ' + $SourceId.Substring(5)) }
        Throw-JointReject 'persona.mapping_loss' 'mapping' $Path ('Source field is absent from the pinned crosswalk: ' + $SourceId)
    }
    return $CrosswalkBundle.CrosswalkRows[$SourceId]
}

function Get-JointCrosswalkAction([object]$CrosswalkBundle, [object]$MappingRow, [object]$Value, [string]$Path) {
    $encodingId = Get-JointRequiredString $MappingRow 'valueEncoding' $Path
    $encodings = Get-JointJsonProperty $CrosswalkBundle.Crosswalk 'valueEncodings'
    $encoding = $null
    foreach ($candidate in $encodings) {
        if ((Get-JointOptionalString $candidate 'id' $Path) -eq $encodingId) { $encoding = $candidate; break }
    }
    if ($null -eq $encoding) { Throw-JointReject 'persona.mapping_loss' 'mapping' ($Path + '.valueEncoding') ('Value encoding is absent from the pinned crosswalk: ' + $encodingId) }
    if (-not (Test-JointJsonProperty $encoding 'actions')) { Throw-JointReject 'persona.mapping_loss' 'mapping' ($Path + '.valueEncoding') 'Value encoding has no action table.' }
    $actions = Get-JointJsonProperty $encoding 'actions'
    $valueKey = if ($Value -is [bool]) { ([bool]$Value).ToString().ToLowerInvariant() } else { [string]$Value }
    if (-not (Test-JointJsonProperty $actions $valueKey)) { Throw-JointReject 'persona.mapping_loss' 'mapping' ($Path + '.value') ('Value is outside the crosswalk encoding domain: ' + $valueKey) }
    $action = Get-JointJsonProperty $actions $valueKey
    if (-not (Test-JointJsonProperty $action 'action')) { Throw-JointReject 'persona.mapping_loss' 'mapping' ($Path + '.value') 'Crosswalk action is missing its action kind.' }
    return [pscustomobject]@{
        Kind = Get-JointRequiredString $action 'action' ($Path + '.value')
        Warning = Get-JointOptionalString $action 'warning' ($Path + '.value')
    }
}

function Resolve-JointCrosswalkValue([object]$CrosswalkBundle, [string]$SourceId, [object]$Value, [Collections.Generic.HashSet[string]]$TargetTagIds, [string]$Path) {
    $mappingRow = Get-JointCrosswalkRow $CrosswalkBundle $SourceId $Path
    $action = Get-JointCrosswalkAction $CrosswalkBundle $mappingRow $Value $Path
    if ($action.Kind -eq 'omit') {
        return [pscustomobject]@{ Action = 'omit'; MappingRow = $mappingRow; TargetId = $null; Warning = $action.Warning }
    }
    if ($action.Kind -eq 'preserve_only') {
        if ((Get-JointRequiredString $mappingRow 'lossPolicy' $Path) -ne 'preserve_only') { Throw-JointReject 'persona.mapping_loss' 'mapping' $Path 'Crosswalk preserve-only action is not backed by preserve_only loss policy.' }
        return [pscustomobject]@{ Action = 'preserve_only'; MappingRow = $mappingRow; TargetId = $null; Warning = $action.Warning }
    }
    if ($action.Kind -ne 'emit') { Throw-JointReject 'persona.mapping_loss' 'mapping' $Path ('Unsupported crosswalk action: ' + $action.Kind) }
    if ((Get-JointRequiredString $mappingRow 'status' $Path) -ne 'mapped') { Throw-JointReject 'persona.mapping_loss' 'mapping' $Path 'Crosswalk emit action requires a mapped row.' }
    $targetIdValue = Get-JointJsonProperty $mappingRow 'targetId'
    if ($null -eq $targetIdValue -or $targetIdValue -isnot [string] -or [string]::IsNullOrWhiteSpace([string]$targetIdValue)) { Throw-JointReject 'persona.mapping_loss' 'mapping' $Path 'Crosswalk emit action requires a target ID.' }
    $targetId = [string]$targetIdValue
    if (-not $CrosswalkBundle.TargetRegistryIds.Contains($targetId)) { Throw-JointReject 'persona.registry_digest_mismatch' 'mapping' $Path ('Mapped target tag is absent from the pinned target registry: ' + $targetId) }
    return [pscustomobject]@{ Action = 'emit'; MappingRow = $mappingRow; TargetId = $targetId; Warning = $action.Warning }
}

function Get-JointObservationReview([hashtable]$ReviewMap, [string]$SelectorId, [string]$DefaultProvenance) {
    if ($ReviewMap.ContainsKey($SelectorId)) { return $ReviewMap[$SelectorId] }
    return [ordered]@{ reviewState = 'needs_review'; evidence = ''; enabled = $true; provenance = $DefaultProvenance }
}

function Add-JointObservation([Collections.Generic.List[object]]$Observations, [Collections.Generic.List[object]]$Rows, [hashtable]$ReviewMap, [string]$SelectorId, [object]$Value, [string]$TargetTagId, [string]$SourcePath, [string]$LossPolicy = 'reject', [string]$MappingKind = 'identity', [object]$MappingRow = $null) {
    $review = Get-JointObservationReview $ReviewMap $SelectorId $SourcePath
    $effectiveSourceKind = if ($null -ne $MappingRow) { [string]$MappingRow.sourceKind } else { 'tag' }
    $effectiveTargetField = if ($null -ne $MappingRow) { [string]$MappingRow.targetField } else { 'definition.tags' }
    $effectiveValueEncoding = if ($null -ne $MappingRow) { [string]$MappingRow.valueEncoding } else { 'tag-id' }
    $effectiveLossPolicy = if ($null -ne $MappingRow) { [string]$MappingRow.lossPolicy } else { $LossPolicy }
    $effectiveMappingKind = if ($null -ne $MappingRow) { [string]$MappingRow.mappingKind } else { $MappingKind }
    $effectiveProvenancePath = if ($null -ne $MappingRow) { [string]$MappingRow.provenancePath } else { $SourcePath }
    $observation = [ordered]@{
        selectorId = $SelectorId
        value = $Value
        provenance = [string]$review.provenance
        reviewState = [string]$review.reviewState
        evidence = [string]$review.evidence
        enabled = [bool]$review.enabled
    }
    $Observations.Add($observation)
    $effectiveSourceId = if ($null -ne $MappingRow) { Get-JointRequiredString $MappingRow 'sourceId' $SourcePath } else { $SelectorId }
    $Rows.Add([ordered]@{
        sourceId = $effectiveSourceId
        sourceKind = $effectiveSourceKind
        targetField = $effectiveTargetField
        targetId = $TargetTagId
        mappingKind = $effectiveMappingKind
        valueEncoding = $effectiveValueEncoding
        lossPolicy = $effectiveLossPolicy
        provenancePath = $effectiveProvenancePath
        registrySha256 = ''
        status = if ($null -ne $MappingRow) { [string]$MappingRow.status } else { 'mapped' }
        sourcePath = $SourcePath
        targetPath = $effectiveTargetField
        emittedValue = $TargetTagId
        result = 'emitted'
    })
}

function Add-JointPreservedMapping([Collections.Generic.List[object]]$Rows, [object]$MappingRow, [string]$SourcePath, [object]$Value) {
    $Rows.Add([ordered]@{
        sourceId = Get-JointRequiredString $MappingRow 'sourceId' $SourcePath
        sourceKind = Get-JointRequiredString $MappingRow 'sourceKind' $SourcePath
        targetField = Get-JointRequiredString $MappingRow 'targetField' $SourcePath
        targetId = Get-JointJsonProperty $MappingRow 'targetId'
        mappingKind = Get-JointRequiredString $MappingRow 'mappingKind' $SourcePath
        valueEncoding = Get-JointRequiredString $MappingRow 'valueEncoding' $SourcePath
        lossPolicy = Get-JointRequiredString $MappingRow 'lossPolicy' $SourcePath
        provenancePath = Get-JointRequiredString $MappingRow 'provenancePath' $SourcePath
        registrySha256 = ''
        status = Get-JointRequiredString $MappingRow 'status' $SourcePath
        sourcePath = $SourcePath
        targetPath = Get-JointRequiredString $MappingRow 'targetField' $SourcePath
        emittedValue = $Value
        result = 'preserved'
    })
}

function Join-JointText([string[]]$Values) {
    return (($Values | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join "`n").Trim()
}

function Assert-JointApproval([object]$FixtureInput, [string]$Path = '$.approval') {
    if (-not (Test-JointJsonProperty $FixtureInput 'approval')) { Throw-JointReject 'persona.approval_missing' 'promotion' $Path 'Distinct Workbench and AWAKE approval records are required.' }
    $approval = Get-JointJsonProperty $FixtureInput 'approval'
    Assert-JointExactFields $approval @('workbench','awake') @('workbench','awake') $Path
    $result = [ordered]@{}
    foreach ($name in @('workbench','awake')) {
        $item = Get-JointJsonProperty $approval $name
        Assert-JointExactFields $item @('status','actorId','approvedAt','revision','evidenceId') @('status','actorId','approvedAt','revision','evidenceId') ($Path + '.' + $name)
        $status = Get-JointRequiredString $item 'status' ($Path + '.' + $name)
        if ($name -eq 'workbench' -and $status -notin @('approved','not_approved')) { Throw-JointReject 'persona.approval_invalid' 'promotion' ($Path + '.' + $name + '.status') 'Invalid Workbench approval status.' }
        if ($name -eq 'awake' -and $status -notin @('approved','not_approved')) { Throw-JointReject 'persona.approval_invalid' 'promotion' ($Path + '.' + $name + '.status') 'Invalid AWAKE approval status.' }
        $actor = Get-JointRequiredString $item 'actorId' ($Path + '.' + $name)
        Assert-JointStableId $actor ($Path + '.' + $name + '.actorId')
        $approvedAt = Get-JointRequiredString $item 'approvedAt' ($Path + '.' + $name)
        try { [DateTimeOffset]::Parse($approvedAt, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AssumeUniversal) | Out-Null } catch { Throw-JointReject 'persona.approval_invalid' 'promotion' ($Path + '.' + $name + '.approvedAt') 'Approval timestamp is invalid.' }
        $revision = Get-JointRequiredInteger $item 'revision' ($Path + '.' + $name) 1
        $evidence = Get-JointRequiredString $item 'evidenceId' ($Path + '.' + $name)
        Assert-JointStableId $evidence ($Path + '.' + $name + '.evidenceId')
        $result[$name] = [ordered]@{ status = $status; actorId = $actor; approvedAt = $approvedAt; revision = [int]$revision; evidenceId = $evidence }
        if ($status -ne 'approved') { Throw-JointReject 'persona.approval_missing' 'promotion' ($Path + '.' + $name + '.status') ($name + ' approval is not approved.') }
    }
    return $result
}

function Assert-JointRuleOverrides([object]$FixtureInput, [string]$Path = '$.ruleOverrides') {
    if (-not (Test-JointJsonProperty $FixtureInput 'ruleOverrides')) { return @() }
    $rules = Get-JointJsonProperty $FixtureInput 'ruleOverrides'
    if (-not (Test-JointJsonArray $rules)) { Throw-JointReject 'persona.schema_invalid' 'mapping' $Path 'ruleOverrides must be an array.' }
    $result = [Collections.Generic.List[object]]::new()
    $index = 0
    foreach ($rule in $rules) {
        $allowed = @('ruleId','triggerSelectorId','responseSelectorId','scope','strength','priority','counterweightSelectorId','provenance','evidence','enabled')
        Assert-JointExactFields $rule $allowed $allowed ($Path + '[' + $index + ']')
        $enabled = Get-JointJsonProperty $rule 'enabled'
        if ($enabled -isnot [bool]) { Throw-JointReject 'persona.schema_invalid' 'mapping' ($Path + '[' + $index + '].enabled') 'enabled must be boolean.' }
        if ([bool]$enabled) { Throw-JointReject 'persona.rule_unsupported_for_definition_v1' 'mapping' ($Path + '[' + $index + ']') 'Definition-v1 has no formal rule field.' }
        $result.Add([ordered]@{
            ruleId = Get-JointRequiredString $rule 'ruleId' ($Path + '[' + $index + ']')
            triggerSelectorId = Get-JointRequiredString $rule 'triggerSelectorId' ($Path + '[' + $index + ']')
            responseSelectorId = Get-JointRequiredString $rule 'responseSelectorId' ($Path + '[' + $index + ']')
            scope = Get-JointRequiredString $rule 'scope' ($Path + '[' + $index + ']')
            strength = Get-JointRequiredString $rule 'strength' ($Path + '[' + $index + ']')
            priority = [int](Get-JointRequiredInteger $rule 'priority' ($Path + '[' + $index + ']') 0 100)
            counterweightSelectorId = Get-JointRequiredString $rule 'counterweightSelectorId' ($Path + '[' + $index + ']') -AllowEmpty
            provenance = Get-JointRequiredString $rule 'provenance' ($Path + '[' + $index + ']')
            evidence = Get-JointRequiredString $rule 'evidence' ($Path + '[' + $index + ']') -AllowEmpty
            enabled = [bool]$enabled
        })
        $index++
    }
    return @($result)
}

function Assert-JointDefinition([object]$Definition, [string]$Path = '$.definition') {
    $fields = @('schemaVersion','id','characterId','identityId','role','sourcePackId','templateVersion','status','priority','scope','core','identityFacts','relationStyle','currentStateHints','summary','publicDescription','privateDescription','contradictionDescription','selfClaimRules','realSelfBehaviors','selfClaimExamples','tags','bundles','experiences')
    Assert-JointExactFields $Definition $fields $fields $Path
    if ((Get-JointRequiredString $Definition 'schemaVersion' $Path) -ne $script:JointDefinitionSchema) { Throw-JointReject 'persona.definition_invalid' 'export' ($Path + '.schemaVersion') 'Definition schema is unsupported.' }
    $id = Get-JointRequiredString $Definition 'id' $Path; Assert-JointStableId $id ($Path + '.id')
    foreach ($name in @('characterId','identityId','role','sourcePackId','templateVersion','scope','core','identityFacts','relationStyle','currentStateHints','summary','publicDescription','privateDescription','contradictionDescription')) { [void](Get-JointRequiredString $Definition $name $Path -AllowEmpty) }
    $status = Get-JointRequiredString $Definition 'status' $Path
    if ($status -notin @('approved','draft','disabled')) { Throw-JointReject 'persona.definition_invalid' 'export' ($Path + '.status') 'Definition status is invalid.' }
    [void](Get-JointRequiredInteger $Definition 'priority' $Path)
    foreach ($name in @('selfClaimRules','realSelfBehaviors','selfClaimExamples','bundles')) { [void](Get-JointRequiredStringArray $Definition $name $Path) }
    $tags = Get-JointJsonProperty $Definition 'tags'
    if (-not (Test-JointJsonArray $tags)) { Throw-JointReject 'persona.definition_invalid' 'export' ($Path + '.tags') 'Definition tags must be an array.' }
    $tagIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $index = 0
    foreach ($tag in $tags) {
        Assert-JointExactFields $tag @('id','priority','sceneKeywords','contextModes') @('id','priority','sceneKeywords','contextModes') ($Path + '.tags[' + $index + ']')
        $tagId = Get-JointRequiredString $tag 'id' ($Path + '.tags[' + $index + ']')
        if (-not $tagIds.Add($tagId)) { Throw-JointReject 'persona.duplicate_id' 'export' ($Path + '.tags[' + $index + '].id') 'Duplicate definition tag ID.' }
        [void](Get-JointRequiredInteger $tag 'priority' ($Path + '.tags[' + $index + ']'))
        [void](Get-JointRequiredStringArray $tag 'sceneKeywords' ($Path + '.tags[' + $index + ']'))
        [void](Get-JointRequiredStringArray $tag 'contextModes' ($Path + '.tags[' + $index + ']'))
        $index++
    }
    $experiences = Get-JointJsonProperty $Definition 'experiences'
    if (-not (Test-JointJsonArray $experiences)) { Throw-JointReject 'persona.definition_invalid' 'export' ($Path + '.experiences') 'Definition experiences must be an array.' }
    $experienceIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $index = 0
    foreach ($experience in $experiences) {
        Assert-JointExactFields $experience @('id','text','status','source','priority') @('id','text','status','source','priority') ($Path + '.experiences[' + $index + ']')
        $experienceId = Get-JointRequiredString $experience 'id' ($Path + '.experiences[' + $index + ']')
        if (-not $experienceIds.Add($experienceId)) { Throw-JointReject 'persona.duplicate_id' 'export' ($Path + '.experiences[' + $index + '].id') 'Duplicate definition experience ID.' }
        foreach ($field in @('text','status','source')) { [void](Get-JointRequiredString $experience $field ($Path + '.experiences[' + $index + ']') -AllowEmpty) }
        [void](Get-JointRequiredInteger $experience 'priority' ($Path + '.experiences[' + $index + ']'))
        $index++
    }
}

function Get-JointSelectionFive([object]$Selection) {
    return [ordered]@{
        characterId = $Selection.characterId
        identityId = $Selection.identityId
        role = $Selection.role
        scope = $Selection.scope
        priority = $Selection.priority
    }
}

function Convert-JointWorkbenchToAwake([object]$FixtureInput, [string]$FixtureId, [string]$FixtureDirectory = '') {
    Assert-JointExactFields $FixtureInput @('schemaVersion','fixtureId','gate','operation','referenceFixture','source','sourceVariants','workbench','workbenchRaw','sourceRevision','sourceSha256','workbenchSha256','selection','registry','registryFile','registryRaw','registrySha256','pinned','registryVersion','instructionVersion','compilerVersion','authoringRevision','approval','migrationReview','observationReview','ruleOverrides','runtimeEntry','runtimeManifest','runtimePayload','partialExport','pathChecks','lastKnownGood','reloadCandidate','currentBundle','candidateReload','protectedRoots','isolatedSibling','attempts','exportCandidates','pathAttempts','exportState','mappingIssues','nativePrerequisite','mutation','storagePrerequisite','sequence','expectedStatus','metadata') @('schemaVersion','fixtureId','registry') '$'
    $fixtureSchema = Get-JointRequiredString $FixtureInput 'schemaVersion' '$'
    if ($fixtureSchema -ne $script:JointFixtureInputSchema) { Throw-JointReject 'persona.schema_version_unsupported' 'fixture' '$.schemaVersion' 'Fixture input schema is unsupported.' }
    if ((Get-JointRequiredString $FixtureInput 'fixtureId' '$') -ne $FixtureId) { Throw-JointReject 'persona.fixture_mismatch' 'fixture' '$.fixtureId' 'Fixture ID does not match command line.' }
    $source = Get-JointWorkbenchSource $FixtureInput $FixtureId $FixtureDirectory
    $workbench = $source.Document
    $sourcePath = $source.Path
    $documentId = Assert-JointWorkbenchDocument $workbench $sourcePath
    if ((Get-JointRequiredString $workbench 'status' $sourcePath) -ne 'approved') {
        Throw-JointReject 'persona.workbench_not_approved' 'promotion' ($sourcePath + '.status') 'Only an explicitly approved Workbench document may enter export.'
    }
    if (-not (Test-JointJsonProperty $FixtureInput 'selection') -or $null -eq (Get-JointJsonProperty $FixtureInput 'selection')) { Throw-JointReject 'persona.schema_required_field' 'schema' '$.selection' 'Required selection sidecar is missing.' }
    $selectionSidecar = Get-JointJsonProperty $FixtureInput 'selection'
    $selection = Assert-JointSelection $selectionSidecar '$.selection'
    if ([int]$selection.sourceRevision -ne [int]$source.SourceRevision) { Throw-JointReject 'persona.stale_revision' 'selection' '$.selection.sourceRevision' 'Selection sourceRevision does not match source.sourceRevision.' }
    $registry = Get-JointJsonProperty $FixtureInput 'registry'
    $targetTagIds = Assert-JointRegistry $registry
    Assert-JointFixtureMappingIssues $FixtureInput
    $reviewMap = Get-JointReviewMap $FixtureInput
    $authoringRevision = if (Test-JointJsonProperty $FixtureInput 'authoringRevision') { [int](Get-JointRequiredInteger $FixtureInput 'authoringRevision' '$' 1) } else { 1 }
    $sourceRawBytes = if ($null -ne $source.RawBytes) { $source.RawBytes } else { [byte[]]$script:JointUtf8.GetBytes((Get-JointCanonicalText $workbench)) }
    $sourceSha256 = $source.SourceSha256
    $registryRawBytes = if (Test-JointJsonProperty $FixtureInput 'registryRaw') {
        $rawRegistry = Get-JointRequiredString $FixtureInput 'registryRaw' '$'
        $parsedRegistryRaw = Read-JointJsonText $rawRegistry '$.registryRaw'
        if ((Get-JointCanonicalHash $parsedRegistryRaw.Value) -ne (Get-JointCanonicalHash $registry)) { Throw-JointReject 'persona.registry_digest_mismatch' 'registry' '$.registryRaw' 'Raw registry bytes do not describe registry.' }
        $parsedRegistryRaw.RawBytes
    } elseif (Test-JointJsonProperty $FixtureInput 'registryFile') {
        if ([string]::IsNullOrWhiteSpace($FixtureDirectory)) { Throw-JointReject 'persona.registry_digest_mismatch' 'read' '$.registryFile' 'Fixture directory is required to verify registryFile.' }
        $registryFileName = Get-JointRequiredString $FixtureInput 'registryFile' '$'
        $registryFilePath = Assert-JointReadablePath $registryFileName $FixtureDirectory '$.registryFile'
        $registryFileRead = Read-JointJsonFile $registryFilePath ('fixture ' + $FixtureId + ' registry file')
        if (-not [StringComparer]::Ordinal.Equals($registryFileRead.CanonicalText, (Get-JointCanonicalText $registry))) { Throw-JointReject 'persona.registry_digest_mismatch' 'digest' '$.registryFile' 'registryFile canonical JSON does not match registry.' }
        $registryFileRead.RawBytes
    } else { [byte[]]$script:JointUtf8.GetBytes((Get-JointCanonicalText $registry)) }
    $registrySha256 = Get-JointHashBytes $registryRawBytes
    $registryDigestIsPending = $false
    if (Test-JointJsonProperty $FixtureInput 'registrySha256') {
        $expectedRegistrySha = (Get-JointRequiredString $FixtureInput 'registrySha256' '$').ToUpperInvariant()
        if ($expectedRegistrySha -eq 'PENDING_FIXTURE_REGISTRY_SHA256' -or $expectedRegistrySha -eq '0000000000000000000000000000000000000000000000000000000000000000') {
            $registryDigestIsPending = $true
        } elseif ($expectedRegistrySha -ne $registrySha256) {
            Throw-JointReject 'persona.registry_digest_mismatch' 'registry' '$.registrySha256' 'Pinned registry digest does not match.'
        }
    }
    if (Test-JointJsonProperty $FixtureInput 'metadata') {
        $metadata = Get-JointJsonProperty $FixtureInput 'metadata'
        if (Test-JointJsonObject $metadata -and (Test-JointJsonProperty $metadata 'targetRegistrySha256')) {
            $targetRegistrySha256 = Get-JointRequiredSha256 $metadata 'targetRegistrySha256' '$.metadata'
            if ($targetRegistrySha256 -ne $registrySha256) {
                Throw-JointNotAttempted 'persona.registry_digest_mismatch' 'promotion' '$.metadata.targetRegistrySha256' 'Isolated fixture registry does not match the pinned target registry; production promotion was not attempted.'
            }
        }
    }
    $rows = [Collections.Generic.List[object]]::new()
    $observations = [Collections.Generic.List[object]]::new()
    $warnings = [Collections.Generic.List[string]]::new()
    if ($registryDigestIsPending) { $warnings.Add('registry_digest_pending_fixture') }
    $crosswalk = Get-JointCrosswalkBundle
    $targetRegistrySha256 = $crosswalk.TargetRegistrySha256
    $preservedLegacyData = [ordered]@{}
    $tags = Get-JointRequiredStringArray $workbench 'tags' $sourcePath
    $seenSelectors = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $tagIndex = 0
    foreach ($tag in $tags) {
        $crosswalkSourceId = 'tags.' + $tag
        $tagPath = $sourcePath + '.tags[' + $tagIndex + ']'
        $resolution = Resolve-JointCrosswalkValue $crosswalk $crosswalkSourceId $true $targetTagIds $tagPath
        if (-not [string]::IsNullOrWhiteSpace($resolution.Warning)) { $warnings.Add($resolution.Warning + ':' + $crosswalkSourceId) }
        $selector = $tag
        if (-not $seenSelectors.Add($selector)) { Throw-JointReject 'persona.duplicate_id' 'mapping' $selector 'Duplicate migrated selector.' }
        if ($resolution.Action -eq 'emit') {
            Add-JointObservation $observations $rows $reviewMap $selector $resolution.TargetId $resolution.TargetId $tagPath 'reject' 'identity' $resolution.MappingRow
        } elseif ($resolution.Action -eq 'preserve_only') {
            Add-JointPreservedMapping $rows $resolution.MappingRow $tagPath 'present'
            $preservedLegacyData[$crosswalkSourceId] = 'present'
        }
        $tagIndex++
    }
    $facetStrengths = Get-JointJsonProperty $workbench 'facetStrengths'
    foreach ($facetId in (Get-JointJsonProperties $facetStrengths | Sort-Object)) {
        $strength = [int](Get-JointJsonProperty $facetStrengths $facetId)
        $crosswalkSourceId = 'facetStrengths.' + $facetId
        $facetPath = $sourcePath + '.facetStrengths.' + $facetId
        $resolution = Resolve-JointCrosswalkValue $crosswalk $crosswalkSourceId $strength $targetTagIds $facetPath
        if (-not [string]::IsNullOrWhiteSpace($resolution.Warning)) { $warnings.Add($resolution.Warning + ':' + $crosswalkSourceId) }
        $selector = $crosswalkSourceId
        if (-not $seenSelectors.Add($selector)) { Throw-JointReject 'persona.duplicate_id' 'mapping' $selector 'Duplicate migrated selector.' }
        if ($resolution.Action -eq 'emit') {
            Add-JointObservation $observations $rows $reviewMap $selector ([ordered]@{ tagId = $resolution.TargetId; strength = [int]$strength }) $resolution.TargetId $facetPath 'reject' 'facet_to_observation' $resolution.MappingRow
        } elseif ($resolution.Action -eq 'preserve_only') {
            Add-JointPreservedMapping $rows $resolution.MappingRow $facetPath $strength
            $preservedLegacyData[$crosswalkSourceId] = $strength
        }
    }
    $axisProfiles = [ordered]@{
        traitProfile = @('caution','ambition','pride','pragmatism','inGroupLoyalty','tradition')
        expressionProfile = @('restraint','directness','formality','playfulness','warmth')
        behaviorProfile = @('conditionality','deliberation','trustTesting','leverage','inGroupPriority','leadership')
        reactionProfile = @('confrontation','expression','timing','resentment','supportSeeking')
        commitmentProfile = @('promiseCaution','promisePersistence','valueTradeability')
    }
    foreach ($profileName in $axisProfiles.Keys) {
        foreach ($field in $axisProfiles[$profileName]) {
            $value = Get-JointOptionalInteger (Get-JointJsonProperty $workbench $profileName) $field ($sourcePath + '.' + $profileName) -Minimum -2 -Maximum 2
            if ($null -eq $value) { continue }
            $axisPath = $sourcePath + '.' + $profileName + '.' + $field
            $crosswalkSourceId = $profileName + '.' + $field
            $resolution = Resolve-JointCrosswalkValue $crosswalk $crosswalkSourceId ([int]$value) $targetTagIds $axisPath
            if (-not [string]::IsNullOrWhiteSpace($resolution.Warning)) { $warnings.Add($resolution.Warning + ':' + $crosswalkSourceId) }
            $selector = $profileName + '.' + $field
            if (-not $seenSelectors.Add($selector)) { Throw-JointReject 'persona.duplicate_id' 'mapping' $selector 'Duplicate migrated selector.' }
            if ($resolution.Action -eq 'emit') {
                Add-JointObservation $observations $rows $reviewMap $selector ([ordered]@{ axisValue = [int]$value; targetTagId = $resolution.TargetId }) $resolution.TargetId $axisPath 'reject' 'axis_to_observation' $resolution.MappingRow
            } elseif ($resolution.Action -eq 'preserve_only') {
                Add-JointPreservedMapping $rows $resolution.MappingRow $axisPath ([int]$value)
                $preservedLegacyData[$crosswalkSourceId] = [int]$value
            }
        }
    }
    foreach ($observation in $observations) {
        if ([string]$observation.reviewState -ne 'accepted' -or -not [bool]$observation.enabled -or [string]::IsNullOrWhiteSpace([string]$observation.provenance) -or [string]::IsNullOrWhiteSpace([string]$observation.evidence)) {
            Throw-JointReject 'persona.authoring_observation_unreviewed' 'promotion' ('$.observations.' + $observation.selectorId) 'All migrated runtime observations require accepted review, evidence, provenance and enabled=true.'
        }
    }
    $pinned = Get-JointPinnedValues $FixtureInput $registry
    $ruleOverrides = Assert-JointRuleOverrides $FixtureInput
    $authored = [ordered]@{
        core = Get-JointOptionalString $workbench 'core' '$.workbench'
        identityFacts = Get-JointOptionalString $workbench 'identityFacts' '$.workbench'
        summary = Get-JointOptionalString $workbench 'summary' '$.workbench'
        publicDescription = Get-JointOptionalString $workbench 'publicDescription' '$.workbench'
        privateDescription = Join-JointText @((Get-JointOptionalString $workbench 'privateDescription' '$.workbench'), ('SENSITIVE_CONDITIONS=' + (Get-JointOptionalString (Get-JointJsonProperty $workbench 'reactionProfile') 'sensitiveConditions' '$.workbench.reactionProfile')), ('CONDITIONAL_RESPONSES=' + (Get-JointOptionalString (Get-JointJsonProperty $workbench 'reactionProfile') 'conditionalResponses' '$.workbench.reactionProfile')))
        contradictionDescription = Join-JointText @((Get-JointOptionalString $workbench 'contradictionDescription' '$.workbench'), ('PRIORITY_ORDER=' + (Get-JointOptionalString (Get-JointJsonProperty $workbench 'commitmentProfile') 'priorityOrder' '$.workbench.commitmentProfile')), ('PROTECTED_VALUES=' + (Get-JointOptionalString (Get-JointJsonProperty $workbench 'commitmentProfile') 'protectedValues' '$.workbench.commitmentProfile')), ('APPLICABLE_SCOPE=' + (Get-JointOptionalString (Get-JointJsonProperty $workbench 'commitmentProfile') 'applicableScope' '$.workbench.commitmentProfile')), ('EXCEPTION_COST=' + (Get-JointOptionalString (Get-JointJsonProperty $workbench 'commitmentProfile') 'exceptionCost' '$.workbench.commitmentProfile')), ('BREACH_RESPONSE=' + (Get-JointOptionalString (Get-JointJsonProperty $workbench 'commitmentProfile') 'breachResponse' '$.workbench.commitmentProfile')))
        selfClaimRules = Get-JointStringArrayOrEmpty (Get-JointJsonProperty $workbench 'selfClaimRules') '$.workbench.selfClaimRules'
        realSelfBehaviors = Get-JointStringArrayOrEmpty (Get-JointJsonProperty $workbench 'realSelfBehaviors') '$.workbench.realSelfBehaviors'
        selfClaimExamples = Get-JointStringArrayOrEmpty (Get-JointJsonProperty $workbench 'selfClaimExamples') '$.workbench.selfClaimExamples'
    }
    $facts = [Collections.Generic.List[object]]::new()
    if (-not [string]::IsNullOrWhiteSpace($authored.identityFacts)) { $facts.Add([ordered]@{ factId = 'fact.identity_note'; kind = 'identityNote'; value = $authored.identityFacts; trust = 'source'; provenance = 'workbench.v1.identityFacts'; enabled = $true }) }
    $facts.Add([ordered]@{ factId = 'fact.role'; kind = 'role'; value = $selection.role; trust = 'selection'; provenance = 'selection.v1.role'; enabled = $true })
    $migration = [ordered]@{
        originSchema = 'persona-workbench.character.v1'
        originTemplateVersion = Get-JointRequiredString $workbench 'templateVersion' '$.workbench'
        authoringRevision = [int]$authoringRevision
        warnings = @($warnings | Sort-Object -Unique)
        preservedLegacyData = $preservedLegacyData
    }
    $authoring = [ordered]@{
        schemaVersion = $script:JointAuthoringSchema
        documentId = $documentId
        displayName = Get-JointRequiredString $workbench 'displayName' '$.workbench' -AllowEmpty
        reviewStatus = Get-JointRequiredString $workbench 'status' '$.workbench'
        registryVersion = $pinned.registryVersion
        registryDigest = $targetRegistrySha256
        instructionVersion = $pinned.instructionVersion
        compilerVersion = $pinned.compilerVersion
        sourcePackId = Get-JointOptionalString $workbench 'sourcePackId' '$.workbench'
        source = [ordered]@{
            confirmedText = Get-JointOptionalString $workbench 'sourceDescription' '$.workbench'
            expandedText = ''
            confirmedTextSha256 = Get-JointHashText ((Get-JointOptionalString $workbench 'sourceDescription' '$.workbench').Normalize([Text.NormalizationForm]::FormC))
        }
        authored = $authored
        facts = @($facts)
        observations = @($observations)
        rules = @($ruleOverrides)
        migration = $migration
    }
    $mappingReportId = Get-JointStableHashId ($documentId + '.mapping') ($sourceSha256 + '|' + $targetRegistrySha256 + '|' + $authoringRevision)
    foreach ($row in $rows) { $row.registrySha256 = $targetRegistrySha256 }
    $mappingReport = [ordered]@{
        schemaVersion = 'awake.persona.mapping-report.v1'
        reportId = $mappingReportId
        sourceSha256 = $sourceSha256
        authoringSha256 = Get-JointCanonicalHash $authoring
        targetRegistrySha256 = $targetRegistrySha256
        rows = @($rows)
        warnings = @($warnings | Sort-Object -Unique)
        errors = @()
        status = 'pass'
    }
    $mappingReportSha256 = Get-JointCanonicalHash $mappingReport
    $approval = Assert-JointApproval $FixtureInput
    $definitionTagById = [ordered]@{}
    foreach ($observation in $observations) {
        $targetId = [string]$observation.value
        if ($observation.value -is [Collections.IDictionary]) {
            $targetId = [string](Get-JointJsonProperty $observation.value 'targetTagId')
            if ([string]::IsNullOrWhiteSpace($targetId)) { $targetId = [string](Get-JointJsonProperty $observation.value 'tagId') }
        }
        $priority = 0
        if ($observation.value -is [Collections.IDictionary] -and (Test-JointJsonProperty $observation.value 'strength')) { $priority = [int](Get-JointJsonProperty $observation.value 'strength') * 100 }
        $tagValue = [ordered]@{ id = $targetId; priority = $priority; sceneKeywords = @(); contextModes = @() }
        if (-not $definitionTagById.Contains($targetId)) {
            $definitionTagById[$targetId] = $tagValue
        } elseif ([int]$definitionTagById[$targetId].priority -lt $priority) {
            $definitionTagById[$targetId] = $tagValue
        }
    }
    $definitionTags = [Collections.Generic.List[object]]::new()
    foreach ($targetId in $definitionTagById.Keys) { $definitionTags.Add($definitionTagById[$targetId]) }
    $definition = [ordered]@{
        schemaVersion = $script:JointDefinitionSchema
        id = $documentId
        characterId = $selection.characterId
        identityId = $selection.identityId
        role = $selection.role
        sourcePackId = Get-JointOptionalString $workbench 'sourcePackId' '$.workbench'
        templateVersion = $pinned.compilerVersion
        status = 'approved'
        priority = [int]$selection.priority
        scope = $selection.scope
        core = $authored.core
        identityFacts = $authored.identityFacts
        relationStyle = ''
        currentStateHints = ''
        summary = $authored.summary
        publicDescription = $authored.publicDescription
        privateDescription = $authored.privateDescription
        contradictionDescription = $authored.contradictionDescription
        selfClaimRules = @($authored.selfClaimRules)
        realSelfBehaviors = @($authored.realSelfBehaviors)
        selfClaimExamples = @($authored.selfClaimExamples)
        tags = @($definitionTags)
        bundles = @()
        experiences = @()
    }
    Assert-JointDefinition $definition
    $exportId = Get-JointStableHashId ($documentId + '.export') ($sourceSha256 + '|' + $selection.selectionRevision + '|' + $authoringRevision)
    $export = [ordered]@{
        schemaVersion = $script:JointExportSchema
        exportId = $exportId
        sourceSchema = 'persona-workbench.character.v1'
        sourceDocumentId = $documentId
        sourceRevision = [int]$selection.sourceRevision
        sourceSha256 = $sourceSha256
        authoringSchema = $script:JointAuthoringSchema
        authoringRevision = [int]$authoringRevision
        authoringSha256 = Get-JointCanonicalHash $authoring
        selectionRevision = [int]$selection.selectionRevision
        registrySchema = $script:JointRegistrySchema
        registrySha256 = $targetRegistrySha256
        mappingReportId = $mappingReportId
        mappingReportSha256 = $mappingReportSha256
        approval = $approval
        selection = Get-JointSelectionFive $selection
        definition = $definition
        revoke = $null
        supersedes = $null
    }
    Assert-JointExactFields $export @('schemaVersion','exportId','sourceSchema','sourceDocumentId','sourceRevision','sourceSha256','authoringSchema','authoringRevision','authoringSha256','selectionRevision','registrySchema','registrySha256','mappingReportId','mappingReportSha256','approval','selection','definition','revoke','supersedes') @('schemaVersion','exportId','sourceSchema','sourceDocumentId','sourceRevision','sourceSha256','authoringSchema','authoringRevision','authoringSha256','selectionRevision','registrySchema','registrySha256','mappingReportId','mappingReportSha256','approval','selection','definition','revoke','supersedes') '$.export'
    if ([int](Get-JointJsonProperty $export 'selectionRevision') -ne [int]$selection.selectionRevision) { Throw-JointReject 'persona.stale_revision' 'export' '$.export.selectionRevision' 'Export selectionRevision does not match the selection sidecar.' }
    $result = [ordered]@{
        schemaVersion = 'awake.persona.adapter-result.v1'
        fixtureId = $FixtureId
        source = $workbench
        selection = $selectionSidecar
        authoring = $authoring
        mappingReport = $mappingReport
        export = $export
        definition = $definition
    }
    return [pscustomobject]@{
        Result = $result
        SourceSha256 = $sourceSha256
        RegistrySha256 = $targetRegistrySha256
        SourceRegistrySha256 = $registrySha256
        SourceRevision = [int]$source.SourceRevision
        AuthoringRevision = [int]$authoringRevision
        SelectionRevision = [int]$selection.selectionRevision
        ExportSelectionRevision = [int](Get-JointJsonProperty $export 'selectionRevision')
        ApprovalEvidenceIds = [ordered]@{
            workbench = [string]$approval.workbench.evidenceId
            awake = [string]$approval.awake.evidenceId
        }
        AuthoringSha256 = Get-JointCanonicalHash $authoring
        MappingReportSha256 = $mappingReportSha256
        ExportSha256 = Get-JointCanonicalHash $export
        DefinitionSha256 = Get-JointCanonicalHash $definition
        ObservedWarnings = @($warnings | Sort-Object -Unique)
        InputSourceRawBytes = $sourceRawBytes
        InputRegistryRawBytes = $registryRawBytes
    }
}

function Convert-JointExceptionToError([Exception]$Exception, [string]$ArtifactId = '') {
    $message = $Exception.Message
    if ($message.StartsWith('__JOINT_REJECT__|', [StringComparison]::Ordinal) -or $message.StartsWith('__JOINT_BLOCKED__|', [StringComparison]::Ordinal) -or $message.StartsWith('__JOINT_NOT_ATTEMPTED__|', [StringComparison]::Ordinal)) {
        $parts = $message.Split('|', 5)
        $kind = $parts[0]
        $code = if ($parts.Count -gt 1) { $parts[1] } else { 'persona.adapter_error' }
        $stage = if ($parts.Count -gt 2) { $parts[2] } else { 'unknown' }
        $path = if ($parts.Count -gt 3) { $parts[3] } else { '$' }
        $detail = if ($parts.Count -gt 4) { $parts[4] } else { $message }
        return [pscustomobject]@{
            schemaVersion = 'awake.persona.adapter-error.v1'
            errorId = Get-JointStableHashId 'error' ($code + '|' + $stage + '|' + $path + '|' + $detail)
            code = $code
            stage = $stage
            artifactId = $ArtifactId
            path = $path
            detail = $detail
            retryable = $false
            kind = $kind
        }
    }
    return [pscustomobject]@{
        schemaVersion = 'awake.persona.adapter-error.v1'
        errorId = Get-JointStableHashId 'error' ($Exception.GetType().FullName + '|' + $message)
        code = 'persona.adapter_internal_error'
        stage = 'internal'
        artifactId = $ArtifactId
        path = '$'
        detail = $message
        retryable = $false
        kind = '__JOINT_ERROR__'
    }
}

function Get-JointFixtureRoot() {
    return Get-JointFullPath (Join-Path $script:JointAwakeRoot 'docs\fixtures\persona-awake-joint')
}

function Get-JointArtifactRoot() {
    $root = Join-Path $script:JointToolRoot 'artifacts'
    [void](Assert-JointOutputPath (Join-Path $root '.keep') 'ArtifactRoot')
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { New-Item -ItemType Directory -Path $root -Force | Out-Null }
    return $root
}

function Get-JointFixtureInput([string]$FixtureId, [string]$FixtureRoot = '') {
    if ([string]::IsNullOrWhiteSpace($FixtureRoot)) { $FixtureRoot = Get-JointFixtureRoot }
    $fixturePath = Get-JointFullPath (Join-Path $FixtureRoot $FixtureId)
    if (-not (Test-JointPathUnder $fixturePath $FixtureRoot -AllowEqual:$false)) { Throw-JointReject 'persona.path_protected' 'fixture' '$.fixtureId' 'Fixture path escapes the fixture root.' }
    if (Test-JointReparsePath $fixturePath) { Throw-JointReject 'persona.path_protected' 'fixture' '$.fixtureId' 'Fixture path traverses a reparse point.' }
    $inputPath = Join-Path $fixturePath 'input.json'
    if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf)) { throw [System.IO.FileNotFoundException]::new('Fixture input is missing.', $inputPath) }
    return Read-JointJsonFile $inputPath ('fixture ' + $FixtureId + ' input')
}

function Get-JointProtectedPathSnapshot([string[]]$Paths) {
    $result = [Collections.Generic.List[object]]::new()
    foreach ($path in $Paths) {
        $full = Get-JointFullPath $path
        if (Test-JointReparsePath $full) { Throw-JointReject 'persona.path_protected' 'isolation' $full 'Protected path traverses a reparse point.' }
        if (Test-Path -LiteralPath $full -PathType Leaf) {
            $result.Add([ordered]@{ path = $full; kind = 'file'; sha256 = Get-JointHashFile $full; length = (Get-Item -LiteralPath $full).Length })
        } elseif (Test-Path -LiteralPath $full -PathType Container) {
            $entries = Get-ChildItem -LiteralPath $full -Recurse -File | Sort-Object FullName
            $lines = [Collections.Generic.List[string]]::new()
            foreach ($entry in $entries) {
                if (Test-JointReparsePath $entry.FullName) { Throw-JointReject 'persona.path_protected' 'isolation' $entry.FullName 'Protected tree contains a reparse point.' }
                $relative = Get-JointRelativePath $entry.FullName $full
                $lines.Add($relative + "`0" + (Get-JointHashFile $entry.FullName) + "`n")
            }
            $treeHash = Get-JointHashText ($lines -join '')
            $result.Add([ordered]@{ path = $full; kind = 'tree'; sha256 = $treeHash; fileCount = $entries.Count })
        } else {
            $result.Add([ordered]@{ path = $full; kind = 'missing'; sha256 = $null; fileCount = 0 })
        }
    }
    return @($result.ToArray())
}

function Compare-JointSnapshots([object[]]$Before, [object[]]$After) {
    $beforeText = Get-JointCanonicalText @($Before)
    $afterText = Get-JointCanonicalText @($After)
    return [StringComparer]::Ordinal.Equals($beforeText, $afterText)
}

function Get-JointReportBase([string]$FixtureId, [string]$CommandLine, [string]$NormalizedCwd) {
    return [ordered]@{
        schemaVersion = $script:JointFixtureReportSchema
        fixtureId = $FixtureId
        commandLine = $CommandLine
        normalizedCwd = $NormalizedCwd
        inputHashes = [ordered]@{}
        outputHashes = [ordered]@{}
        handoff = $null
        status = 'error'
        exitCode = 40
        assertions = @()
        observedErrors = @()
        cleanup = [ordered]@{ status = 'not_attempted'; paths = @(); errors = @() }
    }
}

function Add-JointAssertion([Collections.Generic.List[object]]$Assertions, [string]$Name, [bool]$Passed, [string]$Detail) {
    $Assertions.Add([ordered]@{ id = $Name; passed = $Passed; detail = $Detail })
}

function Get-JointRawProperty([object]$Value, [string]$Name) {
    if ($null -eq $Value) { return $null }
    if ($Value -is [Collections.IDictionary]) { return $Value[$Name] }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Convert-JointReportHashMapToArray([object]$Value) {
    $entries = [Collections.Generic.List[object]]::new()
    if ($null -eq $Value) { return ,([object[]]@()) }
    if (Test-JointJsonArray $Value) { return ,([object[]]$Value) }
    foreach ($name in (Get-JointJsonProperties $Value | Sort-Object)) {
        $entries.Add([ordered]@{
            path = [string]$name
            sha256 = Get-JointRawProperty $Value $name
        })
    }
    return ,([object[]]$entries.ToArray())
}

function Convert-JointReportObservedErrors([object]$Errors) {
    $normalized = [Collections.Generic.List[object]]::new()
    foreach ($error in @($Errors)) {
        if ($null -eq $error) { continue }
        $code = [string](Get-JointRawProperty $error 'code')
        if ([string]::IsNullOrWhiteSpace($code) -or $code -notmatch '^persona\.[a-z0-9_.-]+$') { $code = 'persona.report_error' }
        $stage = [string](Get-JointRawProperty $error 'stage')
        if ([string]::IsNullOrWhiteSpace($stage) -or $stage -notmatch '^[a-z][a-z0-9_.-]*$') { $stage = 'report' }
        $path = [string](Get-JointRawProperty $error 'path')
        if ([string]::IsNullOrWhiteSpace($path)) { $path = '$' }
        $detail = [string](Get-JointRawProperty $error 'detail')
        if ([string]::IsNullOrWhiteSpace($detail)) { $detail = $code }
        $rawArtifactId = [string](Get-JointRawProperty $error 'artifactId')
        $artifactId = if ($rawArtifactId -match '^[a-z0-9_]+(?:\.[a-z0-9_]+)*$') {
            $rawArtifactId
        } else {
            Get-JointStableHashId 'fixture_artifact' ($rawArtifactId + '|' + $code + '|' + $path + '|' + $detail)
        }
        $rawErrorId = [string](Get-JointRawProperty $error 'errorId')
        $errorId = if ($rawErrorId -match '^[a-z0-9_]+(?:\.[a-z0-9_]+)*$') {
            $rawErrorId
        } else {
            Get-JointStableHashId 'error' ($code + '|' + $stage + '|' + $artifactId + '|' + $path + '|' + $detail)
        }
        $retryableValue = Get-JointRawProperty $error 'retryable'
        $retryable = if ($retryableValue -is [bool]) { [bool]$retryableValue } else { $false }
        $normalized.Add([ordered]@{
            schemaVersion = 'awake.persona.adapter-error.v1'
            errorId = $errorId
            code = $code
            stage = $stage
            artifactId = $artifactId
            path = $path
            detail = $detail
            retryable = $retryable
        })
    }
    return ,([object[]]$normalized.ToArray())
}

function Convert-JointReportForOutput([object]$Report) {
    $normalized = [ordered]@{}
    foreach ($name in (Get-JointJsonProperties $Report | Sort-Object)) {
        if ($name -eq 'cleanupStatus' -or $name -eq 'kind') { continue }
        $value = Get-JointRawProperty $Report $name
        if ($name -in @('inputHashes', 'outputHashes')) {
            $normalized[$name] = Convert-JointReportHashMapToArray $value
        } elseif ($name -eq 'observedErrors') {
            $normalized[$name] = Convert-JointReportObservedErrors $value
        } else {
            $normalized[$name] = $value
        }
    }
    return $normalized
}

function Write-JointReport([object]$Report, [string]$ReportPath) {
    $text = Get-JointCanonicalText (Convert-JointReportForOutput $Report)
    Write-JointUtf8Atomic $ReportPath $text | Out-Null
}
