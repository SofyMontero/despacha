using Despacha.Api.Modules.Platform.Internal;

namespace Despacha.UnitTests;

public class LegacyPasswordsTests
{
    private const string Password = "clave-historica-1";

    /// <summary>PHP genera $2y$; el algoritmo es el mismo que $2b$.</summary>
    private static string PhpHash(string password) =>
        "$2y$" + BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4)[4..];

    [Fact]
    public void Acepta_el_hash_bcrypt_de_PHP_con_la_clave_correcta()
    {
        var hash = PhpHash(Password);

        Assert.True(LegacyPasswords.IsBcrypt(hash));
        Assert.True(LegacyPasswords.Verify(Password, hash));
        Assert.False(LegacyPasswords.Verify("otra-clave", hash));
    }

    [Theory]
    [InlineData("clave-historica-1")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("$2y$10$corto")]
    public void Nunca_compara_texto_plano_ni_valores_que_no_son_bcrypt(string? stored)
    {
        Assert.False(LegacyPasswords.IsBcrypt(stored));
        Assert.False(LegacyPasswords.Verify(Password, stored));
    }

    [Fact]
    public void Una_clave_vacia_no_valida_aunque_el_hash_sea_correcto()
    {
        Assert.False(LegacyPasswords.Verify(string.Empty, PhpHash(string.Empty + "x")));
    }
}
