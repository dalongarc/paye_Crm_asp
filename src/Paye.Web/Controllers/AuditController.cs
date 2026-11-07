using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Paye.Core.Auditing;
using Paye.Core.Authorization;
using Paye.Infrastructure.Data;

namespace Paye.Web.Controllers;

[Authorize(Policy = CorePermissions.AuditoriaVer)]
public sealed class AuditController(PayeDbContext dbContext) : Controller
{
    [HttpGet("/admin/audit")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        const int pageSize = 50;
        var safePage = Math.Max(1, page);

        var entries = await dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.OccurredAtUtc)
            .Skip((safePage - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return View(entries);
    }
}
