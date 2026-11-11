using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Domain.Entities;
using SareeStore.Infrastructure.Data;
using SareeStore.Infrastructure.PaymentProviders;
using SareeStore.Infrastructure.Repositories;
using SareeStore.Infrastructure.Security;

namespace SareeStore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                });
            }
        });

        // Register Repository Implementations
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IRefundRepository, RefundRepository>();

        // Register Payment & Refund Provider Implementations
        services.AddHttpContextAccessor();
        services.AddSingleton<IPaymentProvider, SimulatedPaymentProvider>();
        services.AddSingleton<IRefundProvider, SimulatedRefundProvider>();

        // Register Security Services
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IPasswordHashService, PasswordHashService>();

        // Register JWT Configuration with validation
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}
