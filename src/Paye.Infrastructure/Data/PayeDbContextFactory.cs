using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Paye.Infrastructure.Data;

public sealed class PayeDbContextFactory : IDesignTimeDbContextFactory<PayeDbContext>
{
    public PayeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DESIGN_TIME_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=paye_design_time;Username=postgres;Password=design_time_only";

        var optionsBuilder = new DbContextOptionsBuilder<PayeDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(PayeDbContext).Assembly.FullName);
        });

        return new PayeDbContext(optionsBuilder.Options);
    }
}
