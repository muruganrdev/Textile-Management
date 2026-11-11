using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SareeStore.Application.DTOs.Categories;
using SareeStore.Application.DTOs.Collections;
using SareeStore.Application.DTOs.Products;

namespace SareeStore.IntegrationTests.Controllers;

[Collection("DatabaseTests")]
public class ProductsApiTests : IClassFixture<SareeStoreApiFactory>
{
    private readonly HttpClient _client;

    public ProductsApiTests(SareeStoreApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<CategoryResponse> CreateCategoryAsync(string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var request = new CreateCategoryRequest
        {
            Name = $"{prefix} {suffix}",
            Slug = $"{prefix.ToLower().Replace(" ", "-")}-{suffix}",
            IsActive = true
        };
        var res = await _client.PostAsJsonAsync("/api/categories", request);
        res.EnsureSuccessStatusCode();
        var cat = await res.Content.ReadFromJsonAsync<CategoryResponse>();
        return cat!;
    }

    private async Task<CollectionResponse> CreateCollectionAsync(string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var request = new CreateCollectionRequest
        {
            Name = $"{prefix} {suffix}",
            Slug = $"{prefix.ToLower().Replace(" ", "-")}-{suffix}",
            IsActive = true
        };
        var res = await _client.PostAsJsonAsync("/api/collections", request);
        res.EnsureSuccessStatusCode();
        var coll = await res.Content.ReadFromJsonAsync<CollectionResponse>();
        return coll!;
    }

    [Fact]
    public async Task Product_FullCrudLifecycle_ShouldSucceed()
    {
        var category = await CreateCategoryAsync("Kanchipuram");
        var collection = await CreateCollectionAsync("Bridal");
        var uniqueSku = $"TEST-SKU-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        var uniqueSlug = $"pure-silk-saree-{Guid.NewGuid():N}";

        // 1. Create Product (POST /api/products -> 201 Created)
        var createRequest = new CreateProductRequest
        {
            CategoryId = category.Id,
            SKU = uniqueSku,
            Title = "Kanchipuram Silk Saree with Pure Zari",
            Slug = uniqueSlug,
            Description = "Traditional handwoven pure silk saree for weddings",
            Price = 24500m,
            DiscountPrice = 22000m,
            Fabric = "Pure Silk",
            WeaveType = "Korvai",
            ZariType = "Pure Zari",
            PrimaryColor = "Peacock Blue",
            SareeLengthMeters = 5.50m,
            SareeWidthInches = 48.00m,
            BlouseIncluded = true,
            BlouseLengthMeters = 0.80m,
            CraftDetails = "Handwoven by master artisans",
            Occasion = "Wedding",
            Has3DModel = false,
            IsActive = true,
            CollectionIds = new List<int> { collection.Id }
        };

        var postResponse = await _client.PostAsJsonAsync("/api/products", createRequest);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        Assert.NotNull(postResponse.Headers.Location);

        var created = await postResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal(uniqueSku, created.SKU);
        Assert.Equal(category.Name, created.CategoryName);
        Assert.Contains(collection.Id, created.CollectionIds);

        // 2. Get By ID (GET /api/products/{id} -> 200 OK)
        var getByIdResponse = await _client.GetAsync($"/api/products/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getByIdResponse.StatusCode);
        var fetched = await getByIdResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(uniqueSku, fetched.SKU);

        // 3. Get By Category (GET /api/products/category/{categoryId} -> 200 OK)
        var getByCatResponse = await _client.GetAsync($"/api/products/category/{category.Id}");
        Assert.Equal(HttpStatusCode.OK, getByCatResponse.StatusCode);
        var catProducts = await getByCatResponse.Content.ReadFromJsonAsync<List<ProductResponse>>();
        Assert.NotNull(catProducts);
        Assert.Contains(catProducts, p => p.Id == created.Id);

        // 4. Update Product (PUT /api/products/{id} -> 200 OK)
        var updateRequest = new UpdateProductRequest
        {
            CategoryId = category.Id,
            SKU = uniqueSku,
            Title = "Kanchipuram Silk Saree Updated Edition",
            Slug = uniqueSlug,
            Description = "Updated handcrafted silk saree description",
            Price = 25000m,
            DiscountPrice = 23000m,
            Fabric = "Pure Mulberry Silk",
            WeaveType = "Korvai",
            ZariType = "Pure Zari",
            PrimaryColor = "Royal Peacock Blue",
            SareeLengthMeters = 5.50m,
            SareeWidthInches = 48.00m,
            BlouseIncluded = true,
            BlouseLengthMeters = 0.80m,
            IsActive = true,
            CollectionIds = new List<int> { collection.Id }
        };

        var putResponse = await _client.PutAsJsonAsync($"/api/products/{created.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        var updated = await putResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(updated);
        Assert.Equal(updateRequest.Title, updated.Title);
        Assert.Equal(25000m, updated.Price);

        // 5. Deactivate Product (DELETE /api/products/{id} -> 204 No Content)
        var deleteResponse = await _client.DeleteAsync($"/api/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // 6. Verify Product is deactivated
        var verifyResponse = await _client.GetAsync($"/api/products/{created.Id}");
        var verified = await verifyResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(verified);
        Assert.False(verified.IsActive);
    }

    [Fact]
    public async Task GetAll_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/api/products?includeInactive=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var products = await response.Content.ReadFromJsonAsync<List<ProductResponse>>();
        Assert.NotNull(products);
    }

    [Fact]
    public async Task GetById_NonExistent_ShouldReturn404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/products/9999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task Create_InvalidRequest_EmptySku_ShouldReturn400ValidationProblemDetails()
    {
        var category = await CreateCategoryAsync("InvalidTestCat");
        var invalidRequest = new CreateProductRequest
        {
            CategoryId = category.Id,
            SKU = "", // Invalid
            Title = "Sample Title",
            Price = 1000m,
            Fabric = "Silk",
            WeaveType = "Plain",
            ZariType = "None",
            PrimaryColor = "Red"
        };

        var response = await _client.PostAsJsonAsync("/api/products", invalidRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("SKU", out _));
    }

    [Fact]
    public async Task Create_DuplicateSku_ShouldReturn409ConflictProblemDetails()
    {
        var category = await CreateCategoryAsync("DupSkuCat");
        var duplicateSku = $"DUP-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var firstRequest = new CreateProductRequest
        {
            CategoryId = category.Id,
            SKU = duplicateSku,
            Title = "First Saree With SKU",
            Description = "Traditional handwoven silk saree",
            Price = 12000m,
            Fabric = "Silk",
            WeaveType = "Handloom",
            ZariType = "Tested Zari",
            PrimaryColor = "Green",
            IsActive = true
        };

        var firstResponse = await _client.PostAsJsonAsync("/api/products", firstRequest);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Attempt second product with identical SKU
        var secondRequest = new CreateProductRequest
        {
            CategoryId = category.Id,
            SKU = duplicateSku,
            Title = "Second Saree With Duplicate SKU",
            Description = "Traditional handwoven silk saree duplicate test",
            Price = 14000m,
            Fabric = "Silk",
            WeaveType = "Handloom",
            ZariType = "Tested Zari",
            PrimaryColor = "Blue",
            IsActive = true
        };

        var conflictResponse = await _client.PostAsJsonAsync("/api/products", secondRequest);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        Assert.Equal("application/problem+json", conflictResponse.Content.Headers.ContentType?.MediaType);

        var problem = await conflictResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }
}
