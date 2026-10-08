using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppleStore.Tests;

// Runs another request's SQL once, just before the first command whose
// text contains the marker, on the same connection and transaction.
public sealed class SqlRace : DbCommandInterceptor
{
    private string? _marker;
    private string? _sql;

    public bool Ran { get; private set; }

    public void Arm(string marker, string sql) => (_marker, _sql) = (marker, sql);

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (!Ran && _marker is not null && command.CommandText.Contains(_marker))
        {
            Ran = true;
            await using var other = command.Connection!.CreateCommand();
            other.Transaction = command.Transaction;
            other.CommandText = _sql;
            await other.ExecuteNonQueryAsync(cancellationToken);
        }
        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }
}
