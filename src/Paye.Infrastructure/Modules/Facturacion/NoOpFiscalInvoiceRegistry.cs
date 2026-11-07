using Paye.Core.Modules.Facturacion;

namespace Paye.Infrastructure.Modules.Facturacion;

/// <summary>
/// Implementación por defecto (punto de extensión neutro) de <see cref="IFiscalInvoiceRegistry"/>.
/// No decide ni implementa VERI*FACTU ni NO VERI*FACTU hasta que se configure el adaptador fiscal definitivo.
/// </summary>
public sealed class NoOpFiscalInvoiceRegistry : IFiscalInvoiceRegistry
{
    public Task<FiscalRegistrationResult> RegisterIssuedInvoiceAsync(
        IssuedInvoiceFiscalContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;
        return Task.FromResult(FiscalRegistrationResult.Ok());
    }
}
