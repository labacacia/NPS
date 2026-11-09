[English](schema-batches.md) | 中文

# NCP schema批次：候选修订1

2026-11-02。实验、默认关闭；[CR-0017](../cr/NPS-CR-0017-schema-batches.md)，D20-04 #168。
拟定精确选择为`nps.schema_batches=1`，当前是候选契约，不代表整套/registry支持。.NET
显式启用的mTLS参考已实现精确会话选择，其他绑定/整套资格仍待验收。D20-00冻结结果文件和
未选择profile时的基础帧行为不变。

## 准入与表示

两端须显式启用，并在实际已准入连接/操作验证选择后才解释位置数据。调用方布尔值、
锚点、版本、流ID或feature offer均不提供身份或资源权限。接收器绑定一个已认证生产者、
获准来源及订阅，每次交付前复查实时权限。本profile不注册NWP结果流动作，不绕过NIP，
也不公告端点。重连/故障转移须重新准入并获取完整来源快照，不重放未证明的变更。

.NET初版参考只在实际双向认证TLS会话选择此feature；返回应用会话前检查有界原始Hello/
握手Caps、精确选择及旧端遗漏。会话工厂拥有一个有效接收器，施加协商payload/编码上限，
会话关闭时释放接收器。通用NIP身份/来源权限及其他载体须独立验收，详见
[会话绑定ADR](../../docs/adr/2026-11-02-schema-batch-session-binding.md)。

**SB-01.** 字段顺序有意义。使用现有AnchorIdComputer的JCS/SHA-256验证anchor_id，不能用
旧缓存按字段排序的helper解释位置。最多64个唯一非空字段名，每个最多64 UTF-8字节。
参考子集为string、bool、int32、uint64（仅0..9007199254740991），可显式nullable。
不支持浮点/decimal/指数数字，不做转换。schema TTL为1..900秒，绝对到期，同时检查单调
耗时和墙钟；到期边界不可用。

**SB-02.** Caps/Stream的每个data项是按schema顺序的JSON数组，每字段恰好一个值，null
必须显式保留。不允许少格、多格、强转、未知schema或隐式对象转数组。MessagePack使用
现有数组编码，旧对象仍是对象。Caps.count等于行数；本修订的单次Caps不建立差量基线。
这与模型端JSON分词无关。

两种编码均使用本profile的规范snake_case信封/schema/patch成员名，包括带feature的原生
握手Caps；不能把语言属性名当wire键，也不能改写用例键来让某SDK通过。.NET使用显式选择的
SchemaBatchFrameCodec/会话编码器；未选择的旧codec行为保留。旧端仅基础Caps只能被识别为
缺少feature确认，不能构成已选择修订。

## 基线与差量

**SB-03.** 一个接收器最多拥有一个schema和一个基线epoch。stream_id为标识该epoch的
UUID v4。完整基线从seq=0及显式anchor_ref开始，后续seq连续且不回绕，可省略anchor_ref，
出现时必须相同。完整快照暂存且不可见；只有无错误的is_last=true发布版本0，允许空快照。
异常终帧、缺号、重复、乱序、不同流ID或schema变化会清空暂存并终止接收器，不暴露部分来源。

**SB-04.** Diff只能用于已完成基线：entity_id等于基线stream_id，anchor_ref相同，base_seq
等于当前数据版本。epoch标识变化中的数据集，不是不可变结果句柄；完整状态标识为
（生产者/订阅、epoch、schema、版本）。参考只接受json_patch的1..256个不重复replace，路径
为规范`/<行下标>/<列下标>`，无前导零、符号、转义或from。下标须存在，新值须符合字段类型。
全部替换原子生效后版本加一，不得溢出。
显式`value:null`是已提供值，可用于nullable单元格；缺少value无效。仅省略patch_format时
默认json_patch，显式null不能选择默认值。
不支持增删移动/复制、schema patch或binary_bitset回退；格式不支持须明确失败，重复/错误
基线不能静默重放或应用两次。

**SB-05.** schema/更新/序号/窗口/取消失败均使接收器终止并清空数据。恢复必须新建接收器，
重新授权完整Stream基线、使用新epoch ID并从版本0开始；缺失/过期schema还须重新发送已验证
Anchor。不能猜基线或跨重连继承缓存/credit。确定性拒绝是有效结果，隐式部分恢复不是。

## Credit及限额

**SB-06.** 接收器本地在首次交付前设置信用，最多8帧。首帧window_size只能缩小不能扩大；
后续数据帧不能补充。每个已接受chunk消耗一份。消费者排空/处理后才可通过实际绑定的现有
反向空StreamFrame机制补充有界绝对credit。零credit必须暂停，继续发送会窗口溢出并终止。
控制帧按方向/状态识别，不能把同方向空数据块当ACK。不引入无界队列。

**SB-07.** 参考硬上限：每帧编码payload 65536字节、每块512行、留存100000行/33554432字节
紧凑UTF-8元组（包括数组标点）、64列、256个patch、8credit、每接收器一个epoch/schema。
参考计量并持有相同的JsonSerializer/Utf8JsonWriter紧凑元组，不是任意数据的RFC 8785 JCS。
计量时排除的对端空白不能仍保留在持有的行中。
采用协商与本地较小限额。解码分配/进程RSS不同于留存字节，须实测。溢出/取消不返回部分成功，
也不保留有效基线。宿主负责有界解码、串行入口、取消及消费者生命周期；接收器非线程安全。

## 错误、证据与资格

**SB-08.** 复用现有错误：未知schema为AnchorNotFound；schema/hash/类型为AnchorSchemaInvalid
或AnchorIdMismatch；切schema为AnchorStale；块/更新/基线顺序为StreamSeqGap；epoch不符为
StreamNotFound；credit为StreamWindowOverflow；行/字节/数量限额为StreamLimitExceeded或
FramePayloadTooLarge；不支持差量为DiffFormatUnsupported。诊断有界且通用，不含行或任意
对端字符串；本地取消按取消传播。

**SB-09.** 合格证据须含真实编码帧、实际已协商绑定、来源oracle往返/恢复、对抗用例、
慢消费者/credit/取消及协商payload上限。仅本地接收器不代表原生传输/NWP权限/全SDK通过。
声明收益前须把完整schema/帧/数据及schema变化与等价gzip JSON及替代表示在相同边界比较，
报告分配/CPU/峰值内存及负结果。不推导模型token、计费或Arrow/Flight/Protobuf/传输优势。
