using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.DTOs.Auth;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;
using SareeStore.Infrastructure.Data;
using SareeStore.Infrastructure.Security;

namespace SareeStore.IntegrationTests.Controllers;

[Collection("DatabaseTests")]
public class AuthApiTests : IClassFixture<SareeStoreApiFactory>
{
    private readonly SareeStoreApiFactory _factory;
    private readonly HttpClient _client;

    public AuthApiTests(SareeStoreApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Registration Tests

    [Fact]
    public async Task Register_WithValidCustomerRequest_ShouldReturnOkWithJwtAndCustomerProfile()
    {
        // Arrange
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var request = new RegisterCustomerRequest
        {
            FullName = "Radha Krishna",
            Email = $"radha_{uniqueSuffix}@example.com",
            Password = "SecurePassword123#",
            PhoneNumber = "+919876543210"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", content, StringComparison.OrdinalIgnoreCase);

        var authResponse = JsonSerializer.Deserialize<AuthResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(authResponse);
        Assert.False(string.IsNullOrWhiteSpace(authResponse.AccessToken));
        Assert.True(authResponse.ExpiresAtUtc > DateTime.UtcNow);
        Assert.NotNull(authResponse.User);
        Assert.True(authResponse.User.Id > 0);
        Assert.Equal(request.Email.ToLowerInvariant(), authResponse.User.Email);
        Assert.Equal("Radha Krishna", authResponse.User.FullName);
        Assert.Equal(nameof(UserRole.Customer), authResponse.User.Role);
        Assert.True(authResponse.User.IsActive);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturn409ConflictProblemDetails()
    {
        // Arrange
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var request = new RegisterCustomerRequest
        {
            FullName = "Duplicate User",
            Email = $"duplicate_{uniqueSuffix}@example.com",
            Password = "SecurePassword123#",
            PhoneNumber = "+919876543210"
        };

        // First registration succeeds
        var firstResponse = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // Act: Second registration with same email
        var secondResponse = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal("application/problem+json", secondResponse.Content.Headers.ContentType?.MediaType);

        var problem = await secondResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task Register_CustomerRoleIsAlwaysAssigned_NeverExposesPasswordHash()
    {
        // Arrange: Try sending a payload with an injected Role
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var payload = new
        {
            FullName = "Attacker User",
            Email = $"attacker_{uniqueSuffix}@example.com",
            Password = "SecurePassword123#",
            Role = "SuperAdmin" // Malicious injected role
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", payload);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", content, StringComparison.OrdinalIgnoreCase);

        var authResponse = JsonSerializer.Deserialize<AuthResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(authResponse);
        // Server unconditionally forces Customer role
        Assert.Equal(nameof(UserRole.Customer), authResponse.User.Role);
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnJwtAndUserProfile()
    {
        // Arrange: Register customer first
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"login_{uniqueSuffix}@example.com";
        var password = "LoginPassword123#";

        var registerRequest = new RegisterCustomerRequest
        {
            FullName = "Login Customer",
            Email = email,
            Password = password
        };
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // Act
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var content = await loginResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", content, StringComparison.OrdinalIgnoreCase);

        var authResponse = JsonSerializer.Deserialize<AuthResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(authResponse);
        Assert.False(string.IsNullOrWhiteSpace(authResponse.AccessToken));
        Assert.Equal(email, authResponse.User.Email);
    }

    [Theory]
    [InlineData("wrongpassword@example.com", "WrongPassword!")] // Wrong email
    [InlineData("existing@example.com", "IncorrectPassword123#")] // Wrong password
    public async Task Login_WithInvalidCredentials_ShouldReturn401ProblemDetails(string email, string password)
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(401, problem.Status);
    }

    [Fact]
    public async Task Login_WithInactiveAccount_ShouldReturn401Unauthorized()
    {
        // Arrange: Register user then deactivate in DB
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"inactive_{uniqueSuffix}@example.com";
        var password = "SecurePassword123#";

        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterCustomerRequest
        {
            FullName = "Inactive Test",
            Email = email,
            Password = password
        });
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        // Deactivate user in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        // Act: Attempt login
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    #endregion

    #region Authentication /api/auth/me Tests

    [Fact]
    public async Task GetCurrentUser_WithoutToken_ShouldReturn401Unauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WithMalformedToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "this.is.not.a.valid.jwt");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WithExpiredToken_ShouldReturn401Unauthorized()
    {
        // Arrange: Generate an expired token using same key and issuer
        using var scope = _factory.Services.CreateScope();
        var jwtOptions = scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(jwtOptions.SecretKey);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "999"),
                new Claim(ClaimTypes.Role, "Customer")
            }),
            NotBefore = DateTime.UtcNow.AddMinutes(-60),
            IssuedAt = DateTime.UtcNow.AddMinutes(-60),
            Expires = DateTime.UtcNow.AddMinutes(-30), // Expired 30 minutes ago
            Issuer = jwtOptions.Issuer,
            Audience = jwtOptions.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var expiredToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WithValidToken_ShouldReturnUserProfileWithoutPasswordHash()
    {
        // Arrange: Register customer and get token
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"me_{uniqueSuffix}@example.com";
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterCustomerRequest
        {
            FullName = "Profile User",
            Email = email,
            Password = "ProfilePassword123#",
            PhoneNumber = "+919123456789"
        });
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        var authResponse = await regResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResponse.AccessToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", content, StringComparison.OrdinalIgnoreCase);

        var userResponse = JsonSerializer.Deserialize<UserResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(userResponse);
        Assert.Equal(authResponse.User.Id, userResponse.Id);
        Assert.Equal(email, userResponse.Email);
        Assert.Equal("Profile User", userResponse.FullName);
        Assert.Equal(nameof(UserRole.Customer), userResponse.Role);
    }

    #endregion

    #region Role-Based Authorization Tests (Employee Provisioning)

    [Fact]
    public async Task CreateEmployee_WhenCalledByCustomer_ShouldReturn403Forbidden()
    {
        // Arrange: Register a customer
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterCustomerRequest
        {
            FullName = "Normal Customer",
            Email = $"cust_{uniqueSuffix}@example.com",
            Password = "CustomerPass123#"
        });
        var authResponse = await regResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);

        var employeeRequest = new CreateEmployeeRequest
        {
            FullName = "New Staff",
            Email = $"staff_{uniqueSuffix}@example.com",
            Password = "StaffPassword123#"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/employees")
        {
            Content = JsonContent.Create(employeeRequest)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResponse.AccessToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateEmployee_WhenCalledByEmployee_ShouldReturn403Forbidden()
    {
        // Arrange: Generate an employee token
        var employeeToken = GenerateTokenForRole(UserRole.Employee, 101, "employee@example.com");

        var employeeRequest = new CreateEmployeeRequest
        {
            FullName = "Peer Staff",
            Email = $"peer_{Guid.NewGuid():N}@example.com",
            Password = "StaffPassword123#"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/employees")
        {
            Content = JsonContent.Create(employeeRequest)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", employeeToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateEmployee_WhenCalledBySuperAdmin_ShouldReturn201CreatedWithEmployeeProfile()
    {
        // Arrange: Generate a SuperAdmin token
        var superAdminToken = GenerateTokenForRole(UserRole.SuperAdmin, 1, "superadmin@sareestore.com");

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var employeeRequest = new CreateEmployeeRequest
        {
            FullName = "Sales Representative",
            Email = $"sales_{uniqueSuffix}@sareestore.com",
            Password = "StaffPassword123#",
            PhoneNumber = "+919876543299"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/employees")
        {
            Content = JsonContent.Create(employeeRequest)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", content, StringComparison.OrdinalIgnoreCase);

        var created = JsonSerializer.Deserialize<UserResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal(employeeRequest.Email.ToLowerInvariant(), created.Email);
        Assert.Equal(nameof(UserRole.Employee), created.Role);
        Assert.True(created.IsActive);
    }

    #endregion

    private string GenerateTokenForRole(UserRole role, int userId, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var tokenGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        var user = new User
        {
            Id = userId,
            FullName = $"{role} User",
            Email = email,
            Role = role,
            IsActive = true
        };

        return tokenGenerator.GenerateToken(user);
    }
}
