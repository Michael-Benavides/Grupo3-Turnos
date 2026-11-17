Imports System.Linq
Imports Trips.Application
Imports Trips.Domain
Imports Trips.Infrastructure
Imports Xunit

Public Class FareCalculatorTests
    Private ReadOnly _calculator As IFareCalculator = New FareCalculator()

    <Fact>
    Public Sub Calculate_AppliesOnlyHighestOccupancyTier()
        Dim atSeventyPercent = Calculate(100D, Shift.Morning, New DateOnly(2026, 11, 16), 28)
        Dim aboveSeventyPercent = Calculate(100D, Shift.Morning, New DateOnly(2026, 11, 16), 29)
        Dim atNinetyPercent = Calculate(100D, Shift.Morning, New DateOnly(2026, 11, 16), 36)
        Dim aboveNinetyPercent = Calculate(100D, Shift.Morning, New DateOnly(2026, 11, 16), 37)

        Assert.Equal(100D, atSeventyPercent.FinalPrice)
        Assert.Equal(115D, aboveSeventyPercent.FinalPrice)
        Assert.Equal(115D, atNinetyPercent.FinalPrice)
        Assert.Equal(130D, aboveNinetyPercent.FinalPrice)
        Assert.Single(aboveNinetyPercent.AppliedMultipliers)
        Assert.Equal("occupancy>90%", aboveNinetyPercent.AppliedMultipliers(0).Condition)
    End Sub

    <Fact>
    Public Sub Calculate_AppliesNightShiftRule()
        Dim quote = Calculate(100D, Shift.Night, New DateOnly(2026, 11, 16), 0)

        Assert.Equal(110D, quote.FinalPrice)
        Assert.Contains(quote.AppliedMultipliers, Function(rule) rule.Type = FareRuleType.Shift)
    End Sub

    <Fact>
    Public Sub Calculate_AppliesWeekendRule()
        Dim quote = Calculate(100D, Shift.Morning, New DateOnly(2026, 11, 15), 0)

        Assert.Equal(110D, quote.FinalPrice)
        Assert.Contains(quote.AppliedMultipliers, Function(rule) rule.Type = FareRuleType.Weekend)
    End Sub

    <Fact>
    Public Sub Calculate_AppliesHolidayOnlyForConfiguredDates()
        Dim holidayRules = DefaultRules().
            Select(Function(rule)
                       If rule.Type = FareRuleType.Holiday Then
                           Return New FareRule With {
                               .Id = rule.Id,
                               .Type = rule.Type,
                               .Multiplier = rule.Multiplier,
                               .Condition = "dates=2026-11-16;2026-12-25"
                           }
                       End If
                       Return rule
                   End Function).
            ToList()

        Dim holidayQuote = Calculate(100D, Shift.Morning, New DateOnly(2026, 11, 16), 0, holidayRules)
        Dim regularQuote = Calculate(100D, Shift.Morning, New DateOnly(2026, 11, 17), 0, holidayRules)

        Assert.Equal(125D, holidayQuote.FinalPrice)
        Assert.Equal(100D, regularQuote.FinalPrice)
        Assert.Contains(holidayQuote.AppliedMultipliers, Function(rule) rule.Type = FareRuleType.Holiday)
    End Sub

    <Fact>
    Public Sub Calculate_CompoundsNightWeekendAndEightyPercentOccupancy()
        Dim quote = Calculate(100D, Shift.Night, New DateOnly(2026, 11, 15), 32)

        Assert.Equal(139.15D, quote.FinalPrice)
        Assert.Equal(3, quote.AppliedMultipliers.Count)
    End Sub

    <Fact>
    Public Sub Calculate_RoundsToTwoDecimalsAndDoesNotLowerBasePrice()
        Dim rounded = Calculate(10.01D, Shift.Night, New DateOnly(2026, 11, 16), 0)
        Dim discountRule = New FareRule With {
            .Id = Guid.NewGuid(),
            .Type = FareRuleType.Shift,
            .Multiplier = 0.5D,
            .Condition = "shift=Night"
        }
        Dim floorQuote = Calculate(10D, Shift.Night, New DateOnly(2026, 11, 16), 0, New List(Of FareRule) From {discountRule})

        Assert.Equal(11.01D, rounded.FinalPrice)
        Assert.Equal(10D, floorQuote.FinalPrice)
    End Sub

    Private Function Calculate(
        basePrice As Decimal,
        shift As Shift,
        departureDate As DateOnly,
        occupiedSeatCount As Integer,
        Optional rules As IReadOnlyList(Of FareRule) = Nothing) As FareQuoteDto

        Return _calculator.Calculate(New FareCalculationRequest With {
            .BasePrice = basePrice,
            .Shift = shift,
            .DepartureDate = departureDate,
            .OccupiedSeatCount = occupiedSeatCount,
            .TotalSeatCount = 40,
            .Rules = If(rules, DefaultRules())
        })
    End Function

    Private Shared Function DefaultRules() As List(Of FareRule)
        Return New List(Of FareRule) From {
            New FareRule With {.Id = Guid.NewGuid(), .Type = FareRuleType.Occupancy, .Multiplier = 1.15D, .Condition = "occupancy>70%"},
            New FareRule With {.Id = Guid.NewGuid(), .Type = FareRuleType.Occupancy, .Multiplier = 1.3D, .Condition = "occupancy>90%"},
            New FareRule With {.Id = Guid.NewGuid(), .Type = FareRuleType.Shift, .Multiplier = 1.1D, .Condition = "shift=Night"},
            New FareRule With {.Id = Guid.NewGuid(), .Type = FareRuleType.Weekend, .Multiplier = 1.1D, .Condition = "weekend=Saturday,Sunday"},
            New FareRule With {.Id = Guid.NewGuid(), .Type = FareRuleType.Holiday, .Multiplier = 1.25D, .Condition = "dates="}
        }
    End Function
End Class
