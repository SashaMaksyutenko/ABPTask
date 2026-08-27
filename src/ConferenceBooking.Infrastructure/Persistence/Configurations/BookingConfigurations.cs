using ConferenceBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConferenceBooking.Infrastructure.Persistence.Configurations;

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.RoomName)
            .IsRequired()
            .HasMaxLength(Domain.Rooms.ConferenceRoom.MaxNameLength);

        builder.Property(b => b.CustomerName)
            .IsRequired()
            .HasMaxLength(Booking.MaxCustomerNameLength);

        builder.Property(b => b.CustomerEmail)
            .IsRequired()
            .HasMaxLength(Booking.MaxCustomerEmailLength);

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(b => b.RoomCost).HasColumnType(MoneyColumn.TypeName);
        builder.Property(b => b.AmenitiesCost).HasColumnType(MoneyColumn.TypeName);
        builder.Property(b => b.TotalCost).HasColumnType(MoneyColumn.TypeName);

        // Обчислювані властивості домену не мають колонок у БД.
        builder.Ignore(b => b.Period);
        builder.Ignore(b => b.Hours);
        builder.Ignore(b => b.IsBlocking);

        // Головний індекс сервісу: перевірка перетинів виконується на кожне бронювання
        // і на кожен пошук вільних залів.
        builder.HasIndex(b => new { b.RoomId, b.StartAt, b.EndAt });
        builder.HasIndex(b => b.Date);
        builder.HasIndex(b => b.Status);

        builder.HasOne<Domain.Rooms.ConferenceRoom>()
            .WithMany()
            .HasForeignKey(b => b.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.Amenities)
            .WithOne()
            .HasForeignKey(a => a.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Booking.Amenities))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class BookingAmenityConfiguration : IEntityTypeConfiguration<BookingAmenity>
{
    public void Configure(EntityTypeBuilder<BookingAmenity> builder)
    {
        builder.ToTable("BookingAmenities");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(Domain.Rooms.Amenity.MaxNameLength);

        builder.Property(a => a.Price).HasColumnType(MoneyColumn.TypeName);

        builder.HasIndex(a => a.AmenityId);
    }
}
