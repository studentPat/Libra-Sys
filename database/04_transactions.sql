USE librasys;

-- Borrow one available copy atomically. Run with @member_id and @copy_id set.
SET @member_id = (SELECT member_id FROM members ORDER BY member_id LIMIT 1);
SET @copy_id = (SELECT copy_id FROM book_copies WHERE status = 'available' LIMIT 1);
SET @actor_user_id = (SELECT user_id FROM users WHERE username = 'demo_librarian' LIMIT 1);

START TRANSACTION;
SELECT copy_id FROM book_copies WHERE copy_id = @copy_id AND status = 'available'
FOR UPDATE;
INSERT INTO borrowings (copy_id, member_id, due_date)
VALUES (@copy_id, @member_id, DATE_ADD(CURRENT_TIMESTAMP, INTERVAL 14 DAY));
UPDATE book_copies SET status = 'borrowed' WHERE copy_id = @copy_id;
INSERT INTO transaction_logs (user_id, action, entity_type, entity_id, details)
VALUES (@actor_user_id, 'borrow', 'copy', @copy_id,
        JSON_OBJECT('demonstration', TRUE));
COMMIT;

-- Rollback demonstration: the copy update and log disappear together.
START TRANSACTION;
UPDATE book_copies SET status = 'reserved' WHERE copy_id = @copy_id;
INSERT INTO transaction_logs (user_id, action, entity_type, entity_id, details)
VALUES (@actor_user_id, 'intentional_rollback', 'copy', @copy_id, JSON_OBJECT());
ROLLBACK;

-- Return the newest active borrowing atomically.
SET @borrowing_id = (
    SELECT borrowing_id FROM borrowings
    WHERE copy_id = @copy_id AND status = 'active'
    ORDER BY borrowing_id DESC LIMIT 1
);
START TRANSACTION;
SELECT copy_id, member_id FROM borrowings
WHERE borrowing_id = @borrowing_id AND status = 'active'
FOR UPDATE;
UPDATE borrowings
SET status = 'returned', return_date = CURRENT_TIMESTAMP, return_condition = 'good'
WHERE borrowing_id = @borrowing_id;
UPDATE book_copies SET status = 'available', item_condition = 'good'
WHERE copy_id = @copy_id;
INSERT INTO transaction_logs (user_id, action, entity_type, entity_id, details)
VALUES (@actor_user_id, 'return', 'borrowing', @borrowing_id, JSON_OBJECT());
COMMIT;

-- Concurrency exercise:
-- Run the SELECT ... FOR UPDATE portion in two sessions against the same
-- available copy. Session 2 waits, then observes the committed status and
-- must not create a second active borrowing. The ASP.NET service must handle
-- deadlock/serialization errors with a bounded idempotent retry.
