using Despacha.Api.Integrations.Storage;
using Microsoft.AspNetCore.Http;

namespace Despacha.UnitTests;

public sealed class ImageStorageTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private readonly string _root = Path.Combine(Path.GetTempPath(), "despacha-media-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "jpg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "png")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, "gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, "webp")]
    public void Detecta_el_formato_por_contenido(byte[] content, string extension)
    {
        Assert.Equal(extension, ImageValidation.Detect(content)?.Extension);
    }

    [Fact]
    public async Task Rechaza_un_archivo_que_no_es_imagen_aunque_diga_png()
    {
        var file = FormFile("<?php echo 1; ?>"u8.ToArray(), "foto.png");

        var (image, error) = await ImageValidation.ValidateAsync(file, CancellationToken.None);

        Assert.Null(image);
        Assert.Contains("Formato no permitido", error);
    }

    [Fact]
    public async Task Rechaza_imagenes_de_mas_de_3_MB()
    {
        var content = new byte[ImageValidation.MaxBytes + 1];
        Png.CopyTo(content, 0);

        var (image, error) = await ImageValidation.ValidateAsync(FormFile(content, "grande.png"), CancellationToken.None);

        Assert.Null(image);
        Assert.Contains("3 MB", error);
    }

    [Theory]
    [InlineData("../t1/catalog/products/0123456789abcdef0123456789abcdef.png")]
    [InlineData("t1/../../0123456789abcdef0123456789abcdef.png")]
    [InlineData("t1/catalog/products/0123456789abcdef0123456789abcdef.php")]
    [InlineData("C:/Windows/0123456789abcdef0123456789abcdef.png")]
    [InlineData("legacy/licores/foto.png")]
    public void La_ruta_local_no_sale_de_la_carpeta_raiz(string key)
    {
        Assert.Null(Storage().ResolvePath(key));
    }

    [Fact]
    public async Task Guarda_y_borra_dentro_de_la_carpeta_raiz()
    {
        var storage = Storage();

        var key = await storage.SaveAsync("t1/catalog/products", new ValidatedImage(Png, "png", "image/png"), CancellationToken.None);
        var path = storage.ResolvePath(key);

        Assert.True(ImageKeys.IsValidOwnKey(key));
        Assert.True(File.Exists(path));
        Assert.Equal("/media/" + key, storage.GetUrl(key));

        await storage.DeleteAsync(key, CancellationToken.None);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task No_acepta_carpetas_fuera_del_patron()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Storage().SaveAsync("../fuera", new ValidatedImage(Png, "png", "image/png"), CancellationToken.None));
    }

    [Fact]
    public void Las_claves_heredadas_se_resuelven_con_la_url_de_solo_lectura()
    {
        var storage = Storage("https://ejemplo.test/sistema/assets/img/");

        Assert.Equal("https://ejemplo.test/sistema/assets/img/licores/mi%20foto.png", storage.GetUrl("legacy/licores/mi foto.png"));
        Assert.Equal("https://ejemplo.test/sistema/assets/img/licores/AguardientesC.png", storage.GetUrl("AguardientesC.png"));
        Assert.Null(Storage().GetUrl("AguardientesC.png"));
        Assert.Null(Storage().GetUrl("legacy/licores/foto.png"));
        Assert.Null(new DisabledImageStorage(new StorageOptions()).GetUrl("t1/catalog/products/0123456789abcdef0123456789abcdef.png"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private LocalImageStorage Storage(string? legacyBaseUrl = null) =>
        new(new StorageOptions { LegacyBaseUrl = legacyBaseUrl }, _root);

    private static FormFile FormFile(byte[] content, string name) =>
        new(new MemoryStream(content), 0, content.Length, "file", name);
}
