-- Creates the least-privilege role and database used by the integration test suite.
--
-- Run as the PostgreSQL superuser, on the machine that will run the tests:
--
--   psql -U postgres -h localhost -d postgres -v ON_ERROR_STOP=1 \
--        -f scripts/create-test-role.sql
--
-- The password below is generated for local test use only. Change it if you prefer;
-- it grants nothing beyond the inventory_test database.
--
-- The role is created without SUPERUSER, CREATEDB, CREATEROLE or BYPASSRLS on
-- purpose. Integration tests must never need privileges that a CI runner would not
-- have, otherwise they pass locally and fail in CI for reasons unrelated to the code.

DO $do$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'inventory_test') THEN
        EXECUTE 'CREATE ROLE inventory_test LOGIN PASSWORD ''inventory_test_local'' '
             || 'NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS';
        RAISE NOTICE 'created role inventory_test';
    ELSE
        EXECUTE 'ALTER ROLE inventory_test WITH LOGIN PASSWORD ''inventory_test_local'' '
             || 'NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS';
        RAISE NOTICE 'role inventory_test already existed; password reset';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'inventory_test') THEN
        EXECUTE 'CREATE DATABASE inventory_test OWNER inventory_test ENCODING ''UTF8''';
        RAISE NOTICE 'created database inventory_test';
    ELSE
        RAISE NOTICE 'database inventory_test already existed';
    END IF;
END
$do$;

-- Expected: inventory_test | f | f | f | f
SELECT rolname, rolsuper, rolcreatedb, rolcreaterole, rolbypassrls
FROM pg_roles
WHERE rolname = 'inventory_test';