Public Class FareRule
    Public Property Id As Guid = Guid.NewGuid()
    Public Property [Type] As FareRuleType
    Public Property Multiplier As Decimal
    Public Property Condition As String
End Class
