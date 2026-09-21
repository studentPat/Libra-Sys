USE librasys;

-- Baseline catalog query. Capture the plan and timing before/after indexes.
EXPLAIN
SELECT b.book_id, b.title, b.isbn, COUNT(bc.copy_id) AS available_copies
FROM books b
LEFT JOIN book_copies bc
  ON bc.book_id = b.book_id AND bc.status = 'available'
WHERE b.title LIKE CONCAT('%', 'code', '%')
GROUP BY b.book_id, b.title, b.isbn
ORDER BY b.title
LIMIT 25;

-- Full-text alternative for larger catalogs.
EXPLAIN
SELECT book_id, title, isbn
FROM books
WHERE MATCH(title) AGAINST('+code' IN BOOLEAN MODE)
LIMIT 25;

-- Overdue-loan workload uses ix_borrowings_due_status.
EXPLAIN
SELECT borrowing_id, copy_id, member_id, due_date
FROM borrowings
WHERE status = 'active' AND due_date < CURRENT_TIMESTAMP
ORDER BY due_date
LIMIT 100;

-- Reporting view workload.
EXPLAIN SELECT * FROM v_member_balances WHERE balance > 0;

-- Record rows examined and execution time in the lab report. Use
-- EXPLAIN ANALYZE on a supported MySQL 8.x version for actual runtime
-- evidence, and compare a deliberately unindexed copy of a query only in
-- the course test database.
