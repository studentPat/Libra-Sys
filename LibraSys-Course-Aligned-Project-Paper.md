# LibraSys: Course-Aligned Database Project Paper

## 1. System overview

LibraSys is a web-based library management system for one library branch. It manages a catalog of physical books, physical copies, members, borrowing and returning, reservations, fines, payments, reports, and audit records.

The application stack is:

- **Database:** MySQL 8.x using InnoDB
- **Backend:** ASP.NET 8 Core Web API
- **Frontend:** Vanilla HTML, CSS, and JavaScript

The database portion demonstrates the course topics at an appropriate project scale:

1. Transaction management and recovery
2. Database encryption
3. Database authorization and privileges
4. Query optimization

The project demonstrates these topics with executable SQL, controlled test cases, and before-and-after evidence. It does not claim to be a complete enterprise security or disaster-recovery platform.

## 2. Scope and limitations

### Included

- Public catalog search and book details
- Member accounts and profile management
- Book and copy management
- Borrowing, returning, and reservation workflows
- Overdue, lost, and damaged-book fines
- Recording and viewing payments
- Basic operational reports
- Application role-based access control
- MySQL roles, privileges, views, encryption demonstrations, transactions, and query plans
- Audit logging for important business and security events

### Excluded

- Multiple library branches
- Digital books or online reading
- Interlibrary loans and acquisitions
- Automated SMS, email, and push notifications
- Online payment gateways and accounting
- Predictive analytics
- Enterprise key-management infrastructure and multi-region disaster recovery

## 3. Roles and authorization boundary

Application roles are **Guest**, **Member**, **Librarian**, and **DBA**. Application authorization is enforced by ASP.NET authentication and authorization policies. These roles are not a substitute for MySQL accounts.

| Actor | Application access |
|---|---|
| Guest | Search and view public catalog information |
| Member | Manage own profile, borrow/return where permitted, reserve books, view own history and fines |
| Librarian | Manage books, copies, members, loans, reservations, fines, payments, and reports |
| DBA | Database administration and security configuration; not an ordinary library operator |

The API does not connect as a different MySQL account for every member. It uses a restricted application account. MySQL administrative accounts are used only for controlled database administration and demonstrations.

## 4. Relational design

The ERD should use consistent lowercase `snake_case` names and show all cardinalities and foreign keys. The principal tables are:

- `roles(role_id, role_name)`
- `permissions(permission_id, permission_name)`
- `role_permissions(role_id, permission_id)`
- `users(user_id, role_id, username, password_hash, status, created_at, updated_at)`
- `members(member_id, user_id, first_name, last_name, email, contact_info, address_ciphertext, created_at, updated_at)`
- `books(book_id, isbn, title, publisher, publication_year, created_at, updated_at)`
- `authors(author_id, name)`
- `book_authors(book_id, author_id)`
- `book_copies(copy_id, book_id, accession_number, status, condition, created_at, updated_at)`
- `reservations(reservation_id, member_id, book_id, reserved_at, expires_at, status, fulfilled_at, cancelled_at)`
- `borrowings(borrowing_id, copy_id, member_id, borrow_date, due_date, return_date, status, return_condition)`
- `fines(fine_id, borrowing_id, amount, reason, status, created_at, settled_at, waived_at, waived_by)`
- `payments(payment_id, fine_id, amount_paid, payment_date, payment_method, receipt_reference, recorded_by)`
- `transaction_logs(transaction_id, user_id, action, entity_type, entity_id, event_time, success, details)`

Important constraints:

- `username`, `email`, `isbn`, and `accession_number` have appropriate unique constraints.
- Every foreign key has documented `ON DELETE` behavior. Audit, payment, and fine history are not cascade-deleted.
- Amounts use a fixed precision such as `DECIMAL(10,2)` and cannot be negative.
- Status values are restricted by documented lookup values or MySQL `CHECK` constraints.
- Passwords are never stored; only a password hash is stored.
- A copy cannot have more than one active borrowing.
- A member cannot have duplicate active reservations for the same book.
- A fine can have multiple payments, but the total paid cannot exceed the fine amount.
- `borrowings` is the consistent name; the draft name `borrowBook` is removed.

Multiple authors are supported through `authors` and `book_authors` instead of a single free-form author column. If categories are required for the UI, add `categories` and `book_categories` using the same many-to-many pattern.

## 5. Transaction management and recovery

All business tables use InnoDB. The API uses an explicit transaction for each workflow that changes more than one related record. A transaction is committed only when every required change succeeds; otherwise it is rolled back and an error is returned.

### Borrow transaction

1. Begin a transaction.
2. Lock the selected copy with a row lock.
3. Verify that the copy is available.
4. Verify member status, borrowing limit, and duplicate-loan rules.
5. Insert the borrowing row.
6. Update the copy status.
7. Update or fulfill the applicable reservation.
8. Insert an audit log row.
9. Commit.

If any step fails, roll back all changes. The API returns a conflict for an unavailable copy rather than silently selecting another copy.

### Return transaction

1. Begin a transaction.
2. Lock the active borrowing and its copy.
3. Set `return_date`, `return_condition`, and borrowing status.
4. Update the copy status to available, lost, damaged, or another documented state.
5. Create or update the fine exactly once when applicable.
6. Update the reservation queue if applicable.
7. Insert an audit log row.
8. Commit.

### Payment transaction

1. Begin a transaction and lock the fine.
2. Validate that the fine is payable and the amount is positive.
3. Verify that the new total will not exceed the fine amount.
4. Insert the payment.
5. Recalculate the fine status.
6. Insert an audit log row.
7. Commit.

### Course demonstrations and tests

The project demonstrates:

- `START TRANSACTION`, `COMMIT`, and `ROLLBACK`
- `SAVEPOINT` and partial rollback where useful
- Atomicity by forcing an error after one business update
- Consistent results when two sessions attempt to borrow the same copy
- The selected MySQL isolation level and row-lock behavior
- Foreign-key and check-constraint failures
- Deadlock handling with a bounded retry in the API

The project documents that transaction retries must be idempotent and must not create duplicate loans, fines, payments, or audit events. Recovery coverage is limited to rollback and restore procedures appropriate for the course project. A full production RPO/RTO program is out of scope.

## 6. Database encryption

The project distinguishes hashing, transport encryption, at-rest protection, and column protection:

- Passwords use a slow password-hashing library in ASP.NET 8, such as the framework password hasher. They are not reversible-encrypted.
- API-to-MySQL connections use TLS in deployed environments.
- Sensitive member fields, particularly address/contact information, are protected as encrypted application data or with a documented MySQL encryption function.
- Encryption keys are supplied through environment configuration or a secret store and are never committed to source control or stored in the same database table as ciphertext.
- Development keys are separate from deployment keys. Key rotation and loss-of-key behavior are documented.
- Database backups and exports are protected separately; column encryption alone does not encrypt dumps or binary logs.

The demonstration encrypts and decrypts a selected sensitive field, proves that ciphertext is stored rather than plaintext, and proves that a user without the decryption key or permitted application path cannot read it. The paper explicitly states whether encrypted fields can be searched and does not promise ordinary indexing or equality search over randomized ciphertext.

## 7. Database authorization and privileges

The database security design uses least privilege:

| MySQL identity | Privileges |
|---|---|
| `libra_api` | Required CRUD access or approved procedures/views only; no grant administration |
| `libra_report` | Read-only access to reporting views |
| `libra_migration` | Temporary schema-change access during deployment |
| `libra_audit` | Restricted audit insertion/read access if a separate service is used |
| DBA account | Role, schema, grant, view, and security administration |

The project demonstrates MySQL roles and privileges with the selected MySQL version:

- Create roles and users.
- `GRANT` only the required privileges.
- Use `REVOKE` and verify the resulting access.
- Demonstrate `WITH GRANT OPTION` only in an isolated teaching database, never for the API account.
- Demonstrate privilege inheritance and any cascading revoke behavior using version-correct SQL.
- Grant reporting users access to views rather than sensitive base tables.

Application permissions remain in `roles`, `permissions`, and `role_permissions`. MySQL grants protect the database boundary; they do not implement member ownership rules by themselves. The API must enforce that a member can read only the member's own history, fines, and profile. Negative tests include a member requesting another member's data, a librarian attempting DBA operations, and the API account attempting to grant privileges.

## 8. Query optimization

Representative queries are defined before tuning:

- Catalog search and pagination
- Available copies for a book
- A member's active and historical loans
- Overdue loans
- Unpaid fines and payment balances
- Reservation queue
- Monthly borrowing report

Initial indexes include primary keys, unique keys, foreign-key columns, and workload indexes such as:

- `books(title, isbn)`
- `book_copies(book_id, status)`
- `reservations(book_id, status, reserved_at)`
- `borrowings(member_id, status)`
- `borrowings(copy_id, status)`
- `borrowings(due_date, status)`
- `fines(status, created_at)`
- `payments(fine_id, payment_date)`
- `transaction_logs(user_id, event_time)`

The project demonstrates query optimization using:

1. A baseline query and representative data volume.
2. `EXPLAIN` or `EXPLAIN ANALYZE` before the change.
3. A justified index or query rewrite.
4. The execution plan after the change.
5. Before-and-after execution time and rows examined.

The API uses parameterized queries through the selected MySQL EF Core provider or another approved data-access library. Catalog and report endpoints use pagination. The project avoids loading entire tables, unbounded result sets, and non-sargable predicates where a practical alternative exists. The report records the dataset, hardware/environment, repeated-run method, and limitations of the measurement.

## 9. API and UI requirements

The API exposes authenticated endpoints for members and librarians and public read-only catalog endpoints. Every protected endpoint has an authorization policy and validates resource ownership.

Required workflow endpoints include:

- Catalog search and book details
- Member profile and own history
- Librarian book/copy/member management
- Borrow, return, and reserve
- Fine and payment operations
- Reports
- Audit/security administration where permitted

The vanilla frontend provides separate guest, member, librarian, and DBA-facing views as appropriate. It displays validation failures, conflict responses, failed authorization, transaction errors, and payment/fine status without exposing stack traces or sensitive data.

## 10. Testing and evidence

The submission includes:

- ERD and data dictionary
- DDL with keys, constraints, indexes, and seed roles
- Transaction scripts and concurrent-session results
- Rollback and failure demonstrations
- Encryption demonstration without committing secrets
- MySQL role/privilege scripts and negative-access results
- Query plans and tuning comparison tables
- ASP.NET endpoint authorization tests
- Validation of duplicate borrowing, duplicate reservations, overpayment, invalid status changes, and unauthorized data access

## 11. Completion criteria

The project is complete when each database topic has an executable demonstration, expected result, and recorded evidence:

| Topic | Minimum evidence |
|---|---|
| Transactions | Atomic borrow/return/payment workflow, rollback, locking or isolation test |
| Encryption | Hashed password handling, protected sensitive field, key boundary, ciphertext evidence |
| Authorization | Roles, grants, revokes, view/procedure boundary, and denied-access tests |
| Query optimization | Representative query, index/plan analysis, tuning change, and measured comparison |

This scope is sufficient for a course-level database project while remaining implementable with MySQL, ASP.NET 8 Core Web API, and a vanilla frontend.
