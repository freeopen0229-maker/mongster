-- Read-only post-migration report. Expected: same rows, LegacyRows=0, integrity=ok.
PRAGMA integrity_check;
SELECT 'LiveRows' AS Metric, COUNT(*) AS Value FROM PetTable
 UNION ALL SELECT 'BackupRows',COUNT(*) FROM PetTable_Backup_2026
 UNION ALL SELECT 'PreservedPendingRows',COUNT(*) FROM PetTable_MigrationPending_2026
 UNION ALL SELECT 'LegacyRows',COUNT(*) FROM PetTable WHERE CAST(Rarity AS TEXT) IN ('0','1','2','3','4','5');
SELECT Rarity,COUNT(*) AS Count FROM PetTable GROUP BY Rarity;
SELECT marker,applied_utc FROM PetRarityMigration_2026;
