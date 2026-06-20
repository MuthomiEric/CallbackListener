# Handoff: Callback Relay — Web UI

## Overview
Callback Relay is a self-hosted developer tool (ASP.NET Core 9 + SignalR, vanilla-JS static frontend) that receives HTTP webhooks at a public URL and forwards them in real time to an agent running on a developer's local machine — a self-hosted blend of Webhook.site + ngrok.

This bundle contains the **full web UI**: a logged-out marketing/welcome screen and four authenticated app screens, plus the auth pages. The visual language is deliberately **VS Code–flavored**: editor chrome (traffic-light titlebar, tab strip, blue status bar), dark surfaces, monospace data, syntax-highlight accent colors.

## About the Design Files
The files in this bundle are **design references created in HTML** — prototypes that show the intended look, layout, and interaction behavior. They are **not production code to copy directly**.

They are authored as "Design Components" (`*.dc.html` + a shared `support.js` runtime). That runtime is a prototyping harness, **not** part of your stack. Your task is to **recreate these designs in the Callback Relay codebase** (ASP.NET Core static files + vanilla JS + SignalR, per the project's stack) using its established patterns. Read each `.dc.html` for exact markup, inline styles, and the logic class at the bottom (state + handlers) — translate those into your own HTML/CSS/JS. Do not ship `support.js` or the `<x-dc>` wrappers.

If you open the files to read them: each file is one document. The visible template is between `<x-dc …>` and `</x-dc>`; the `class Component extends DCLogic { … }` block beneath it holds the state model, mock data, and event handlers — treat that as the behavioral spec.

## Fidelity
**High-fidelity.** Final colors, typography, spacing, badges, and interactions are all specified. Recreate pixel-accurately. All data shown is **mock/placeholder** — wire real data from your API/SignalR hub in its place.

---

## Global Chrome (shared by all authenticated screens)
Every authenticated screen (Feed, Apps, Clients, Admin) uses the same three-band frame. Build it once as a layout shell.

1. **Titlebar** — height **30px**, background `#3a3a3c`, bottom border `1px #2b2b2b`.
   - Left: three traffic-light dots (12px circles, `#ff5f57` / `#febc2e` / `#28c840`), 8px gap, 12px left pad, in a 140px-wide cell.
   - Center: page title, 12px, `#9d9d9d`, format `"{Page} — Callback Relay"`.
   - Right (140px cell, right-aligned): 26px circular avatar `#2f6fd0`, white initials (`NE`), links to the auth/account view.

2. **Nav/tab bar** — height **46px**, background `#2d2d2e`, bottom border `1px #1e1e1e`. A row of tabs:
   - First cell: a 46px-wide "collapse sidebar" icon button, color `#858585`.
   - Tabs: **Feed**, **Apps**, **Clients**, **Admin**. Each: 16px icon + 13px label, 16px horizontal padding.
   - Active tab: `border-top: 2px solid #00d4aa`, background `#1e1e1e`, label/icon white `#ffffff`. Inactive: transparent top border, label/icon `#9d9d9d`; hover background `#2a2d2e`, label lightens to `#d4d4d4`.
   - Far right: connection badge — 8px green dot `#00d68f` + "Connected" `#4ade80` 13px. (States: Connected green; Disconnected red `#ff4757`; Reconnecting amber `#ffa502`.)

3. **Status bar** — height **24px**, background `#1668c4` (ASP.NET blue), white text 12px, monospace. Left: relay glyph + "Callback Relay". Right: `© 2026 Erick Muthomi`. Screens add their own middle segments (e.g. Feed shows "{n} of {m} online" and the callback count).

Body sits between the nav bar and status bar. Root container is `height: 100vh; display:flex; flex-direction:column; overflow:hidden`.

---

## Screens / Views

### 1. Landing / Welcome (`Landing.dc.html`) — logged-out
**Purpose:** First screen for unauthenticated visitors. Mirrors the VS Code "Welcome" tab.
**Layout:** Titlebar (centered "Welcome — Callback Relay", no avatar) → tab strip with a `Welcome` tab (active, teal top border, close glyph) and a `Get Started` tab → body row. Body row = **disabled activity bar** (56px, `#0d0e0f`, `opacity:0.38`, `pointer-events:none`) + **disabled explorer** (250px, `#1a1c1c`, `opacity:0.4`, shows a fake `callback-relay` file tree ending in a "sign in to open" lock row) + **main content** (fluid, fills remaining width; inner wrapper `max-width:1100px; padding:44px 48px 70px`, left-aligned).
**Main content sections:**
- **Hero:** 78px square logo tile (`#16323a` bg, `#21505a` border, relay glyph) + `Callback Relay` title (38px, weight 500, `#f0f3f5`) + subtitle `// Receive, inspect & relay webhooks to localhost` (16px, `#6A9955`, monospace).
- **START:** eyebrow label (12px, weight 600, letter-spacing .1em, `#8a9297`) + hairline. Two rows (hover bg `#16191a`, text→white): "Sign in with Google" (`arrow_outward` icon) → Register; "Sign in" (`person` icon) → Login.
- **THE PROBLEM IT SOLVES:** a code-block card (`#1a1c1c`, border `#2c3032`, monospace, `overflow-x:auto; white-space:nowrap`) narrating the POST→relay→localhost flow using syntax colors (see tokens).
- **FEATURES:** 2×2 grid (gap 14px) of cards (`#1a1c1c`, border `#2c3032`, hover border `#4a5258`). Each card: a monospace function-signature heading in teal `#4EC9B0` + a `#8a9297` description. Titles: `realtime_feed()`, `relay_to_local(port, path)`, `delivery_mode: "Both"`, `rate_limit(60, "per_slug")`.
- **QUICK TEST:** intro line + a `curl` code block (monospace, syntax-highlighted, horizontal scroll).
- **Footer actions:** primary `Get started` button (`#1668c4`, white, hover `#2f7fd6`) + ghost `Sign in` + an amber "Home lab — may be down occasionally" badge (border + text `#DCDCAA`).
**Status bar:** right side shows `UTF-8`, `ASP.NET Core 9`, and a `Ready` segment.
**Note on icons:** uses the **Material Symbols Outlined** web font (`<span class="ms">name</span>`). If the font is slow to load you'll briefly see the literal word — ensure the font is linked/preloaded.

### 2. Feed / Dashboard (`Dashboard.dc.html`) — the primary screen
**Purpose:** Watch incoming webhooks live while the server runs.
**Layout:** Global chrome. Body = **sidebar (264px)** + **main feed**.
- **Sidebar** (`#252526`): header row "APPS" (11px, 700, uppercase, `#bbbbbb`) + "`{n} · live`" count (monospace, `#858585`). An **"All apps"** selector row (selected style: bg `#37373d`, border `#454549`, white text). Below, a scroll list of app rows: 8px status dot (online `#00d68f` **pulsing**, offline `#5a5a5a`), slug (13px, weight 600, monospace, online `#d4d4d4` / offline `#7a7a7a`), label (11px, `#6f6f6f`). Clicking a row sets the slug filter; selected row bg `#2a2d2e`.
- **Main → toolbar** (12px/16px pad, bottom border `#2b2b2b`): 300px search input (icon-left, `#3c3c3c` bg, focus border `#00d4aa`), a **slug** `<select>`, a **status** `<select>`, a flex spacer, a monospace count label ("42 callbacks" / "N of M callbacks"), and a **Clear** button.
- **Main → feed** (scroll, `padding:14px 16px 40px`, cards `max-width:1080px`, gap 8px). Each callback is a card (`#252526`, border `#2b2b2b`; dropped cards border `rgba(255,71,87,0.3)`):
  - **Header row** (clickable, hover bg `#2a2d2e`): caret (rotates 90° when open), timestamp (64px, monospace, `#858585`), **status badge** (uppercase 10px: Routed green `#34e0a8` on `rgba(0,214,143,.15)`; Dropped red `#ff6b78`; Received blue `#7aa9f7`), **slug pill** (purple `#c4b5fd` on `rgba(167,139,250,.15)`, monospace), **method pill** (`#cccccc` on `#3c3c3c`, monospace), **sub-path** (13px, monospace, `#d4d4d4`), optional **relay-target pill** (teal `#5eead4` on `rgba(45,212,191,.12)`, arrow glyph, only when relay configured), flex spacer, **source IP** (right, monospace, `#6f6f6f`).
  - **Dropped notice** (only if status = Dropped): a bar below the header, bg `rgba(255,71,87,.09)`, top border `rgba(255,71,87,.2)`, warning glyph + reason text `#ff8c96` (e.g. "Agent offline — client for 'twilio-sms' is not connected").
  - **Expanded body** (toggles on header click): meta row (content-type / bytes / id) then a `<pre>` of **pretty-printed JSON** in green monospace `#4ade80` on `#1e1e1e`.
  - **Empty state:** centered relay glyph + title/sub. Two variants: filtered ("No callbacks match your filters") vs idle ("Waiting for callbacks…").

### 3. Apps (`Apps.dc.html`)
**Purpose:** Manage registered apps (the public slugs that receive callbacks).
**Layout:** Global chrome → scroll body, inner `max-width:1180px; padding:30px 28px 60px`. Header: "Apps" (24px, 700, white) + "`{n} registered · {m} active`" + **Add app** button (right; `#00d4aa` bg, `#04261f` text, hover `#13e0b6`, plus glyph).
**Table** (`#1c1c1c`, border `#2b2b2b`, radius 10px): header row bg `#232325`. Columns: **Slug** (purple monospace `#c4b5fd`), **Label**, **Client** (dot + name; online dot `#00d68f`, none `#4a4a4a` muted), **Mode** (inline `<select>`: Both / Web only / Relay only — `#2a2a2c` bg), **Status** (pill: Active green / Inactive `#8a8a8a`), **Actions** (112px): copy-URL icon (turns to check + "Copied" 1.6s), edit (pencil), delete (trash; hover red). Below each row, a **URL strip** (`#161617`): `URL` eyebrow + full callback URL `https://callback.erickmuthomi.dev/?slug={slug}` (monospace, ellipsis) + a text "Copy" button.
**Add-app modal** (overlay `rgba(0,0,0,.62)`, card `#252526` max-width 520px, radius 14px, fade+rise in): fields — **Label** (auto-generates **Slug** via slugify until the slug is manually edited), **Scheme** (`http`/`https`), **Port**, **Base path**, **Delivery mode** (Web only / Relay only / Both), **Client** (None / Personal / Home server). A "Relays to" preview box shows `{scheme}://localhost:{port}{path}`, or "Not forwarded — web display only" when Client = None. Footer: Cancel (ghost) + Create app (primary).

### 4. Clients (`Clients.dc.html`)
**Purpose:** Manage machines running the relay agent; each holds an API key.
**Layout:** Global chrome → scroll body (`max-width:1180px`). Header: "Clients" + "`Each client represents a machine running the relay agent. {n} registered.`" + **Add client** button.
**Table:** columns **Label** (white, 600), **Key** (masked `cr_live_••••{last4}`, monospace), **Status** (Online green + pulsing dot / Offline `#7a7a7a`), **Created** (monospace), **Last used** (monospace; "never" muted when null), **Actions** (Remove button; hover red).
**Add-client modal — two stages:**
- **Stage 1 (label):** "Add client" + a single "Client label" input + helper text. Cancel / Create client.
- **Stage 2 (reveal):** success header (check tile) + an **amber warning** ("Copy this key now — it won't be shown again…"), the **full generated key** (`cr_live_` + 32 hex) in a `#161617` box, a full-width **Copy key** button (→ "Copied to clipboard" 1.6s), and **Done** (appends the new client row, masked, offline, created today).

### 5. Admin (`Admin.dc.html`)
**Purpose:** Operator overview of all users.
**Layout:** Global chrome → scroll body (`max-width:1180px`). Header: "Users" (24px, 700) + "`{n} accounts`".
**Stat cards:** 4-up grid (gap 16px), each `#1c1c1c` card (border `#2b2b2b`, radius 10px, hover border `#3a3a3c`): a big number (28px, 700, monospace, white) + label. Cards: **Total users**, **Total apps**, **Live agents**, **API keys**. *All values are computed by summing the user rows — do not hardcode; this is where the earlier `NaN`/`undefined` bug was, so derive every stat from real data.*
**Users table:** columns **User** (32px colored avatar with initials + name white 600 + email monospace `#6f6f6f`), **Joined**, **Apps**, **Keys**, **Live agents** (green pill "N online" with pulsing dot, or muted "—" when zero), **Last callback** (monospace; "—" muted when null).

### 6. Login / Register (`Login.dc.html`, `Register.dc.html`)
Centered dark auth cards (these predate the VS Code chrome — restyle to match if desired, but functionally: email + password (+ display name / confirm on Register), a primary **Google OAuth** button, and an "or use email" toggle that reveals the email form). Cross-link to each other.

---

## Interactions & Behavior
- **Tab nav:** plain links between the five page files; active tab = teal top border + white label.
- **Feed filtering:** search box (matches path, IP, slug, method, and stringified payload — case-insensitive), slug `<select>`, status `<select>`, and sidebar app rows all narrow the list; **Clear** resets search + both filters + collapses expanded cards. Count label reflects filtered vs total.
- **Card expand:** click header → toggle JSON body; caret rotates 0°→90° (140ms).
- **Copy actions** (Apps URL, Clients key): write to clipboard, swap label/icon to a "Copied" confirmation for **1600ms**, then revert.
- **Add-app:** Label→Slug auto-slugify (`lowercase`, non-alphanumerics→`-`, trim dashes) until the slug field is manually edited (then it stops tracking). Port input strips non-digits. Save requires a non-empty slug; appends a new active row and closes.
- **Add-client:** two-stage; Create requires a non-empty label, generates `cr_live_`+32 random hex, reveals once; Done appends the masked row.
- **Mode select** on an Apps row updates that row's delivery mode in place.
- **Delete/Remove:** filters the row out of state.
- **Pulsing dot:** `@keyframes pulse-dot` — expanding `box-shadow` ring on `rgba(0,214,143,…)`, 2.4s infinite, on online indicators only.
- **Modals:** overlay click closes; inner click `stopPropagation`; `overlay-in` (120ms fade) + `modal-in` (180ms rise+scale, `cubic-bezier(0.16,1,0.3,1)`).

## State Management
Replace the mock arrays in each file's logic class with live data:
- **Feed:** `apps[]` (slug, label, online) for the sidebar + live SignalR stream of `callbacks[]` (id, time, status ∈ Routed/Dropped/Received, slug, method, path, relay target | null, source IP, reason?, evtId, payload object). UI state: `search`, `slugFilter`, `statusFilter`, `expanded{}` map. Connection badge ← SignalR connection state.
- **Apps:** `apps[]` (slug, label, client, clientOnline, mode, scheme, port, basePath, active) + modal `form` state with the slug-tracking flag + `copiedSlug`.
- **Clients:** `clients[]` (label, full key, online, created, lastUsed) + modal `stage`/`label`/`generatedKey`/`keyCopied`.
- **Admin:** `users[]` (name, email, joined, apps, keys, online, agents, lastCallback, avatarBg); **stats are derived** by reducing over `users`.

## Design Tokens
**Surfaces:** app bg `#1e1e1e` / `#121414` (landing); panels `#252526`, `#1c1c1c`, `#1a1c1c`; titlebar `#3a3a3c`; nav `#2d2d2e`; sidebar `#252526`; card alt `#161617`.
**Borders / hairlines:** `#2b2b2b`, `#2c3032`, `#242424`, `#3a3a3c` (inputs).
**Inputs:** bg `#3c3c3c` (feed) / `#1c1c1c` (modals); focus border `#00d4aa` (+ `0 0 0 3px rgba(0,212,170,.12)` ring in modals).
**Text:** primary `#e2e2e2`/`#d4d4d4`/`#cccccc`; white `#ffffff`/`#f0f3f5`; muted `#9d9d9d`, `#858585`, `#6f6f6f`, `#6a7177`; disabled chrome `#6a7177`.
**Accent (primary / online):** teal `#00d4aa` (hover `#13e0b6`), online dot `#00d68f`, "Connected" `#4ade80`, relay/secondary teal `#2dd4bf` / `#5eead4` / `#4EC9B0`.
**Primary button (blue):** `#1668c4` (hover `#2f7fd6`), white text — also the status-bar color.
**Status colors:** green `#34e0a8`/`#00d68f`; red `#ff4757`/`#ff6b78`/`#ff8c96`; amber `#ffa502`/`#DCDCAA`/`#ffb84d`/`#ffc97a`; blue `#3b82f6`/`#7aa9f7`/`#9cdcfe`; purple `#a78bfa`/`#c4b5fd`/`#C586C0`.
**Syntax (landing/JSON):** keyword/method green `#4EC9B0`; string `#CE9178`; comment `#6A9955`; var/param `#9cdcfe`; number `#b5cea8`; fn `#DCDCAA`; punctuation-accent `#C586C0`; JSON value green `#4ade80`.
**Typography:** UI = `"Segoe UI", system-ui, -apple-system, sans-serif` (landing uses **Inter**); code/data = `"JetBrains Mono", Consolas, "Fira Code", monospace`. Sizes: page titles 24–38px; section eyebrows 11–12px (700, letter-spacing .07–.1em, uppercase); body 13–14px; badges 10–11px (700, letter-spacing .04–.06em, uppercase); stat numbers 28px.
**Radius:** chrome/tables 10px; cards/modals 14px; inputs/buttons 6–8px; badges/pills 3–6px; feed cards 5px. (Note: feed/landing cards use tighter radii to read as editor panels.)
**Icons:** inline stroke SVGs (~1.6–1.9px) throughout; **Landing** additionally uses the **Material Symbols Outlined** Google font.
**Motion:** transitions 100–120ms; pulse 2.4s; modal in 120ms/180ms.

## Assets
No raster assets. The relay logo is an inline SVG (two arrows pointing inward to a center dot, teal `#00d4aa` + `#2dd4bf`). Material Symbols Outlined + Inter + JetBrains Mono are loaded from Google Fonts (swap to self-hosted if your build forbids external font CDNs). All status/method/avatar visuals are CSS.

## Files
- `Landing.dc.html` — logged-out Welcome screen
- `Dashboard.dc.html` — Feed (live callback feed)
- `Apps.dc.html` — Apps management + Add-app modal
- `Clients.dc.html` — Clients management + 2-stage Add-client modal
- `Admin.dc.html` — Admin users overview + stat cards
- `Login.dc.html`, `Register.dc.html` — auth screens
- `support.js` — **prototype runtime only; do not ship.** Present so the `.dc.html` files render if opened in a browser.

> To preview a file as-is, open it in a browser — it self-renders. Read the `class Component` block at the bottom of each for the exact state model, mock data shapes, and handler logic.
