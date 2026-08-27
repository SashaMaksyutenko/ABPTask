using ConferenceBooking.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConferenceBooking.Infrastructure.Persistence.Configurations;

/// <summary>Спільні налаштування зберігання грошових сум.</summary>
internal static class MoneyColumn
{
    /// <summary>
    /// Гроші зберігаються з фіксованою точністю. Тип із плаваючою комою тут неприпустимий:
    /// накопичена похибка в підсумках рахунків рано чи пізно призводить до розбіжностей у звітності.
    /// </summary>
    public const string TypeName = "decimal(18,2)";
}

public sealed class AmenityConfiguration : IEntityTypeConfiguration<Amenity>
{
    public void Configure(EntityTypeBuilder<Amenity> builder)
    {
        builder.ToTable("Amenities");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(Amenity.MaxNameLength);

        builder.Property(a => a.NormalizedName)
            .IsRequired()
            .HasMaxLength(Amenity.MaxNameLength);

        builder.Property(a => a.DefaultPrice)
            .HasColumnType(MoneyColumn.TypeName);

        // Унікальність — за нормалізованою назвою: «Проєктор» і «проєктор» мають бути
        // однією позицією каталогу, а не двома.
        builder.HasIndex(a => a.NormalizedName).IsUnique();
    }
}

public sealed class ConferenceRoomConfiguration : IEntityTypeConfiguration<ConferenceRoom>
{
    public void Configure(EntityTypeBuilder<ConferenceRoom> builder)
    {
        builder.ToTable("Rooms");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(ConferenceRoom.MaxNameLength);

        builder.Property(r => r.NormalizedName)
            .IsRequired()
            .HasMaxLength(ConferenceRoom.MaxNameLength);

        builder.Property(r => r.BasePricePerHour)
            .HasColumnType(MoneyColumn.TypeName);

        builder.Property(r => r.IsDeleted).HasDefaultValue(false);

        // Унікальність назви діє лише серед активних залів: видалений «Зал А» не повинен
        // блокувати створення нового залу з тією ж назвою.
        builder.HasIndex(r => r.NormalizedName)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = 0");

        builder.HasIndex(r => r.IsDeleted);

        builder.HasMany(r => r.Amenities)
            .WithOne()
            .HasForeignKey(a => a.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        // Колекція інкапсульована за приватним полем, тому EF має працювати з полем,
        // а не з властивістю тільки для читання.
        builder.Metadata
            .FindNavigation(nameof(ConferenceRoom.Amenities))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class RoomAmenityConfiguration : IEntityTypeConfiguration<RoomAmenity>
{
    public void Configure(EntityTypeBuilder<RoomAmenity> builder)
    {
        builder.ToTable("RoomAmenities");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Price).HasColumnType(MoneyColumn.TypeName);

        builder.HasOne(a => a.Amenity)
            .WithMany()
            .HasForeignKey(a => a.AmenityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Та сама послуга не може бути двічі в одному залі.
        builder.HasIndex(a => new { a.RoomId, a.AmenityId }).IsUnique();
    }
}
