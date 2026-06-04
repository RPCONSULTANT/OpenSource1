# ──────────────────────────────────────────────────────────────────
# Makefile — Comandos de gestión del stack AxionERP
# ──────────────────────────────────────────────────────────────────
.PHONY: rebuild up down logs ps clean help

## rebuild  → Rebuild limpio sin cache (recompila todo desde cero)
rebuild:
	@bash rebuild.sh

## soft     → Rebuild aprovechando cache de capas Docker (más rápido)
soft:
	@bash rebuild.sh --soft

## up       → Levanta el stack (sin rebuild; usa imágenes existentes)
up:
	docker compose up -d

## down     → Detiene y elimina contenedores (preserva volumen postgres)
down:
	docker compose down --remove-orphans

## clean    → Detiene, elimina contenedores E imágenes locales
clean:
	docker compose down --remove-orphans
	docker rmi opensource1-api:local opensource1-blazor:local 2>/dev/null || true

## logs     → Sigue los logs de todos los servicios
logs:
	docker compose logs -f

## logs-api → Sigue los logs solo del API
logs-api:
	docker compose logs -f api

## logs-blazor → Sigue los logs solo de Blazor
logs-blazor:
	docker compose logs -f blazor

## ps       → Estado de los contenedores
ps:
	docker compose ps

## help     → Muestra esta ayuda
help:
	@grep -E '^## ' Makefile | sed 's/## /  /'
