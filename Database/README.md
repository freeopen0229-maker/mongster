# 5등급 마이그레이션 서버 실행 패키지

이 저장소에서 실제 확인된 DB는 로컬 참조용 SQLite뿐이다. 운영 DB 제공자는 아직 미정이다. 아래 SQL은 **SQLite 전용**이며 MySQL/PostgreSQL/SQL Server에 그대로 적용하지 않는다. 운영 DB 이관은 실행하지 않았다.

| 이전 값 | 새 값 |
|---|---|
| 0 | Common |
| 1 | Rare |
| 2 | Rare |
| 3 | Epic |
| 4 | Legendary |
| 5 | Divine |

최신 사용자 승인 매핑이다. 기존 V1 enum 이름으로 추측하거나 단순 enum 캐스팅을 하지 않는다. 이미 표준 문자열이면 유지하고, NULL/미지 값은 원본 그대로 둔 채 `PetTable_MigrationPending_2026`에도 보존한다. 미지 값을 임의로 Common으로 바꾸지 않는다. 대기 행이 남으면 전체 데이터가 5등급 정규화를 마친 상태는 아니다.

## 포함 파일

- `SQLite/001_pet_rarity_v2.sql`: BEGIN IMMEDIATE → 원본 테이블/스키마 백업 → 미지 값 보존 → 표준 매핑 → 전체 예상 행/행 수 검사 → 이관 마커 → COMMIT.
- `SQLite/002_verify.sql`: integrity_check, 원본/사본/대기/잔여 레거시 수량, 등급 집계 및 실행 마커 확인.
- `../Tools/migrate_pet_rarity.py`: 기본 읽기 전용 계획, `--apply` 시 트랜잭션 적용. 기존 마커가 있고 레거시 행이 없으면 중복 실행을 성공적으로 건너뛰는 기존 도구.
- `../Tools/test_sql_package.py`: 새 SQL 자체를 실행하는 10개 fixture 검사. 기존 사본 보존, NULL/미지 값 보존, 재실행 거절 및 CHECK 실패 시 rollback 포함.

SQL 직접 실행 경로는 백업 테이블이 존재하면 중단하는 **한 번 실행용**이다. 기존 Python 도구와 둘 중 하나의 적용 경로만 선택한다. Python 도구가 생성한 백업을 SQL이 덮어쓰지 않으며 그 반대도 마찬가지다.

## SQLite 서버에서 적용

1. 대상 제공자·스키마 확인, 게임 쓰기 작업 중지, 데이터 파일 경로 확인. Rarity가 표준 문자열을 저장할 수 있어야 한다. CHECK/STRICT/트리거 등 기존 제약이 맞지 않으면 스키마 변경을 먼저 설계한다. 스키마 제약을 우회하지 않는다.
2. SQLite `.backup` 또는 제공자 백업 기능으로 **전체 DB 사본**을 별도 보관한다. SQL의 CTAS 테이블 사본은 행 데이터만 보존하며 원본 인덱스·제약·트리거·외래키를 복제하지 않으므로 전체 백업을 대체하지 않는다. 백업 복원 가능 여부도 운영 환경에서 확인한다.
3. 원본 읽기 전용 계획을 확인한다. 제공된 SQLite 참조 fixture를 운영 DB라고 사용하지 않는다.

```bash
test -f /path/to/game.sqlite
python3 Tools/migrate_pet_rarity.py --db /path/to/game.sqlite
sqlite3 -readonly /path/to/game.sqlite ".backup '/secure/backups/game-before-rarity-v2.sqlite'"
```

4. 다음 둘 중 하나만 실행한다.

```bash
# 경로 A: 기존 멱등성 지원 Python 도구
python3 Tools/migrate_pet_rarity.py --db /path/to/game.sqlite --apply
# 또는 경로 B: SQL 패키지. -bail을 생략하지 않는다.
sqlite3 -bail /path/to/game.sqlite < Database/SQLite/001_pet_rarity_v2.sql
```

5. `sqlite3 -readonly /path/to/game.sqlite < Database/SQLite/002_verify.sql`로 검사한다. LiveRows=BackupRows, LegacyRows=0, integrity_check=ok가 필요하다. 대기 데이터 건수와 원본 보존 여부를 별도로 확인한다. 미지 행은 수동 검토 후 별도 승인된 매핑으로 처리한다.
6. 실패 시 SQL 클라이언트는 즉시 중단·ROLLBACK/연결 종료해야 한다. `sqlite3 -bail`은 오류 후 연결을 닫아 미커밋 트랜잭션을 되돌린다. 다른 드라이버는 예외에서 명시적으로 rollback한다. COMMIT 후 복구는 쓰기 중지 상태에서 검증한 전체 DB 사본을 복원하는 운영 절차로 진행한다. 사본 테이블 삭제/덮어쓰기를 자동 수행하지 않는다.

`--apply` 없이 서버의 실제 접속 정보를 조회·추정하는 동작은 없다. DB 비밀번호는 이 문서·SQL·채팅에 입력하지 않고 해당 배포 환경의 비밀 설정으로 관리한다.

## 다른 DB 제공자

PostgreSQL/MySQL 등의 연결 문자열을 입력받는 범용 실행기로 포장하지 않았다. 해당 제공자 확인 후 컬럼 타입 변경, DDL 트랜잭션 지원, 쓰기 잠금, 온라인 백업, PK 기준 행 비교와 복구 절차에 맞춰 별도 SQL을 작성·스테이징 검증해야 한다. 아직 해당 환경용 검증 완료 SQL은 없다.
