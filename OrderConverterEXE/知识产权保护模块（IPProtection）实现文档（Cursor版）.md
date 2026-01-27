# 知识产权保护模块（IPProtection）实现文档（Cursor版）

> 目标：在 `OrderConverterEXE` 中加入“必须经你授权才能使用、到期必须续签、Windows 离线运行、尽量防系统时间回拨”的知识产权保护模块。  
> 本文档面向 **Cursor** 开发流程：按步骤执行即可落地实现并完成验收。

---

## 1. 需求与约束（验收口径）

### 1.1 必须满足
- **无有效授权文件则程序不可用**（建议：直接退出，exit code ≠ 0）
- **授权有截止日期**：超过 `validTo` 后必须替换为你重新签发的新授权文件才能继续使用
- **Windows 运行**（可使用 Windows 专有能力，如 DPAPI、Authenticode 校验等）
- **授权文件由你签名**：程序内置公钥，只做验签，不包含私钥
- **尽量防系统时间回拨**：检测到明显回拨则拒绝运行或进入锁定态
- **日志可追踪**：授权状态、失败原因要写入 `xlsx_converter.log`

### 1.2 推荐增强（可选）
- **对 exe 做代码签名（Authenticode）**：证明发布者身份（你本人实名认证证书）
- **输出溯源**：对 Orders 输出的 `.xlsx`/采购表写入隐形指纹（隐藏 sheet/自定义属性）

---

## 2. 总体设计（模块拆分）

新增目录：`OrderConverterEXE/IpProtection/`

- **`IIpGuard`**：对外门禁接口（启动校验 + 周期复检）
- **`LicenseManager`**：加载/验签/解析 license
- **`MachineFingerprint`**：计算机器指纹（Windows）
- **`TimeTamperDetector`**：时间回拨检测（LastSeenUtc + DPAPI）
- **`IntegrityVerifier`（可选）**：完整性校验（hash manifest / Authenticode 状态）
- **`FeatureGate`（可选）**：授权功能开关（本需求可先不用，直接“允许/禁止”）

---

## 3. License 文件规范（强制）

### 3.1 文件位置（推荐二选一）
- **方案 A（简单）**：与发布 exe 同目录：`license.json`
- **方案 B（更规范）**：`C:\\ProgramData\\OrderConverterEXE\\license.json`

实现时建议同时支持 A + B，优先 B，其次 A。

### 3.2 JSON 内容（示例）

```json
{
  "schemaVersion": 1,
  "licenseId": "LIC-2026-000123",
  "issuedTo": "客户公司/项目",
  "validFromUtc": "2026-01-01T00:00:00Z",
  "validToUtc": "2026-03-31T23:59:59Z",
  "machineBinding": {
    "fingerprintSha256": "HEX-OR-BASE64"
  },
  "issuer": {
    "name": "你的姓名",
    "idHash": "SHA256(身份证号+salt)截断（不要存明文身份证号）"
  },
  "signature": "BASE64(签名值)"
}
```

### 3.3 签名覆盖规则（关键）
签名必须覆盖 **除 `signature` 外的全部字段**，并且必须解决 JSON 顺序/空格差异问题：

- **做法**：对 JSON 做“规范化序列化（Canonical JSON）”，再对 UTF-8 bytes 签名
- **建议规则**：
  - 属性按字典序排序
  - 不输出多余空格/换行
  - 时间统一使用 UTC ISO-8601：`yyyy-MM-ddTHH:mm:ssZ`

> 验收点：同一份 license 在不同机器/不同序列化方式下仍可验签通过（因为你是“签规范化后的内容”）。

### 3.4 签名算法（推荐）
- **Ed25519**（优先推荐）：验签快、实现简单
- 备选：ECDSA P-256

实现时建议引入成熟库（NuGet）来做 Ed25519，避免自写密码学。

---

## 4. 机器指纹（Windows）

### 4.1 指纹策略
指纹需要“稳定 + 难伪造 + 不泄露隐私”。推荐组合并哈希：
- `MachineGuid`（注册表）：`HKLM\\SOFTWARE\\Microsoft\\Cryptography\\MachineGuid`
- 系统盘卷序列（可选）
- 计算机名（可选）

最终计算：
- `fingerprintRaw = $"{machineGuid}|{volumeSerial}|{computerName}".ToUpperInvariant().Trim()`
- `fingerprintSha256 = SHA256(fingerprintRaw)`（输出 HEX / Base64）

> 不要在日志里输出原始指纹材料，只输出哈希前 8~12 位用于排查。

---

## 5. 防系统时间回拨（强烈建议实现）

### 5.1 思路
保存一个“最后见到的 UTC 时间” `lastSeenUtc`：
- 每次启动/周期复检：读取 `state.dat`，对比 `nowUtc`
- 若 `nowUtc < lastSeenUtc - 容忍阈值(5min)`：判定回拨，拒绝运行（或锁定）
- 每次正常运行：`lastSeenUtc = max(lastSeenUtc, nowUtc)` 并写回

### 5.2 state 存储位置
`C:\\ProgramData\\OrderConverterEXE\\state.dat`

### 5.3 state 加密（Windows DPAPI）
使用 DPAPI `CurrentMachine` 加密后落盘，避免被直接编辑：
- 存储内容建议包含：
  - `lastSeenUtc`
  - `lastLicenseId`
  - `lastLicenseValidToUtc`

> 验收点：用户手工修改 `state.dat` 不应轻易绕过（至少会导致解析/解密失败并拒绝运行）。

---

## 6. 程序接入点（与你现有结构对齐）

### 6.1 启动前强制门禁（硬门禁）
在 `Program.cs` 中 `host.RunAsync()` 之前做：
- 读取并校验 license
- 校验到期
- 校验指纹绑定
- 校验时间回拨
-（可选）校验 exe 代码签名/完整性

失败：打印用户可理解提示 + 写日志 + 退出。

### 6.2 运行中周期复检（软门禁）
在 `XlsxConverterService.ExecuteAsync` 循环中每隔 N 分钟复检（推荐 10~30 分钟）：
- 发现过期/回拨/文件被替换为无效：停止扫描并退出（或停止服务）

> 好处：防止“启动时用新 license，启动后换回旧 license”。

---

## 7. 错误码与日志规范（方便客户沟通）

建议统一错误码前缀：`IP-XXXX`

示例：
- `IP-0001`：未找到授权文件
- `IP-0002`：授权文件验签失败
- `IP-0003`：授权已过期（展示 `validToUtc`）
- `IP-0004`：机器不匹配（展示指纹 hash 前 12 位）
- `IP-0005`：检测到系统时间回拨（展示 lastSeenUtc / nowUtc）
- `IP-0006`：授权文件时间格式错误
- `IP-0007`：state.dat 解密失败（可能被篡改）

日志要求：
- 写入现有 `xlsx_converter.log`
- 不输出身份证号明文
- 只输出必要摘要（licenseId、validTo、fingerprint 前缀）

---

## 8. 续签流程（你的运营流程）

### 8.1 客户侧
- 客户运行一次程序（或你提供一个小命令）输出指纹：
  - `fingerprintSha256`（只需要哈希）
- 客户把指纹和客户名发给你

### 8.2 你侧签发
你使用“签发工具”生成新 `license.json`：
- 填 `issuedTo`、`validFromUtc`、`validToUtc`、`fingerprintSha256`
- 用私钥签名
- 发给客户替换

### 8.3 客户续签
替换 `license.json` 后重启程序即可。

> 需要支持“licenseId 递增/唯一”，并在 `state.dat` 记录最近 licenseId，避免回滚到旧 license。

---

## 9. 发布签名（Authenticode，强烈建议）

### 9.1 目标
让 Windows 在文件属性中显示“数字签名”，证明该 exe 由你签名发布，且未被篡改。

### 9.2 实施方式
- 申请 **个人代码签名证书**（实名核验，通常会核验身份证）
- 发布后对 exe（以及单文件包）执行签名
-（推荐）带时间戳服务，避免证书过期后签名失效

### 9.3 验收点
- 未签名/被篡改时，校验模块可检测并拒绝运行（可选）
- Windows 资源管理器属性可看到签名信息

---

## 10. Cursor 落地实施步骤（强执行清单）

> 下面是你在 Cursor 里逐步实施的建议“操作顺序”，每一步都能独立验收。

### 步骤 A：创建模块骨架
- 新建目录 `IpProtection/`
- 新建接口与 DTO：
  - `IIpGuard`
  - `LicenseRecord`（对应 JSON 字段）
  - `IpGuardResult`（是否通过 + 错误码 + 用户可读消息）

### 步骤 B：实现 License 验签
- 选定 Ed25519 库并接入
- 实现 Canonical JSON：
  - 固定字段顺序
  - 时间字段严格校验为 UTC
- 验签通过后再做业务校验：
  - `validFromUtc <= nowUtc <= validToUtc`
  - `fingerprintSha256` 匹配

### 步骤 C：实现 MachineFingerprint
- 读取 `MachineGuid`
-（可选）卷序列
- 输出 SHA256

### 步骤 D：实现 TimeTamperDetector（DPAPI）
- 读写 `state.dat`
- 回拨判断
- 正常写回 lastSeen

### 步骤 E：接入 Program.cs + Service 周期复检
- `Program.cs`：启动前强校验，失败退出
- `XlsxConverterService`：每 N 分钟复检一次（失败停止）

### 步骤 F：签发工具（可独立小项目/脚本）
建议新增一个小控制台项目 `LicenseIssuerTool`（只在你手里用，不发给客户）：
- 输入：issuedTo、validTo、fingerprint
- 输出：license.json（含签名）

### 步骤 G：验收用例（必须跑）
- 无 license：拒绝运行（IP-0001）
- license 验签失败：拒绝（IP-0002）
- 过期：拒绝（IP-0003）
- 换机器：拒绝（IP-0004）
- 时间回拨：拒绝（IP-0005）
- 续签替换新 license：恢复运行

---

## 11. 安全注意事项（避免踩坑）
- **私钥永不进入代码库**，也不要放在客户机
- **不要存身份证号明文**，只存 hash（可截断）
- Canonical JSON 不要“随便 ToString()”，否则跨平台/跨版本序列化差异会导致验签失败
- 回拨防护并非绝对（例如系统快照回滚/重装系统），但能挡住大多数低成本绕过

---

## 12. 你需要提供的信息（实现时的常量/配置）
- 公钥（Ed25519 / ECDSA 的公钥字符串）
- state 路径策略（是否用 ProgramData）
- 复检间隔（建议 10~30 分钟）
- 失败策略（建议：直接退出）

