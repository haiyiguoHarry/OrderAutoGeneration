# Docker Compose 基础知识与本项目配置说明

本文档分为两部分：**Docker Compose 基础知识**（概念与常用指令），以及**本项目 docker-compose.yml 逐项说明与注释**。

---

## 一、Docker Compose 基础知识

### 1.1 什么是 Docker Compose

- **Docker**：把应用及其依赖打包成“镜像”（Image），在“容器”（Container）里运行，保证环境一致。
- **Docker Compose**：用一份 **YAML 配置文件**（通常叫 `docker-compose.yml`）定义**多个服务**（如数据库、缓存、应用），用一条命令一起启动、停止，方便本地开发或简单部署。

**本项目中的用途**：一条命令启动 **PostgreSQL** 和 **Redis**，无需在本地单独安装这两个软件。

### 1.2 核心概念对照

| 概念 | 说明 |
|------|------|
| **镜像 (Image)** | 只读模板，如 `postgres:16-alpine`、`redis:7-alpine`。 |
| **容器 (Container)** | 镜像的运行实例，有独立文件系统、网络，可启动/停止/删除。 |
| **服务 (Service)** | 在 docker-compose 里指“一个可运行的镜像 + 配置”，如 `postgres`、`redis`。 |
| **卷 (Volume)** | 持久化存储，容器删掉后数据仍在，如本项目的 `pgdata`。 |
| **网络 (Network)** | 默认 Compose 会为项目建一个网络，同一文件里的服务可通过**服务名**互相访问（如 `postgres`、`redis`）。 |

### 1.3 常用命令

```bash
# 在 docker-compose.yml 所在目录执行

# 启动所有服务（后台运行）
docker-compose up -d

# 停止并删除容器（卷不删，数据保留）
docker-compose down

# 查看运行中的容器
docker-compose ps

# 查看日志（可加服务名只看某一服务）
docker-compose logs -f
docker-compose logs -f postgres

# 进入某容器的 shell（调试用）
docker-compose exec postgres sh
docker-compose exec redis redis-cli ping
```

### 1.4 YAML 语法简要

- **缩进**：用**空格**（本项目用 2 空格），不要用 Tab。
- **键值**：`key: value`，字符串一般可省略引号。
- **列表**：用 `-` 表示一项，例如：
  ```yaml
  ports:
    - "5432:5432"
  ```
- **多行字符串**：可用 `|` 或 `>`，本项目未用到。

---

## 二、本项目 docker-compose.yml 逐项说明

下面按“配置文件结构”逐块说明含义，并给出**带注释的等价写法**（注释在 YAML 中一般用 `#`，这里用说明框表示）。

### 2.1 文件顶层：version 与 services

```yaml
version: '3.8'

services:
```

| 键 | 说明 |
|----|------|
| **version** | 使用的 Compose 文件格式版本，`3.8` 支持较新特性（如 healthcheck、卷配置）。部分新 Docker 已弱化 version，但保留可兼容旧环境。 |
| **services** | 下面所有“服务”的根键，每个服务名（如 `postgres`、`redis`）即运行时的**服务名/容器名前缀**，同文件内可通过该名访问（如连接串里写 `Host=postgres`）。 |

---

### 2.2 服务：postgres（PostgreSQL 数据库）

```yaml
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: helloorder
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 5s
      timeout: 5s
      retries: 5
```

#### image

- **含义**：使用哪个镜像（未本地拉取时会从 Docker Hub 拉取）。
- **postgres:16-alpine**：PostgreSQL 主版本 16，基于 Alpine 的轻量镜像。
- 本项目后端连接串里的 `Database=helloorder` 等需与下面 environment 一致。

#### environment

- **含义**：注入到容器内的环境变量；官方 Postgres 镜像用这些变量做**首次初始化**（仅当数据目录为空时生效）。

| 变量 | 含义 | 本项目取值 |
|------|------|------------|
| **POSTGRES_USER** | 超级用户名 | `postgres` |
| **POSTGRES_PASSWORD** | 该用户密码 | `postgres`（生产务必改掉） |
| **POSTGRES_DB** | 默认创建的数据库名 | `helloorder` |

- 连接串对应关系：
  - `Host=localhost;Port=5432;Database=helloorder;Username=postgres;Password=postgres`
  - 若后端也在 Docker 里且与 postgres 同 compose，Host 可改为 `postgres`（服务名）。

#### ports

- **含义**：端口映射，格式为 `"宿主机端口:容器内端口"`。
- **"5432:5432"**：把容器内的 5432（PostgreSQL 默认端口）映射到本机 5432，本机或 IDE 可连 `localhost:5432`。

#### volumes

- **含义**：把**命名卷**（或宿主机路径）挂载到容器内路径，数据写在该路径会持久化到卷里。
- **pgdata:/var/lib/postgresql/data**：
  - `pgdata` 是文件末尾 `volumes` 里定义的**命名卷**，由 Docker 管理位置。
  - `/var/lib/postgresql/data` 是 PostgreSQL 在容器内存数据的位置。
  - 这样重启容器或 `docker-compose down` 再 `up`，数据库数据仍然存在。

#### healthcheck

- **含义**：Docker 定期在容器内执行一条命令，根据退出码判断容器是否“健康”；其他服务可用 `depends_on` 的 `condition: service_healthy` 等它健康后再启动（本项目未用，但便于运维和扩展）。
- **test**：要执行的命令。`pg_isready -U postgres` 检查能否用用户 `postgres` 连接本机 Postgres。
- **interval**：每 5 秒检查一次。
- **timeout**：单次检查超时 5 秒。
- **retries**：连续失败 5 次则标记为不健康。

---

### 2.3 服务：redis（Redis 缓存）

```yaml
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 5
```

#### image

- **redis:7-alpine**：Redis 主版本 7，Alpine 镜像；本项目用于会话/缓存等（可选）。

#### ports

- **"6379:6379"**：Redis 默认端口，本机可连 `localhost:6379`；后端连接串 `Redis=localhost:6379` 即指这里。

#### healthcheck

- **test**：在容器内执行 `redis-cli ping`，返回 `PONG` 表示正常。
- **interval / timeout / retries**：含义同 postgres，用于标记容器健康状态。

---

### 2.4 卷定义：volumes

```yaml
volumes:
  pgdata: {}
```

- **含义**：声明**命名卷** `pgdata`，供上面 `postgres` 的 `volumes` 使用。
- **pgdata: {}**：使用默认驱动和配置；数据存在 Docker 管理的位置（如 `/var/lib/docker/volumes/项目名_pgdata`），不随容器删除而丢失。

---

## 三、带完整注释的配置示例（仅供学习）

下面是一份“等价于本项目配置”的写法，每项都加了注释，便于对照理解（YAML 中 `#` 后为注释）：

```yaml
# Compose 文件格式版本（3.x）
version: '3.8'

# 定义多个服务，每个服务会变成至少一个容器
services:
  # 服务名：postgres。同一 compose 内可用此名作主机名连接
  postgres:
    # 使用 PostgreSQL 16 的 Alpine 镜像（体积小）
    image: postgres:16-alpine
    # 环境变量：官方镜像用这些在首次启动时创建用户和数据库
    environment:
      POSTGRES_USER: postgres      # 超级用户名
      POSTGRES_PASSWORD: postgres  # 密码（生产环境务必修改）
      POSTGRES_DB: helloorder      # 默认数据库名，与后端连接串一致
    # 端口映射：宿主机:容器
    ports:
      - "5432:5432"
    # 数据持久化：命名卷 pgdata 挂载到容器内数据目录
    volumes:
      - pgdata:/var/lib/postgresql/data
    # 健康检查：每 5 秒执行 pg_isready，失败 5 次标为不健康
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 5s
      timeout: 5s
      retries: 5

  # 服务名：redis
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 5

# 声明命名卷，供上面 services 引用
volumes:
  pgdata: {}
```

---

## 四、与本项目其他部分的对应关系

| 配置项 | 本项目中的使用处 |
|--------|------------------|
| **POSTGRES_DB=helloorder** | 后端 `appsettings.json` 里 `ConnectionStrings__DefaultConnection` 的 `Database=helloorder` |
| **POSTGRES_USER / POSTGRES_PASSWORD** | 连接串里的 `Username=postgres`、`Password=postgres` |
| **ports 5432 / 6379** | 本机运行后端时用 `Host=localhost;Port=5432`、`Redis=localhost:6379` |
| **卷 pgdata** | 数据库文件持久化，避免 `docker-compose down` 后数据丢失 |

---

## 五、常用操作速查

```bash
# 首次或修改配置后启动
docker-compose up -d

# 查看状态
docker-compose ps

# 只看 Postgres 日志
docker-compose logs -f postgres

# 停止并删除容器（保留卷）
docker-compose down

# 停止并删除容器和卷（会清空数据库！）
docker-compose down -v
```

把上述“基础知识”和“逐项说明”对照项目里的 `docker-compose.yml` 看，即可掌握本项目中用到的 Docker Compose 配置与含义。
