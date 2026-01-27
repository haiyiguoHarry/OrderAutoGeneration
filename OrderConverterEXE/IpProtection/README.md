# IPProtection（知识产权保护）模块说明

本模块实现：
- **无授权不可用**
- **授权到期不可用（必须替换你重新签发的 license）**
- **机器绑定**
- **时间回拨检测（Windows DPAPI 加密 state.dat）**

## 使用方式（客户侧）

### 1) 放置授权文件
把你签发的 `license.json` 放到任意一个位置（优先级从高到低）：
1. `C:\ProgramData\OrderConverterEXE\license.json`
2. 程序 exe 同目录：`license.json`

### 2) 打印机器指纹
在客户机器运行：
```bash
OrderConverterEXE.exe --print-fingerprint
```
把输出的指纹发给你用于签发授权。

## 你需要做的唯一配置（发行方）

在 `IpProtectionConstants.IssuerPublicKeyPem` 中替换为你的 **ECDSA P-256 公钥 PEM**。

> 私钥只用于你本地签发工具，不要放进项目/不要发给客户。

