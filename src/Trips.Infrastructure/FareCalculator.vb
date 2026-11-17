Imports System.Globalization
Imports System.Linq
Imports Trips.Application
Imports Trips.Domain

Public Class FareCalculator
    Implements IFareCalculator

    Public Function Calculate(request As FareCalculationRequest) As FareQuoteDto Implements IFareCalculator.Calculate
        If request.BasePrice < 0D Then
            Throw New ArgumentOutOfRangeException(NameOf(request.BasePrice), "El precio base no puede ser negativo.")
        End If

        If request.TotalSeatCount < 0 OrElse request.OccupiedSeatCount < 0 OrElse request.OccupiedSeatCount > request.TotalSeatCount Then
            Throw New ArgumentOutOfRangeException(NameOf(request.OccupiedSeatCount), "La ocupación debe estar entre cero y la capacidad total.")
        End If
        If request.Rules.Any(Function(rule) rule.Multiplier <= 0D) Then
            Throw New InvalidOperationException("Cada multiplicador de tarifa debe ser mayor que cero.")
        End If

        Dim applied = New List(Of FareMultiplierDto)()
        Dim occupancy = If(request.TotalSeatCount = 0, 0D, CDec(request.OccupiedSeatCount) / request.TotalSeatCount)
        Dim occupancyRule = request.Rules.
            Where(Function(rule) rule.Type = FareRuleType.Occupancy AndAlso IsOccupancyRuleApplicable(rule, occupancy)).
            OrderByDescending(Function(rule) GetOccupancyThreshold(rule.Condition)).
            FirstOrDefault()

        If occupancyRule IsNot Nothing Then
            applied.Add(ToDto(occupancyRule))
        End If

        For Each rule In request.Rules
            Select Case rule.Type
                Case FareRuleType.Shift
                    If request.Shift = Shift.Night AndAlso String.Equals(rule.Condition, "shift=Night", StringComparison.OrdinalIgnoreCase) Then
                        applied.Add(ToDto(rule))
                    End If
                Case FareRuleType.Weekend
                    If (request.DepartureDate.DayOfWeek = DayOfWeek.Saturday OrElse request.DepartureDate.DayOfWeek = DayOfWeek.Sunday) AndAlso
                       String.Equals(rule.Condition, "weekend=Saturday,Sunday", StringComparison.OrdinalIgnoreCase) Then
                        applied.Add(ToDto(rule))
                    End If
                Case FareRuleType.Holiday
                    If IsHoliday(rule.Condition, request.DepartureDate) Then
                        applied.Add(ToDto(rule))
                    End If
            End Select
        Next

        Dim multiplier = applied.Aggregate(1D, Function(current, item) current * item.Multiplier)
        Dim calculatedPrice = Decimal.Round(request.BasePrice * multiplier, 2, MidpointRounding.AwayFromZero)

        Return New FareQuoteDto With {
            .BasePrice = request.BasePrice,
            .AppliedMultipliers = applied,
            .FinalPrice = Math.Max(request.BasePrice, calculatedPrice)
        }
    End Function

    Private Shared Function IsOccupancyRuleApplicable(rule As FareRule, occupancy As Decimal) As Boolean
        Dim threshold = GetOccupancyThreshold(rule.Condition)
        Return threshold >= 0D AndAlso occupancy > threshold
    End Function

    Private Shared Function GetOccupancyThreshold(condition As String) As Decimal
        Select Case condition
            Case "occupancy>70%"
                Return 0.7D
            Case "occupancy>90%"
                Return 0.9D
            Case Else
                Return -1D
        End Select
    End Function

    Private Shared Function IsHoliday(condition As String, departureDate As DateOnly) As Boolean
        If Not condition.StartsWith("dates=", StringComparison.OrdinalIgnoreCase) Then
            Return False
        End If

        Dim configuredDates = condition.Substring("dates=".Length).
            Split(New Char() {","c, ";"c}, StringSplitOptions.RemoveEmptyEntries)

        For Each holidayDateText In configuredDates
            Dim parsedDate As DateOnly
            If DateOnly.TryParseExact(holidayDateText.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, parsedDate) AndAlso
               parsedDate = departureDate Then
                Return True
            End If
        Next

        Return False
    End Function

    Private Shared Function ToDto(rule As FareRule) As FareMultiplierDto
        Return New FareMultiplierDto With {
            .Type = rule.Type,
            .Multiplier = rule.Multiplier,
            .Condition = rule.Condition
        }
    End Function
End Class
