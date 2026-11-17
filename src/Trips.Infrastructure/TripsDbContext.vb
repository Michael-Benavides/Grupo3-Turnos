Imports Microsoft.EntityFrameworkCore
Imports Microsoft.EntityFrameworkCore.Metadata.Builders
Imports Trips.Domain

Public Class TripsDbContext
    Inherits DbContext

    Public Sub New(options As DbContextOptions(Of TripsDbContext))
        MyBase.New(options)
    End Sub

    Public Property ScheduleTemplates As DbSet(Of ScheduleTemplate)
    Public Property Trips As DbSet(Of Trip)
    Public Property TripSeats As DbSet(Of TripSeat)
    Public Property FareRules As DbSet(Of FareRule)

    Protected Overrides Sub OnModelCreating(modelBuilder As ModelBuilder)
        MyBase.OnModelCreating(modelBuilder)

        modelBuilder.Entity(Of ScheduleTemplate)(
            Sub(entity)
                entity.HasKey(Function(template) template.Id)
                entity.Property(Function(template) template.Shift).HasConversion(Of String)()
                entity.Property(Function(template) template.BasePrice).HasPrecision(10, 2)
            End Sub)

        modelBuilder.Entity(Of Trip)(
            Sub(entity)
                entity.HasKey(Function(trip) trip.Id)
                entity.Property(Function(trip) trip.Shift).HasConversion(Of String)()
                entity.Property(Function(trip) trip.Status).HasConversion(Of String)()
                entity.Property(Function(trip) trip.CurrentPrice).HasPrecision(10, 2)
                entity.Property(Function(trip) trip.DepartureDate).HasColumnType("date")
                entity.HasIndex(Function(trip) New With {trip.TemplateId, trip.DepartureDate}).
                    IsUnique().
                    HasFilter(Nothing)
                entity.HasMany(Function(trip) trip.Seats).
                    WithOne().
                    HasForeignKey(Function(seat) seat.TripId).
                    OnDelete(DeleteBehavior.Cascade)
            End Sub)

        modelBuilder.Entity(Of TripSeat)(
            Sub(entity)
                entity.HasKey(Function(seat) seat.Id)
                entity.HasIndex(Function(seat) New With {seat.TripId, seat.SeatNumber}).
                    IsUnique().
                    HasFilter(Nothing)
                entity.Property(Function(seat) seat.Status).HasConversion(Of String)()
                entity.Property(Function(seat) seat.RowVersion).IsRowVersion().IsRequired()
                entity.ToTable(
                    Sub(table)
                        table.HasCheckConstraint("CK_TripSeats_SeatNumber", "[SeatNumber] BETWEEN 1 AND 40")
                    End Sub)
            End Sub)

        modelBuilder.Entity(Of FareRule)(
            Sub(entity)
                entity.HasKey(Function(rule) rule.Id)
                entity.Property(Function(rule) rule.Multiplier).HasPrecision(10, 2)
                entity.Property(Function(rule) rule.Condition).IsRequired()
                entity.Property(Function(rule) rule.[Type]).HasConversion(Of String)()
                entity.HasData(
                    New FareRule With {
                        .Id = New Guid("a0100000-0000-0000-0000-000000000001"),
                        .Type = FareRuleType.Occupancy,
                        .Multiplier = 1.15D,
                        .Condition = "occupancy>70%"
                    },
                    New FareRule With {
                        .Id = New Guid("a0100000-0000-0000-0000-000000000002"),
                        .Type = FareRuleType.Occupancy,
                        .Multiplier = 1.3D,
                        .Condition = "occupancy>90%"
                    },
                    New FareRule With {
                        .Id = New Guid("a0100000-0000-0000-0000-000000000003"),
                        .Type = FareRuleType.Shift,
                        .Multiplier = 1.1D,
                        .Condition = "shift=Night"
                    },
                    New FareRule With {
                        .Id = New Guid("a0100000-0000-0000-0000-000000000004"),
                        .Type = FareRuleType.Weekend,
                        .Multiplier = 1.1D,
                        .Condition = "weekend=Saturday,Sunday"
                    },
                    New FareRule With {
                        .Id = New Guid("a0100000-0000-0000-0000-000000000005"),
                        .Type = FareRuleType.Holiday,
                        .Multiplier = 1.25D,
                        .Condition = "dates="
                    })
            End Sub)
    End Sub
End Class
