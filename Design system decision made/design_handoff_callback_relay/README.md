# Handoff: Callback Relay — Web UI

## Overview
Callback Relay is a self-hosted developer tool that receives HTTP webhooks from external
services and forwards them in real time to apps running on a developer's local machine
(a self-hosted mix of Webhook.site + ngrok). This package covers the full web UI:

1. **Dashboard** (`/`, served as `wwwroot/index.html`) — the live callback feed
2. **Login** (`/account/login`)
3. **Register** (`/account/register`)
4. **Listener management** (`/account/listeners`)
5. **API Keys** (`/account/keys`)

## About the Design Files
The files in this bundle are **design references created in HTML** — interactive prototypes
that show the intended look, layout, and behavior. They are **not production code to copy
directly.**

> **Important authoring note:** the prototypes are written as "Design Components"
> (`*.dc.html`) — a streaming-template format used by the design tool. The custom tags
> (`<x-dc>`, `<sc-for>`, `<sc-if>`, `{{ holes }}`) and the `support.js` runtime are an
> artifact of that tool and **must not be shipped**. Read them only to understand structure,
> data, and behavior. Re-implement in the real stack.

**Target stack for this project:** ASP.NET Core 9, SignalR for real-time updates, **vanilla
JS (no framework)**, served as static files from `wwwroot/`. Recreate each screen as plain
HTML + CSS + vanilla JS using that environment's conventions. The live feed should be driven
by **SignalR** (each incoming callback pushes a new card to the top); the prototype fakes the
data with a static in-memory array.

## Fidelity
**High-fidelity.** Final colors, typography, spacing, radii, and interactions are all
specified below and present in the prototypes. Recreate the UI pixel-accurately, then wire
the data to the real backend (SignalR hub for the feed, REST endpoints for
listeners/keys/auth).

---

## Design Tokens

### Color — surfaces & lines (dark theme, VS Code-like)
| Token | Hex | Use |
|---|---|---|
| `bg` | `#0a0a0a` | App background, scrollbar track |
| `bg-header` | `#0d0d0d` | Top bar, toolbars |
| `bg-sidebar` | `#0c0c0c` | Sidebar, inset preview panels |
| `surface` | `#111111` | Cards, modals, table body |
| `surface-2` | `#141414` | Inputs, icon buttons |
| `surface-raised` | `#1a1a1a` | Active nav pill, hover fills |
| `surface-hover` | `#131313` / `#161616` | Row & card-head hover |
| `line` | `#1f1f1f` | Primary hairline borders |
| `line-soft` | `#161616` | Row separators |
| `border` | `#2a2a2a` | Input/button borders |
| `border-2` | `#232323` | Card borders on auth pages |

### Color — text
| Token | Hex | Use |
|---|---|---|
| `text` | `#e6e6e6` | Primary text |
| `text-strong` | `#d6d6d6` | Mono paths, labels |
| `text-muted` | `#8a8a8a` | Secondary text |
| `text-faint` | `#6a6a6a` | Tertiary / counts |
| `text-dim` | `#5a5a5a` | Timestamps, hints |

### Color — accent & status
| Token | Hex | Use |
|---|---|---|
| `accent` (teal) | `#00d4aa` | Primary buttons, brand, focus ring, online dot glow base |
| `accent-hover` | `#00e6b8` | Primary button hover |
| `accent-text-on` | `#062d24` | Text/icon color **on** teal buttons |
| `accent-teal-2` | `#2dd4bf` / `#5eead4` | Relay-target pills, secondary brand stroke |
| `green` | `#00d68f` | Online indicator dot |
| `green-text` | `#34e0a8` | "Routed" / "Active" / success text |
| `red` | `#ff4757` | Dropped / destructive base |
| `red-text` | `#ff6b78` / `#ff8c96` | Dropped text, delete hover |
| `amber` | `#ffa502` / `#ffb84d` / `#ffc97a` | Warning callout (key reveal) |
| `blue` | `#3b82f6` / `#7aa9f7` | "Received" status |
| `purple` | `#a78bfa` / `#c4b5fd` | Slug pills (mono) |

Status badge backgrounds use the status color at low alpha, e.g.
`rgba(0,214,143,0.13)` (Routed), `rgba(255,71,87,0.13)` (Dropped),
`rgba(59,130,246,0.15)` (Received), `rgba(167,139,250,0.13)` (slug pill).

### Typography
- **UI font:** `'Segoe UI', system-ui, -apple-system, sans-serif`
- **Monospace** (paths, payloads, slugs, IPs, URLs, keys): `'JetBrains Mono', 'Consolas', 'Fira Code', monospace`
  *(prototype loads JetBrains Mono from Google Fonts; in production use the Consolas/Fira Code system stack or self-host JetBrains Mono.)*
- **Antialiasing:** `-webkit-font-smoothing: antialiased`

Type scale (size / weight):
| Use | Size | Weight | Notes |
|---|---|---|---|
| Page H1 | 20px | 650 | `letter-spacing:-0.01em` |
| Card / modal H2 | 16px | 650 | |
| Auth H1 | 19px | 650 | |
| Brand wordmark | 14–15px | 650 | |
| Body / inputs | 13–14px | 400–550 | |
| Table cell | 13px | 500 | |
| Nav link | 13px | — | |
| Status / method badge | 10–11px | 600–700 | `text-transform:uppercase`, `letter-spacing:0.04–0.07em` |
| Column header | 11px | 700 | uppercase, `letter-spacing:0.07em`, `#6a6a6a` |
| Eyebrow ("URL", "CALLBACK URL") | 10px | 700 | uppercase, `letter-spacing:0.06em`, `#5a5a5a` |
| Timestamp / meta | 11–12px | 400 | mono, muted |
| JSON payload | 12.5px | 400 | mono, `#4ade80` (green), `line-height:1.6` |

### Spacing, radius, shadow
- **Radius:** inputs/buttons/badges `7–8px`; cards `9–10px`; modals `14px`; pills `11–20px`; status badges `5–6px`.
- **Shadow:** minimal. Only modals: `0 24px 60px rgba(0,0,0,0.5)`. No card shadows.
- **Borders:** 1px, colors per token table. Dropped cards use `rgba(255,71,87,0.25)` border.
- **Heights:** top bar `52px`; inputs/selects `34–42px`; primary buttons `38–42px`; icon buttons `30–32px` square.
- **Transitions:** `120ms` on color/background/border for hover/focus; modal enter `180ms cubic-bezier(0.16,1,0.3,1)`; overlay fade `120ms`.
- **Focus ring:** `border-color:#00d4aa` + `box-shadow:0 0 0 3px rgba(0,212,170,0.10–0.12)`.

### Scrollbar (webkit)
`width:11px`, track `#0a0a0a`, thumb `#2a2a2a` with `border:3px solid #0a0a0a` (inset look), radius `6px`, hover `#3a3a3a`.

---

## Shared chrome (all logged-in pages: Dashboard, Listeners, Keys)
**Top bar** — 52px tall, `#0d0d0d`, bottom border `#1f1f1f`, horizontal padding 18px, flex row, 24px gap:
- **Brand** (left): inline SVG relay glyph (two arrows pointing inward at a center dot, teal `#00d4aa` / `#2dd4bf`) + wordmark "Callback Relay" (14px / 650).
- **Nav**: three links — "Feed" → `/`, "Listeners" → `/account/listeners`, "API Keys" → `/account/keys`. Active link: `#e6e6e6` text on `#1a1a1a` pill, `padding:6px 11px`, `border-radius:6px`. Inactive: `#8a8a8a`, transparent. Hover: text `#e6e6e6`, bg `#1a1a1a`.
- **Spacer** (flex:1).
- **(Dashboard only) Connection badge**: pill, `padding:5px 11px`, `border-radius:20px`. Three states:
  - Connected — dot `#00d68f`, text `#34e0a8`, bg `rgba(0,214,143,0.08)`, border `rgba(0,214,143,0.2)`, label "Connected"
  - Disconnected — use red equivalents (`#ff4757` / `#ff6b78`), label "Disconnected"
  - Reconnecting — use amber equivalents (`#ffa502` / `#ffc97a`), label "Reconnecting…"
- **Avatar**: 30px circle, `#1a1a1a` bg, `#2a2a2a` border, initials "EM", links to `/account/login`.

---

## Screens

### 1. Dashboard — live callback feed (`wwwroot/index.html`)
**Purpose:** watch incoming webhooks arrive in real time, filter them, and inspect payloads.

**Layout:** full-viewport flex column. Top bar (above). Below: a flex row filling remaining
height — fixed **252px sidebar** (left) + **flexible main area**.

**Sidebar** (`#0c0c0c`, right border `#1f1f1f`, scrolls):
- Header row: "LISTENERS" eyebrow (left) + `online/total` count in mono (right), padding `18px 16px 10px`.
- List of agents. Each item: `padding:9px 10px`, `border-radius:7px`, flex row, 11px gap:
  - Status dot 8px circle. **Online** = `#00d68f` with pulsing animation (see below). **Offline** = `#3a3a3a`, no animation.
  - Slug (mono, 13px, online `#d6d6d6` / offline `#6a6a6a`), truncated with ellipsis.
  - Below slug: `connected {time} ago` (online) or `offline` — 11px, `#5a5a5a`.

**Main area** (`#0a0a0a`):
- **Toolbar** (`#0c0c0c`, bottom border, `padding:12px 18px`, flex row, 10px gap):
  - Search box, 280px wide, 34px tall, `#141414` bg, magnifier icon at left (14px, `#5a5a5a`), placeholder "Search payloads, paths, IPs…".
  - Select "All listeners" (+ one option per registered slug).
  - Select "All statuses" (Routed / Dropped / Received).
  - Spacer.
  - Counter (mono, `#6a6a6a`): "42 callbacks", or "N of M callbacks" when filtered.
  - "Clear" button — ghost, `border:1px #2a2a2a`, `#8a8a8a`; hover border+text turn red (`#ff4757` / `#ff6b78`).
- **Feed** (scrolls, `padding:14px 18px 40px`, cards in a `max-width:1080px` column, 9px gap).

**Callback card** (`#111`, `border-radius:9px`, border `#1f1f1f`; Dropped cards border `rgba(255,71,87,0.25)`):
- **Header row** (clickable, `padding:12px 14px`, hover bg `#161616`, flex row 11px gap):
  - Caret (11px chevron, `#6a6a6a`) — rotates 0°→90° when expanded, `transition:transform 160ms`.
  - Timestamp — mono 11px `#6a6a6a`, fixed 64px width (e.g. `14:32:09`).
  - **Status badge** — 10px/700 uppercase, `padding:3px 8px`, `border-radius:5px`. Routed=`rgba(0,214,143,0.13)`/`#34e0a8`; Dropped=`rgba(255,71,87,0.13)`/`#ff6b78`; Received=`rgba(59,130,246,0.15)`/`#7aa9f7`.
  - **Slug pill** — mono 10px, `padding:3px 9px`, `border-radius:11px`, `rgba(167,139,250,0.13)`/`#c4b5fd`.
  - **Method badge** — mono 10px/700 uppercase, `padding:3px 8px`, `border-radius:5px`, `#1e1e1e`/`#b6b6b6` (POST/PUT/DELETE/…).
  - **Sub-path** — mono 13px `#d6d6d6` (e.g. `/charge.succeeded`), truncates.
  - **Relay-target badge** (only when the slug has a relay config) — mono 10px, `padding:3px 8px`, `border-radius:5px`, `rgba(45,212,191,0.1)`/`#5eead4`, with a small arrow icon, text `http://localhost:4000/`.
  - Spacer, then **source IP** right-aligned (mono 11px `#5a5a5a`).
- **Dropped notice** (only Dropped) — bar below header, `rgba(255,71,87,0.08)` bg, top border `rgba(255,71,87,0.18)`, warning icon + reason text `#ff8c96` (e.g. "Agent offline — listener 'twilio-sms' is not connected").
- **Expanded body** (only when card is open) — top border `#1f1f1f`, bg `#0c0c0c`:
  - Meta row: `content-type application/json`, `bytes {n}`, `id {evtId}` — 11px, label `#5a5a5a` / value `#9a9a9a` mono.
  - `<pre>` with **pretty-printed JSON** (2-space indent), mono 12.5px, color `#4ade80`, `line-height:1.6`, horizontal scroll.

**Empty state** (no cards match): centered column, faded relay glyph, title + sub.
- No filters active: "Waiting for callbacks…" / "Incoming webhooks will appear here in real time."
- Filters active: "No callbacks match your filters" / "Try clearing the search or filters."

---

### 2. Login (`/account/login`)
**Purpose:** sign in. Centered card on a dark radial-wash background.

**Layout:** full-viewport centered column. Background `radial-gradient(900px 600px at 50% -10%, #131313 0%, #0a0a0a 60%)`.
- Brand (glyph + wordmark) above the card, 30px margin-bottom.
- **Card**: `max-width:384px`, `#111`, border `#232323`, `border-radius:12px`, `padding:30px 30px 26px`.
  - H1 "Sign in" (19px/650) + subtext "Welcome back. Connect your listeners and watch callbacks flow." (`#8a8a8a`).
  - **"Continue with Google"** button — full width, 42px, **white `#fff` bg, dark `#1a1a1a` text**, 8px radius, multicolor Google "G" SVG, 14px/600. Hover bg `#f5f5f5`. This is the primary path.
  - Divider row: hairline — **"or use email"** toggle button (12px `#8a8a8a`, hover `#c6c6c6`) — hairline.
  - **Email form** (revealed by the toggle, hidden initially): Email input (mono), Password input (mono) with "Forgot?" link, and a teal **"Sign in"** submit (42px, `#00d4aa` bg, `#062d24` text, hover `#00e6b8`).
- Below card: "New here? Create an account" → `/account/register` (link `#34e0a8`).

---

### 3. Register (`/account/register`)
Same layout/styling as Login. H1 "Create your account", sub "Register listeners and route
webhooks to your machine." Google button reads **"Sign up with Google"**. Email form
(revealed by "or use email") has four fields: **Display name** (text), **Email** (mono),
and a 2-up row of **Password** + **Confirm** (mono). Submit: teal **"Create account"**.
Fine print under the button: terms/acceptable-use line (11px `#5a5a5a`). Footer:
"Already have an account? Sign in" → `/account/login`.

---

### 4. Listener management (`/account/listeners`)
**Purpose:** manage registered listeners; copy callback URLs; add new listeners.

**Layout:** shared top bar; content `max-width:1120px`, centered, `padding:30px 24px 60px`.
- **Header row:** H1 "Listeners" + sub "{n} registered · {m} active"; right-aligned teal
  **"+ Add listener"** button (38px, `#00d4aa`/`#062d24`).
- **Table** (`#0d0d0d`, border `#1f1f1f`, `border-radius:10px`):
  - Header row (`#111`): grid `1.4fr 1.6fr 1.4fr 0.8fr auto`, 16px gap, columns
    **Slug · Label · Relay target · Status · Actions** (uppercase 11px/700 `#6a6a6a`).
  - Each listener = a two-part block, separated by `#161616`, hover bg `#131313`:
    - **Main grid row** (same columns): slug (mono `#c4b5fd`), label (`#d6d6d6`),
      relay-target pill (mono `#5eead4` on `rgba(45,212,191,0.09)`, `border-radius:6px`),
      status badge (dot + "Active" `#34e0a8` on `rgba(0,214,143,0.12)` / "Inactive"
      `#7a7a7a` on `#1a1a1a` with `#4a4a4a` dot), and **Actions**: three 32px icon buttons
      (`#141414`, border `#2a2a2a`, radius 7px) — **copy URL**, **edit** (pencil),
      **delete** (trash; hover turns red). Copy button shows a green check + recolors for
      ~1.6s after click.
    - **URL strip** below: inset bar (`#111`, border `#1c1c1c`, radius 7px) with "URL"
      eyebrow + full callback URL (mono `#9a9a9a`, truncates) + a ghost "Copy"/"Copied"
      text button.
  - Full callback URL format: `https://callback.erickmuthomi.dev/collections/callback/{slug}`

**Add-listener modal** (overlay `rgba(0,0,0,0.6)`, fade-in; panel `max-width:480px`, `#111`,
border `#2a2a2a`, radius 14px, shadow `0 24px 60px rgba(0,0,0,0.5)`, rise-in):
- Header "Add listener" + close (×) icon button.
- Fields: **Label** (text); **Slug** (mono `#c4b5fd`, sub-label "auto-generated, editable");
  a row of **Scheme** (select http/https, 110px), **Port** (numeric, 110px), **Base path** (text, flex);
- **Live preview panel** (`#0c0c0c`, border `#1c1c1c`): "CALLBACK URL" → `…/collections/callback/{slug}`
  and "RELAYS TO" → `{scheme}://localhost:{port}{basePath}` (mono `#5eead4`), both updating live.
- Footer: ghost "Cancel" + teal "Create listener".

---

### 5. API Keys (`/account/keys`)
**Purpose:** manage agent auth keys; generate new ones (shown once).

**Layout:** shared top bar; content `max-width:1000px`, centered, `padding:30px 24px 60px`.
- **Header:** H1 "API Keys" + sub "Authenticate the relay agent on your machine. {n} active.";
  right-aligned teal **"+ Generate new key"** button.
- **Table** (same shell as Listeners): grid `1.3fr 1.5fr 1fr 1fr auto`, columns
  **Label · Key · Created · Last used · Actions**. Rows (`padding:14px 18px`, hover `#131313`):
  - Label (`#d6d6d6`/500), masked key (mono `#9a9a9a`, e.g. `cr_live_••••••••be7d`),
    created date (mono `#8a8a8a`, e.g. "May 28, 2026"), last used (mono; `#8a8a8a` or, when
    never used, "never" in `#5a5a5a`), and a **"Revoke"** button (`#141414`, border `#2a2a2a`,
    radius 7px; hover red).

**Generate-key modal** (same shell as above) — **two stages**:
- **Stage 1 — label:** "Generate new key" header + close. Input "Key label"
  (placeholder "Macbook Pro — local dev") + helper text. Footer: ghost "Cancel" +
  teal "Generate key".
- **Stage 2 — reveal:** green check badge + "Key created" + the label. **Amber warning
  callout** (`rgba(255,165,2,0.08)` bg, `rgba(255,165,2,0.25)` border, triangle icon,
  text `#ffc97a`): "Copy this key now — it won't be shown again. Store it somewhere safe."
  The full key shown in a mono `#34e0a8` box. Full-width teal **"Copy key"** button
  (label flips to "Copied to clipboard" for ~1.6s) + ghost **"Done"** (closes modal and
  prepends the new key to the table, masked).
- Key format: `cr_live_` + 32 lowercase-hex chars.

---

## Interactions & Behavior

### Dashboard
- **Search** filters cards live (case-insensitive) across path, IP, slug, method, and the JSON payload string.
- **Slug select** + **Status select** filter the feed; combine with search (AND).
- **Clear** resets search + both selects + collapses all expanded cards.
- **Click a card header** toggles its expanded JSON body (caret rotates). Multiple can be open.
- **Live data (production):** subscribe to the **SignalR** hub; each pushed callback prepends a
  new card to the top of the feed and updates the counter and (if new) the listener list /
  online status. The connection badge reflects hub state (Connected / Reconnecting… /
  Disconnected). The prototype omits this and uses a static array.
- **Online dot pulse** (`@keyframes`): box-shadow ring expanding from `rgba(0,214,143,0.5)`
  to transparent over a 2.4s loop. Suppress under `prefers-reduced-motion`.

### Auth pages
- **"or use email" / "or use email" toggle** expands/collapses the email form in place; label
  flips to "hide email sign-in" / "hide email sign-up".

### Listeners
- **Auto-slug:** typing the **Label** auto-fills **Slug** via slugify (lowercase, non-alphanumeric → `-`,
  trim leading/trailing `-`). Once the user edits Slug manually, it stops auto-updating.
- **Port** input strips non-digits. **Live preview** of callback URL + relay target updates on every keystroke.
- **Create listener** validates a non-empty slug, appends the row (active), closes the modal.
- **Copy** (both the action icon and the URL-strip button) writes the callback URL to the
  clipboard and shows a 1.6s "Copied" confirmation.
- Modal closes on overlay click, × button, or Cancel (panel click does not close — stopPropagation).

### Keys
- **Generate** opens stage 1; "Generate key" validates a non-empty label, creates the key,
  moves to stage 2 (reveal). "Copy key" → clipboard + 1.6s confirm. "Done" prepends the
  masked key to the table and closes.
- **Revoke** removes the key (wire to DELETE endpoint; add a confirm if desired).

---

## State Management (per page — translate to vanilla JS module state)
- **Dashboard:** `search`, `slugFilter`, `statusFilter`, `expanded{id→bool}`, plus the live
  `callbacks[]` and `agents[]` (from SignalR in production). Derived: filtered list, counts,
  online count, empty-state messaging.
- **Login/Register:** `showEmail` boolean.
- **Listeners:** `listeners[]`, `modalOpen`, `copiedSlug`, and modal `form{label, slug,
  slugEdited, scheme, port, basePath}`.
- **Keys:** `keys[]`, `modalOpen`, `stage('label'|'reveal')`, `label`, `generatedKey`, `keyCopied`.

Data the real backend must provide:
- **Listeners:** slug, label, scheme, port, basePath, active. Callback URL is derived
  (`https://callback.erickmuthomi.dev/collections/callback/{slug}`); relay target is
  `{scheme}://localhost:{port}{basePath}`.
- **Callbacks (feed):** timestamp, status (Routed|Dropped|Received), slug, HTTP method,
  sub-path, relay target (nullable), source IP, byte size, event id, JSON payload, and
  (Dropped only) a reason string.
- **Keys:** label, created date, last-used (nullable), and the full key value once at creation.

---

## Assets
- **No image assets.** All icons are inline stroke SVGs (relay glyph, search, caret, arrow,
  copy, edit/pencil, trash, close ×, check, warning triangle, plus). The Google "G" is a
  4-color inline SVG. Reuse these SVGs from the prototype markup.
- **Fonts:** UI = system `Segoe UI` stack; mono = JetBrains Mono (Google Fonts in the
  prototype). In production prefer the `Consolas/Fira Code` system stack or self-host
  JetBrains Mono — avoid the external Google Fonts dependency for an offline-capable tool.

## Files in this bundle
| File | Screen |
|---|---|
| `Dashboard.dc.html` | Live callback feed (→ `wwwroot/index.html`) |
| `Login.dc.html` | `/account/login` |
| `Register.dc.html` | `/account/register` |
| `Listeners.dc.html` | `/account/listeners` |
| `Keys.dc.html` | `/account/keys` |
| `support.js` | Design-tool runtime — **reference/run only, do NOT ship** |

Open any `.dc.html` in a browser to see the working prototype (search, filters, expand,
modals, auto-slug, key generation all function). Read the markup for exact SVG paths,
inline styles, and copy.
