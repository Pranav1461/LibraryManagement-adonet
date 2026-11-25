# LibraDesk — Library Management System

A full-stack library management web application built with **C# (ASP.NET Core Web API)**, **raw ADO.NET (`Microsoft.Data.SqlClient`)**, **SQL Server**, **`IMemoryCache`**, and a **Bootstrap 5 + Chart.js** frontend.

No ORMs (no Entity Framework, no Dapper) are used — every query runs directly through `SqlConnection`, `SqlCommand`, `SqlDataReader`, `SqlDataAdapter`, `DataSet`/`DataTable`, and `SqlTransaction`.

> **Before You Start:** You must install the **.NET 8.0 SDK** and **Microsoft SQL Server** (plus `sqlcmd`) first. Once installed, you can run the project either way:
> - **Easy Installation (`setup.bat`)** → [Section 14](#14-easy-installation-setupbat)
> - **Full Manual Installation** → [Sections 15–17](#15-full-installation--step-1-database-setup)

---

## 1. What This Project Does

LibraDesk manages book titles separately from physical copies (`AccessionNumber`), tracks authors (many-to-many), categories, publishers, member registrations and expiry dates, borrowing/return transactions, overdue fines, loan renewals, reservation queues, and exportable library reports.

## 2. Core Concepts Demonstrated

- **Connected ADO.NET**: `SqlConnection`, `SqlCommand`, and `SqlDataReader` (`ExecuteReaderAsync`, `ExecuteScalarAsync`, `ExecuteNonQueryAsync`) for CRUD and lookups.
- **Disconnected ADO.NET**: `SqlDataAdapter` filling `DataSet` and `DataTable` objects for the dashboard and all 12 reports, closing the connection immediately after `Fill()`.
- **SQL Transactions**: `SqlTransaction` (`BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`) for issuing copies, returning copies with fine calculation, renewing loans, creating books with authors/copies, and bulk copy inserts.
- **Concurrency-Safe Issue**: Atomic copy claim (`UPDATE dbo.BookCopies SET CopyStatus = 'Borrowed' ... WHERE CopyStatus = 'Available'`) plus a filtered unique index (`UX_Loans_ActiveCopy` on `Loans(CopyId) WHERE ReturnDate IS NULL`) so simultaneous requests cannot issue the same physical copy twice.
- **In-Memory Caching**: Configurable `IMemoryCache` with per-group `CancellationChangeToken` invalidation on writes.

## 3. Tech Stack

- **Backend**: C# / .NET 8, ASP.NET Core Web API, `Microsoft.Data.SqlClient` (7.1.2), `IMemoryCache`, Swashbuckle (Swagger)
- **Database**: Microsoft SQL Server (10 tables, foreign keys, check constraints, unique constraints, filtered index)
- **Frontend**: HTML5, CSS3, ES Module JavaScript, Bootstrap 5.3.3, Bootstrap Icons 1.11.3, Chart.js 4.4.3
- **Tests**: xUnit

## 4. Features

- **Dashboard**: Real-time counts (titles, total/available/borrowed copies, registered/active members, overdue loans, due today, total borrowings/returns), Monthly Borrowing & Returns chart, Most Borrowed Books chart, Books by Category chart, Overdue Alerts, Due Soon table, Recent Returns, and Recent Borrowing Activity.
- **Books**: Full CRUD, multi-author linking, optional initial copies, search by title/ISBN/author, category/publisher filters, sorting, pagination, and copy list detail view. Soft-deactivates (`IsActive = 0`) books with loan history.
- **Authors, Categories & Publishers**: Full CRUD, search, reference guards on delete, and "View Books" lookups for authors and publishers.
- **Physical Book Copies**: Individual copy tracking by `AccessionNumber`, shelf location, status (`Available`, `Borrowed`, `Reserved`, `Lost`, `Damaged`, `Inactive`), single & bulk transactional creation, and per-copy borrowing history.
- **Members**: Registration, editing, search, status filtering (`Active`, `Inactive`, `Expired`), current/overdue/full loan history view, and safe inactivation when loan history exists.
- **Issue, Return & Renewals**: Configurable borrowing period (default 14 days), active loan limit (default 3), daily overdue fine rate (default ₹5/day), max renewals (default 2), renewal audit history (`LoanRenewals`), and reservation queue (`Reservations`).
- **Reports & Analytics**: 12 disconnected ADO.NET reports with filters, pagination, summary totals, and CSV export (`?format=csv`).

## 5. Project Architecture

- **`LibraryManagement.Models`**: Entities, `LibraryPolicySettings`, validated request DTOs, and response/report DTOs.
- **`LibraryManagement.Data`**: `SqlConnectionFactory`, domain exceptions, repository interfaces, `AdoHelper`, and 10 ADO.NET repositories.
- **`LibraryManagement.API`**: REST controllers, `MemoryCacheService`, `GlobalExceptionMiddleware`, CORS, and Swagger.
- **`LibraryManagement.Web`**: 12-page Bootstrap 5 frontend connected directly to the API.

## 6. Database Design

Tables created in `LibraryManagementDB`:
1. `Categories` — unique `CategoryName`
2. `Publishers` — unique `PublisherName`
3. `Authors` — author profiles
4. `Books` — unique `ISBN`, FKs to `Categories` and `Publishers`, `IsActive`
5. `BookAuthors` — composite PK `(BookId, AuthorId)` many-to-many junction
6. `BookCopies` — unique `AccessionNumber`, FK to `Books`, `CopyStatus` check constraint
7. `Members` — unique `MembershipNumber` and `Email`, `MembershipStatus` check constraint
8. `Loans` — FKs to `Members` and `BookCopies`, filtered unique index `UX_Loans_ActiveCopy` (`WHERE ReturnDate IS NULL`), `FineAmount >= 0`
9. `LoanRenewals` — FK to `Loans`, tracks `PreviousDueDate` → `NewDueDate`
10. `Reservations` — FKs to `Members` and `Books` (`Pending`, `Fulfilled`, `Cancelled`)

## 7. Connected ADO.NET (`Book`, `Author`, `Category`, `Publisher`, `BookCopy`, `Member`, `Reservation`)

Uses `SqlConnection`, `SqlCommand`, and `SqlDataReader` inside `await using` blocks with parameterized SQL:
- `ExecuteReaderAsync()` for paginated lists and detail views.
- `ExecuteScalarAsync()` for `COUNT(1)` totals, validation checks, and `OUTPUT INSERTED.*Id`.
- `ExecuteNonQueryAsync()` for updates and deletes.

## 8. Disconnected ADO.NET (`ReportRepository` & `DashboardRepository`)

Uses `AdoHelper.FillDataTable` and `AdoHelper.FillDataSet` with `SqlDataAdapter.Fill(...)`. The adapter fetches rows into memory and closes the connection right away; repositories then build DTOs, page results, and compute summary totals from `DataTable.Rows`.

## 9. SQL Transactions (`LoanRepository`, `BookRepository`, `BookCopyRepository`)

- **Issue (`LoanRepository.IssueBookAsync`)**: Runs at `IsolationLevel.Serializable`. Checks member status/expiry/limit, atomically flips `BookCopies.CopyStatus` from `Available` to `Borrowed`, inserts `Loans`, and fulfills any matching pending reservation before committing.
- **Return (`LoanRepository.ReturnBookAsync`)**: Verifies the loan is unreturned, computes overdue days and `FineAmount` (`overdueDays * DailyFineRate`), updates `Loans`, and resets `BookCopies.CopyStatus` inside one `SqlTransaction`.
- **Renew (`LoanRepository.RenewLoanAsync`)**: Validates active, non-overdue status and renewal limit, updates `DueDate`/`RenewalCount`, and logs `LoanRenewals` together.
- **Book + Authors / Bulk Copies**: Inserts parent and child rows atomically.

## 10. Web API & Error Handling

`GlobalExceptionMiddleware` maps domain exceptions cleanly without leaking SQL or stack traces:
- `400 Bad Request` — Model validation errors
- `404 Not Found` — Missing ID or loan
- `409 Conflict` — Duplicate ISBN/accession/membership number or delete guard
- `422 Unprocessable Entity` — Business rule violation (expired member, borrowing limit, unavailable copy, overdue renewal)
- `500 Internal Server Error` — Safe generic error message

## 11. Caching (`IMemoryCache`)

Read endpoints (`GET /api/dashboard`, `GET /api/books`, `GET /api/categories`, `GET /api/authors`, `GET /api/publishers`, `GET /api/reports/*`) cache responses with timeouts configured under `CacheSettings` in `appsettings.json` and return `X-Cache: HIT` or `X-Cache: MISS`. Mutations invalidate affected cache groups via `CancellationChangeToken`. Copy availability during issue is always checked live inside SQL Server.

## 12. Folder Structure

```text
LibraryManagement/
├── LibraryManagement.sln
├── LibraryManagement.API/
│   ├── Configuration/CacheOptions.cs
│   ├── Controllers/
│   ├── Middleware/GlobalExceptionMiddleware.cs
│   ├── Properties/launchSettings.json
│   ├── Services/CacheService.cs
│   ├── appsettings.json
│   └── Program.cs
├── LibraryManagement.Data/
│   ├── Connection/SqlConnectionFactory.cs
│   ├── Exceptions/DomainExceptions.cs
│   ├── Interfaces/IRepositories.cs
│   └── Repositories/
├── LibraryManagement.Models/
│   ├── Configuration/LibraryPolicySettings.cs
│   ├── Entities/Entities.cs
│   ├── Requests/LibraryRequests.cs
│   └── Responses/LibraryResponses.cs
├── LibraryManagement.Web/
│   ├── index.html
│   ├── pages/
│   ├── css/styles.css
│   └── js/
├── LibraryManagement.Tests/
├── Database/
│   ├── 01_CreateDatabase.sql
│   ├── 02_CreateTables.sql
│   ├── 03_ConstraintsAndIndexes.sql
│   └── 04_SampleData.sql
├── setup.bat
└── README.md
```

---

## 13. Required Software (Must Be Installed First)

Both **Easy Installation (`setup.bat`)** and **Full Manual Installation** require **.NET 8.0 SDK** and **Microsoft SQL Server** first.

Open **PowerShell as Administrator** and install:

1. **.NET 8.0 SDK**:
   ```powershell
   winget install --id Microsoft.DotNet.SDK.8 --exact --accept-package-agreements --accept-source-agreements
   ```
   - Manual download: https://dotnet.microsoft.com/en-us/download/dotnet/8.0

2. **Microsoft SQL Server 2022 Express**:
   ```powershell
   winget install --id Microsoft.SQLServer.2022.Express --exact --accept-package-agreements --accept-source-agreements
   ```
   - Manual download: https://www.microsoft.com/en-us/sql-server/sql-server-downloads *(select **Basic**)*.

3. **SQL Command Line Tools (`sqlcmd`)**:
   ```powershell
   winget install --id Microsoft.Sqlcmd --exact --accept-package-agreements --accept-source-agreements
   ```
   - Docs & download: https://learn.microsoft.com/en-us/sql/tools/sqlcmd/sqlcmd-utility

4. **Local Web Server** *(Python 3 `http.server`, Node.js `npx serve`, or VS Code Live Server)*.

### Windows 11 NVMe SSD Note (`0x851A001A`)

If SQL Server 2022 Express fails on Windows 11 with `"Wait on the Database Engine recovery handle failed"` / `"256 misaligned log IOs"`, run this in **PowerShell as Administrator**, reboot, remove the failed SQL instance under **Settings → Apps**, and reinstall:

```powershell
New-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Services\stornvme\Parameters\Device" -Force | Out-Null
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\stornvme\Parameters\Device" -Name "ForcedPhysicalSectorSizeInBytes" -PropertyType MultiString -Force -Value "* 4095"
Restart-Computer
```

---

## 14. Easy Installation (`setup.bat`)

Once `.NET 8.0 SDK`, `SQL Server`, and `sqlcmd` are installed, double-click [setup.bat](setup.bat) or run:

```cmd
cd /d b:\.NET\LibraryManagement
setup.bat
```

`setup.bat` checks prerequisites, initializes `LibraryManagementDB` if needed (use `setup.bat --reset-db` to reset and re-seed), runs `dotnet restore`, `dotnet build`, and `dotnet test`, launches the API (`http://localhost:5056`) and Frontend (`http://localhost:5506`), and opens `http://localhost:5506/index.html`.

---

## 15. Full Installation — Step 1: Database Setup

```powershell
cd b:\.NET\LibraryManagement\Database
sqlcmd -S "lpc:.\SQLEXPRESS" -E -C -b -i 01_CreateDatabase.sql
sqlcmd -S "lpc:.\SQLEXPRESS" -E -C -b -i 02_CreateTables.sql
sqlcmd -S "lpc:.\SQLEXPRESS" -E -C -b -i 03_ConstraintsAndIndexes.sql
sqlcmd -S "lpc:.\SQLEXPRESS" -E -C -b -i 04_SampleData.sql
```

*(Use `-S localhost` for a default SQL Server instance, or `-S ".\SQLEXPRESS"` if TCP/IP + SQL Browser are enabled.)*

---

## 16. Full Installation — Step 2: Configuration

`LibraryManagement.API\appsettings.json` defaults to `Server=.\SQLEXPRESS;Database=LibraryManagementDB;Trusted_Connection=True;TrustServerCertificate=True;`. To override via User Secrets:

```powershell
cd b:\.NET\LibraryManagement\LibraryManagement.API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=LibraryManagementDB;Trusted_Connection=True;TrustServerCertificate=True;"
```

Library policy defaults (`LibraryPolicy` in `appsettings.json`):
- `DefaultBorrowingDays`: `14`
- `MaxActiveLoansPerMember`: `3`
- `DailyFineRate`: `5.00` (₹5/day)
- `MaxRenewals`: `2`
- `DefaultRenewalDays`: `7`

---

## 17. Full Installation — Step 3: Build & Run

**Backend API:**
```powershell
cd b:\.NET\LibraryManagement
dotnet restore
dotnet build
dotnet run --project LibraryManagement.API --launch-profile http
```

- API Base URL: `http://localhost:5056/api`
- Swagger UI: `http://localhost:5056/swagger`

**Frontend (in a second terminal):**
```powershell
cd b:\.NET\LibraryManagement\LibraryManagement.Web
python -m http.server 5506
```

Open `http://localhost:5506/index.html`.

---

## 18. API Endpoints

| Module | Method | Route | Description |
|---|---|---|---|
| Dashboard | `GET` | `/api/dashboard` | Summary KPIs, charts, overdue/due-soon alerts |
| Dashboard | `GET` | `/api/dashboard/policy` | Configured borrowing period, limit, fine rate |
| Dashboard | `POST` | `/api/dashboard/cache/clear` | Clears `IMemoryCache` |
| Books | `GET` | `/api/books` | Paged, searchable, filterable, sortable books |
| Books | `GET` | `/api/books/{id}` | Book details + physical copies |
| Books | `POST` | `/api/books` | Create book + authors (+ optional initial copies) |
| Books | `PUT` | `/api/books/{id}` | Update book and author links |
| Books | `DELETE` | `/api/books/{id}` | Delete or deactivate if referenced |
| Authors | `GET/POST/PUT/DELETE` | `/api/authors` | Author CRUD + `GET /api/authors/{id}/books` |
| Categories | `GET/POST/PUT/DELETE` | `/api/categories` | Category CRUD with assigned-book guard |
| Publishers | `GET/POST/PUT/DELETE` | `/api/publishers` | Publisher CRUD + `GET /api/publishers/{id}/books` |
| Book Copies | `GET/POST/PUT` | `/api/bookcopies` | Copy CRUD, history, and `POST /api/bookcopies/bulk` |
| Members | `GET/POST/PUT/DELETE` | `/api/members` | Member CRUD + current/overdue/full history |
| Loans | `GET` | `/api/loans` | Filterable/paged loans (`GET /api/loans/{id}` with renewals) |
| Loans | `POST` | `/api/loans/issue` | Issue copy atomically in `SqlTransaction` |
| Loans | `POST` | `/api/loans/return` | Return copy, compute fine, update status atomically |
| Loans | `POST` | `/api/loans/{id}/renew` | Renew due date and log `LoanRenewals` |
| Reservations | `GET/POST/PUT` | `/api/reservations` | Queue list, create, `/cancel`, `/fulfill` |
| Reports | `GET` | `/api/reports/{name}` | 12 disconnected reports (`?format=csv` supported) |

---

## 19. Running Tests

Unit tests:
```powershell
cd b:\.NET\LibraryManagement
dotnet test
```

Full integration tests against `LibraryManagementDB`:
```powershell
$env:LMS_RUN_DB_TESTS = "true"
dotnet test
```

---

## 20. Screenshots

- **Dashboard**: `docs/screenshots/dashboard.png`
- **Books & Copies**: `docs/screenshots/books.png`
- **Issue & Return**: `docs/screenshots/circulation.png`
- **Reports**: `docs/screenshots/reports.png`
