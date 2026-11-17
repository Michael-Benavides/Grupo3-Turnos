Imports Microsoft.EntityFrameworkCore
Imports Microsoft.EntityFrameworkCore.Infrastructure
Imports Trips.Domain

<DbContext(GetType(TripsDbContext))>
Partial Public Class TripsDbContextModelSnapshot
    Inherits ModelSnapshot

    Protected Overrides Sub BuildModel(modelBuilder As ModelBuilder)
        modelBuilder.HasAnnotation("ProductVersion", "11.0.0")

        modelBuilder.Entity("Trips.Domain.FareRule",
            Sub(entity)
                entity.Property(Of String)("Condition").IsRequired().HasColumnType("nvarchar(max)")
                entity.Property(Of Guid)("Id").HasColumnType("uniqueidentifier")
                entity.Property(Of Decimal)("Multiplier").HasPrecision(10, 2).HasColumnType("decimal(10,2)")
                entity.Property(Of FareRuleType)("Type").IsRequired().HasConversion(Of String)().HasColumnType("nvarchar(max)")
                entity.HasKey("Id")
                entity.HasData(
                    New With {
                        .Id = New Guid("a0100000-0000-0000-0000-000000000001"),
                        .Condition = "occupancy>70%",
                        .Multiplier = 1.15D,
                        .Type = FareRuleType.Occupancy
                    },
                    New With {
                        .Id = New Guid("a0100000-0000-0000-0000-000000000002"),
                        .Condition = "occupancy>90%",
                        .Multiplier = 1.3D,
                        .Type = FareRuleType.Occupancy
                    },
                    New With {
                        .Id = New Guid("a0100000-0000-0000-0000-000000000003"),
                        .Condition = "shift=Night",
                        .Multiplier = 1.1D,
                        .Type = FareRuleType.Shift
                    },
                    New With {
                        .Id = New Guid("a0100000-0000-0000-0000-000000000004"),
                        .Condition = "weekend=Saturday,Sunday",
                        .Multiplier = 1.1D,
                        .Type = FareRuleType.Weekend
                    },
                    New With {
                        .Id = New Guid("a0100000-0000-0000-0000-000000000005"),
                        .Condition = "dates=",
                        .Multiplier = 1.25D,
                        .Type = FareRuleType.Holiday
                    })
                entity.ToTable("FareRules")
            End Sub)

        modelBuilder.Entity("Trips.Domain.ScheduleTemplate",
            Sub(entity)
                entity.Property(Of Decimal)("BasePrice").HasPrecision(10, 2).HasColumnType("decimal(10,2)")
                entity.Property(Of Guid)("BusId").HasColumnType("uniqueidentifier")
                entity.Property(Of TimeSpan)("DepartureTime").HasColumnType("time")
                entity.Property(Of Guid)("Id").HasColumnType("uniqueidentifier")
                entity.Property(Of Boolean)("IsActive").HasColumnType("bit")
                entity.Property(Of Guid)("RouteId").HasColumnType("uniqueidentifier")
                entity.Property(Of String)("Shift").IsRequired().HasColumnType("nvarchar(max)")
                entity.HasKey("Id")
                entity.ToTable("ScheduleTemplates")
            End Sub)

        modelBuilder.Entity("Trips.Domain.Trip",
            Sub(entity)
                entity.Property(Of Decimal)("CurrentPrice").HasPrecision(10, 2).HasColumnType("decimal(10,2)")
                entity.Property(Of DateTime)("DepartureAt").HasColumnType("datetime2")
                entity.Property(Of DateOnly)("DepartureDate").HasColumnType("date")
                entity.Property(Of Guid)("BusId").HasColumnType("uniqueidentifier")
                entity.Property(Of Guid)("Id").HasColumnType("uniqueidentifier")
                entity.Property(Of Guid)("RouteId").HasColumnType("uniqueidentifier")
                entity.Property(Of Shift)("Shift").HasConversion(Of String)().HasColumnType("nvarchar(max)")
                entity.Property(Of TripStatus)("Status").HasConversion(Of String)().HasColumnType("nvarchar(max)")
                entity.Property(Of Guid)("TemplateId").HasColumnType("uniqueidentifier")
                entity.HasKey("Id")
                entity.HasIndex("TemplateId", "DepartureDate").IsUnique().HasFilter(Nothing)
                entity.ToTable("Trips")
            End Sub)

        modelBuilder.Entity("Trips.Domain.TripSeat",
            Sub(entity)
                entity.Property(Of String)("HeldBy").HasColumnType("nvarchar(max)")
                entity.Property(Of DateTime?)("HeldUntil").HasColumnType("datetime2")
                entity.Property(Of Guid)("Id").HasColumnType("uniqueidentifier")
                entity.Property(Of Byte())("RowVersion").IsRequired().IsRowVersion().HasColumnType("rowversion")
                entity.Property(Of Integer)("SeatNumber").HasColumnType("int")
                entity.Property(Of TripSeatStatus)("Status").IsRequired().HasConversion(Of String)().HasColumnType("nvarchar(max)")
                entity.Property(Of Guid)("TripId").HasColumnType("uniqueidentifier")
                entity.HasKey("Id")
                entity.HasIndex("TripId", "SeatNumber").IsUnique().HasFilter(Nothing)
                entity.ToTable(
                    "TripSeats",
                    Sub(table)
                        table.HasCheckConstraint("CK_TripSeats_SeatNumber", "[SeatNumber] BETWEEN 1 AND 40")
                    End Sub)
            End Sub)

        modelBuilder.Entity("Trips.Domain.TripSeat",
            Sub(entity)
                entity.HasOne("Trips.Domain.Trip", Nothing).
                    WithMany("Seats").
                    HasForeignKey("TripId").
                    OnDelete(DeleteBehavior.Cascade).
                    IsRequired()
            End Sub)

        modelBuilder.Entity("Trips.Domain.Trip",
            Sub(entity)
                entity.Navigation("Seats")
            End Sub)
    End Sub
End Class
