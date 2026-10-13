using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SPWPOS.Application.Authentication;
namespace SPWPOS.Infrastructure.Persistence;
// Every account mutation uses the SAME normalized-email lock, including token flows.
// SQL Server application locks are shared by all API instances, and released at commit/rollback.
public static class AccountTransaction
{
    public static async Task<IDbContextTransaction> BeginAsync(ApplicationDbContext db, string normalizedEmail, CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            if (db.Database.IsSqlServer())
            {
                await LockAsync(db, tx, "account", normalizedEmail, ct);
            }
            return tx;
        }
        catch { await tx.DisposeAsync(); throw; }
    }
    public static Task LockUserNameAsync(ApplicationDbContext db, string normalizedUserName, CancellationToken ct) =>
        db.Database.IsSqlServer() ? LockAsync(db, db.Database.CurrentTransaction!, "username", normalizedUserName, ct) : Task.CompletedTask;

    private static async Task LockAsync(ApplicationDbContext db, IDbContextTransaction tx, string scope, string identifier, CancellationToken ct)
    {
        var resource = "SPWPOS:" + scope + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identifier)));
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = tx.GetDbTransaction();
        command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = @key, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; SELECT @result;";
        var p = command.CreateParameter(); p.ParameterName = "@key"; p.Value = resource;
        command.Parameters.Add(p);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        if (result < 0) throw new AuthException("REQUEST_BUSY", "Please retry the request.", 409);
    }
}
