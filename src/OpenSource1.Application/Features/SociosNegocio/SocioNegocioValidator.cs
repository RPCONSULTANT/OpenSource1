using System.ComponentModel.DataAnnotations;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Application.Storage;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.Application.Features.SociosNegocio;

/// <summary>
/// Validación compartida entre Create/Update (mismo patrón que <c>TerminoPagoValidator</c>). Solo
/// reglas que no requieren base de datos: la existencia del término de pago y la unicidad del
/// documento fiscal se comprueban en los handlers. Cada error lleva el nombre del campo del DTO.
/// Los value objects (<c>DireccionFiscal</c>, <c>Pais</c>, <c>Sector</c>) tienen sus propias
/// reglas con <c>Of(...)</c>; aquí se reproducen las condiciones que harían lanzar a esos
/// factories para devolver un <c>Result</c> fallido en vez de una excepción.
/// </summary>
internal static class SocioNegocioValidator
{
    // numeric(18,4): 14 dígitos enteros.
    private const decimal LimiteCreditoMaximo = 99_999_999_999_999.9999m;

    public static IReadOnlyList<Error> Validar(IDatosSocioNegocio datos)
    {
        var errores = new List<Error>();

        if (string.IsNullOrWhiteSpace(datos.NombreComercial))
        {
            errores.Add(new Error("socio_negocio.nombre_comercial_requerido", "El nombre comercial es obligatorio.", nameof(datos.NombreComercial)));
        }
        else if (datos.NombreComercial.Trim().Length > 200)
        {
            errores.Add(new Error("socio_negocio.nombre_comercial_invalido", "El nombre comercial no puede superar los 200 caracteres.", nameof(datos.NombreComercial)));
        }

        if (Longitud(datos.RazonSocial) > 200)
        {
            errores.Add(new Error("socio_negocio.razon_social_invalida", "La razón social no puede superar los 200 caracteres.", nameof(datos.RazonSocial)));
        }

        if (!Enum.IsDefined(datos.Tipo))
        {
            errores.Add(new Error("socio_negocio.tipo_invalido", "El tipo de socio no es válido (1=Cliente, 2=Proveedor, 3=Ambos).", nameof(datos.Tipo)));
        }

        if (!Enum.IsDefined(datos.TipoDocumentoFiscal))
        {
            errores.Add(new Error("socio_negocio.tipo_documento_invalido", "El tipo de documento fiscal no es válido (1=Rnc, 2=Cedula, 3=Pasaporte, 9=SinDocumento).", nameof(datos.TipoDocumentoFiscal)));
        }
        else
        {
            var tieneNumero = !string.IsNullOrWhiteSpace(datos.NumeroDocumentoFiscal);

            if (datos.TipoDocumentoFiscal == TipoDocumentoFiscal.SinDocumento)
            {
                if (tieneNumero)
                {
                    errores.Add(new Error("socio_negocio.documento_no_aplica", "No se debe indicar número de documento fiscal cuando el tipo es SinDocumento.", nameof(datos.NumeroDocumentoFiscal)));
                }
            }
            else if (!tieneNumero)
            {
                errores.Add(new Error("socio_negocio.documento_requerido", "El número de documento fiscal es obligatorio para el tipo de documento indicado.", nameof(datos.NumeroDocumentoFiscal)));
            }
            else if (datos.NumeroDocumentoFiscal!.Trim().Length > 20)
            {
                errores.Add(new Error("socio_negocio.documento_invalido", "El número de documento fiscal no puede superar los 20 caracteres.", nameof(datos.NumeroDocumentoFiscal)));
            }
        }

        if (datos.LimiteCredito < 0 || datos.LimiteCredito > LimiteCreditoMaximo)
        {
            errores.Add(new Error("socio_negocio.limite_credito_invalido", "El límite de crédito debe ser un importe mayor o igual a cero.", nameof(datos.LimiteCredito)));
        }
        else if (decimal.Round(datos.LimiteCredito, 4) != datos.LimiteCredito)
        {
            // numeric(18,4): con más decimales Postgres redondearía en silencio.
            errores.Add(new Error("socio_negocio.limite_credito_invalido", "El límite de crédito admite como máximo 4 decimales.", nameof(datos.LimiteCredito)));
        }

        if (!Enum.IsDefined(datos.Bloqueado))
        {
            errores.Add(new Error("socio_negocio.bloqueo_invalido", "El nivel de bloqueo no es válido (0=Ninguno, 1=Facturacion, 2=Todo).", nameof(datos.Bloqueado)));
        }

        if (!string.IsNullOrWhiteSpace(datos.Email))
        {
            if (datos.Email.Trim().Length > 256)
            {
                errores.Add(new Error("socio_negocio.email_invalido", "El email no puede superar los 256 caracteres.", nameof(datos.Email)));
            }
            else if (!new EmailAddressAttribute().IsValid(datos.Email.Trim()))
            {
                errores.Add(new Error("socio_negocio.email_invalido", "El email no tiene un formato válido.", nameof(datos.Email)));
            }
        }

        if (Longitud(datos.Telefono) > 50)
        {
            errores.Add(new Error("socio_negocio.telefono_invalido", "El teléfono no puede superar los 50 caracteres.", nameof(datos.Telefono)));
        }

        if (string.IsNullOrWhiteSpace(datos.DireccionLinea1) && !string.IsNullOrWhiteSpace(datos.DireccionLinea2))
        {
            errores.Add(new Error("direccion.linea1_requerida", "La línea 1 de la dirección es obligatoria si se indica la línea 2.", nameof(datos.DireccionLinea1)));
        }

        if (Longitud(datos.DireccionLinea1) > 300)
        {
            errores.Add(new Error("socio_negocio.direccion_invalida", "La línea 1 de la dirección no puede superar los 300 caracteres.", nameof(datos.DireccionLinea1)));
        }

        if (Longitud(datos.DireccionLinea2) > 300)
        {
            errores.Add(new Error("socio_negocio.direccion_invalida", "La línea 2 de la dirección no puede superar los 300 caracteres.", nameof(datos.DireccionLinea2)));
        }

        if (Longitud(datos.Ciudad) > 100)
        {
            errores.Add(new Error("socio_negocio.ciudad_invalida", "La ciudad no puede superar los 100 caracteres.", nameof(datos.Ciudad)));
        }

        if (Longitud(datos.Sector) > 100)
        {
            errores.Add(new Error("socio_negocio.sector_invalido", "El sector no puede superar los 100 caracteres.", nameof(datos.Sector)));
        }

        if (!string.IsNullOrWhiteSpace(datos.PaisCodigo) && !Pais.EsCodigoValido(datos.PaisCodigo))
        {
            errores.Add(new Error("pais.codigo_invalido", "El código de país no es válido.", nameof(datos.PaisCodigo)));
        }

        // La ruta acaba en un borrado de fichero al sustituir la imagen: solo se acepta /uploads/clientes/<nombre>.
        if (RutaImagen.Validar(datos.ImagePath, RutaImagen.CarpetaClientes) is { } errorImagen)
        {
            errores.Add(errorImagen);
        }

        return errores;
    }

    private static int Longitud(string? valor) => valor?.Trim().Length ?? 0;
}
