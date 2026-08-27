using ConferenceBooking.Domain.Bookings;
using ConferenceBooking.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace ConferenceBooking.Infrastructure.Persistence;

/// <summary>Контекст бази даних застосунку.</summary>
public sealed class ConferenceBookingDbContext : DbContext
{
    public ConferenceBookingDbContext(DbContextOptions<ConferenceBookingDbContext> options)
        : base(options)
    {
    }

    public DbSet<ConferenceRoom> Rooms => Set<ConferenceRoom>();

    public DbSet<Amenity> Amenities => Set<Amenity>();

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Конфігурації винесені в окремі класи: інакше OnModelCreating перетворюється
        // на кількасотрядковий метод, у якому неможливо знайти потрібну сутність.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConferenceBookingDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
