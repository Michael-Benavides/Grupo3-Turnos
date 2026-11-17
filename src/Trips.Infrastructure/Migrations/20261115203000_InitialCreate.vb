Imports Microsoft.EntityFrameworkCore.Infrastructure
Imports Microsoft.EntityFrameworkCore.Migrations

<DbContext(GetType(TripsDbContext))>
<Migration("20261115203000_InitialCreate")>
Partial Public Class InitialCreate
    Inherits Migration

    Protected Overrides Sub Up(migrationBuilder As MigrationBuilder)
        migrationBuilder.CreateTable(
            name:="FareRules",
            columns:=Function(table) New With {
                .Id = table.Column(Of Guid)(type:="uniqueidentifier", nullable:=False),
                .Type = table.Column(Of String)(type:="nvarchar(max)", nullable:=False),
                .Multiplier = table.Column(Of Decimal)(type:="decimal(10,2)", precision:=10, scale:=2, nullable:=False),
                .Condition = table.Column(Of String)(type:="nvarchar(max)", nullable:=False)
            },
            constraints:=Sub(table)
                              table.PrimaryKey("PK_FareRules", Function(row) row.Id)
                          End Sub)

        migrationBuilder.CreateTable(
            name:="ScheduleTemplates",
            columns:=Function(table) New With {
                .Id = table.Column(Of Guid)(type:="uniqueidentifier", nullable:=False),
                .RouteId = table.Column(Of Guid)(type:="uniqueidentifier", nullable:=False),
                .BusId = table.Column(Of Guid)(type:="uniqueidentifier", nullable:=False),
                .Shift = table.Column(Of String)(type:="nvarchar(max)", nullable:=False),
                .DepartureTime = table.Column(Of TimeSpan)(type:="time", nullable:=False),
                .BasePrice = table.Column(Of Decimal)(type:="decimal(10,2)", precision:=10, scale:=2, nullable:=False),
                .IsActive = table.Column(Of Boolean)(type:="bit", nullable:=False)
            },
            constraints:=Sub(table)
                              table.PrimaryKey("PK_ScheduleTemplates", Function(row) row.Id)
                          End Sub)

        migrationBuilder.CreateTable(
            name:="Trips",
            columns:=Function(table) New With {
                .Id = table.Column(Of Guid)(type:="uniqueidentifier", nullable:=False),
                .TemplateId = table.Column(Of Guid)(type:="uniqueidentifier", nullable:=False),
                .RouteId = table.Column(Of Guid)(type:="uniqueidentifier", nullable:=False),
                .BusId = table.Column(Of Guid)(type:="uniqueidentifier", nullable:=False),
                .DepartureAt = table.Column(Of DateTime)(type:="datetime2", nullable:=False),
                .DepartureDate = table.Column(Of DateOnly)(type:="date", nullable:=False),
                .Shift = table.Column(Of String)(type:="nvarchar(max)", nullable:=False),
                .Status = table.Column(Of String)(type:="nvarchar(max)", nullable:=False),
                .CurrentPrice = table.Column(Of Decimal)(type:="decimal(10,2)", precision:=10, scale:=2, nullable:=False)
            },
            constraints:=Sub(table)
                              table.PrimaryKey("PK_Trips", Function(row) row.Id)
                          End Sub)

        migrationBuilder.CreateTable(
            name:="TripSeats",
            columns:=Function(table) New With {
                .Id = table.Column(Of Guid)(type:="uniqueidentifier", nullable:=False),
                .TripId = table.Column(Of Guid)(type:="uniqueidentifier", nullable:=False),
                .SeatNumber = table.Column(Of Integer)(type:="int", nullable:=False),
                .Status = table.Column(Of String)(type:="nvarchar(max)", nullable:=False),
                .HeldBy = table.Column(Of String)(type:="nvarchar(max)", nullable:=True),
                .HeldUntil = table.Column(Of DateTime?)(type:="datetime2", nullable:=True),
                .RowVersion = table.Column(Of Byte())(type:="rowversion", rowVersion:=True, nullable:=False)
            },
            constraints:=Sub(table)
                              table.PrimaryKey("PK_TripSeats", Function(row) row.Id)
                              table.ForeignKey(
                                  name:="FK_TripSeats_Trips_TripId",
                                  column:=Function(row) row.TripId,
                                  principalTable:="Trips",
                                  principalColumn:="Id",
                                  onDelete:=ReferentialAction.Cascade)
                          End Sub)

        migrationBuilder.CreateIndex(
            name:="IX_TripSeats_TripId_SeatNumber",
            table:="TripSeats",
            columns:=New String() {"TripId", "SeatNumber"},
            unique:=True,
            filter:=Nothing)

        migrationBuilder.CreateIndex(
            name:="IX_Trips_TemplateId_DepartureDate",
            table:="Trips",
            columns:=New String() {"TemplateId", "DepartureDate"},
            unique:=True,
            filter:=Nothing)

        migrationBuilder.Sql(
            "ALTER TABLE [TripSeats] ADD CONSTRAINT [CK_TripSeats_SeatNumber] CHECK ([SeatNumber] BETWEEN 1 AND 40);")
    End Sub

    Protected Overrides Sub Down(migrationBuilder As MigrationBuilder)
        migrationBuilder.DropTable(name:="FareRules")
        migrationBuilder.DropTable(name:="ScheduleTemplates")
        migrationBuilder.DropTable(name:="TripSeats")
        migrationBuilder.DropTable(name:="Trips")
    End Sub
End Class
