#!/usr/bin/env bash
set -euo pipefail

APP_DIR="${APP_DIR:-$HOME/shopai}"
BACKEND_REPO="${BACKEND_REPO:-https://github.com/SwagaTeam/ShopAI-Backend.git}"
FRONTEND_REPO="${FRONTEND_REPO:-https://github.com/SwagaTeam/ShopAI-Frontend.git}"
BACKEND_BRANCH="${BACKEND_BRANCH:-master}"
FRONTEND_BRANCH="${FRONTEND_BRANCH:-main}"

BACKEND_DIR="$APP_DIR/backend"
FRONTEND_DIR="$APP_DIR/frontend"
COMPOSE_FILE="$BACKEND_DIR/deploy/docker-compose.prod.yml"

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required command '$1' is not installed." >&2
    exit 1
  fi
}

sync_repo() {
  local repo="$1"
  local branch="$2"
  local dir="$3"

  if [ ! -d "$dir/.git" ]; then
    git clone --branch "$branch" --single-branch "$repo" "$dir"
    return
  fi

  git -C "$dir" fetch origin "$branch"
  git -C "$dir" checkout "$branch"
  git -C "$dir" pull --ff-only origin "$branch"
}

require_cmd git
require_cmd docker

if ! docker compose version >/dev/null 2>&1; then
  echo "Docker Compose v2 is required." >&2
  exit 1
fi

mkdir -p "$APP_DIR"

if [ "${SKIP_GIT_SYNC:-false}" = "true" ]; then
  if [ ! -d "$BACKEND_DIR" ]; then
    echo "Missing backend directory: $BACKEND_DIR" >&2
    exit 1
  fi

  if [ ! -d "$FRONTEND_DIR" ]; then
    echo "Missing frontend directory: $FRONTEND_DIR" >&2
    exit 1
  fi
else
  sync_repo "$BACKEND_REPO" "$BACKEND_BRANCH" "$BACKEND_DIR"
  sync_repo "$FRONTEND_REPO" "$FRONTEND_BRANCH" "$FRONTEND_DIR"
fi

if [ -n "${SHOPAI_ENV_FILE:-}" ] && [ -f "$SHOPAI_ENV_FILE" ]; then
  install -m 600 "$SHOPAI_ENV_FILE" "$BACKEND_DIR/.env"
  rm -f "$SHOPAI_ENV_FILE"
fi

if [ -n "${SHOPAI_ENV_B64:-}" ]; then
  printf '%s' "$SHOPAI_ENV_B64" | base64 -d > "$BACKEND_DIR/.env"
  chmod 600 "$BACKEND_DIR/.env"
fi

if [ ! -f "$BACKEND_DIR/.env" ]; then
  echo "Missing $BACKEND_DIR/.env. Create it from .env.example or set the PROD_ENV_FILE GitHub secret." >&2
  exit 1
fi

export BACKEND_DIR
export FRONTEND_DIR

docker compose --env-file "$BACKEND_DIR/.env" -f "$COMPOSE_FILE" up -d --build --remove-orphans
docker compose --env-file "$BACKEND_DIR/.env" -f "$COMPOSE_FILE" ps

if command -v curl >/dev/null 2>&1; then
  http_port="$(grep -E '^HTTP_PORT=' "$BACKEND_DIR/.env" | tail -n 1 | cut -d '=' -f 2- || true)"
  http_port="${http_port:-80}"
  curl -fsS --retry 12 --retry-delay 5 "http://127.0.0.1:${http_port}/healthz" >/dev/null
fi
