#!/usr/bin/env bash
# Exporta E2E_PASSWORD desde el .env de la raíz (AUTH_SEED_DEFAULT_PASSWORD) sin escribirlo en ningún archivo, y ejecuta
# el comando recibido. Uso (desde tests/e2e): bash scripts/con-credenciales.sh npx playwright test
set -euo pipefail
raiz="$(cd "$(dirname "$0")/../../.." && pwd)"
if [[ -z "${E2E_PASSWORD:-}" ]]; then
  E2E_PASSWORD="$(grep -E '^AUTH_SEED_DEFAULT_PASSWORD=' "$raiz/.env" | head -n1 | cut -d= -f2- | tr -d '\r')"
  # Quita comillas envolventes ("valor" o 'valor'), como las acepta docker compose.
  if [[ "$E2E_PASSWORD" =~ ^\"(.*)\"$ || "$E2E_PASSWORD" =~ ^\'(.*)\'$ ]]; then
    E2E_PASSWORD="${BASH_REMATCH[1]}"
  fi
  export E2E_PASSWORD
fi
exec "$@"
