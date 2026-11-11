# Database Architecture & Management — Microsoft SQL Server

## 1. Overview
This directory is designated for database scripts, seed datasets, and database maintenance documentation for the Saree and Textile E-Commerce platform.

The application uses **Microsoft SQL Server** as its primary relational database engine, managed primarily through **Entity Framework Core (EF Core 10)** code-first migrations.

## 2. Migration Strategy
- **Source of Truth**: The EF Core `ApplicationDbContext` located in `BE/src/SareeStore.Infrastructure` owns the entity mappings, model configurations, and migration history.
- **Migration Commands**: Migrations are generated and applied via the .NET CLI targeting the `SareeStore.Infrastructure` project with `SareeStore.Api` as the startup project:
  ```bash
  # Add a new migration (run from BE/ directory)
  dotnet ef migrations add <MigrationName> --project src/SareeStore.Infrastructure --startup-project src/SareeStore.Api

  # Update database to latest migration
  dotnet ef database update --project src/SareeStore.Infrastructure --startup-project src/SareeStore.Api

  # Generate idempotent SQL script from migrations
  dotnet ef migrations script --idempotent --project src/SareeStore.Infrastructure --startup-project src/SareeStore.Api -o ../Database/Scripts/migrations.sql
  ```

## 3. Schema & Business Entities
In accordance with initial phase instructions, no business entities or final schemas are hard-coded at this stage. Entity definitions, relationships (e.g. Products, Categories, Collections, Weave Types, Fabrics, Inventory, Orders, Customers), constraints, and performance indexes will be introduced in subsequent phases following confirmation of business requirements.

## 4. Environment Configuration
Connection strings are configured through application configuration (`appsettings.json` / `appsettings.Development.json`) and environment variables / user-secrets:
```text
Server=localhost;Database=SareeStoreDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```
Production deployments should utilize secure environment secrets or Azure Key Vault rather than committed configuration files.
