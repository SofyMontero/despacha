using Despacha.Api.Shared.Logging;

namespace Despacha.UnitTests;

public class LogMaskingTests
{
    [Theory]
    [InlineData("+57 300 123 4567", "********4567")]
    [InlineData("1234", "****")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Enmascara_telefonos(string? phone, string expected)
    {
        Assert.Equal(expected, LogMasking.MaskPhone(phone));
    }
}
