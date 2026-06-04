#!/bin/bash
# Script de inicialización de PostgreSQL para AxionERP
# Se ejecuta automáticamente al crear el contenedor por primera vez.
# El usuario Rainiery ya existe porque se creó con POSTGRES_USER.

set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres <<-EOSQL
    CREATE DATABASE "AxionERP_App"
        WITH OWNER = "$POSTGRES_USER"
        ENCODING = 'UTF8'
        LC_COLLATE = 'en_US.utf8'
        LC_CTYPE = 'en_US.utf8'
        TEMPLATE = template0;

    CREATE DATABASE "AxionERP_Identity"
        WITH OWNER = "$POSTGRES_USER"
        ENCODING = 'UTF8'
        LC_COLLATE = 'en_US.utf8'
        LC_CTYPE = 'en_US.utf8'
        TEMPLATE = template0;

    GRANT ALL PRIVILEGES ON DATABASE "AxionERP_App" TO "$POSTGRES_USER";
    GRANT ALL PRIVILEGES ON DATABASE "AxionERP_Identity" TO "$POSTGRES_USER";
EOSQL

echo "✔ Databases AxionERP_App and AxionERP_Identity created successfully."
