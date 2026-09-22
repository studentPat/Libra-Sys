-- Run this file as a MySQL administrator in a development database only.
USE librasys;

CREATE ROLE IF NOT EXISTS 'librasys_api_role';
CREATE ROLE IF NOT EXISTS 'librasys_report_role';
CREATE ROLE IF NOT EXISTS 'librasys_demo_grantor_role';

CREATE USER IF NOT EXISTS 'librasys_api'@'localhost'
  IDENTIFIED BY 'REPLACE_WITH_LOCAL_SECRET';
CREATE USER IF NOT EXISTS 'librasys_report'@'localhost'
  IDENTIFIED BY 'REPLACE_WITH_LOCAL_SECRET';
CREATE USER IF NOT EXISTS 'librasys_demo_grantor'@'localhost'
  IDENTIFIED BY 'REPLACE_WITH_LOCAL_SECRET';

GRANT SELECT, INSERT, UPDATE ON librasys.books TO 'librasys_api_role';
GRANT SELECT, INSERT, UPDATE ON librasys.book_copies TO 'librasys_api_role';
GRANT SELECT, INSERT, UPDATE ON librasys.reservations TO 'librasys_api_role';
GRANT SELECT, INSERT, UPDATE ON librasys.borrowings TO 'librasys_api_role';
GRANT SELECT, INSERT, UPDATE ON librasys.fines TO 'librasys_api_role';
GRANT SELECT, INSERT ON librasys.payments TO 'librasys_api_role';
GRANT SELECT ON librasys.users, librasys.members, librasys.authors,
    librasys.book_authors, librasys.categories, librasys.book_categories
    TO 'librasys_api_role';
GRANT INSERT ON librasys.transaction_logs TO 'librasys_api_role';

GRANT SELECT ON librasys.v_available_catalog, librasys.v_member_balances
    TO 'librasys_report_role';

GRANT 'librasys_api_role' TO 'librasys_api'@'localhost';
GRANT 'librasys_report_role' TO 'librasys_report'@'localhost';
SET DEFAULT ROLE 'librasys_api_role' TO 'librasys_api'@'localhost';
SET DEFAULT ROLE 'librasys_report_role' TO 'librasys_report'@'localhost';

-- Course demonstration: grant-option is isolated to this demo identity.
GRANT SELECT ON librasys.books TO 'librasys_demo_grantor_role'
  WITH GRANT OPTION;
SHOW GRANTS FOR 'librasys_demo_grantor'@'localhost';
-- Revoke the delegated privilege after observing the result:
-- REVOKE SELECT ON librasys.books FROM 'librasys_demo_grantor'@'localhost';

-- Verify least privilege. These statements should fail when executed as
-- librasys_api, and are intentionally comments so setup remains successful:
-- CREATE USER 'should_fail'@'localhost' IDENTIFIED BY 'not-used';
-- GRANT ALL PRIVILEGES ON *.* TO 'librasys_api';
