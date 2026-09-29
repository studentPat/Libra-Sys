# LibraSys

Course-aligned database foundation for the secure library-management system.

The first implementation phase is database-first. See [`database/README.md`](database/README.md) for MySQL 8.x setup and the demonstration scripts for transactions, encryption, authorization, and query optimization.

The first ASP.NET 8 API slice is in [`src/LibraSys.Api`](src/LibraSys.Api/README.md) and provides health and public catalog endpoints. Member/librarian workflows and the vanilla HTML/CSS/JavaScript frontend are planned follow-on phases. No credentials, encryption keys, or production secrets are included in this repository.