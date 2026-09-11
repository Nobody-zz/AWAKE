using System;

namespace MarcusAwakeStorage;

public sealed class SqliteStorageOptions
{
    public int MaxValueBytes { get; set; } = 512 * 1024;

    public int MaxDocumentBytes { get; set; } = 512 * 1024;

    public int MaxLedgerPayloadBytes { get; set; } = 512 * 1024;

    public int MaxResultBytes { get; set; } = 16 * 1024;

    public int MaxQueryBytes { get; set; } = 64 * 1024;

    public int BusyTimeoutMilliseconds { get; set; } = 5000;

    public int MaxLedgerRead { get; set; } = 256;

    internal void Validate()
    {
        ValidatePositive(MaxValueBytes, nameof(MaxValueBytes), 4 * 1024 * 1024);
        ValidatePositive(MaxDocumentBytes, nameof(MaxDocumentBytes), 4 * 1024 * 1024);
        ValidatePositive(MaxLedgerPayloadBytes, nameof(MaxLedgerPayloadBytes), 4 * 1024 * 1024);
        ValidatePositive(MaxResultBytes, nameof(MaxResultBytes), 4 * 1024 * 1024);
        ValidatePositive(MaxQueryBytes, nameof(MaxQueryBytes), 4 * 1024 * 1024);
        ValidatePositive(BusyTimeoutMilliseconds, nameof(BusyTimeoutMilliseconds), 120000);
        ValidatePositive(MaxLedgerRead, nameof(MaxLedgerRead), 4096);
    }

    private static void ValidatePositive(int value, string name, int maximum)
    {
        if (value < 1 || value > maximum) throw new ArgumentOutOfRangeException(name);
    }
}
