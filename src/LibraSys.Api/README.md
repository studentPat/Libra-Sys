# LibraSys API

The first ASP.NET 8 backend slice provides:

- `GET /api/health`
- `GET /api/catalog?search=&page=1&pageSize=20`
- `GET /api/catalog/{bookId}`
- `POST /api/auth/login`
- `GET /api/auth/me`
- `GET /api/member/profile`
- `PUT /api/member/profile`
- `GET /api/member/borrowings`
- `POST /api/member/borrowings`
- `POST /api/member/borrowings/{borrowingId}/return`
- `GET /api/member/reservations`
- `POST /api/member/reservations`
- `DELETE /api/member/reservations/{reservationId}`
- `GET /api/member/fines`
- `GET /api/member/payments`
- `POST /api/librarian/fines`
- `POST /api/librarian/payments`
- `POST /api/librarian/catalog/books`
- `POST /api/librarian/catalog/copies`
- `PUT /api/librarian/catalog/books/{bookId}`
- `POST /api/librarian/catalog/copies/{copyId}/retire`
- `GET /api/librarian/members?search=&status=&page=1&pageSize=20`
- `PUT /api/librarian/members/{memberId}/status`
- `GET /api/librarian/reports/member-balances`
- `GET /api/librarian/reports/catalog-availability?search=&page=1&pageSize=20`
- `GET /api/librarian/reports/audit-logs?action=&entityType=&page=1&pageSize=20`
- `POST /api/librarian/reservations/{reservationId}/ready`
- `POST /api/librarian/reservations/{reservationId}/fulfill`
- `POST /api/librarian/reservations/{reservationId}/expire`
- `POST /api/librarian/reservations/expire-due`

## Local configuration

Do not commit a MySQL password or connection string. Configure the connection string with user secrets:

```powershell
dotnet user-secrets init --project src\LibraSys.Api
dotnet user-secrets set "Database:ConnectionString" "Server=localhost;Port=3306;Database=librasys;User ID=librasys_api;Password=YOUR_LOCAL_PASSWORD;" --project src\LibraSys.Api
dotnet user-secrets set "Jwt:SigningKey" "GENERATE_A_LOCAL_RANDOM_KEY_WITH_AT_LEAST_32_CHARACTERS" --project src\LibraSys.Api
```

Use the restricted `librasys_api` account from `database/03_security.sql`, not the DBA account.

## Run

```powershell
dotnet run --project src\LibraSys.Api
```

The health endpoint works without a database connection. Catalog endpoints require the validated `librasys` database and report a configuration error if no connection string is configured.

The login endpoint verifies ASP.NET password hashes stored in `users.password_hash` and issues a short-lived JWT containing the application role. Do not commit the JWT key or passwords. Member profile, borrowing, reservation, fine, and payment endpoints require a Member JWT and always scope queries to the authenticated user. Borrow, return, and reservation mutations use explicit MySQL transactions and row locks. Members can view financial records but cannot modify fines or payments. Librarians can create fines, record payments, add books, and add physical copies through protected transactional endpoints.
