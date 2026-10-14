# NPS 文档站点（`/docs`）

> [English](README.md) | 中文版

本目录是一个自包含的 **GitHub Pages** 站点，基于 [MDWiki](https://dynalon.github.io/mdwiki/) 0.6.2 构建。

零构建步骤 — Markdown 写入，浏览器端渲染。

---

## 启用 GitHub Pages

1. 把本目录推到 `main`。
2. 仓库 → **Settings** → **Pages**。
3. **Source**：*Deploy from a branch*。
4. **Branch**：`main` / **Folder**：`/docs`。
5. Save。站点发布在 `https://nps.labacacia.com/`。

本目录里的 `.nojekyll` 用于关闭 Jekyll 处理 — MDWiki 是纯客户端 SPA，不需要任何构建流程。

---

## 本地预览

```bash
cd docs
python -m http.server 8080
# 打开 http://localhost:8080/
```

MDWiki 需要 **http(s)**，不能用 `file://` — 自带的 Python server 就够用。

---

## 写作规范

- 顶层英文页面：`index.md`、`overview.md`、`protocols.md`、`sdks.md`、`roadmap.md`。
- 中文镜像：追加 `.cn.md`（例如 `overview.cn.md`）。按项目文档规范，每页顶部必须有语言切换器。
- 导航：`navigation.md` — 单文件，两种语言共用。MDWiki 会把它渲染成顶栏。语言下拉把用户带到另一语种的首页。
- 站点配置：`config.json`（标题、主题、页脚）。
- URL 使用 MDWiki 的 `#!` 片段路由 — 如 `#!overview.md` 或 `#!overview.cn.md`。

### 新增页面

1. 建 `yourpage.md`（英文）和 `yourpage.cn.md`（中文）。
2. 两个文件的第一行都放语言切换器：
   - 英文：`> English | [中文版](yourpage.cn.md)`
   - 中文：`> [English](yourpage.md) | 中文版`
3. 如果想出现在顶部导航，在 `navigation.md` 里加一行。

### 内部链接

- 中文页链中文页，英文页链英文页。
- 跨语言仅通过顶部的语言切换器。

---

## 本目录下的文件清单

| 文件 | 用途 |
|------|------|
| `index.html` | MDWiki 0.6.2 SPA（从 `mdwiki.html` 重命名）|
| `MDWIKI-LICENSE.txt` | MDWiki GPLv3 许可证 — 必需的归属声明 |
| `config.json` | 标题、主题（`flatly`）、页脚 |
| `navigation.md` | 顶栏导航 |
| `.nojekyll` | 关闭 GitHub Pages 的 Jekyll |
| `index.md` / `index.cn.md` | 首页（英 / 中） |
| `overview.md` / `overview.cn.md` | "什么是 NPS？" |
| `protocols.md` / `protocols.cn.md` | 五层协议 |
| `sdks.md` / `sdks.cn.md` | SDK 矩阵 + 快速开始 |
| `roadmap.md` / `roadmap.cn.md` | Phase 0 → Phase 4 |

---

## 升级 MDWiki

MDWiki 发布页：<https://github.com/Dynalon/mdwiki/releases>。

```bash
cd docs
curl -L https://github.com/Dynalon/mdwiki/releases/download/X.Y.Z/mdwiki-X.Y.Z.zip -o /tmp/mdwiki.zip
unzip -p /tmp/mdwiki.zip mdwiki-X.Y.Z/mdwiki.html > index.html
```

MDWiki 本体是 **GPLv3**；本目录下的 NPS 内容是 **Apache 2.0**。把 `index.html` 与 `MDWIKI-LICENSE.txt` 放在一起，既保留了 GPL 归属，又不会污染周边的 Markdown 内容。
