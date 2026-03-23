## HelloOrder：Docker Desktop + ngrok 外网访问（后端 API）

本文记录了如何在你的机器上：

- 用 Docker Desktop 跑 `HelloOrder` 的 ASP.NET 8 Web API（带 PostgreSQL + Redis）
- 用本地 `ngrok` 把 API 暴露到公网，供外网访问

> 说明：`ngrok` 的公网域名是临时的，每次重启隧道可能会变化；但本地到容器的端口映射规则不变。

---

## 1. 前置条件

- 已安装并启动 `Docker Desktop`
- 已安装并能运行 `ngrok`
- 机器上端口 `5432`（PostgreSQL）和 `6379`（Redis）以及你选定的 API 映射端口（本文用 `5001`）可用

---

## 2. 启动 PostgreSQL + Redis（示例使用本地缓存镜像）

在 `HelloOrder` 根目录执行（确保上下文正确）：

```bash
cd /path/to/HelloOrder
```

创建一个独立网络（避免跟其他项目冲突）：

```bash
docker network create helloorder_net >/dev/null 2>&1 || true
```

启动数据库与缓存（本文与本次构建一致，使用 `postgres:15-alpine` + `redis:6-alpine`，避免拉取慢）：

```bash
docker rm -f helloorder-postgres helloorder-redis >/dev/null 2>&1 || true

docker run -d --name helloorder-postgres \
  --network helloorder_net \
  -p 5432:5432 \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=helloorder \
  postgres:15-alpine

docker run -d --name helloorder-redis \
  --network helloorder_net \
  -p 6379:6379 \
  redis:6-alpine
```

---

## 3. 构建并启动 HelloOrder 后端 API（ASP.NET 8）

构建镜像：

```bash
docker build -f Dockerfile.api -t helloorder-api:latest .
```

启动 API 容器（**端口映射：`5001 -> 8080`**）：

```bash
docker rm -f helloorder-api >/dev/null 2>&1 || true

docker run -d --name helloorder-api \
  --network helloorder_net \
  -p 5001:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e ConnectionStrings__DefaultConnection="Host=helloorder-postgres;Port=5432;Database=helloorder;Username=postgres;Password=postgres" \
  -e ConnectionStrings__Redis="helloorder-redis:6379" \
  helloorder-api:latest
```

> 如果你机器上 `5001` 不可用，可以把 `-p 5001:8080` 改成别的宿主端口，并在下一节把 `ngrok http` 端口也改掉。

---

## 4. 启动 ngrok，把本地 API 暴露到公网

在 `HelloOrder` 目录打开一个新终端运行：

```bash
ngrok http 5001
```

> 如果你使用的是 `ngrok Free`，一般不支持自定义子域名；直接用 `ngrok http <端口>` 获取系统分配的临时公网域名即可。

### 查看公网地址

ngrok 会在控制台输出 `Forwarding` 地址。也可以打开：

- `http://127.0.0.1:4040/api/tunnels`

本次示例公网地址（会变化）：

- `https://a31f-209-146-14-158.ngrok-free.app`

---

## 5. 外网访问与验证

### 5.1 验证服务已联通（不需要 token）

该接口只允许 `POST`；你用 `GET`/`HEAD` 访问会返回 `405 Method Not Allowed`，这恰好说明服务已经通了。

```bash
curl -i https://<你的ngrok域名>/api/Auth/login
```

看到 `Allow: POST` + `405` 即可。

### 5.2 Swagger（建议用浏览器打开）

- 本地：`http://127.0.0.1:5001/swagger`
- 外网：`https://<你的ngrok域名>/swagger`

---

## 6. 停止与清理（可选）

停止 API：

```bash
docker stop helloorder-api
```

停止 ngrok：

```bash
pkill ngrok
```

