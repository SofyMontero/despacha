using Microsoft.EntityFrameworkCore;

namespace Despacha.Api.Shared.Persistence;

/// <summary>Único punto donde se elige el proveedor MySQL de EF Core (ver backend/README.md).</summary>
public static class DespachaMySql
{
    public static DbContextOptionsBuilder UseDespachaMySql(this DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseMySQL(connectionString);
}
