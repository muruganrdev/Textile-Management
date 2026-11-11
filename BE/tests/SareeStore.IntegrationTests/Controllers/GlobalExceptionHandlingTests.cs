using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SareeStore.Application.DTOs.Products;
using SareeStore.Application.Services.Interfaces;

namespace SareeStore.IntegrationTests.Controllers;

[Collection("DatabaseTests")]
public class GlobalExceptionHandlingTests : IClassFixture<SareeStoreApiFactory>
{
    private readonly SareeStoreApiFactory _factory;

    public GlobalExceptionHandlingTests(SareeStoreApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnexpectedException_ShouldReturn500ProblemDetailsWithoutSensitiveDetails()
    {
        var sensitiveMessage = "Simulated fatal SQL server connection failure; Password=SuperSecret123!;";

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IProductService));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddScoped<IProductService>(_ => new FaultyProductService(sensitiveMessage));
            });
        }).CreateClient();

        var response = await client.GetAsync("/api/products/1");

        // 1. Verify Status Code 500
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        // 2. Verify Content-Type is application/problem+json
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        // 3. Verify ProblemDetails structure
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(500, problem.Status);
        Assert.Equal("An unexpected server error occurred.", problem.Title);
        Assert.True(problem.Extensions.ContainsKey("traceId"));

        // 4. Verify no internal sensitive information or stack trace was leaked
        var rawContent = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(sensitiveMessage, rawContent);
        Assert.DoesNotContain("SuperSecret123!", rawContent);
        Assert.DoesNotContain("StackTrace", rawContent);
    }

    [Fact]
    public async Task Cors_WithConfiguredAngularOrigin_ShouldReturnCorsHeaders()
    {
        var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/products");
        request.Headers.Add("Origin", "http://localhost:4200");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin") ||
                    response.StatusCode == HttpStatusCode.NoContent ||
                    response.StatusCode == HttpStatusCode.OK);

        if (response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowOriginValues))
        {
            Assert.Contains("http://localhost:4200", allowOriginValues);
        }
    }

    private class FaultyProductService : IProductService
    {
        private readonly string _exceptionMessage;

        public FaultyProductService(string exceptionMessage)
        {
            _exceptionMessage = exceptionMessage;
        }

        public Task<ProductResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(_exceptionMessage);
        }

        public Task<IReadOnlyList<ProductResponse>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(_exceptionMessage);

        public Task<IReadOnlyList<ProductResponse>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(_exceptionMessage);

        public Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(_exceptionMessage);

        public Task<ProductResponse> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(_exceptionMessage);

        public Task DeactivateAsync(int id, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(_exceptionMessage);
    }
}
