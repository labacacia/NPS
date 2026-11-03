[English](alpha20-security-observation-2026-11-03.md) | 中文

# D20-08 依赖修复证据

状态：IN_PROGRESS，所列本地审计均通过，待干净提交 CI。这只是集成的一个增量，不代表 alpha.20
发布或完整安全验收。基线 D07 `2023e0b` 的常规 CI41592588893 十项全过；Security
Gates41592357004 的 npm/NuGet/Go/Maven/RustSec 失败，registry authority 跳过，
PyPI 和工作流/历史扫描通过。常规 CI 成功不豁免安全失败。

| 范围 | 修复及边界 |
|---|---|
| .NET | MessagePack 3.1.9、SQLite bundle 3.0.5，对齐 SDK 和 daemon 源码/发布 overlay；固定 codec/API 不变。 |
| TypeScript | Vitest 4.1.12 及 mocker/Vite/source-map 修复；CA 示例 Fastify 5.12.6/fast-uri 修复，含开发依赖一同审计。 |
| Java | Jackson 2.18.11、BC 1.86；CA 迁移 Boot 4.0.9/Spring 7.0.10，签名/存储显式保留 Jackson 2，Web 采用 Jackson 3。保留 Java 21，Gradle 8.14.3 固定官方校验和。 |
| Rust | 锁定图更新 Quinn 0.11.19、rustls 0.23.46、webpki 0.103.16、roots 1.0.11、Tokio 1.54.1；CA 示例更新 Tokio。使用者的已有锁文件也须自行更新。 |
| Go | CI/安全及 CA 构建器采用 Go 1.26.10；保留 Go 1.23 语言下限，不宣称旧运行时已修复。可达结果与所需模块公告分开报告。 |

[英文页](alpha20-security-observation-2026-11-03.md)列出官方公告和 Boot 迁移依据。
Spring 6.2 分支对应公告无公开修复版本，因此迁移完整 Boot 4 依赖图，不混搭 Boot 3/Spring 7。

已执行：.NET1666/1666、runner46/46、TypeScript1324/1324、Java744/744、Rust765/765
及 Go SDK 测试。六个 NuGet 解析目标无报告告警/警告；两个 Maven 运行时图 OSV 为零；
两个 npm 图报告为零。Go SDK govulncheck 为零可达漏洞、零受影响的导入包，但所需模块
仍有 20 项未调用代码的公告，原样保留。这些只代表相应扫描范围，不能概括为“完全无漏洞”。
Rust SDK/CA 审计的漏洞及警告均为零。CA-Go 也为零可达/导入包漏洞，保留22项所需模块公告。远端门槛仍待完成。

Java CA 实际 HTTP 验证包括未授权注册在写库前拒绝、嵌套 Unicode/整数签名的独立 JDK
验证、重复登记、查询及 v2 X.509 叶证书/根/公钥。构建的 Boot JAR 独立启动，嵌入 schema
加载和 health/discovery/401 检查通过。验证暴露并修复两个旧启动问题：WAL 结果集未关闭、
schema 仅按文件系统路径读取。初次 Jackson2 缺失编译失败和后续 SQLite 启动失败均保留，
修复后重测，并完成独立审查。另一次实际检查发现旧密钥文件权限000；已修正0600及fallback顺序，并验证同一密钥/SQLite重启后的公钥/serial连续性。这不证明生产 TLS/PKI；空 operator key 的旧宽松
行为仍在本轮“已配置 key”准入边界之外。

本地 npm9 解析器 edgesOut 失败后，通过任务内 npm11.11.0 正常生成和安装锁定图，未全局
替换 npm 或压制公告。Rust/Go 工具下载到仓库外任务产物目录，以官方 SHA-256 验证，未
修改 shell profile。NuGet 门槛在原有零告警判定前输出包/公告元数据，便于诊断。未新增
scanner ignore、放宽阈值或改变默认能力，无付费请求、发行投影、标签或发布。

下一步：完成剩余审计/CI，记录精确源码和证据，继续 D08 支持/兼容/旧门槛/漂移/包交付。
Registry 凭据、外部所有者/采用方决定、其余运行时绑定、Beta 及发布要求均须各自闭环。

[绑定源码/测试/审计的本地证据](2026-11-03-alpha20-security-tests.json)。

干净源码535d0f7的 Security Gates41597419804 七项扫描全部通过；PR 事件跳过 registry
authority。首次常规 CI41597571436 九项通过，Java 的最后打包探针因 runner 无 rg 而失败，
此前 SDK/CA 测试及 bootJar 均已通过。已改用 Python 标准库选择唯一 JAR，实际目录探针
通过，空/多 JAR 目录在创建状态前拒绝。[保留失败及修复证据](2026-11-03-java-ca-portable-probe.json)。
修正源码 CI 待完成；另行手工触发的 registry preflight 因缺凭据清单失败，七项扫描成功
不豁免这项要求。

修正源码 `92bf98e` 的 [普通 CI 41599401714](https://github.com/labacacia/NPS-Dev/actions/runs/41599401714) 全部 10 项通过，[安全扫描 41599410395](https://github.com/labacacia/NPS-Dev/actions/runs/41599410395) 的 7 个扫描任务通过。PR 中发布权限库存检查被跳过，先前手动检查的失败仍未解决；这些结果不证明已具备发布权限。
