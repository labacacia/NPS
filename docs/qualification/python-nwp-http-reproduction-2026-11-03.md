English | [中文](python-nwp-http-reproduction-2026-11-03.cn.md)

# Existing Python public HTTP client reproduction (#156)

The source-bound wheel at074eeb59015ca5489d1262988844afb12bfa5969 reproduces the
existing general-client compatibility defect. [Observed receipt](2026-11-03-python-nwp-http-reproduction.json)
records actual SDK/module/wheel hashes, OS, runtime and public request MIME values.
SDK source is unchanged. This is additional D08 evidence, not a compatibility pass.

| Path | Observed result | Provider calls |
|---|---|---|
| Public NwpClient.invoke/default MessagePack → official ActionNodeApp | HTTP400; legacy Content-Type/Accept |0|
| Public NwpClient.invoke/JSON tier → official ActionNodeApp | HTTP400; legacy Content-Type/Accept |0|
| Explicit raw JSON canonical control → same official app | HTTP200/application/nwp-capsule |1|
| Public client receiving synthetic valid binary canonical capsule | UnicodeDecodeError; binary response sent to JSON decoder | synthetic transport |

The first three rows use httpx.ASGITransport and the actual official SDK app, without
changing client requests in the observing harness. They are in-process HTTP/ASGI
execution, not a socket/TLS test or complete official deployment qualification.
The binary response row uses MockTransport and must not be relabeled as an official
server result. It isolates the existing MIME-dependent response-dispatch failure.
The raw JSON control is not a workaround secretly applied to the public client.

The issue is broader than two MIME constants: the public client emits complete NCP
header+payload messages; the official app currently parses raw JSON. The NCP/NWP
HTTP byte/JSON contract, advertised server wire formats, response decoding and public
error behavior must be reconciled through129/130/156/158 before general interoperability
is qualified. No automatic mutation retry, transport fallback or frame-to-dict shortcut
is introduced: preserving raw operation fields and accounting matters to server policy.

Reproduce after installing the exact source-bound wheel:

~~~sh
python tools/conformance/probe-python-nwp-http.py \
  --package-manifest /absolute/path/to/packages/074eeb59015c/source.json \
  --output /absolute/fresh/path/python-http-reproduction.json
~~~

The probe checks captured wheel SHA, installed version and module bytes, refuses an
existing output, makes no socket/provider-model requests and changes no SDK. It is a
failure reproducer and intentionally requires re-review if the client is corrected.
#156 remains open; distribution synchronization, released-package adoption and real
network validation are separate unfulfilled AC. The data-profile ResultHttpClient
path proven in D05 is distinct and remains supported within its recorded scope.
