using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SareeStore.Infrastructure.Data;

namespace SareeStore.IntegrationTests;

public static class TestDbContextFactory
{
    private const string DefaultLocalDbConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=SareeStoreDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public static ApplicationDbContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(DefaultLocalDbConnectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
