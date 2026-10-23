-- SQLite ONLY. Stop game writers; take a full database backup first.
-- Invoke sqlite3 -bail database.sqlite < this-file.sql (not without -bail).
-- Existing backup names cause failure instead of overwriting preserved data.
BEGIN IMMEDIATE;
CREATE TABLE PetTable_Backup_2026 AS SELECT * FROM PetTable;
CREATE TABLE PetSchema_Backup_2026 AS
 SELECT sql AS original_sql FROM sqlite_master WHERE type='table' AND name='PetTable';
CREATE TABLE PetTable_MigrationPending_2026 AS SELECT * FROM PetTable
 WHERE Rarity IS NULL OR CAST(Rarity AS TEXT) NOT IN
 ('0','1','2','3','4','5','Common','Rare','Epic','Legendary','Divine');
-- Expected full rows retained only within this transaction/connection.
CREATE TEMP TABLE PetRarityExpected AS SELECT * FROM PetTable_Backup_2026;
UPDATE PetRarityExpected SET Rarity=CASE CAST(Rarity AS TEXT)
 WHEN '0' THEN 'Common' WHEN '1' THEN 'Rare' WHEN '2' THEN 'Rare'
 WHEN '3' THEN 'Epic' WHEN '4' THEN 'Legendary' WHEN '5' THEN 'Divine' END
 WHERE CAST(Rarity AS TEXT) IN ('0','1','2','3','4','5');
UPDATE PetTable SET Rarity=CASE CAST(Rarity AS TEXT)
 WHEN '0' THEN 'Common' WHEN '1' THEN 'Rare' WHEN '2' THEN 'Rare'
 WHEN '3' THEN 'Epic' WHEN '4' THEN 'Legendary' WHEN '5' THEN 'Divine' END
 WHERE CAST(Rarity AS TEXT) IN ('0','1','2','3','4','5');
-- Constraint failure aborts. The client must rollback/close on any SQL error.
CREATE TEMP TABLE PetRarityAssertions(ok INTEGER NOT NULL CHECK(ok=1));
INSERT INTO PetRarityAssertions SELECT
 (SELECT COUNT(*) FROM PetTable)=(SELECT COUNT(*) FROM PetTable_Backup_2026);
INSERT INTO PetRarityAssertions SELECT NOT EXISTS
 (SELECT * FROM PetTable EXCEPT SELECT * FROM PetRarityExpected);
INSERT INTO PetRarityAssertions SELECT NOT EXISTS
 (SELECT * FROM PetRarityExpected EXCEPT SELECT * FROM PetTable);
INSERT INTO PetRarityAssertions SELECT NOT EXISTS
 (SELECT 1 FROM PetTable WHERE CAST(Rarity AS TEXT) IN ('0','1','2','3','4','5'));
CREATE TABLE PetRarityMigration_2026(marker TEXT PRIMARY KEY,applied_utc TEXT NOT NULL,report_json TEXT NOT NULL);
INSERT INTO PetRarityMigration_2026 VALUES
 ('five-grades-v1',strftime('%Y-%m-%dT%H:%M:%fZ','now'),'SQLite SQL package applied; unknown values preserved');
DROP TABLE PetRarityExpected;
DROP TABLE PetRarityAssertions;
COMMIT;
SELECT Rarity, COUNT(*) AS Count FROM PetTable GROUP BY Rarity;
SELECT COUNT(*) AS PreservedPendingCount FROM PetTable_MigrationPending_2026;
