-- Runs only when Postgres initializes a brand-new data volume (the official image's own
-- entrypoint mechanism for /docker-entrypoint-initdb.d/*.sql — never re-runs against an existing
-- volume). POSTGRES_DB already creates the primary database by the time this runs.
--
-- hodnota_agent is a second, separate database on the same server: a scratch space for
-- agent/manual verification (registering test users, running searches, etc.) that never touches
-- the developer's own persistent data in the primary database. See CLAUDE.md and the
-- run-hodnota skill's Cleanup section for how it's used and reset.
CREATE DATABASE hodnota_agent OWNER hodnota;
