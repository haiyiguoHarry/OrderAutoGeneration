# PostgreSQL 基础知识与本项目应用

本文档分为两部分：**PostgreSQL 基础知识**（入门必知）和**本项目中用到的 PostgreSQL 知识**（HelloOrder 实战对应）。

---

## 一、PostgreSQL 基础知识

### 1.1 什么是 PostgreSQL

- **PostgreSQL** 是一种开源**关系型数据库**（RDBMS），支持 SQL 标准，可在 Windows、Linux、macOS 上运行。
- 特点：支持复杂查询、外键、事务、触发器、视图、JSON 等；适合业务系统、分析场景。
- 本项目中用 **PostgreSQL 16**，通过 Docker 运行（见根目录 `docker-compose.yml`）。

### 1.2 核心概念

| 概念 | 说明 |
|------|------|
| **数据库 (Database)** | 一个独立的数据容器，本项目中为 `helloorder`。 |
| **模式 (Schema)** | 数据库内的命名空间，默认有 `public`，表都在该 schema 下。 |
| **表 (Table)** | 由行和列组成，如 `sys_user`、`order`、`merchant`。 |
| **行 (Row)** | 一条记录。 |
| **列 (Column)** | 一个字段，有类型（如 varchar、int、uuid）。 |
| **主键 (Primary Key)** | 唯一标识一行，本项目中多为 `id`（UUID）。 |
| **外键 (Foreign Key)** | 指向另一张表的主键，表示关联关系。 |

### 1.3 本项目中用到的数据类型（PostgreSQL ↔ C#）

| PostgreSQL 类型 | C# 类型 | 说明 |
|-----------------|---------|------|
| `uuid` | `Guid` | 全局唯一标识，如主键、外键。 |
| `character varying(n)` / `varchar(n)` | `string` | 可变长字符串，如用户名、名称。 |
| `integer` | `int` | 整数，如状态、数量。 |
| `numeric(p,s)` / `decimal` | `decimal` | 精确小数，如金额。 |
| `timestamp without time zone` | `DateTime` | 日期时间（无时区）。 |
| `boolean` | `bool` | 真/假。 |
| `text` | `string` | 不限长文本。 |
| `jsonb` | `string` / 对象 | JSON（设计文档中 commission_rule.config 等）。 |

### 1.4 连接数据库

**连接串格式：**

```
Host=主机;Port=端口;Database=数据库名;Username=用户名;Password=密码
```

本项目在 `appsettings.json` 中：

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=helloorder;Username=postgres;Password=postgres"
}
```

- **Host**：数据库所在机器，本地为 `localhost`，Docker 编排时可为服务名如 `postgres`。
- **Port**：默认 `5432`。
- **Database**：要连接的库名，本项目为 `helloorder`。

**用命令行连接（psql）：**

```bash
# 若用 docker-compose 启动的 PostgreSQL
docker exec -it <postgres容器名> psql -U postgres -d helloorder
```

### 1.5 SQL 入门（与本项目表对应）

#### 查询：SELECT

```sql
-- 查所有列
SELECT * FROM sys_user;

-- 指定列、条件、排序、条数（分页）
SELECT id, username, real_name, created_at
FROM sys_user
WHERE status = 1
ORDER BY created_at DESC
LIMIT 20 OFFSET 0;
```

对应到本项目：商家列表分页、按状态筛选等，都是用类似的逻辑（在 C# 里用 LINQ 表达，最终会生成此类 SQL）。

#### 插入：INSERT

```sql
INSERT INTO sys_user (id, username, password_hash, real_name, role_id, status, created_at, updated_at)
VALUES (
  'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  'admin',
  '...',
  '系统管理员',
  '11111111-1111-1111-1111-111111111111',
  1,
  NOW(),
  NOW()
);
```

本项目通过 **EF Core** 的 `_db.SysUsers.Add(entity)` 等完成插入，无需手写 SQL。

#### 更新：UPDATE

```sql
UPDATE merchant
SET name = '新名称', updated_at = NOW()
WHERE id = '某个uuid';
```

本项目通过 EF Core 修改实体后 `SaveChangesAsync()` 生成 UPDATE。

#### 删除：DELETE 与软删除

```sql
-- 物理删除（本项目一般不用）
DELETE FROM sys_user WHERE id = '...';

-- 软删除：用 status 标记禁用（本项目商家等用此方式）
UPDATE merchant SET status = 0, updated_at = NOW() WHERE id = '...';
```

### 1.6 主键、外键与索引

**主键 (Primary Key)：**

- 每张表通常有一个主键，唯一、非空。
- 本项目主键多为 `id uuid`，如 `sys_user.id`、`order.id`。

**外键 (Foreign Key)：**

- 表示“引用另一张表的主键”。
- 例如：`order.merchant_id` 引用 `merchant.id`，表示订单属于某商家。
- 本项目在 EF Core 中通过 `HasOne().WithMany().HasForeignKey()` 配置，会生成外键约束。

**唯一约束 (Unique)：**

- 某列（或列组合）不能重复。
- 例如：`sys_user.username` 唯一，在 EF 中配置为 `HasIndex(x => x.Username).IsUnique()`。

**索引 (Index)：**

- 加快按某列查询（如按订单号查订单）。
- 本项目对 `order_no`、`quotation_no`、`purchase_no` 等建了索引（`HasIndex`）。

### 1.7 多表关联查询：JOIN

```sql
-- 订单列表带商家名称（类似本项目订单列表接口）
SELECT o.id, o.order_no, o.total_amount, m.name AS merchant_name
FROM "order" o
INNER JOIN merchant m ON o.merchant_id = m.id
WHERE o.business_user_id = '某个用户id'
ORDER BY o.created_at DESC
LIMIT 20;
```

注意：`order` 是 SQL 保留字，在 PostgreSQL 里用双引号写成 `"order"`。本项目中 EF 映射的表名就是 `order`，生成的 SQL 会带引号。

### 1.8 事务 (Transaction)

- **事务**：一组 SQL 要么全部成功，要么全部回滚（保证数据一致）。
- **ACID**：原子性、一致性、隔离性、持久性。
- 本项目在 ASP.NET 中通过 EF Core 的 `SaveChangesAsync()` 在一个请求内多次修改后一次性提交，默认就是一个事务；需要多步逻辑时可用 `await _db.Database.BeginTransactionAsync()` 等。

---

## 二、本项目中用到的 PostgreSQL 知识

### 2.1 项目中的数据库与表

- **数据库名**：`helloorder`（在连接串中指定）。
- **表名**：采用 **snake_case**（小写+下划线），与 C# 实体类名（PascalCase）通过 EF 映射，例如：

| C# 实体 / DbSet | 表名 |
|-----------------|------|
| SysUser / SysUsers | sys_user |
| SysRole / SysRoles | sys_role |
| Merchant / Merchants | merchant |
| Order / Orders | order |
| OrderItem / OrderItems | order_item |
| Quotation / Quotations | quotation |
| PurchaseOrder / PurchaseOrders | purchase_order |
| Shipment / Shipments | shipment |
| CommissionRule / CommissionRecords | commission_rule, commission_record |

表结构详见《设计文档》第四节；实际建表由 **EF Core** 在首次运行时的 `EnsureCreatedAsync()` 根据 `AppDbContext` 的配置生成。

### 2.2 连接与配置（Program.cs）

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

- **Npgsql**：.NET 连接 PostgreSQL 的驱动，包名 `Npgsql.EntityFrameworkCore.PostgreSQL`。
- **UseNpgsql**：指定使用 PostgreSQL，连接串从配置读取。
- 生产环境可将连接串放到环境变量，例如：`ConnectionStrings__DefaultConnection=Host=...;...`。

### 2.3 主键与关系在本项目中的用法

**主键：**

- 实体类用 `Guid Id`，对应 PostgreSQL 的 `uuid`。
- 在 `OnModelCreating` 中：`e.HasKey(x => x.Id)`。

**一对多：**

- 例如：一个商家（Merchant）对应多个订单（Order），订单表有 `merchant_id` 外键。
- EF 配置示例：`e.HasOne(x => x.Merchant).WithMany().HasForeignKey(x => x.MerchantId)`。
- 订单与订单行（OrderItem）：`e.HasMany(x => x.Items).WithOne(x => x.Order).HasForeignKey(x => x.OrderId)`。

**多对多：**

- 角色与权限：`sys_role_permission` 为中间表，两个外键 `role_id`、`permission_id`。
- EF 中通过 `HasKey(x => new { x.RoleId, x.PermissionId })` 配置联合主键。

### 2.4 本项目中的常见查询模式

**只读列表、分页（不跟踪实体）：**

```csharp
var q = _db.Merchants.AsNoTracking();  // 不跟踪，只读更快
if (!string.IsNullOrWhiteSpace(name))
    q = q.Where(m => m.Name.Contains(name));
var total = await q.CountAsync(ct);
var list = await q.OrderByDescending(m => m.CreatedAt)
    .Skip((page - 1) * pageSize).Take(pageSize)
    .Select(m => new MerchantDto { ... })
    .ToListAsync(ct);
```

- **AsNoTracking()**：只读场景使用，不把实体放入 EF 变更跟踪，减少内存与开销。
- **Where / OrderByDescending / Skip / Take**：对应 SQL 的 WHERE、ORDER BY、LIMIT、OFFSET。
- **Select(...).ToListAsync()**：只取需要的列并转成 DTO，生成的 SQL 只查询这些列。

**带关联的查询（Include）：**

```csharp
var order = await _db.Orders
    .AsNoTracking()
    .Include(o => o.Merchant)   // 顺带查出商家
    .Include(o => o.Items)      // 顺带查出订单行
    .FirstOrDefaultAsync(o => o.Id == id, ct);
```

- **Include**：生成 JOIN，一次查询把关联数据查出来，避免 N+1 次查询。

**按当前用户过滤（数据权限）：**

```csharp
if (role != "Admin" && role != "Boss")
    q = q.Where(m => m.BusinessUserId == userId);
```

- 业务员、助理只能看自己负责的数据，对应 SQL 的 `WHERE business_user_id = @userId`。

### 2.5 本项目中的写入

- **新增**：`_db.Merchants.Add(entity); await _db.SaveChangesAsync(ct);`
- **更新**：从 `_db` 查出实体，改属性后 `SaveChangesAsync()`。
- **软删除**：如把 `merchant.Status = 0` 再 `SaveChangesAsync()`。

所有写操作最终都会由 EF Core 翻译成针对 PostgreSQL 的 INSERT/UPDATE/DELETE。

### 2.6 表与列命名约定（本项目）

- **表名**：snake_case，如 `sys_user`、`order_item`。
- **列名**：snake_case，如 `created_at`、`business_user_id`、`order_no`。
- C# 端用 PascalCase（如 `CreatedAt`、`BusinessUserId`），EF 默认会把属性名映射为同名的 snake_case 列（取决于 Npgsql/EF 的命名配置；当前项目通过 Fluent API 显式配置了表名）。

### 2.7 枚举与状态在数据库中的表示

- 订单状态、报价单状态等在 C# 里是枚举（如 `OrderStatus`），在 PostgreSQL 中存为 **整数**（如 0、1、2）。
- 设计文档中的“草稿/已发送/已确认”等对应枚举值，查询时用 `Where(o => o.Status == OrderStatus.Paid)` 等，EF 会生成 `WHERE status = 3` 这样的条件。

### 2.8 首次运行：建表与种子数据

```csharp
await db.Database.EnsureCreatedAsync();  // 若库不存在则建表
await db.SeedAsync();                    // 插入初始角色与 admin 用户
```

- **EnsureCreatedAsync()**：根据当前 DbContext 模型创建数据库和表（开发常用；生产更推荐用 **EF 迁移** 管理表结构变更）。
- **SeedAsync()**：往 `sys_role`、`sys_user` 等表插入种子数据，保证有默认管理员账号。

---

## 三、小结：对照表

| 学习点 | 基础知识 | 在本项目中的体现 |
|--------|----------|------------------|
| 连接 | 连接串 Host/Port/Database/Username/Password | appsettings.json → UseNpgsql(连接串) |
| 表与列 | 表、列、类型 | sys_user、order、merchant 等，Guid/varchar/int/decimal/timestamp |
| 主键/外键 | 主键唯一、外键关联 | id uuid 主键，merchant_id、order_id 等外键 |
| 索引/唯一 | 加速查询、唯一约束 | order_no、quotation_no 索引；username 唯一 |
| 查询 | SELECT、WHERE、ORDER BY、LIMIT、JOIN | AsNoTracking、Where、OrderBy、Skip/Take、Include |
| 写入 | INSERT、UPDATE、DELETE | Add、修改实体、SaveChangesAsync；软删除用 status |
| 事务 | 多步要么全成功要么全回滚 | SaveChangesAsync 单次提交；复杂逻辑可用 BeginTransaction |

---

## 四、延伸阅读与练习建议

1. **官方**：[PostgreSQL 官方文档](https://www.postgresql.org/docs/)（英文），可重点看 Tutorial、数据类型、SQL 命令。
2. **本仓库**：在 `src/HelloOrder.Infrastructure/AppDbContext.cs` 中看每张表的映射；在 Controllers 里看查询与分页写法。
3. **练习**：用 psql 或 pgAdmin 连上 `helloorder`，执行 `\dt` 看表列表，用简单 `SELECT * FROM sys_user LIMIT 5` 观察数据。

把上述“基础知识”和“本项目中用到的知识”对照代码与设计文档一起看，会更容易掌握 PostgreSQL 在本项目中的实际用法。
