# LibraSys API

The first ASP.NET 8 backend slice provides:

- `GET /api/health`
- `GET /api/catalog?search=&page=1&pageSize=20`
- `GET /api/catalog/{bookId}`

## Local configuration

Do not commit a MySQL password or connection string. Configure the connection string with user secrets:

```powershell
dotnet user-secrets init --project src\LibraSys.Api
dotnet user-secrets set "Database:ConnectionString" "Server=localhost;Port=3306;Database=librasys;User ID=librasys_api;Password=YOUR_LOCAL_PASSWORD;" --project src\LibraSys.Api
```

Use the restricted `librasys_api` account from `database/03_security.sql`, not the DBA account.

## Run

```powershell
dotnet run --project src\LibraSys.Api
```

The health endpoint works without a database connection. Catalog endpoints require the validated `librasys` database and report a configuration error if no connection string is configured.

Authentication and role policies will be added before member and librarian endpoints are exposed. The current public catalog slice intentionally does not expose personal or administrative data.
