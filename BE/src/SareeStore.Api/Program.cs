using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SareeStore.Api.Middleware;
using SareeStore.Application;
using SareeStore.Infrastructure;
using SareeStore.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// Register centralized exception handler and Problem Details
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Configure lowercase URLs for RESTful routes
builder.Services.AddRouting(options => options.LowercaseUrls = true);

// Add controllers
builder.Services.AddControllers();

// Configure Application and Infrastructure layer dependencies
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Configure JWT Authentication
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.Name
    };
});

// Configure Authorization
builder.Services.AddAuthorization();

// Configure CORS for Angular frontend
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularApp", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure OpenAPI with JWT Bearer Security Scheme
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new Microsoft.OpenApi.OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter JWT Bearer token to authorize requests."
        };

        var schemeRef = new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document, null);
        var requirement = new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            [schemeRef] = new List<string>()
        };

        document.Security ??= new List<Microsoft.OpenApi.OpenApiSecurityRequirement>();
        document.Security.Add(requirement);

        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Configure HTTP request pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "SareeStore API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "SareeStore API - Swagger UI";
    });
}

app.UseHttpsRedirection();

app.UseCors("AngularApp");

// Middleware order: Authentication must precede Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Expose Program class for WebApplicationFactory integration testing
public partial class Program { }
