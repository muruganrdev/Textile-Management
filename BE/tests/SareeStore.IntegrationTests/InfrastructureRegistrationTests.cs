using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SareeStore.Application;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.Services.Interfaces;
using SareeStore.Infrastructure;
using SareeStore.Infrastructure.Data;

namespace SareeStore.IntegrationTests;

public class InfrastructureRegistrationTests
{
    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=SareeStoreDB;Trusted_Connection=True;TrustServerCertificate=True",
                ["Jwt:SecretKey"] = "INTEGRATION_TEST_SECRET_KEY_1234567890_AB",
                ["Jwt:Issuer"] = "SareeStore.Api",
                ["Jwt:Audience"] = "SareeStore.Client",
                ["Jwt:ExpirationMinutes"] = "60"
            })
            .Build();
    }

    [Fact]
    public void AddInfrastructureServices_ShouldRegisterDbContextAndRepositories()
    {
        var services = new ServiceCollection();
        services.AddInfrastructureServices(CreateConfiguration());

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
        var productRepo = scope.ServiceProvider.GetService<IProductRepository>();
        var categoryRepo = scope.ServiceProvider.GetService<ICategoryRepository>();
        var collectionRepo = scope.ServiceProvider.GetService<ICollectionRepository>();
        var passwordHasher = scope.ServiceProvider.GetService<Microsoft.AspNetCore.Identity.IPasswordHasher<Domain.Entities.User>>();
        var passwordHashService = scope.ServiceProvider.GetService<IPasswordHashService>();
        var tokenGenerator = scope.ServiceProvider.GetService<IJwtTokenGenerator>();

        Assert.NotNull(dbContext);
        Assert.NotNull(productRepo);
        Assert.NotNull(categoryRepo);
        Assert.NotNull(collectionRepo);
        Assert.NotNull(passwordHasher);
        Assert.NotNull(passwordHashService);
        Assert.NotNull(tokenGenerator);
    }

    [Fact]
    public void ServiceWiring_ShouldResolveApplicationServices_WithRepositoriesInjected()
    {
        var services = new ServiceCollection();
        var config = CreateConfiguration();

        // Mirrors API composition root registration in Program.cs
        services.AddApplicationServices();
        services.AddInfrastructureServices(config);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var productService = scope.ServiceProvider.GetService<IProductService>();
        var categoryService = scope.ServiceProvider.GetService<ICategoryService>();
        var collectionService = scope.ServiceProvider.GetService<ICollectionService>();

        Assert.NotNull(productService);
        Assert.NotNull(categoryService);
        Assert.NotNull(collectionService);
    }
}
