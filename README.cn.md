[English](README.md) | 中文

# NPS — Neural Protocol Suite

**1.0.0-alpha.20 发布候选，尚未发布。**

NPS提供NWP请求/结果资源、NIP身份/授权、NCP传输、NOP编排和NDP发现。
本候选完成的是有边界的数据工作流实验，未完成完整生产或beta资格。

- .NET和Python的ResultHttpClient至.NET HTTP参考实现已有界限、授权及真实
  mTLS的100/1000/100000行证据。
- .NET原生重复schema批次已有JSON和MessagePack参考测试。
- 本地四worker NOP交接及两种易失NDP转移已有故障/恢复证据；远程生产编排未获资格。
- 六SDK辅助契约/解析器/回归检查与上述运行路径分开。其他结果/原生路径及Python
  通用NwpClient仍未获资格。

使用前阅读[支持台账](docs/qualification/alpha20-support-2026-11-03.cn.md)、
[接受范围](docs/adr/2026-11-03-alpha20-experimental-closeout.cn.md)、
[实验证据](docs/plans/2026-11-03-alpha20-experimental-ac-evidence.cn.md)及
[协议](spec/NPS-0-Overview.cn.md)。

等价HTTPgzip及普通工具对照未证明普遍的token、费用、延迟或传输优势；负面测量
保留。历史生产和beta义务没有被计作通过。

候选分支和本地包供审查，不代表alpha.20已在公开registry可安装。实际发布需完成
[#134](https://github.com/labacacia/NPS-Dev/issues/134)的源码绑定候选、发布前检查、
发布权限及发布记录。

源码在NPS-Dev编写，由发布归属工具投影至NPS、六SDK、NPS-Daemons和
NIP-CA-Server。Model-Runtime、Console和Paper保持独立生命周期。
