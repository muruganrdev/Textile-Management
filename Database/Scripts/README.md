# Database SQL Scripts

## Purpose
This folder is intended for auxiliary SQL scripts, including:
- **Baseline Scripts**: Manual environment provisioning scripts or local developer setup scripts.
- **Idempotent Migration Scripts**: Generated SQL scripts produced by `dotnet ef migrations script` for staging and production database releases where direct CLI execution is restricted.
- **Seed Scripts**: Initial master catalog data, regional weave categories (e.g., Kanchipuram, Banarasi, Chanderi, Tussar), and reference data.
- **Maintenance & Diagnostics**: Performance tuning scripts, index reorganization, and data consistency verification queries.

## Guidelines
- All custom SQL scripts must be idempotent (safe to run multiple times without causing errors or duplicating data).
- Never commit hardcoded production credentials or sensitive data in SQL scripts.
- Schema modifications should be generated through EF Core migrations to keep C# domain models in lockstep with SQL Server tables.
