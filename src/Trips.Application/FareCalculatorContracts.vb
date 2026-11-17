Imports Trips.Domain

Public Class FareMultiplierDto
    Public Property Type As FareRuleType
    Public Property Multiplier As Decimal
    Public Property Condition As String
End Class

Public Class FareQuoteDto
    Public Property BasePrice As Decimal
    Public Property AppliedMultipliers As IReadOnlyList(Of FareMultiplierDto) = Array.Empty(Of FareMultiplierDto)()
    Public Property FinalPrice As Decimal
End Class

Public Class FareCalculationRequest
    Public Property BasePrice As Decimal
    Public Property Shift As Shift
    Public Property DepartureDate As DateOnly
    Public Property OccupiedSeatCount As Integer
    Public Property TotalSeatCount As Integer
    Public Property Rules As IReadOnlyList(Of FareRule) = Array.Empty(Of FareRule)()
End Class

Public Interface IFareCalculator
    Function Calculate(request As FareCalculationRequest) As FareQuoteDto
End Interface
