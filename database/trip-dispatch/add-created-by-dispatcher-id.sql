-- Additive migration for databases initialized before dispatcher ownership was added.
USE trip_dispatch_db;

SET @migration_sql = (
    SELECT IF(COUNT(*) = 0,
        'ALTER TABLE trips ADD COLUMN created_by_dispatcher_id CHAR(36) NULL AFTER required_capacity',
        'SELECT ''created_by_dispatcher_id already exists''')
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'trips'
      AND column_name = 'created_by_dispatcher_id'
);
PREPARE migration_stmt FROM @migration_sql;
EXECUTE migration_stmt;
DEALLOCATE PREPARE migration_stmt;
