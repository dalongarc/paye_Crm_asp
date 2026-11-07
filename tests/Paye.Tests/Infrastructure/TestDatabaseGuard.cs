namespace Paye.Tests.Infrastructure;

public static class TestDatabaseGuard
{
    public static void EnsureIsolatedTestDatabase(string? testConnectionString, string? productionConnectionString = null)
    {
        if (string.IsNullOrWhiteSpace(testConnectionString))
        {
            throw new InvalidOperationException(
                "SEGURIDAD DE PRUEBAS: TEST_DATABASE_URL es obligatoria. Nunca ejecutes pruebas sin una base de datos de pruebas explícita.");
        }

        var prodEnv = productionConnectionString
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (!string.IsNullOrWhiteSpace(prodEnv) &&
            string.Equals(Normalize(testConnectionString), Normalize(prodEnv), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "SEGURIDAD DE PRUEBAS: TEST_DATABASE_URL no puede coincidir con DATABASE_URL ni con ConnectionStrings__DefaultConnection.");
        }
    }

    private static string Normalize(string connectionString) =>
        connectionString.Trim().TrimEnd(';');
}
