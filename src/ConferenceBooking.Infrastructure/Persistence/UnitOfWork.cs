using System.Data;
using ConferenceBooking.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ConferenceBooking.Infrastructure.Persistence;

/// <inheritdoc cref="IUnitOfWork"/>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ConferenceBookingDbContext _db;

    public UnitOfWork(ConferenceBookingDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Якщо транзакція вже відкрита вище за стеком, не починаємо вкладену:
        // вкладені транзакції EF Core не підтримує, а операція має лишитися перевикористовуваною.
        if (_db.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }

        // Стратегія виконання враховує налаштовані політики повторів провайдера;
        // без неї повтор транзакції на транзієнтній помилці кинув би InvalidOperationException.
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async ct =>
        {
            // Serializable: між перевіркою «чи вільний зал» і вставкою бронювання не має
            // з'явитися чуже бронювання на той самий проміжок.
            await using var transaction = await _db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, ct)
                .ConfigureAwait(false);

            try
            {
                var result = await operation(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }, cancellationToken).ConfigureAwait(false);
    }
}
