#!/usr/bin/env bash
set -euo pipefail

# 一键发布脚本（Docker Desktop）
# 会做的事：
# 1) 创建网络 helloorder_net（默认）
# 2) 启动/重建 postgres + redis（使用命名卷 pgdata 持久化）
# 3) docker build 构建 API + Web 镜像
# 4) 重建 API 容器（容器名固定为 api，以匹配 web/nginx.conf）
# 5) 重建 Web 容器
#
# 默认端口：
# - API: 5001(宿主) -> 8080(容器)
# - Web: 80(宿主) -> 80(容器)
# - Postgres: 5432(宿主) -> 5432(容器)
# - Redis: 6379(宿主) -> 6379(容器)
#
# 你可以用环境变量覆盖端口/开关，例如：
#   API_HOST_PORT=5002 WEB_HOST_PORT=8080 bash docker_publish_one_click.sh

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT_DIR"

NETWORK="${NETWORK:-helloorder_net}"

POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-helloorder-postgres}"
REDIS_CONTAINER="${REDIS_CONTAINER:-helloorder-redis}"
API_CONTAINER="${API_CONTAINER:-api}" # 必须匹配 web/nginx.conf 的 proxy_pass http://api:8080/api/
WEB_CONTAINER="${WEB_CONTAINER:-helloorder-web}"

POSTGRES_IMAGE="${POSTGRES_IMAGE:-postgres:16-alpine}"
REDIS_IMAGE="${REDIS_IMAGE:-redis:7-alpine}"

API_IMAGE="${API_IMAGE:-helloorder-api:latest}"
WEB_IMAGE="${WEB_IMAGE:-helloorder-web:latest}"

PG_VOLUME="${PG_VOLUME:-pgdata}"

API_HOST_PORT="${API_HOST_PORT:-5001}"
WEB_HOST_PORT="${WEB_HOST_PORT:-80}"

ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
JWT_KEY="${JWT_KEY:-HelloOrder-SecretKey-32Chars!!}"

# 是否把数据库/Redis 端口映射到宿主机（默认 1；若和你本机其他服务冲突可设为 0）
EXPOSE_DB_PORTS="${EXPOSE_DB_PORTS:-1}"

NGROK_ENABLED="${NGROK_ENABLED:-0}" # 0/1：是否启动 ngrok
NGROK_PORT="${NGROK_PORT:-$API_HOST_PORT}" # ngrok 对外暴露的宿主机端口（通常=API_HOST_PORT）
NGROK_KILL_EXISTING="${NGROK_KILL_EXISTING:-1}" # 1：启动前尝试杀掉旧 ngrok，避免端口占用
NGROK_BIN="${NGROK_BIN:-ngrok}"
NGROK_LOG_FILE="${NGROK_LOG_FILE:-$ROOT_DIR/.ngrok.log}"
NGROK_PID_FILE="${NGROK_PID_FILE:-$ROOT_DIR/.ngrok.pid}"

if [ ! -d "src/HelloOrder.Api" ] || [ ! -d "web" ]; then
  echo "请在 HelloOrder 目录执行，或确保脚本在 HelloOrder 下。"
  exit 1
fi

ensure_network() {
  docker network create "$NETWORK" >/dev/null 2>&1 || true
}

ensure_volume() {
  docker volume inspect "$PG_VOLUME" >/dev/null 2>&1 || docker volume create "$PG_VOLUME" >/dev/null
}

wait_postgres() {
  echo "等待 Postgres 就绪..."
  for _ in $(seq 1 60); do
    if docker exec "$POSTGRES_CONTAINER" pg_isready -U postgres >/dev/null 2>&1; then
      echo "Postgres 已就绪。"
      return 0
    fi
    sleep 2
  done
  echo "Postgres 等待超时。"
  return 1
}

wait_redis() {
  echo "等待 Redis 就绪..."
  for _ in $(seq 1 30); do
    if docker exec "$REDIS_CONTAINER" redis-cli ping >/dev/null 2>&1; then
      echo "Redis 已就绪。"
      return 0
    fi
    sleep 2
  done
  echo "Redis 等待超时。"
  return 1
}

build_images() {
  echo "构建 API 镜像：$API_IMAGE"
  docker build -f Dockerfile.api -t "$API_IMAGE" .

  echo "构建 Web 镜像：$WEB_IMAGE"
  docker build -f Dockerfile.web -t "$WEB_IMAGE" .
}

publish_api_web() {
  echo "重建容器..."
  docker rm -f "$API_CONTAINER" >/dev/null 2>&1 || true
  # 兼容历史命名：README/旧脚本可能把 API 容器命名为 helloorder-api
  docker rm -f helloorder-api >/dev/null 2>&1 || true
  docker rm -f "$WEB_CONTAINER" >/dev/null 2>&1 || true

  # 后端运行参数：连接字符串必须指向 docker 网络内的服务名
  docker run -d --name "$API_CONTAINER" \
    --network "$NETWORK" \
    -p "${API_HOST_PORT}:8080" \
    -e "ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT" \
    -e "ASPNETCORE_URLS=http://+:8080" \
    -e "ConnectionStrings__DefaultConnection=Host=${POSTGRES_CONTAINER};Port=5432;Database=helloorder;Username=postgres;Password=postgres" \
    -e "ConnectionStrings__Redis=${REDIS_CONTAINER}:6379" \
    -e "Jwt__Key=$JWT_KEY" \
    "$API_IMAGE" >/dev/null

  docker run -d --name "$WEB_CONTAINER" \
    --network "$NETWORK" \
    -p "${WEB_HOST_PORT}:80" \
    "$WEB_IMAGE" >/dev/null
}

start_ngrok_if_needed() {
  if [ "$NGROK_ENABLED" != "1" ]; then
    return 0
  fi

  if [ "$NGROK_KILL_EXISTING" = "1" ]; then
    # 防止已有 ngrok 占用端口导致启动失败
    pkill -f "ngrok http ${NGROK_PORT}" >/dev/null 2>&1 || true
  fi

  echo "启动 ngrok（端口：${NGROK_PORT}）..."
  rm -f "$NGROK_LOG_FILE" "$NGROK_PID_FILE" >/dev/null 2>&1 || true
  (
    "$NGROK_BIN" http "${NGROK_PORT}"
  ) >"$NGROK_LOG_FILE" 2>&1 &
  echo $! > "$NGROK_PID_FILE"

  # 等待 ngrok API（4040）就绪
  for _ in $(seq 1 20); do
    if curl -s "http://127.0.0.1:4040/api/tunnels" >/dev/null 2>&1; then
      break
    fi
    sleep 1
  done

  # 尝试打印 public_url（不做强依赖，失败也不影响脚本整体发布）
  local public_url
  public_url="$(
    curl -s "http://127.0.0.1:4040/api/tunnels" 2>/dev/null | sed -n 's/.*"public_url":"\([^"]*\)".*/\1/p' | head -n 1 || true
  )"
  if [ -n "${public_url:-}" ]; then
    echo "ngrok 地址：${public_url}"
  else
    echo "ngrok 已启动：请打开 http://127.0.0.1:4040/api/tunnels 查看转发地址"
  fi
}

publish_db_cache() {
  echo "重建数据库与缓存..."
  docker rm -f "$POSTGRES_CONTAINER" >/dev/null 2>&1 || true
  docker rm -f "$REDIS_CONTAINER" >/dev/null 2>&1 || true

  ensure_volume

  POSTGRES_PORT_FLAGS=()
  REDIS_PORT_FLAGS=()
  if [ "$EXPOSE_DB_PORTS" = "1" ]; then
    POSTGRES_PORT_FLAGS=(-p 5432:5432)
    REDIS_PORT_FLAGS=(-p 6379:6379)
  fi

  docker run -d --name "$POSTGRES_CONTAINER" \
    --network "$NETWORK" \
    "${POSTGRES_PORT_FLAGS[@]}" \
    -e POSTGRES_USER=postgres \
    -e POSTGRES_PASSWORD=postgres \
    -e POSTGRES_DB=helloorder \
    -v "$PG_VOLUME":/var/lib/postgresql/data \
    "$POSTGRES_IMAGE" >/dev/null

  docker run -d --name "$REDIS_CONTAINER" \
    --network "$NETWORK" \
    "${REDIS_PORT_FLAGS[@]}" \
    "$REDIS_IMAGE" >/dev/null

  wait_postgres
  wait_redis
}

main() {
  ensure_network
  publish_db_cache
  build_images
  publish_api_web
  start_ngrok_if_needed

  echo "发布完成。"
  echo "API Swagger: http://127.0.0.1:${API_HOST_PORT}/swagger"
  echo "Web 前端: http://127.0.0.1:${WEB_HOST_PORT}/"
  echo "若需要外网：ngrok http ${API_HOST_PORT}（也可通过 NGROK_ENABLED=1 自动启动）"
}

main "$@"

