USE librasys;

-- Development-only demonstration. Do not put real keys in source control.
SET @demo_key = UNHEX(SHA2('LOCAL_DEMO_KEY_REPLACE_ME', 256));
SET @member_id = (SELECT member_id FROM members ORDER BY member_id LIMIT 1);

UPDATE members
SET address_ciphertext = AES_ENCRYPT('Demo address; replace locally', @demo_key)
WHERE member_id = @member_id;

SELECT member_id, address_ciphertext,
       CAST(AES_DECRYPT(address_ciphertext, @demo_key) AS CHAR) AS decrypted_value
FROM members
WHERE member_id = @member_id;

-- Production requirements:
-- * Generate password hashes in ASP.NET 8; never encrypt or store passwords.
-- * Supply encryption keys through a secret manager/environment configuration.
-- * Use TLS for the API-to-MySQL connection.
-- * Encrypt backups independently of column ciphertext.
-- * Rotate keys through a planned migration; do not commit the demo key.
