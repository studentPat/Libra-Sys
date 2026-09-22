USE librasys;

INSERT INTO roles (role_name) VALUES
('Guest'), ('Member'), ('Librarian'), ('DBA');

INSERT INTO permissions (permission_name) VALUES
('catalog.read'), ('profile.own.read'), ('profile.own.update'),
('borrowing.create'), ('borrowing.return'), ('reservation.manage'),
('books.manage'), ('copies.manage'), ('members.manage'),
('fines.manage'), ('payments.manage'), ('reports.read'),
('audit.read'), ('database.security.manage');

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.role_id, p.permission_id
FROM roles r CROSS JOIN permissions p
WHERE (r.role_name = 'Guest' AND p.permission_name = 'catalog.read')
   OR (r.role_name = 'Member' AND p.permission_name IN
       ('catalog.read', 'profile.own.read', 'profile.own.update',
        'borrowing.create', 'borrowing.return', 'reservation.manage'))
   OR (r.role_name = 'Librarian' AND p.permission_name IN
       ('catalog.read', 'books.manage', 'copies.manage', 'members.manage',
        'fines.manage', 'payments.manage', 'reports.read', 'audit.read'))
   OR (r.role_name = 'DBA' AND p.permission_name = 'database.security.manage');

-- These are placeholders, not usable credentials. Replace with ASP.NET-generated
-- password hashes during local setup; never store plaintext passwords in SQL.
INSERT INTO users (role_id, username, password_hash)
SELECT role_id, 'demo_librarian', '$DEMO_HASH_REPLACE_IN_LOCAL_SETUP'
FROM roles WHERE role_name = 'Librarian';

INSERT INTO users (role_id, username, password_hash)
SELECT role_id, 'demo_member', '$DEMO_HASH_REPLACE_IN_LOCAL_SETUP'
FROM roles WHERE role_name = 'Member';

INSERT INTO members (user_id, first_name, last_name, email, contact_info)
SELECT user_id, 'Demo', 'Member', 'demo.member@example.invalid', 'not-a-real-contact'
FROM users WHERE username = 'demo_member';

INSERT INTO authors (name) VALUES
('Robert C. Martin'), ('Martin Fowler'), ('Andrew Hunt');

INSERT INTO categories (category_name) VALUES
('Software Engineering'), ('Reference'), ('Programming');

INSERT INTO books (isbn, title, publisher, publication_year) VALUES
('9780132350884', 'Clean Code', 'Prentice Hall', 2008),
('9780201485677', 'Refactoring', 'Addison-Wesley', 1999),
('9780135957059', 'The Pragmatic Programmer', 'Addison-Wesley', 2019);

INSERT INTO book_authors (book_id, author_id)
SELECT b.book_id, a.author_id FROM books b JOIN authors a ON
    (b.isbn = '9780132350884' AND a.name = 'Robert C. Martin')
 OR (b.isbn = '9780201485677' AND a.name = 'Martin Fowler')
 OR (b.isbn = '9780135957059' AND a.name = 'Andrew Hunt');

INSERT INTO book_categories (book_id, category_id)
SELECT b.book_id, c.category_id FROM books b JOIN categories c ON
    (b.isbn IN ('9780132350884', '9780201485677') AND c.category_name = 'Software Engineering')
 OR (b.isbn = '9780135957059' AND c.category_name IN ('Reference', 'Programming'));

INSERT INTO book_copies (book_id, accession_number, status, item_condition)
SELECT book_id, CONCAT('ACC-', LPAD(book_id, 4, '0'), '-01'), 'available', 'good' FROM books;
