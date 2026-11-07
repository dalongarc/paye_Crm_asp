using Microsoft.EntityFrameworkCore;
using Paye.Core.Demo;
using Paye.Core.Modules.Notas;
using Paye.Infrastructure.Data;

namespace Paye.Infrastructure.Modules.Notas;

public sealed class NotasDemoDataProvider(
    PayeDbContext dbContext) : IDemoModuleDataProvider
{
    public IReadOnlyList<string> GetDemoPermissions() =>
    [
        NotasPermissions.Ver,
        NotasPermissions.Crear,
        NotasPermissions.Editar,
        NotasPermissions.Borrar
    ];

    public async Task SeedDemoDataAsync(
        string demoUserId,
        CancellationToken cancellationToken = default)
    {
        if (!DemoSandboxConstants.IsDemoUserId(demoUserId))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        Nota[] sampleNotas =
        [
            new()
            {
                Id = Guid.NewGuid(),
                Titulo = "Bienvenido al Sandbox Demo — Prueba a editar o crear notas",
                Contenido = "Esta sesión es 100% aislada. Puedes crear nuevas notas, editarlas o archivarlas. Al pulsar 'Salir y limpiar Demo' arriba a la derecha, todos los datos de esta sesión se eliminan automáticamente.",
                Archivada = false,
                CreatedAtUtc = now,
                CreatedBy = demoUserId
            },
            new()
            {
                Id = Guid.NewGuid(),
                Titulo = "Pedido mayorista pendiente — Cafetería Central",
                Contenido = "Revisar entrega del viernes: 12 kg café grano tueste natural (Base: 180,00 € + IVA Reducido 10% = 198,00 €) y 2 cajas de tazas térmicas (IVA General 21%).",
                Archivada = false,
                CreatedAtUtc = now.AddMinutes(-15),
                CreatedBy = demoUserId
            },
            new()
            {
                Id = Guid.NewGuid(),
                Titulo = "Checklist de cierre de caja diario",
                Contenido = "1. Comprobar tickets de datáfono frente a efectivo en cajón.\n2. Anotar retiradas a caja fuerte.\n3. Confirmar stock mínimo de consumibles para el turno de mañana.",
                Archivada = false,
                CreatedAtUtc = now.AddMinutes(-45),
                CreatedBy = demoUserId
            },
            new()
            {
                Id = Guid.NewGuid(),
                Titulo = "Presupuesto inicial de implantación (Archivada de ejemplo)",
                Contenido = "Nota archivada de demostración para probar el filtro 'Archivadas' en el buscador superior.",
                Archivada = true,
                CreatedAtUtc = now.AddHours(-2),
                CreatedBy = demoUserId
            }
        ];

        dbContext.Set<Nota>().AddRange(sampleNotas);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CleanupDemoDataAsync(
        string demoUserId,
        CancellationToken cancellationToken = default)
    {
        if (!DemoSandboxConstants.IsDemoUserId(demoUserId))
        {
            return;
        }

        await dbContext.Set<Nota>()
            .IgnoreQueryFilters()
            .Where(n => n.CreatedBy == demoUserId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
