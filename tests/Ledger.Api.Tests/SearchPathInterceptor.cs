using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ledger.Api.Tests;

public sealed class SearchPathInterceptor(string schema) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetSearchPath(connection);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetSearchPathAsync(connection, cancellationToken);
    }

    private void SetSearchPath(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SET search_path TO \"{schema}\"";
        command.ExecuteNonQuery();
    }

    private async Task SetSearchPathAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SET search_path TO \"{schema}\"";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
