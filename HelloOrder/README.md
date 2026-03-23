# HelloOrder 外贸业务管理系统

基于《需求文档》与《设计文档》实现的 Web 系统，为海外虾皮（Shopee）等商家提供订单找货、报价、采购、仓储发货及报表提成等全流程管理。

---

## 项目信息

| 项目 | 说明 |
|------|------|
| **项目名称** | HelloOrder |
| **版本** | 1.0 |
| **文档** | [需求文档.md](./需求文档.md) · [设计文档.md](./设计文档.md) |

### 技术栈

| 层次 | 技术 |
|------|------|
| 前端 | React 18 + Vite 5 + TypeScript + Ant Design 5 |
| 后端 | ASP.NET 8 Web API、JWT 认证 |
| 数据库 | PostgreSQL 16 |
| 缓存 | Redis 7 |
| 部署 | Docker、Nginx |

### 角色与默认账号

| 角色 | 说明 | 默认账号（种子数据） |
|------|------|------------------------|
| 系统管理员 | 用户/角色/权限、系统参数、日志 | `admin` / `admin123` |
| 老板 | 销量/盈利/提成报表、审批 | 需在用户管理中创建并分配「老板」角色 |
| 外贸业务员 | 商家、订单、报价、采购、发货 | 需创建并分配「外贸业务员」角色 |
| 助理 | 被分配的任务与订单操作 | 需创建并分配「助理」角色 |

---

## 项目结构

```
HelloOrder/
├── 需求文档.md                 # 业务需求与优先级
├── 设计文档.md                 # 架构、模块、库表、接口、流程
├── README.md                   # 本文件
├── .gitignore
├── HelloOrder.sln              # 后端解决方案
├── docker-compose.yml          # 开发/部署：PostgreSQL + Redis
├── Dockerfile.api              # 后端 API 镜像
├── Dockerfile.web              # 前端 Nginx 镜像
│
├── src/                        # 后端 (ASP.NET 8)
│   ├── HelloOrder.Api/         # Web API、Controllers、Program、appsettings
│   ├── HelloOrder.Core/        # 实体、枚举
│   ├── HelloOrder.Application/ # 应用服务接口（如 IAuthService）
│   └── HelloOrder.Infrastructure/  # DbContext、EF、AuthService、SeedData
│
└── web/                        # 前端 (React + Vite)
    ├── package.json
    ├── vite.config.ts          # 开发代理 /api → 后端
    ├── nginx.conf              # 生产环境 Nginx 配置
    └── src/
        ├── main.tsx, App.tsx
        ├── layout/             # 布局、侧栏菜单
        ├── pages/              # 登录、工作台、商家、订单、用户
        ├── services/           # API 请求封装
        └── store/              # Auth 上下文
```

---

## 环境要求

- **.NET 8** SDK  
- **Node.js** 18+（前端开发）  
- **Docker** 与 **Docker Compose**（数据库、Redis、生产部署）  
- **PostgreSQL** 16（若不用 Docker 则需本地安装）  
- **Redis** 7（可选，用于缓存；未配置时不影响基本运行）

---

## 配置说明

### 后端配置（appsettings.json / 环境变量）

| 配置项 | 说明 | 示例 |
|--------|------|------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL 连接串 | `Host=localhost;Port=5432;Database=helloorder;Username=postgres;Password=postgres` |
| `ConnectionStrings__Redis` | Redis 连接（可选） | `localhost:6379` |
| `Jwt__Key` | JWT 签名密钥（至少 32 字符） | 生产环境务必更换 |
| `Jwt__Issuer` / `Jwt__Audience` | JWT 签发者与受众 | `HelloOrder` |
| `Jwt__ExpiryMinutes` | Token 有效期（分钟） | `120` |
| `Cors__Origins` | 允许的前端来源 | `http://localhost:5173`（生产改为实际前端域名） |

### 前端配置

- 开发环境：Vite 将 `/api` 代理到 `http://localhost:5000`（见 `web/vite.config.ts`）。  
- 生产环境：由 Nginx 将 `/api` 反向代理到后端服务（见 `web/nginx.conf`）。

---

## 本地开发

### 1. 启动数据库与 Redis

在项目根目录执行：

```bash
docker-compose up -d
```

将启动：

- **PostgreSQL**：端口 `5432`，数据库名 `helloorder`，用户/密码 `postgres/postgres`  
- **Redis**：端口 `6379`

### 2. 运行后端

```bash
cd src/HelloOrder.Api
dotnet run
```

- **API 地址**：http://localhost:5000  
- **Swagger**：http://localhost:5000/swagger  
- 首次运行会自动建表并执行种子数据（含 `admin` 账号）。

### 3. 运行前端

```bash
cd web
npm install
npm run dev
```

- **前端地址**：http://localhost:5173  
- 开发时请求 `/api/*` 会由 Vite 代理到后端 5000 端口。

### 4. 登录

使用默认管理员账号：**用户名** `admin`，**密码** `admin123`。

---

## 部署
#### 快速启动（Docker + ngrok）

如果你只想把后端 API 跑起来并用公网访问：见 `docs/Docker-ngrok快速启动.md`。

### 方式一：仅用 Docker 跑数据库，本机跑前后端

适合在服务器或本机已安装 .NET 与 Node 时使用：

1. `docker-compose up -d` 启动 PostgreSQL、Redis。  
2. 修改 `src/HelloOrder.Api/appsettings.json`（或通过环境变量）中的连接串，指向 Docker 中的数据库。  
3. 后端：`cd src/HelloOrder.Api && dotnet run` 或发布后运行。  
4. 前端：`cd web && npm run build`，将 `web/dist` 用任意静态服务器或 Nginx 托管，并配置 `/api` 反向代理到后端。

### 方式二：Docker 构建前后端镜像并编排

**1. 构建镜像**

```bash
# 后端（在项目根目录执行，上下文包含 src）
docker build -f Dockerfile.api -t helloorder-api:latest .

# 前端（需能访问后端 API 地址；生产建议在 nginx.conf 中写死 API 地址或用环境变量）
docker build -f Dockerfile.web -t helloorder-web:latest .
```

**2. 后端运行示例（需可访问 PostgreSQL、Redis）**

```bash
docker run -d --name helloorder-api -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=宿主机或数据库服务;Port=5432;Database=helloorder;Username=postgres;Password=你的密码" \
  -e ConnectionStrings__Redis="redis:6379" \
  -e Jwt__Key="生产环境请使用至少32字符的密钥" \
  -e ASPNETCORE_URLS="http://+:8080" \
  helloorder-api:latest
```

**3. 前端 Nginx 配置**

- 镜像内已包含 `web/nginx.conf`，其中 `proxy_pass http://api:8080/api/;` 表示将 `/api` 转到名为 `api` 的后端服务。  
- 若后端容器名为 `helloorder-api`，可改为 `proxy_pass http://helloorder-api:8080/api/;`，或使用 Docker 网络 / 实际域名。

**4. 使用 Docker Compose 编排（示例）**

可在当前 `docker-compose.yml` 基础上增加服务，例如：

```yaml
services:
  postgres:
    # ... 保持现有
  redis:
    # ... 保持现有
  api:
    build:
      context: .
      dockerfile: Dockerfile.api
    ports:
      - "8080:8080"
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=helloorder;Username=postgres;Password=postgres
      - ConnectionStrings__Redis=redis:6379
      - Jwt__Key=生产环境请使用至少32字符的密钥
      - ASPNETCORE_URLS=http://+:8080
    depends_on:
      - postgres
      - redis
  web:
    build:
      context: .
      dockerfile: Dockerfile.web
    ports:
      - "80:80"
    depends_on:
      - api
```

生产环境请务必：

- 更换 `Jwt__Key` 与数据库密码；  
- 将 `Cors__Origins` 设为实际前端域名；  
- 使用 HTTPS 与可信证书。

---

## API 概要

- **Base URL**：开发环境 `http://localhost:5000`，生产环境为实际域名。  
- **认证**：除登录外，请求头需带 `Authorization: Bearer <token>`。  
- **统一响应**：`{ "code": 0, "message": "success", "data": ... }`，分页为 `{ "list": [], "total": 0, "page": 1, "pageSize": 20 }`。

| 分组 | 示例接口 |
|------|----------|
| 认证 | `POST /api/auth/login`、`GET /api/auth/profile` |
| 用户 | `GET /api/users`、`GET /api/users/roles`（Admin） |
| 商家 | `GET/POST/PUT/DELETE /api/merchants`、`GET /api/merchants/{id}` |
| 订单 | `GET/POST /api/orders`、`GET /api/orders/{id}`、`PUT /api/orders/{id}/status` |
| 报价单 | `GET /api/quotations`（占位） |
| 采购单 | `GET /api/purchase-orders`（占位） |
| 报表 | `GET /api/reports/sales`、`/profit`、`/commission`（占位） |

更多接口与字段见 [设计文档.md](./设计文档.md)。

---

## 已实现功能

- **认证**：登录、JWT、退出、个人资料  
- **权限**：按角色（Admin/Boss/Business/Assistant）过滤数据  
- **商家管理**：列表、新增、编辑、删除（按业务员过滤）  
- **订单管理**：列表、详情、新增、状态更新  
- **用户管理**：用户列表、角色列表（仅 Admin）  
- **工作台**：占位统计与快捷入口  

报价单、采购单、仓储发货、报表与提成等模块见设计文档，可按需扩展实现。

---

## 后续可扩展

- 报价单生成与发送、采购表生成与跟进、仓储与面单、物流与售后  
- 老板看板：销量、盈利、提成规则与计算、审核  
- 店小秘/1688 对接、Excel 导入导出、操作日志与审计  

详见 [需求文档.md](./需求文档.md) 与 [设计文档.md](./设计文档.md)。
