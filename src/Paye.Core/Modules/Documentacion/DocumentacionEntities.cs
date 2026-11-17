using System.Globalization;
using Paye.Core.Entities;

namespace Paye.Core.Modules.Documentacion;

/// <summary>
/// Tipo de documento comercial de la sección Documentación.
/// </summary>
public enum TipoDocumentoComercial
{
    Contrato = 1,
    Presupuesto = 2
}

/// <summary>
/// Configuración editable de la empresa emisora para cabecera, pie y textos legales
/// de Contratos y Presupuestos. Nunca se hardcodea en las vistas ni plantillas.
/// </summary>
public sealed class DocumentacionCompanySettings : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string NombreEmpresa { get; set; } = string.Empty;
    public string CifNif { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Web { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string TextoLegalTitulo { get; set; } = "Propiedad Intelectual y Confidencialidad";
    public string TextoLegalContenido { get; set; } = string.Empty;
}

/// <summary>
/// Serie correlativa anual por tipo de documento (CONT-YYYY-NNN y PRES-YYYY-NNN).
/// Se incrementa atómicamente en base de datos con bloqueo de fila y se reinicia cada año.
/// </summary>
public sealed class DocumentoSerie : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public TipoDocumentoComercial Tipo { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public int LastNumber { get; set; }
    public int PaddingDigits { get; set; } = 3;

    public static string GetDefaultCode(TipoDocumentoComercial tipo) =>
        tipo == TipoDocumentoComercial.Contrato ? "CONT" : "PRES";

    public static string FormatNumber(TipoDocumentoComercial tipo, int fiscalYear, int sequentialNumber, int paddingDigits = 3)
    {
        var prefix = GetDefaultCode(tipo);
        var padded = sequentialNumber.ToString(CultureInfo.InvariantCulture).PadLeft(Math.Max(3, paddingDigits), '0');
        return $"{prefix}-{fiscalYear.ToString(CultureInfo.InvariantCulture)}-{padded}";
    }
}

/// <summary>
/// Documento comercial (Contrato o Presupuesto) asociado a un Cliente,
/// con bloques/cláusulas editables en formato JSON y líneas de concepto con desglose fiscal.
/// </summary>
public sealed class DocumentoComercial : AuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public TipoDocumentoComercial Tipo { get; set; }

    public int FiscalYear { get; set; }
    public int SequentialNumber { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;
    public string Subtitulo { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;

    /// <summary>
    /// Relación opcional con la entidad Cliente existente en Paye.Core.Modules.Clientes.
    /// Puede ser null si el presupuesto o contrato se crea sin asociar a un cliente del CRM.
    /// </summary>
    public Guid? ClienteId { get; set; }

    /// <summary>
    /// Snapshot de los datos del cliente autorrellenados al seleccionarlo (y editables por documento).
    /// </summary>
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteCifNif { get; set; } = string.Empty;
    public string ClienteDireccion { get; set; } = string.Empty;
    public string ClienteEmail { get; set; } = string.Empty;
    public string ClienteTelefono { get; set; } = string.Empty;

    public DateOnly FechaEmision { get; set; }
    public DateOnly? FechaValidez { get; set; }

    // Campos específicos de Contrato (variables rápidas del panel izquierdo)
    public string? ObjetoContrato { get; set; }
    public string? DuracionContrato { get; set; }
    public string? PrecioTextoContrato { get; set; }
    public string? FormaPagoContrato { get; set; }
    public string? LugarFirma { get; set; }

    /// <summary>
    /// Lista ordenada de bloques y cláusulas serializada en JSON (reordenables, ocultables, restablecibles).
    /// </summary>
    public string BloquesJson { get; set; } = "[]";

    // Totales calculados y validados siempre en el servidor
    public decimal SubtotalBruto { get; set; }
    public decimal TotalDescuento { get; set; }
    public decimal BaseImponible { get; set; }
    public decimal TotalIva { get; set; }
    public decimal TotalGeneral { get; set; }

    public List<DocumentoLinea> Lineas { get; set; } = [];

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}

/// <summary>
/// Línea de concepto de un Presupuesto (o anexo económico de Contrato).
/// Admite múltiples tipos de IVA por línea (21%, 10%, 4%, 0%) y descuento opcional (0..100%).
/// </summary>
public sealed class DocumentoLinea
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentoId { get; set; }
    public int Orden { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public decimal Cantidad { get; set; } = 1m;
    public decimal PrecioUnitario { get; set; }
    public decimal DescuentoPorcentaje { get; set; }
    public decimal IvaPorcentaje { get; set; } = 21m;

    public decimal ImporteBruto { get; set; }
    public decimal ImporteDescuento { get; set; }
    public decimal BaseImponible { get; set; }
    public decimal CuotaIva { get; set; }
    public decimal TotalLinea { get; set; }
}
