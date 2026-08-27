namespace ConferenceBooking.Domain.Common;

/// <summary>
/// Базовий клас для всіх сутностей домену.
/// Ідентичність сутності визначається виключно її <see cref="Id"/>, а не значеннями полів.
///
/// Ідентифікатор навмисно не заповнюється тут. Корені агрегатів генерують його у власних
/// конструкторах, бо він потрібен їм одразу; дочірні сутності лишають його порожнім, і його
/// проставляє сховище. Якби Id заповнювався всім, сховище приймало б щойно створену дочірню
/// сутність за вже наявний рядок і намагалося оновити те, чого в таблиці ще немає.
/// </summary>
public abstract class Entity
{
    /// <summary>Унікальний ідентифікатор сутності.</summary>
    public Guid Id { get; protected set; }

    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
