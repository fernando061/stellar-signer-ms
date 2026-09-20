#!/bin/sh
set -eu
psql -v ON_ERROR_STOP=1 --username postgres --dbname postgres -v signer_password="$SIGNER_DB_PASSWORD" <<'SQL'
CREATE ROLE signer LOGIN PASSWORD :'signer_password' NOSUPERUSER NOCREATEDB NOCREATEROLE;
CREATE DATABASE stellar_signer OWNER signer;
SQL
