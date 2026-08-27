using ConferenceBooking.Domain.Common;
using ConferenceBooking.Domain.Rooms;

namespace ConferenceBooking.UnitTests.Domain;

public sealed class ConferenceRoomTests
{
    [Fact]
    public void Constructor_TrimsNameAndKeepsValues()
    {
        var room = new ConferenceRoom("  Зал А  ", 50, 2000m);

        Assert.Equal("Зал А", room.Name);
        Assert.Equal(50, room.Capacity);
        Assert.Equal(2000m, room.BasePricePerHour);
        Assert.Null(room.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ChangeCapacity_NonPositive_Throws(int capacity)
    {
        var room = new ConferenceRoom("Зал А", 50, 2000m);

        var exception = Assert.Throws<DomainException>(() => room.ChangeCapacity(capacity));

        Assert.Equal("invalid_capacity", exception.Code);
    }

    [Fact]
    public void ChangeBasePrice_Negative_Throws()
    {
        var room = new ConferenceRoom("Зал А", 50, 2000m);

        var exception = Assert.Throws<DomainException>(() => room.ChangeBasePrice(-1m));

        Assert.Equal("negative_amount", exception.Code);
    }

    [Fact]
    public void AddOrUpdateAmenity_SameAmenityTwice_UpdatesPriceInsteadOfDuplicating()
    {
        var room = new ConferenceRoom("Зал А", 50, 2000m);
        var projector = new Amenity("Проєктор", 500m);

        room.AddOrUpdateAmenity(projector, 500m);
        room.AddOrUpdateAmenity(projector, 650m);

        var stored = Assert.Single(room.Amenities);
        Assert.Equal(650m, stored.Price);
    }

    [Fact]
    public void ReplaceAmenities_WithDuplicates_Throws()
    {
        var room = new ConferenceRoom("Зал А", 50, 2000m);
        var projector = new Amenity("Проєктор", 500m);

        var exception = Assert.Throws<DomainException>(
            () => room.ReplaceAmenities([(projector, 500m), (projector, 700m)]));

        Assert.Equal("duplicate_amenity", exception.Code);
    }

    [Fact]
    public void ResolveAmenities_UnknownAmenity_Throws()
    {
        var room = new ConferenceRoom("Зал А", 50, 2000m);
        room.AddOrUpdateAmenity(new Amenity("Проєктор", 500m), 500m);

        var exception = Assert.Throws<DomainException>(() => room.ResolveAmenities([Guid.NewGuid()]));

        Assert.Equal("amenity_not_available", exception.Code);
    }

    [Fact]
    public void ResolveAmenities_ReturnsRoomSpecificPrice()
    {
        var room = new ConferenceRoom("Зал B", 100, 3500m);
        var sound = new Amenity("Звук", 700m);

        // У цьому залі звук дорожчий за типову ціну каталогу.
        room.AddOrUpdateAmenity(sound, 900m);

        var resolved = Assert.Single(room.ResolveAmenities([sound.Id]));
        Assert.Equal(900m, resolved.Price);
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(51, false)]
    [InlineData(0, false)]
    public void CanAccommodate_ChecksCapacity(int attendees, bool expected)
    {
        var room = new ConferenceRoom("Зал А", 50, 2000m);

        Assert.Equal(expected, room.CanAccommodate(attendees));
    }
}
