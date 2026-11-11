using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SareeStore.Application.DTOs.Categories;

namespace SareeStore.IntegrationTests.Controllers;

[Collection("DatabaseTests")]
public class CategoriesApiTests : IClassFixture<SareeStoreApiFactory>
{
    private readonly HttpClient _client;

    public CategoriesApiTests(SareeStoreApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Category_FullCrudLifecycle_ShouldSucceed()
    {
        // 1. Create Category (POST /api/categories -> 201 Created)
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var createRequest = new CreateCategoryRequest
        {
            Name = $"Chanderi Silk {uniqueSuffix}",
            Slug = $"chanderi-silk-{uniqueSuffix}",
            Description = "Lightweight Chanderi sarees with zari borders",
            IsActive = true
        };

        var postResponse = await _client.PostAsJsonAsync("/api/categories", createRequest);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        Assert.NotNull(postResponse.Headers.Location);

        var created = await postResponse.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal(createRequest.Name, created.Name);

        // 2. Get By ID (GET /api/categories/{id} -> 200 OK)
        var getByIdResponse = await _client.GetAsync($"/api/categories/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getByIdResponse.StatusCode);
        var fetched = await getByIdResponse.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);

        // 3. Get By Slug (GET /api/categories/slug/{slug} -> 200 OK)
        var getBySlugResponse = await _client.GetAsync($"/api/categories/slug/{created.Slug}");
        Assert.Equal(HttpStatusCode.OK, getBySlugResponse.StatusCode);
        var fetchedBySlug = await getBySlugResponse.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(fetchedBySlug);
        Assert.Equal(created.Slug, fetchedBySlug.Slug);

        // 4. Update Category (PUT /api/categories/{id} -> 200 OK)
        var updateRequest = new UpdateCategoryRequest
        {
            Name = $"Chanderi Silk Premium {uniqueSuffix}",
            Slug = $"chanderi-silk-premium-{uniqueSuffix}",
            Description = "Updated Chanderi sarees collection",
            IsActive = true
        };

        var putResponse = await _client.PutAsJsonAsync($"/api/categories/{created.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        var updated = await putResponse.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(updated);
        Assert.Equal(updateRequest.Name, updated.Name);

        // 5. Deactivate Category (DELETE /api/categories/{id} -> 204 No Content)
        var deleteResponse = await _client.DeleteAsync($"/api/categories/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // 6. Verify Category is deactivated
        var verifyResponse = await _client.GetAsync($"/api/categories/{created.Id}");
        var verified = await verifyResponse.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(verified);
        Assert.False(verified.IsActive);
    }

    [Fact]
    public async Task GetAll_ShouldReturnOkWithList()
    {
        var response = await _client.GetAsync("/api/categories?includeInactive=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var categories = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>();
        Assert.NotNull(categories);
    }

    [Fact]
    public async Task GetById_NonExistent_ShouldReturn404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/categories/9999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task Create_EmptyName_ShouldReturn400ValidationProblemDetails()
    {
        var invalidRequest = new CreateCategoryRequest
        {
            Name = "", // Invalid: empty
            Slug = "invalid-empty-slug",
            Description = "Test"
        };

        var response = await _client.PostAsJsonAsync("/api/categories", invalidRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("Name", out _));
    }

    [Fact]
    public async Task Create_DuplicateName_ShouldReturn409ConflictProblemDetails()
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var request = new CreateCategoryRequest
        {
            Name = $"Duplicate Cat {uniqueSuffix}",
            Slug = $"duplicate-cat-{uniqueSuffix}",
            IsActive = true
        };

        // First creation succeeds
        var firstResponse = await _client.PostAsJsonAsync("/api/categories", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Second creation with same name fails with 409 Conflict
        var secondRequest = new CreateCategoryRequest
        {
            Name = request.Name,
            Slug = $"different-slug-{uniqueSuffix}",
            IsActive = true
        };

        var conflictResponse = await _client.PostAsJsonAsync("/api/categories", secondRequest);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        Assert.Equal("application/problem+json", conflictResponse.Content.Headers.ContentType?.MediaType);

        var problem = await conflictResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }
}
