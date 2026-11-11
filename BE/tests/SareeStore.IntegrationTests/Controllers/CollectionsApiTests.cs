using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SareeStore.Application.DTOs.Collections;

namespace SareeStore.IntegrationTests.Controllers;

[Collection("DatabaseTests")]
public class CollectionsApiTests : IClassFixture<SareeStoreApiFactory>
{
    private readonly HttpClient _client;

    public CollectionsApiTests(SareeStoreApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Collection_FullCrudLifecycle_ShouldSucceed()
    {
        // 1. Create Collection (POST /api/collections -> 201 Created)
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var createRequest = new CreateCollectionRequest
        {
            Name = $"Festive Trousseau {uniqueSuffix}",
            Slug = $"festive-trousseau-{uniqueSuffix}",
            Description = "Curated luxury sarees for grand festivals",
            BannerImageUrl = "https://cdn.example.com/banner.jpg",
            IsFeatured = true,
            IsActive = true
        };

        var postResponse = await _client.PostAsJsonAsync("/api/collections", createRequest);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        Assert.NotNull(postResponse.Headers.Location);

        var created = await postResponse.Content.ReadFromJsonAsync<CollectionResponse>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal(createRequest.Name, created.Name);

        // 2. Get By ID (GET /api/collections/{id} -> 200 OK)
        var getByIdResponse = await _client.GetAsync($"/api/collections/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getByIdResponse.StatusCode);
        var fetched = await getByIdResponse.Content.ReadFromJsonAsync<CollectionResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);

        // 3. Get By Slug (GET /api/collections/slug/{slug} -> 200 OK)
        var getBySlugResponse = await _client.GetAsync($"/api/collections/slug/{created.Slug}");
        Assert.Equal(HttpStatusCode.OK, getBySlugResponse.StatusCode);
        var fetchedBySlug = await getBySlugResponse.Content.ReadFromJsonAsync<CollectionResponse>();
        Assert.NotNull(fetchedBySlug);
        Assert.Equal(created.Slug, fetchedBySlug.Slug);

        // 4. Update Collection (PUT /api/collections/{id} -> 200 OK)
        var updateRequest = new UpdateCollectionRequest
        {
            Name = $"Festive Trousseau Luxury {uniqueSuffix}",
            Slug = $"festive-trousseau-luxury-{uniqueSuffix}",
            Description = "Updated luxury festive collection description",
            BannerImageUrl = "https://cdn.example.com/updated-banner.jpg",
            IsFeatured = true,
            IsActive = true
        };

        var putResponse = await _client.PutAsJsonAsync($"/api/collections/{created.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        var updated = await putResponse.Content.ReadFromJsonAsync<CollectionResponse>();
        Assert.NotNull(updated);
        Assert.Equal(updateRequest.Name, updated.Name);

        // 5. Deactivate Collection (DELETE /api/collections/{id} -> 204 No Content)
        var deleteResponse = await _client.DeleteAsync($"/api/collections/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // 6. Verify Collection is deactivated
        var verifyResponse = await _client.GetAsync($"/api/collections/{created.Id}");
        var verified = await verifyResponse.Content.ReadFromJsonAsync<CollectionResponse>();
        Assert.NotNull(verified);
        Assert.False(verified.IsActive);
    }

    [Fact]
    public async Task GetAll_ShouldReturnOkWithList()
    {
        var response = await _client.GetAsync("/api/collections?includeInactive=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var collections = await response.Content.ReadFromJsonAsync<List<CollectionResponse>>();
        Assert.NotNull(collections);
    }

    [Fact]
    public async Task GetById_NonExistent_ShouldReturn404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/collections/9999999");
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
        var invalidRequest = new CreateCollectionRequest
        {
            Name = "", // Invalid
            Slug = "invalid-empty-coll",
            Description = "Test"
        };

        var response = await _client.PostAsJsonAsync("/api/collections", invalidRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("Name", out _));
    }

    [Fact]
    public async Task Create_DuplicateSlug_ShouldReturn409ConflictProblemDetails()
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var request = new CreateCollectionRequest
        {
            Name = $"Collection A {uniqueSuffix}",
            Slug = $"same-collection-slug-{uniqueSuffix}",
            IsActive = true
        };

        // First creation succeeds
        var firstResponse = await _client.PostAsJsonAsync("/api/collections", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Second creation with duplicate slug fails with 409 Conflict
        var secondRequest = new CreateCollectionRequest
        {
            Name = $"Collection B {uniqueSuffix}",
            Slug = request.Slug,
            IsActive = true
        };

        var conflictResponse = await _client.PostAsJsonAsync("/api/collections", secondRequest);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        Assert.Equal("application/problem+json", conflictResponse.Content.Headers.ContentType?.MediaType);

        var problem = await conflictResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }
}
