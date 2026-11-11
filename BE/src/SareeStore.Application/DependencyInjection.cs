using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SareeStore.Application.Services;
using SareeStore.Application.Services.Interfaces;

namespace SareeStore.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register FluentValidation validators found in this assembly
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Register application services
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ICollectionService, CollectionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IRefundService, RefundService>();

        return services;
    }
}
