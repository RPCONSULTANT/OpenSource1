#!/usr/bin/env bash
# ──────────────────────────────────────────────────────────────────
# rebuild.sh — Rebuild limpio de todos los contenedores AxionERP
#
# Uso:
#   ./rebuild.sh          → rebuild completo sin cache (recompila todo)
#   ./rebuild.sh --soft   → rebuild con cache de capas (solo si cambiaron fuentes)
#   ./rebuild.sh --down   → solo detiene y elimina contenedores/volúmenes de app
#
# IMPORTANTE: Usar siempre este script o 'docker compose build' para que
# las imágenes usen los tags Docker Hub definidos en docker-compose.yml:
#   ggeasy75/opensource:api
#   ggeasy75/opensource:blazor
# NO usar 'docker build -f Dockerfile.* -t ...' directamente sin esos tags.
# ──────────────────────────────────────────────────────────────────
set -euo pipefail

COMPOSE="docker compose"
MODE="hard"

for arg in "$@"; do
  case $arg in
    --soft) MODE="soft" ;;
    --down) MODE="down" ;;
  esac
done

echo ""
echo "╔══════════════════════════════════════════════╗"
echo "║         AxionERP — Docker Rebuild            ║"
echo "╚══════════════════════════════════════════════╝"
echo ""

# ── 1. Detener y eliminar contenedores de aplicación (preserva postgres-data) ──
echo "▶ Deteniendo contenedores..."
$COMPOSE down --remove-orphans
echo ""

if [[ "$MODE" == "down" ]]; then
  echo "✔ Contenedores detenidos. (modo --down, sin rebuild)"
  exit 0
fi

# ── 2. Eliminar imágenes locales para forzar rebuild desde cero ─────────────
if [[ "$MODE" == "hard" ]]; then
  echo "▶ Eliminando imágenes locales (hard clean)..."
  docker rmi ggeasy75/opensource:api ggeasy75/opensource:blazor 2>/dev/null || true
  echo ""
fi

# ── 3. Build de imágenes ────────────────────────────────────────────────────
if [[ "$MODE" == "hard" ]]; then
  echo "▶ Construyendo imágenes sin cache (--no-cache)..."
  $COMPOSE build --no-cache --pull
else
  echo "▶ Construyendo imágenes con cache de capas (--build)..."
  $COMPOSE build
fi
echo ""

# ── 4. Levantar stack ───────────────────────────────────────────────────────
echo "▶ Levantando contenedores..."
$COMPOSE up -d
echo ""

# ── 5. Estado final ─────────────────────────────────────────────────────────
echo "▶ Estado del stack:"
$COMPOSE ps
echo ""
echo "✔ Listo."
echo "  Blazor → http://localhost:8080"
echo "  API    → http://localhost:8081"
echo "  Scalar → http://localhost:8081/scalar/v1"
echo ""
