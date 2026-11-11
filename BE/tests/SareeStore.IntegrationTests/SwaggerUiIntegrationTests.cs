using System.Net;
using System.Text.Json;

namespace SareeStore.IntegrationTests;

[Collection("DatabaseTests")]
public class SwaggerUiIntegrationTests : IClassFixture<SareeStoreApiFactory>
{
    private readonly HttpClient _client;

    public SwaggerUiIntegrationTests(SareeStoreApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApi_JsonEndpoint_ShouldReturn200AndIncludeAllEndpointsAndSchemas()
    {
        // Act: GET /openapi/v1.json
        var response = await _client.GetAsync("/openapi/v1.json");

        // Assert: 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // Verify OpenAPI version is present
        Assert.True(root.TryGetProperty("openapi", out _));

        // Verify paths for Products, Categories, Collections, Auth, and Users
        Assert.True(root.TryGetProperty("paths", out var paths));
        Assert.True(paths.TryGetProperty("/api/products", out _));
        Assert.True(paths.TryGetProperty("/api/products/{id}", out _));
        Assert.True(paths.TryGetProperty("/api/categories", out _));
        Assert.True(paths.TryGetProperty("/api/collections", out _));
        Assert.True(paths.TryGetProperty("/api/auth/register", out _));
        Assert.True(paths.TryGetProperty("/api/auth/login", out _));
        Assert.True(paths.TryGetProperty("/api/auth/me", out _));
        Assert.True(paths.TryGetProperty("/api/users/employees", out _));
        Assert.True(paths.TryGetProperty("/api/cart", out _));
        Assert.True(paths.TryGetProperty("/api/cart/items", out _));
        Assert.True(paths.TryGetProperty("/api/cart/items/{productId}", out _));
        Assert.True(paths.TryGetProperty("/api/orders", out _));
        Assert.True(paths.TryGetProperty("/api/orders/my-orders", out _));
        Assert.True(paths.TryGetProperty("/api/orders/{id}", out _));

        // Verify components, schemas, and securitySchemes
        Assert.True(root.TryGetProperty("components", out var components));
        Assert.True(components.TryGetProperty("schemas", out var schemas));
        Assert.True(components.TryGetProperty("securitySchemes", out var securitySchemes));
        Assert.True(securitySchemes.TryGetProperty("Bearer", out var bearerScheme));
        Assert.True(bearerScheme.TryGetProperty("scheme", out var schemeVal));
        Assert.Equal("bearer", schemeVal.GetString());

        // Verify DTO schemas are present
        Assert.True(schemas.TryGetProperty("ProductResponse", out _));
        Assert.True(schemas.TryGetProperty("CreateProductRequest", out _));
        Assert.True(schemas.TryGetProperty("UpdateProductRequest", out _));
        Assert.True(schemas.TryGetProperty("CategoryResponse", out _));
        Assert.True(schemas.TryGetProperty("CreateCategoryRequest", out _));
        Assert.True(schemas.TryGetProperty("CollectionResponse", out _));
        Assert.True(schemas.TryGetProperty("CreateCollectionRequest", out _));
        Assert.True(schemas.TryGetProperty("RegisterCustomerRequest", out _));
        Assert.True(schemas.TryGetProperty("LoginRequest", out _));
        Assert.True(schemas.TryGetProperty("AuthResponse", out _));
        Assert.True(schemas.TryGetProperty("UserResponse", out _));
        Assert.True(schemas.TryGetProperty("CartResponse", out _));
        Assert.True(schemas.TryGetProperty("CartItemResponse", out _));
        Assert.True(schemas.TryGetProperty("AddToCartRequest", out _));
        Assert.True(schemas.TryGetProperty("UpdateCartItemRequest", out _));
        Assert.True(schemas.TryGetProperty("CreateOrderRequest", out _));
        Assert.True(schemas.TryGetProperty("OrderResponse", out _));
        Assert.True(schemas.TryGetProperty("OrderItemResponse", out _));
    }

    [Fact]
    public async Task SwaggerUi_Endpoint_ShouldReturn200AndReferenceOpenApiJson()
    {
        // Act: GET /swagger/index.html
        var response = await _client.GetAsync("/swagger/index.html");

        // Assert: 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("SareeStore API - Swagger UI", html);
        Assert.Contains("swagger-ui", html);

        // Swashbuckle serves the SwaggerUI initialization config via index.js
        var jsResponse = await _client.GetAsync("/swagger/index.js");
        Assert.Equal(HttpStatusCode.OK, jsResponse.StatusCode);
        var js = await jsResponse.Content.ReadAsStringAsync();
        Assert.Contains("/openapi/v1.json", js);
        Assert.Contains("SareeStore API v1", js);
    }

    [Fact]
    public async Task SwaggerUi_RouteRedirect_ShouldServeOrRedirectCorrectly()
    {
        // Act: GET /swagger
        var response = await _client.GetAsync("/swagger");

        // Should return 200 OK (if following redirect) or 301/302 redirecting to /swagger/index.html
        Assert.True(response.StatusCode == HttpStatusCode.OK ||
                    response.StatusCode == HttpStatusCode.MovedPermanently ||
                    response.StatusCode == HttpStatusCode.Redirect);
    }
}
