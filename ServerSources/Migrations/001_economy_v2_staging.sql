-- SQLite 참고 스키마. 운영 DB의 종류/테이블은 아직 제공되지 않았다.
-- 기존 테이블을 바꾸지 않는 사본 staging 전용. DB adapter가 자동으로 이 테이블을 읽지 않는다.
CREATE TABLE EconomyV2WalletStaging (
 account_id TEXT PRIMARY KEY,
 jeon INTEGER NOT NULL CHECK(jeon BETWEEN 0 AND 1000000000000),
 gem INTEGER NOT NULL CHECK(gem BETWEEN 0 AND 2147483647),
 mileage_thread INTEGER NOT NULL CHECK(mileage_thread BETWEEN 0 AND 2147483647)
);
CREATE TABLE EconomyV2HiddenQuotaStaging (
 hidden_type INTEGER PRIMARY KEY CHECK(hidden_type BETWEEN 0 AND 4),
 quota_limit INTEGER NOT NULL,
 issued INTEGER NOT NULL CHECK(issued >= 0 AND issued <= quota_limit),
 CHECK((hidden_type=0 AND quota_limit=10) OR (hidden_type BETWEEN 1 AND 4 AND quota_limit=100))
);
CREATE TABLE EconomyV2LegacyPetStaging (
 pet_id TEXT PRIMARY KEY,
 source_json TEXT NOT NULL,
 proposed_rarity TEXT,
 status TEXT NOT NULL CHECK(status IN ('grade_mapping_required','skills_and_visual_mapping_required'))
);
CREATE TABLE EconomyV2MigrationState (
 singleton INTEGER PRIMARY KEY CHECK(singleton=1),
 ready_for_runtime INTEGER NOT NULL CHECK(ready_for_runtime=0),
 note TEXT NOT NULL
);
INSERT INTO EconomyV2MigrationState VALUES(1,0,'Staging only. Preserve V1 data; skill/visual/grade migration and DB adapter still required.');
