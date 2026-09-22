CREATE DATABASE IF NOT EXISTS librasys
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_0900_ai_ci;

USE librasys;

SET FOREIGN_KEY_CHECKS = 0;
DROP VIEW IF EXISTS v_member_balances;
DROP VIEW IF EXISTS v_available_catalog;
DROP TABLE IF EXISTS transaction_logs;
DROP TABLE IF EXISTS payments;
DROP TABLE IF EXISTS fines;
DROP TABLE IF EXISTS borrowings;
DROP TABLE IF EXISTS reservations;
DROP TABLE IF EXISTS book_copies;
DROP TABLE IF EXISTS book_categories;
DROP TABLE IF EXISTS categories;
DROP TABLE IF EXISTS book_authors;
DROP TABLE IF EXISTS authors;
DROP TABLE IF EXISTS books;
DROP TABLE IF EXISTS members;
DROP TABLE IF EXISTS role_permissions;
DROP TABLE IF EXISTS permissions;
DROP TABLE IF EXISTS users;
DROP TABLE IF EXISTS roles;
SET FOREIGN_KEY_CHECKS = 1;

CREATE TABLE roles (
    role_id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    role_name VARCHAR(40) NOT NULL UNIQUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE = InnoDB;

CREATE TABLE users (
    user_id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    role_id INT UNSIGNED NOT NULL,
    username VARCHAR(80) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    status ENUM('active', 'locked', 'disabled') NOT NULL DEFAULT 'active',
    failed_login_count SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    locked_until DATETIME NULL,
    password_changed_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_users_role FOREIGN KEY (role_id) REFERENCES roles(role_id),
    CONSTRAINT chk_users_failed_login CHECK (failed_login_count >= 0)
) ENGINE = InnoDB;

CREATE TABLE permissions (
    permission_id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    permission_name VARCHAR(80) NOT NULL UNIQUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE = InnoDB;

CREATE TABLE role_permissions (
    role_id INT UNSIGNED NOT NULL,
    permission_id INT UNSIGNED NOT NULL,
    PRIMARY KEY (role_id, permission_id),
    CONSTRAINT fk_role_permissions_role FOREIGN KEY (role_id) REFERENCES roles(role_id),
    CONSTRAINT fk_role_permissions_permission FOREIGN KEY (permission_id) REFERENCES permissions(permission_id)
) ENGINE = InnoDB;

CREATE TABLE members (
    member_id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT UNSIGNED NOT NULL UNIQUE,
    first_name VARCHAR(80) NOT NULL,
    last_name VARCHAR(80) NOT NULL,
    email VARCHAR(255) NOT NULL UNIQUE,
    contact_info VARCHAR(100) NULL,
    address_ciphertext VARBINARY(1024) NULL,
    member_status ENUM('active', 'suspended', 'closed') NOT NULL DEFAULT 'active',
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_members_user FOREIGN KEY (user_id) REFERENCES users(user_id),
    INDEX ix_members_name (last_name, first_name)
) ENGINE = InnoDB;

CREATE TABLE books (
    book_id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    isbn VARCHAR(20) NOT NULL UNIQUE,
    title VARCHAR(255) NOT NULL,
    publisher VARCHAR(255) NULL,
    publication_year YEAR NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX ix_books_title (title),
    FULLTEXT INDEX ftx_books_title (title)
) ENGINE = InnoDB;

CREATE TABLE authors (
    author_id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(180) NOT NULL,
    UNIQUE KEY uq_authors_name (name)
) ENGINE = InnoDB;

CREATE TABLE book_authors (
    book_id BIGINT UNSIGNED NOT NULL,
    author_id BIGINT UNSIGNED NOT NULL,
    PRIMARY KEY (book_id, author_id),
    CONSTRAINT fk_book_authors_book FOREIGN KEY (book_id) REFERENCES books(book_id),
    CONSTRAINT fk_book_authors_author FOREIGN KEY (author_id) REFERENCES authors(author_id)
) ENGINE = InnoDB;

CREATE TABLE categories (
    category_id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    category_name VARCHAR(100) NOT NULL UNIQUE
) ENGINE = InnoDB;

CREATE TABLE book_categories (
    book_id BIGINT UNSIGNED NOT NULL,
    category_id INT UNSIGNED NOT NULL,
    PRIMARY KEY (book_id, category_id),
    CONSTRAINT fk_book_categories_book FOREIGN KEY (book_id) REFERENCES books(book_id),
    CONSTRAINT fk_book_categories_category FOREIGN KEY (category_id) REFERENCES categories(category_id)
) ENGINE = InnoDB;

CREATE TABLE book_copies (
    copy_id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    book_id BIGINT UNSIGNED NOT NULL,
    accession_number VARCHAR(40) NOT NULL UNIQUE,
    status ENUM('available', 'borrowed', 'reserved', 'lost', 'damaged', 'retired') NOT NULL DEFAULT 'available',
    item_condition ENUM('new', 'good', 'fair', 'damaged') NOT NULL DEFAULT 'good',
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_book_copies_book FOREIGN KEY (book_id) REFERENCES books(book_id),
    INDEX ix_book_copies_book_status (book_id, status)
) ENGINE = InnoDB;

CREATE TABLE reservations (
    reservation_id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    member_id BIGINT UNSIGNED NOT NULL,
    book_id BIGINT UNSIGNED NOT NULL,
    reserved_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    expires_at DATETIME NULL,
    status ENUM('queued', 'ready', 'fulfilled', 'cancelled', 'expired') NOT NULL DEFAULT 'queued',
    fulfilled_at DATETIME NULL,
    cancelled_at DATETIME NULL,
    CONSTRAINT fk_reservations_member FOREIGN KEY (member_id) REFERENCES members(member_id),
    CONSTRAINT fk_reservations_book FOREIGN KEY (book_id) REFERENCES books(book_id),
    CONSTRAINT uq_active_reservation UNIQUE (member_id, book_id, status),
    INDEX ix_reservations_queue (book_id, status, reserved_at)
) ENGINE = InnoDB;

CREATE TABLE borrowings (
    borrowing_id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    copy_id BIGINT UNSIGNED NOT NULL,
    member_id BIGINT UNSIGNED NOT NULL,
    borrow_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    due_date DATETIME NOT NULL,
    return_date DATETIME NULL,
    status ENUM('active', 'returned', 'lost', 'cancelled') NOT NULL DEFAULT 'active',
    return_condition ENUM('new', 'good', 'fair', 'damaged') NULL,
    active_copy_id BIGINT UNSIGNED
        GENERATED ALWAYS AS (IF(status = 'active', copy_id, NULL)) STORED,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_borrowings_copy FOREIGN KEY (copy_id) REFERENCES book_copies(copy_id),
    CONSTRAINT fk_borrowings_member FOREIGN KEY (member_id) REFERENCES members(member_id),
    CONSTRAINT chk_borrowings_dates CHECK (due_date >= borrow_date),
    UNIQUE KEY uq_borrowings_one_active_copy (active_copy_id),
    INDEX ix_borrowings_member_status (member_id, status),
    INDEX ix_borrowings_copy_status (copy_id, status),
    INDEX ix_borrowings_due_status (due_date, status)
) ENGINE = InnoDB;

CREATE TABLE fines (
    fine_id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    borrowing_id BIGINT UNSIGNED NOT NULL,
    amount DECIMAL(10, 2) NOT NULL,
    reason ENUM('overdue', 'lost', 'damaged', 'other') NOT NULL,
    status ENUM('unpaid', 'partially_paid', 'paid', 'waived') NOT NULL DEFAULT 'unpaid',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    settled_at DATETIME NULL,
    waived_at DATETIME NULL,
    waived_by BIGINT UNSIGNED NULL,
    CONSTRAINT fk_fines_borrowing FOREIGN KEY (borrowing_id) REFERENCES borrowings(borrowing_id),
    CONSTRAINT fk_fines_waived_by FOREIGN KEY (waived_by) REFERENCES users(user_id),
    CONSTRAINT chk_fines_amount CHECK (amount > 0),
    UNIQUE KEY uq_fines_borrowing_reason (borrowing_id, reason),
    INDEX ix_fines_status_created (status, created_at)
) ENGINE = InnoDB;

CREATE TABLE payments (
    payment_id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    fine_id BIGINT UNSIGNED NOT NULL,
    amount_paid DECIMAL(10, 2) NOT NULL,
    payment_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    payment_method ENUM('cash', 'bank_transfer', 'other') NOT NULL,
    receipt_reference VARCHAR(80) NOT NULL UNIQUE,
    recorded_by BIGINT UNSIGNED NOT NULL,
    CONSTRAINT fk_payments_fine FOREIGN KEY (fine_id) REFERENCES fines(fine_id),
    CONSTRAINT fk_payments_recorded_by FOREIGN KEY (recorded_by) REFERENCES users(user_id),
    CONSTRAINT chk_payments_amount CHECK (amount_paid > 0),
    INDEX ix_payments_fine_date (fine_id, payment_date)
) ENGINE = InnoDB;

DELIMITER //
CREATE TRIGGER trg_payments_no_overpayment
BEFORE INSERT ON payments
FOR EACH ROW
BEGIN
    DECLARE existing_paid DECIMAL(10, 2);
    DECLARE fine_amount DECIMAL(10, 2);

    SELECT amount INTO fine_amount
    FROM fines
    WHERE fine_id = NEW.fine_id
    FOR UPDATE;

    SELECT COALESCE(SUM(amount_paid), 0.00) INTO existing_paid
    FROM payments
    WHERE fine_id = NEW.fine_id;

    IF existing_paid + NEW.amount_paid > fine_amount THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Payment exceeds the remaining fine balance';
    END IF;
END//
DELIMITER ;

CREATE TABLE transaction_logs (
    transaction_id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT UNSIGNED NULL,
    action VARCHAR(80) NOT NULL,
    entity_type VARCHAR(80) NOT NULL,
    entity_id BIGINT UNSIGNED NULL,
    event_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    success BOOLEAN NOT NULL DEFAULT TRUE,
    details JSON NULL,
    CONSTRAINT fk_transaction_logs_user FOREIGN KEY (user_id) REFERENCES users(user_id),
    INDEX ix_transaction_logs_user_time (user_id, event_time),
    INDEX ix_transaction_logs_entity_time (entity_type, entity_id, event_time)
) ENGINE = InnoDB;

CREATE VIEW v_available_catalog AS
SELECT b.book_id, b.isbn, b.title, b.publisher, b.publication_year,
       COUNT(bc.copy_id) AS available_copy_count
FROM books b
LEFT JOIN book_copies bc
  ON bc.book_id = b.book_id AND bc.status = 'available'
GROUP BY b.book_id, b.isbn, b.title, b.publisher, b.publication_year;

CREATE VIEW v_member_balances AS
SELECT m.member_id, m.first_name, m.last_name,
       COALESCE(SUM(f.amount), 0.00) AS assessed_fines,
       COALESCE(SUM(p.paid_amount), 0.00) AS paid_fines,
       COALESCE(SUM(f.amount), 0.00) - COALESCE(SUM(p.paid_amount), 0.00) AS balance
FROM members m
LEFT JOIN borrowings br ON br.member_id = m.member_id
LEFT JOIN fines f ON f.borrowing_id = br.borrowing_id AND f.status <> 'waived'
LEFT JOIN (
    SELECT fine_id, SUM(amount_paid) AS paid_amount
    FROM payments
    GROUP BY fine_id
) p ON p.fine_id = f.fine_id
GROUP BY m.member_id, m.first_name, m.last_name;
