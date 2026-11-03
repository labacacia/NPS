[English](./transport-reconciliation.md) | 中文版

# 传输规则对齐 — alpha.20

- 状态：现行 NCP v0.12 规则澄清；实现审计待完成
- 日期：2026-09-09
- 跟踪：[P20-106 / #128](https://github.com/labacacia/NPS-Dev/issues/128)

## 规范依据

[现行 NCP](./NPS-1-NCP.cn.md) 是线格式的权威来源。
RFC-0006 保留历史 Accepted 决策，其 §6 是 NCP §7.5 引用的 TLS 绑定；
其中冲突的旧文本不是另一套合法线格式。2026 年 5 月的 transport-profile
草案仅作历史记录，不是合规目标。本次对齐不改变协议版本、不分配新帧。

## 冲突台账

| ID | 历史文本 | 现行解释 / 来源 |
|---|---|---|
| TRN-01 | 草案要求每个服务器同时提供 HTTP 和 TCP | 声明并验证实际部署的绑定。NCP §2.2 定义模式，不强制双部署。 |
| TRN-02 | 草案使用通用 /nps 和 application/x-nps-frame | NWP HTTP 使用规定端点及 application/nwp-frame（NCP §2.2）。旧草案未确立通用二进制或分块隧道规范。 |
| TRN-03 | 草案使用端口 7433 和 ALPN nps/1 | 默认端口为 17433（NCP §2.3），原生 ALPN 为 nps/1.0（NCP §7.5）。 |
| TRN-04 | RFC §2.2 使用五字节 TCP 头，§3.3 删除 QUIC 长度 | 两者均采用 NCP §3 的四字节默认头或八字节 EXT 头。类型和标志各占一字节。读取可能拆分或合并帧，必须按声明长度解析，不得按读取边界解析。 |
| TRN-05 | 草案在每个 QUIC 流上创建会话并发送前导 | 保留 RFC §3.2：连接握手位于客户端双向流 0，每个逻辑通道映射到一个双向流；新通道流不重复会话握手。 |
| TRN-06 | 草案允许 early data | NCP-P19-06 禁止在 TLS/QUIC 0-RTT 中发送任何 NPS 前导或帧。恢复连接仍须确认握手并重新发送前导和 Hello。 |
| TRN-07 | RFC §8 允许在 TLS 之前发送明文前导 | RFC §6.2 和 NCP §7.5 要求非显式 local-dev 环境先建立 TLS 再发送 NPS 字节。不得 STARTTLS 或嗅探明文后升级。 |
| TRN-08 | RFC §4.1 虚构 EXT rekey:true | EXT 选择头长度，不是元数据映射。NCP §7.4 规定 NCP-REKEY-REQUIRED 和 ECDH/HKDF 轮换；不得发送虚构字段。 |
| TRN-09 | RFC §5 使用 uint16，最大 65535 | NCP Hello/Caps 定义 uint32，默认 32，协商取最小值。本地资源上限可以更低，但不能缩窄线类型。 |
| TRN-10 | RFC §3.5 强制 Hello.transport | 现行 NCP Hello 模式不要求此字段。载体在 Hello 之前确定；不得因合规 Hello 缺少 transport 而拒绝。 |
| TRN-11 | RFC §2.3/§7 虚构 Caps.status=closing 并判定所有未完成调用失败 | 未定义新的 closing Caps 形状。先本地排空再关闭载体；连接丢失不证明未执行。运行时重试及不确定结果语义由 #126 跟踪。 |
| TRN-12 | RFC §7 要求所有畸形连接发送 ErrorFrame | NCP §2.6 准入规则要求对无效或不完整前导、无效 Hello 静默关闭。在安全且已认证的成帧上下文建立前不得发送 NPS 错误。 |
| TRN-13 | 草案将 WebSocket 和通用 HTTP/2 隧道视为规范绑定 | 这些草案绑定不是资格证据。独立定义的适配器须声明契约；实现隧道本身不代表符合规范 NWP HTTP。 |

## 实现后续

P20-201..208 和 P20-301..306 须按 TRN-01..13 审计六个 SDK 及适用守护进程。
重点检查 TCP/QUIC 拆包粘包、两种头宽度、TLS 后前导、禁用 0-RTT、
仅流 0 握手、省略 Hello.transport、uint32 协商和准入前拒绝。
文档纠正不等于运行时测试通过。

P20-108 负责共享可执行向量；#129 负责规范集成与版本冻结。
若发现需要新线语义才能解决的歧义，应通过 pre-1.0 CR 解决，
不得从传输库 API 推断协议规则。

