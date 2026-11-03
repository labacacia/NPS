[English](python-nwp-http-reproduction-2026-11-03.md) | 中文

# 既有 Python 通用 HTTP 客户端复现（#156）

绑定源码074eeb59015ca5489d1262988844afb12bfa5969的公开wheel复现了既有通用客户端
兼容缺陷。[观测回执](2026-11-03-python-nwp-http-reproduction.json)记录SDK/模块/
wheel哈希、系统、运行时及实际请求MIME。SDK源码未修改，这是D08补充失败证据，
不是兼容通过。

| 路径 | 实际结果 | 业务处理器调用数 |
|---|---|---|
| 公开NwpClient.invoke默认MessagePack → 官方ActionNodeApp | HTTP400，请求Content-Type/Accept仍为旧别名 |0|
| 公开NwpClient.invoke的JSON层 → 同一官方App | HTTP400，请求Content-Type/Accept仍为旧别名 |0|
| 显式原始JSON、规范MIME对照 → 同一App | HTTP200/application/nwp-capsule |1|
| 公开客户端收到合成的有效规范二进制capsule | UnicodeDecodeError，二进制响应进入JSON解析器 |合成传输|

前三项用httpx.ASGITransport执行真实SDK官方App，观察层不改写客户端请求；属于
进程内HTTP/ASGI执行，不是socket/TLS测试或完整部署验收。二进制响应项单独使用
MockTransport，不能称为官方服务端结果。原始JSON对照没有偷偷应用到公开客户端。

问题不只两个MIME常量：公开客户端发完整NCP头+负载，官方App目前直接解析原始
JSON。须通过129/130/156/158核对HTTP字节/JSON契约、服务端宣告格式、响应解码及
公开错误行为，再验收通用互操作。不添加自动重试变更操作、静默格式降级或
frame转dict捷径，接收方策略依赖原始操作字段及字节计量。

安装精确源码wheel后，按英文页命令运行`tools/conformance/probe-python-nwp-http.py`，
提供包`source.json`和新的输出路径。工具核对wheel哈希、安装版本和模块字节，
拒绝覆盖，不发socket或模型请求，不改SDK。它是失败复现器，客户端修正后须重新
审查预期。#156保持开放；真实网络验证、发布仓库同步及发布包采用仍未完成。
D05已验证的数据profile专用ResultHttpClient路径与此不同，其既有支持范围不变。
