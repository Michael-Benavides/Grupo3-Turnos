Imports Microsoft.EntityFrameworkCore.Infrastructure
Imports Microsoft.EntityFrameworkCore.Migrations

<DbContext(GetType(TripsDbContext))>
<Migration("20261115235900_SeedFareRules")>
Partial Public Class SeedFareRules
    Inherits Migration

    Protected Overrides Sub Up(migrationBuilder As MigrationBuilder)
        migrationBuilder.Sql(
            "INSERT INTO [FareRules] ([Id], [Type], [Multiplier], [Condition]) VALUES " &
            "('a0100000-0000-0000-0000-000000000001', N'Occupancy', 1.15, N'occupancy>70%'), " &
            "('a0100000-0000-0000-0000-000000000002', N'Occupancy', 1.30, N'occupancy>90%'), " &
            "('a0100000-0000-0000-0000-000000000003', N'Shift', 1.10, N'shift=Night'), " &
            "('a0100000-0000-0000-0000-000000000004', N'Weekend', 1.10, N'weekend=Saturday,Sunday'), " &
            "('a0100000-0000-0000-0000-000000000005', N'Holiday', 1.25, N'dates=');")
    End Sub

    Protected Overrides Sub Down(migrationBuilder As MigrationBuilder)
        migrationBuilder.Sql(
            "DELETE FROM [FareRules] WHERE [Id] IN " &
            "('a0100000-0000-0000-0000-000000000001', " &
            "'a0100000-0000-0000-0000-000000000002', " &
            "'a0100000-0000-0000-0000-000000000003', " &
            "'a0100000-0000-0000-0000-000000000004', " &
            "'a0100000-0000-0000-0000-000000000005');")
    End Sub
End Class
