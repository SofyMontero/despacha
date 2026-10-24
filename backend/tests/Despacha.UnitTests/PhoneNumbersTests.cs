using Despacha.Api.Shared.Validation;

namespace Despacha.UnitTests;

public class PhoneNumbersTests
{
    [Theory]
    [InlineData("3001234567", "+573001234567")]
    [InlineData("573001234567", "+573001234567")]
    [InlineData("+57 300 123 4567", "+573001234567")]
    [InlineData("(300) 123-4567", "+573001234567")]
    [InlineData("6011234567", "+576011234567")]
    public void Normaliza_como_el_PHP_a_E164(string input, string expected)
    {
        Assert.True(PhoneNumbers.TryNormalizeColombian(input, out var e164));
        Assert.Equal(expected, e164);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("300123456")]
    [InlineData("30012345678")]
    [InlineData("5730012345678")]
    [InlineData("13001234567")]
    public void Rechaza_lo_que_no_es_57_mas_10_digitos(string? input)
    {
        Assert.False(PhoneNumbers.TryNormalizeColombian(input, out var e164));
        Assert.Empty(e164);
    }

    [Fact]
    public void Extrae_solo_digitos_para_buscar()
    {
        Assert.Equal("573001", PhoneNumbers.DigitsOf("+57 300-1"));
        Assert.Empty(PhoneNumbers.DigitsOf(null));
    }

    [Fact]
    public void Un_telefono_historico_con_los_mismos_digitos_no_se_trata_como_cambio()
    {
        Assert.True(PhoneNumbers.SameDigits("3003262244", "+57 300 326 2244"));
        Assert.False(PhoneNumbers.SameDigits("300326224", "3003262244"));
        Assert.Equal(["+573003262244", "573003262244", "3003262244"], PhoneNumbers.EquivalentStored("+573003262244"));
    }
}
