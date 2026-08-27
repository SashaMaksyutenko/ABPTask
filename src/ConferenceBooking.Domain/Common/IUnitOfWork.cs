namespace ConferenceBooking.Domain.Common;

/// <summary>
/// Одиниця роботи: фіксація змін і виконання кількох операцій в одній транзакції.
/// Потрібна, зокрема, щоб перевірка «чи вільний зал» і вставка бронювання були атомарними.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Виконує <paramref name="operation"/> у транзакції з рівнем ізоляції, що не допускає
    /// «фантомних» бронювань між перевіркою і вставкою.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
