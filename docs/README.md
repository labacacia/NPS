# NPS documentation site (`/docs`)

> English | [中文版](README.cn.md)

This folder is a self-contained **GitHub Pages** site built on [MDWiki](https://dynalon.github.io/mdwiki/) 0.6.2.

No build step. Markdown in, HTML in the browser.

---

## Enable GitHub Pages

1. Push this folder to `main`.
2. Repository → **Settings** → **Pages**.
3. **Source**: *Deploy from a branch*.
4. **Branch**: `main` / **Folder**: `/docs`.
5. Save. Site publishes at `https://nps.labacacia.com/`.

`.nojekyll` in this folder disables Jekyll processing — MDWiki is a pure client-side SPA, so nothing needs to be built.

---

## Local preview

```bash
cd docs
python -m http.server 8080
# open http://localhost:8080/
```

MDWiki requires **http(s)**, not `file://` — the built-in Python server is enough.

---

## Authoring

- Top-level English pages: `index.md`, `overview.md`, `protocols.md`, `sdks.md`, `roadmap.md`.
- Chinese mirrors: append `.cn.md` (e.g. `overview.cn.md`). Per project doc policy, every page has a language switcher at the very top.
- Navigation: `navigation.md` — single file, shared across languages. MDWiki renders it into the top bar. Language dropdown returns users to the opposite-language landing page.
- Site config: `config.json` (title, theme, footer).
- URLs use MDWiki's `#!` fragment routing — `#!overview.md` or `#!overview.cn.md`.

### Adding a new page

1. Create `yourpage.md` (English) and `yourpage.cn.md` (Chinese).
2. Both files start with the language switcher line:
   - English: `> English | [中文版](yourpage.cn.md)`
   - Chinese: `> [English](yourpage.md) | 中文版`
3. If the page should appear in the top nav, add a line to `navigation.md`.

### Internal links

- CN pages link to CN pages, EN pages link to EN pages.
- Cross-language links only through the language switcher at the top.

---

## Files shipped in this folder

| File | Purpose |
|------|---------|
| `index.html` | MDWiki 0.6.2 SPA (renamed from `mdwiki.html`) |
| `MDWIKI-LICENSE.txt` | MDWiki GPLv3 license — required attribution |
| `config.json` | Title, theme (`flatly`), footer text |
| `navigation.md` | Top-bar navigation |
| `.nojekyll` | Disables GitHub Pages Jekyll |
| `index.md` / `index.cn.md` | Landing page (EN / CN) |
| `overview.md` / `overview.cn.md` | "What is NPS?" |
| `protocols.md` / `protocols.cn.md` | The five layers |
| `sdks.md` / `sdks.cn.md` | SDK matrix + quick start |
| `roadmap.md` / `roadmap.cn.md` | Phase 0 → Phase 4 |

---

## Updating MDWiki

MDWiki releases: <https://github.com/Dynalon/mdwiki/releases>.

```bash
cd docs
curl -L https://github.com/Dynalon/mdwiki/releases/download/X.Y.Z/mdwiki-X.Y.Z.zip -o /tmp/mdwiki.zip
unzip -p /tmp/mdwiki.zip mdwiki-X.Y.Z/mdwiki.html > index.html
```

MDWiki itself is **GPLv3**; the NPS content in this folder is **Apache 2.0**. Keeping `index.html` and `MDWIKI-LICENSE.txt` together preserves the GPL attribution without infecting the surrounding Markdown.
