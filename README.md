# Traditional Saree & Textile E-Commerce Platform

A high-performance, modular e-commerce solution engineered for traditional handcrafted Indian sarees and heritage textiles. The solution is built with a decoupled modern architecture featuring an **Angular SPA frontend**, an **ASP.NET Core Clean Architecture Web API backend**, and **Microsoft SQL Server** with Entity Framework Core.

---

## 1. Directory Structure

```text
ProjectRoot/
├── FE/                               # Frontend Workspace
│   └── saree-store/                  # Angular SPA (Standalone components, SCSS, strict TypeScript)
│       └── src/
│           ├── app/
│           │   ├── core/             # Auth, guards, interceptors, services, shared models
│           │   ├── shared/           # Reusable UI components, directives, pipes
│           │   ├── layout/           # Public customer layout and Admin backoffice layout
│           │   └── features/         # Lazy-loaded business features (products, cart, etc.)
│           ├── assets/
│           └── styles.scss
├── BE/                               # Backend Workspace
│   ├── SareeStore.sln                # Visual Studio / .NET Solution
│   ├── src/
│   │   ├── SareeStore.Api/           # ASP.NET Core Web API (Controllers, OpenAPI, DI Root)
│   │   ├── SareeStore.Application/   # Application use cases, interfaces, DTOs
│   │   ├── SareeStore.Domain/        # Core business models and rules (Clean Architecture)
│   │   └── SareeStore.Infrastructure/# EF Core 10, SQL Server context, external adapters
│   └── tests/
│       ├── SareeStore.UnitTests/     # Domain & architecture unit tests
│       └── SareeStore.IntegrationTests/# Integration & DI registration tests
├── Database/                         # Database documentation, migration scripts & seeds
│   ├── Scripts/                      # Custom SQL scripts & idempotent migration exports
│   └── README.md
├── docs/                             # Engineering & business documentation
│   ├── business-requirements.md      # Detailed domain & e-commerce requirements
│   └── architecture.md               # Technical architecture & layer boundaries
├── .gitignore                        # Global ignore rules for .NET, Angular, secrets
└── README.md                         # Project overview and quickstart guide
```

---

## 2. Prerequisites

Ensure the following tools are installed on your workstation:
- **.NET SDK**: .NET 10 (or compatible .NET 9 SDK)
- **Node.js**: v20+ LTS (Tested on Node v24)
- **npm**: v10+ (Tested on npm v11)
- **Microsoft SQL Server**: 2022+ / SQL Server Express / LocalDB / Azure SQL
- **EF Core CLI**: `dotnet-ef` is preconfigured locally in `BE` via .NET local tools (`dotnet dotnet-ef`).

---

## 3. How to Run the Applications

### 3.1 Backend — ASP.NET Core Web API

1. Navigate to the backend directory:
   ```bash
   cd BE
   ```

2. Restore packages and build:
   ```bash
   dotnet restore
   dotnet build SareeStore.sln
   ```

3. Run automated tests:
   ```bash
   dotnet test SareeStore.sln
   ```

4. Launch the API server:
   ```bash
   dotnet run --project src/SareeStore.Api
   ```
   - The API will start (default HTTPS port: `https://localhost:7147` / HTTP port: `http://localhost:5000` or launch profile ports).
   - Test the health endpoint: `GET https://localhost:<port>/api/health`
   - Access OpenAPI documentation in Development mode: `https://localhost:<port>/openapi/v1.json`

### 3.2 Frontend — Angular

1. Navigate to the frontend directory:
   ```bash
   cd FE
   # (or cd FE/saree-store)
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

3. Run unit tests (Vitest):
   ```bash
   npm test
   ```

4. Build the production bundle:
   ```bash
   npm run build
   ```

5. Start the local development server:
   ```bash
   npm start
   # or
   npx ng serve --open
   ```
   The application will be accessible at `http://localhost:4200/`.

---

## 4. Database Setup & Configuration

- The database connection string placeholder is preconfigured in `BE/src/SareeStore.Api/appsettings.json` and `appsettings.Development.json`:
  ```json
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=SareeStoreDb_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
  ```
- **Entity Framework Core Migrations**: Migrations are owned by `SareeStore.Infrastructure`. Once business entities are finalized, apply migrations using:
  ```bash
  cd BE
  dotnet ef database update --project src/SareeStore.Infrastructure --startup-project src/SareeStore.Api
  ```
- See [Database/README.md](file:///Database/README.md) for full database workflow instructions.
