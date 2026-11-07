namespace Ledger.Api.Options;

public sealed class LedgerOptions
{
    public const string SectionName = "Ledger";

    public string Currency { get; set; } = "ARS";
}
