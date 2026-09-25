using OpenSource1.Application.Storage;

namespace OpenSource1.SmokeTests.Application.Storage;

public sealed class RutaImagenTests
{
    [Theory]
    [InlineData(null, "clientes")]
    [InlineData("", "clientes")]
    [InlineData("/uploads/clientes/cliente-0123456789abcdef0123456789abcdef.png", "clientes")]
    [InlineData("/uploads/productos/producto-0123456789abcdef0123456789abcdef.webp", "productos")]
    [InlineData("/uploads/users/perfil-0123456789abcdef0123456789abcdef.jpeg", "users")]
    [InlineData("/uploads/clientes/a.b-c_d.JPG", "clientes")]
    [InlineData("/uploads/clientes/x", "clientes")]
    public void Validar_RutasLegitimas_SonValidas(string? ruta, string carpeta)
    {
        Assert.True(RutaImagen.EsValida(ruta, carpeta));
        Assert.Null(RutaImagen.Validar(ruta, carpeta));
    }

    [Theory]
    // Recorridos de directorio
    [InlineData("../../etc/passwd", "clientes")]
    [InlineData("..\\..\\windows\\win.ini", "clientes")]
    [InlineData("/uploads/clientes/../../x.png", "clientes")]
    [InlineData("/uploads/clientes/..", "clientes")]
    [InlineData("/uploads/clientes/.", "clientes")]
    [InlineData("/uploads/clientes/..\\x.png", "clientes")]
    [InlineData("/uploads/clientes/a..b.png", "clientes")]
    [InlineData("/uploads/clientes/.hidden.png", "clientes")]
    [InlineData("/uploads/clientes//etc/passwd", "clientes")]
    [InlineData("/uploads/clientes/sub/x.png", "clientes")]
    [InlineData("/uploads/clientes/sub\\x.png", "clientes")]
    // Rutas absolutas o fuera de /uploads
    [InlineData("/etc/passwd", "clientes")]
    [InlineData("C:\\Windows\\win.ini", "clientes")]
    [InlineData("/tmp/x.png", "clientes")]
    [InlineData("uploads/clientes/x.png", "clientes")]
    [InlineData("/uploads/x.png", "clientes")]
    [InlineData("/uploads/clientes", "clientes")]
    [InlineData("/uploads/clientes/", "clientes")]
    [InlineData("x.png", "clientes")]
    [InlineData("appsettings.json", "clientes")]
    // Otra carpeta o mayúsculas distintas
    [InlineData("/uploads/users/perfil-1.png", "clientes")]
    [InlineData("/uploads/productos/producto-1.png", "clientes")]
    [InlineData("/uploads/clientes/cliente-1.png", "productos")]
    [InlineData("/uploads/clientes/cliente-1.png", "users")]
    [InlineData("/Uploads/clientes/x.png", "clientes")]
    [InlineData("/uploads/Clientes/x.png", "clientes")]
    [InlineData("/uploads/clientes2/x.png", "clientes")]
    // Codificaciones, control y caracteres raros
    [InlineData("/uploads/clientes/%2e%2e/x.png", "clientes")]
    [InlineData("/uploads/clientes/%2e%2e%2fx.png", "clientes")]
    [InlineData("/uploads/clientes/a%2Fb.png", "clientes")]
    [InlineData("/uploads/clientes/x.png\0", "clientes")]
    [InlineData("/uploads/clientes/x\n.png", "clientes")]
    [InlineData("/uploads/clientes/x\t.png", "clientes")]
    [InlineData("/uploads/clientes/x .png", "clientes")]
    [InlineData("/uploads/clientes/x.png ", "clientes")]
    [InlineData("/uploads/clientes/x:stream.png", "clientes")]
    [InlineData("/uploads/clientes/x?.png", "clientes")]
    [InlineData("/uploads/clientes/x*.png", "clientes")]
    [InlineData("/uploads/clientes/ñ.png", "clientes")]
    [InlineData("   ", "clientes")]
    [InlineData("http://evil.test/x.png", "clientes")]
    public void Validar_RutasHostiles_SeRechazanConElCampoImagePath(string ruta, string carpeta)
    {
        Assert.False(RutaImagen.EsValida(ruta, carpeta));

        var error = RutaImagen.Validar(ruta, carpeta);
        Assert.NotNull(error);
        Assert.Equal("ImagePath", error!.Value.Campo);
        Assert.Equal(RutaImagen.CodigoError, error.Value.Codigo);
        Assert.False(string.IsNullOrWhiteSpace(error.Value.Mensaje));
    }

    [Fact]
    public void Validar_NombreDemasiadoLargo_SeRechaza()
    {
        Assert.True(RutaImagen.EsValida($"/uploads/clientes/{new string('a', 255)}", "clientes"));
        Assert.False(RutaImagen.EsValida($"/uploads/clientes/{new string('a', 256)}", "clientes"));
    }

    [Fact]
    public void NombreDe_DevuelveElFicheroDeUnaRutaValida()
    {
        Assert.Equal("cliente-1.png", RutaImagen.NombreDe("/uploads/clientes/cliente-1.png", "clientes"));
    }
}
