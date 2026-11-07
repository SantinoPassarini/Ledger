using Ledger.Api.Contracts.Transactions;

namespace Ledger.Api.Contracts.Transfers;

public sealed record TransferResponse(
    Guid TransferGroupId,
    TransactionResponse OutTransaction,
    TransactionResponse InTransaction);
