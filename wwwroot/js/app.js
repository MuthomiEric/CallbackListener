"use strict";

// ── Auth guard ────────────────────────────────────────────────────────────────
(async () => {
    const res = await fetch("/auth/me");
    if (!res.ok) { window.location.replace("/landing"); return; }
    const me = await res.json();
    currentUserId = me.id;
    const avatarEl = document.getElementById("avatar-el");
    if (avatarEl) avatarEl.textContent = me.initials;
    const emailEl = document.getElementById("user-menu-email");
    if (emailEl) emailEl.textContent = me.email;
    if (me.isAdmin) {
        const adminLink = document.getElementById("admin-link");
        if (adminLink) adminLink.hidden = false;
        const adminSideFile = document.getElementById("admin-sidebar-file");
        if (adminSideFile) adminSideFile.hidden = false;
    }
    await loadApps();
})();

// ── User menu ─────────────────────────────────────────────────────────────────
document.getElementById("avatar-el")?.addEventListener("click", e => {
    e.stopPropagation();
    const menu = document.getElementById("user-menu");
    menu.hidden = !menu.hidden;
});

document.addEventListener("click", () => {
    const menu = document.getElementById("user-menu");
    if (menu) menu.hidden = true;
});

document.getElementById("signout-btn")?.addEventListener("click", async () => {
    await fetch("/auth/logout", { method: "POST" });
    location.replace("/account/login");
});

// ── State ──────────────────────────────────────────────────────────────────────
let allCallbacks = [];
let apps         = [];                 // registered apps from /api/apps
let currentUserId = null;
const agents     = new Map();          // clientId → AgentInfo
const expandedIds  = new Set();        // persist expand state across re-renders
let selectedSlug   = null;             // null = all apps
const unreadCounts = new Map();        // slug → count of unseen callbacks

// ── Connection badge ────────────────────────────────────────────────────────────
const CONN_STATES = {
    online:    { dot: "#0a7d4f", text: "#0d6548", bg: "transparent", border: "transparent", label: "Connected"     },
    offline:   { dot: "#c53030", text: "#b52020", bg: "transparent", border: "transparent", label: "Disconnected"  },
    reconnect: { dot: "#d97706", text: "#9a6800", bg: "transparent", border: "transparent", label: "Reconnecting…" },
};

function setStatus(state) {
    const s  = CONN_STATES[state] || CONN_STATES.offline;
    const el = document.getElementById("conn-status");
    el.style.background   = s.bg;
    el.style.borderColor  = s.border;
    el.querySelector(".conn-dot").style.background  = s.dot;
    el.querySelector(".conn-label").style.color     = s.text;
    el.querySelector(".conn-label").textContent     = s.label;
}

// ── SignalR ─────────────────────────────────────────────────────────────────────
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/dashboard")
    .withAutomaticReconnect([1000, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();

connection.on("History", (entries) => {
    allCallbacks = entries;
    renderCallbacks();
    syncSlugFilter();
});

connection.on("CallbackReceived", (entry) => {
    allCallbacks.unshift(entry);
    if (allCallbacks.length > 500) allCallbacks.pop();

    if (selectedSlug === null || entry.slug === selectedSlug) {
        prependCard(entry);
        updateCount();
    } else {
        unreadCounts.set(entry.slug, (unreadCounts.get(entry.slug) || 0) + 1);
        renderSidebar();
    }
    syncSlugFilter();
});

connection.on("AgentList",          (list)  => { agents.clear(); list.forEach(a => agents.set(a.clientId, a)); renderSidebar(); });
connection.on("AgentStatusChanged", (agent) => { agents.set(agent.clientId, agent); renderSidebar(); });

connection.onreconnecting(() => setStatus("reconnect"));
connection.onreconnected(()  => setStatus("online"));
connection.onclose(()        => setStatus("offline"));

(async function start() {
    try {
        await connection.start();
        setStatus("online");
    } catch {
        setStatus("offline");
        setTimeout(start, 5000);
    }
})();

// ── Render all ─────────────────────────────────────────────────────────────────
function renderCallbacks() {
    const filtered = applyFilters(allCallbacks);
    const container = document.getElementById("callbacks-container");

    if (filtered.length === 0) {
        const isFiltered = !!(document.getElementById("filter-slug").value ||
                               document.getElementById("filter-status").value ||
                               document.getElementById("search").value);
        container.innerHTML = `
            <div class="empty-state">
                <svg width="40" height="40" viewBox="0 0 24 24" fill="none">
                    <path d="M4 12 L9 12 M9 12 L7 9 M9 12 L7 15" stroke="#3a3a3a" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/>
                    <path d="M20 12 L13 12 M13 12 L15 9 M13 12 L15 15" stroke="#3a3a3a" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/>
                </svg>
                <div class="empty-title">${isFiltered ? "No callbacks match your filters" : "Waiting for callbacks…"}</div>
                <div class="empty-sub">${isFiltered ? "Try clearing the search or filters." : "Incoming webhooks will appear here in real time."}</div>
            </div>`;
        updateCount();
        return;
    }

    const col = document.createElement("div");
    col.className = "feed-col";
    filtered.forEach(cb => col.appendChild(buildCard(cb, false)));
    container.innerHTML = "";
    container.appendChild(col);
    updateCount();
}

// Prepend a single card without re-rendering the whole list
function prependCard(entry) {
    const slug   = document.getElementById("filter-slug").value;
    const status = document.getElementById("filter-status").value;
    const search = document.getElementById("search").value.toLowerCase();
    if (!matchesFilter(entry, slug, status, search)) return;

    const container = document.getElementById("callbacks-container");
    let col = container.querySelector(".feed-col");
    if (!col) {
        container.innerHTML = "";
        col = document.createElement("div");
        col.className = "feed-col";
        container.appendChild(col);
    }
    col.insertBefore(buildCard(entry, true), col.firstChild);
}

// ── Card builder ───────────────────────────────────────────────────────────────
function buildCard(cb, isNew) {
    const card    = document.createElement("div");
    const isExp   = expandedIds.has(cb.id);
    const dropped = cb.status === "Dropped";

    card.className = `callback-card${dropped ? " dropped" : ""}${isNew ? " is-new" : ""}`;

    const statusClass = { Routed: "badge-routed", Dropped: "badge-dropped", Received: "badge-received" }[cb.status] ?? "badge-received";

    const ts = new Date(cb.timestamp).toLocaleTimeString(undefined, {
        hour: "2-digit", minute: "2-digit", second: "2-digit"
    });

    const relayBadge = cb.relay
        ? `<span class="badge badge-relay">
             <svg width="10" height="10" viewBox="0 0 24 24" fill="none" style="flex:none"><path d="M4 12 L20 12 M14 6 L20 12 L14 18" stroke="#5eead4" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>
             <span class="relay-url">${esc(cb.relay.scheme)}://localhost:${cb.relay.port}${esc(cb.relay.basePath)}</span>
           </span>`
        : "";

    const bytes   = new Blob([cb.rawBody || ""]).size;
    const metaRow = `
        <div class="card-meta">
            <span>content-type <span class="mv">${esc(cb.contentType || "—")}</span></span>
            <span>bytes <span class="mv">${bytes}</span></span>
            <span>id <span class="mv">${esc(cb.id)}</span></span>
        </div>`;

    const queryEntries = cb.query ? Object.entries(cb.query) : [];
    const querySection = queryEntries.length > 0
        ? `<div class="card-kv-section">
               <div class="kv-section-head">query params</div>
               <div class="kv-grid">${queryEntries.map(([k, v]) =>
                   `<span class="kv-key">${esc(k)}</span><span class="kv-val">${esc(v)}</span>`).join("")}</div>
           </div>`
        : "";

    const headerEntries = cb.headers ? Object.entries(cb.headers) : [];
    const headersSection = headerEntries.length > 0
        ? `<div class="card-kv-section hdr-toggle">
               <div class="kv-section-head hdr-head">
                   <svg class="hdr-caret" width="9" height="9" viewBox="0 0 24 24" fill="none">
                       <path d="M9 6L15 12L9 18" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
                   </svg>
                   headers <span class="hdr-count">${headerEntries.length}</span>
               </div>
               <div class="kv-grid" hidden>${headerEntries.map(([k, v]) =>
                   `<span class="kv-key">${esc(k)}</span><span class="kv-val">${esc(v)}</span>`).join("")}</div>
           </div>`
        : "";

    card.innerHTML = `
        <div class="card-header${isExp ? " is-open" : ""}">
            <svg class="card-caret" width="11" height="11" viewBox="0 0 24 24" fill="none">
                <path d="M9 6 L15 12 L9 18" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
            <span class="card-ts">${ts}</span>
            <span class="badge badge-status ${statusClass}">${esc(cb.status)}</span>
            <span class="badge badge-slug">${esc(cb.slug)}</span>
            <span class="badge badge-method">${esc(cb.method)}</span>
            <span class="card-path">${esc(cb.subPath || "/")}</span>
            ${relayBadge}
            <span class="card-spacer"></span>
            <span class="card-ip">${esc(cb.sourceIp)}</span>
        </div>
        ${dropped && cb.statusDetail ? `
        <div class="card-drop">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" style="flex:none">
                <path d="M12 8 L12 13 M12 16 L12 16.5" stroke="#ff6b78" stroke-width="2" stroke-linecap="round"/>
                <circle cx="12" cy="12" r="9" stroke="#ff6b78" stroke-width="1.6"/>
            </svg>
            <span>${esc(cb.statusDetail)}</span>
        </div>` : ""}
        <div class="card-body${isExp ? "" : " hidden"}">
            ${metaRow}
            ${querySection}
            ${headersSection}
            <div class="pre-wrap">
                <button class="copy-btn" title="Copy body">
                    <svg width="11" height="11" viewBox="0 0 24 24" fill="none">
                        <rect x="9" y="9" width="13" height="13" rx="2" stroke="currentColor" stroke-width="1.9"/>
                        <path d="M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1" stroke="currentColor" stroke-width="1.9"/>
                    </svg>
                    Copy
                </button>
                <pre class="json-pre">${formatBody(cb)}</pre>
            </div>
        </div>`;

    const header = card.querySelector(".card-header");
    const body   = card.querySelector(".card-body");

    const hdrHead = card.querySelector(".hdr-head");
    if (hdrHead) {
        hdrHead.addEventListener("click", e => {
            e.stopPropagation();
            const section = hdrHead.closest(".hdr-toggle");
            const grid    = section.querySelector(".kv-grid");
            const opening = grid.hasAttribute("hidden");
            grid.toggleAttribute("hidden", !opening);
            section.classList.toggle("is-open", opening);
        });
    }

    header.addEventListener("click", () => {
        const open = body.classList.toggle("hidden") === false;
        header.classList.toggle("is-open", open);
        if (open) expandedIds.add(cb.id);
        else       expandedIds.delete(cb.id);
    });

    card.querySelector(".copy-btn").addEventListener("click", async (e) => {
        e.stopPropagation();
        const btn  = e.currentTarget;
        const text = copyText(cb);
        await navigator.clipboard.writeText(text);
        btn.classList.add("copied");
        btn.innerHTML = `<svg width="11" height="11" viewBox="0 0 24 24" fill="none"><path d="M5 13l4 4L19 7" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg> Copied!`;
        setTimeout(() => {
            btn.classList.remove("copied");
            btn.innerHTML = `<svg width="11" height="11" viewBox="0 0 24 24" fill="none"><rect x="9" y="9" width="13" height="13" rx="2" stroke="currentColor" stroke-width="1.9"/><path d="M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1" stroke="currentColor" stroke-width="1.9"/></svg> Copy`;
        }, 2000);
    });

    return card;
}

// ── Apps sidebar ───────────────────────────────────────────────────────────────
async function loadApps() {
    const res = await fetch("/api/apps");
    if (!res.ok) return;
    apps = await res.json();
    renderSidebar();
}

function renderSidebar() {
    const container = document.getElementById("agents-list");
    const countEl   = document.getElementById("agent-count");
    container.innerHTML = "";

    const anyOnline = apps.some(a => a.clientId && agents.get(a.clientId)?.isOnline);
    countEl.textContent = `${apps.length} · ${anyOnline ? "agent live" : "offline"}`;

    // "All apps" row
    const allEl = document.createElement("div");
    allEl.className = "agent-item" + (selectedSlug === null ? " selected" : "");
    allEl.innerHTML = `<div class="agent-info" style="padding-left:19px">
        <span class="agent-slug" style="color:var(--text-muted)">All apps</span>
    </div>`;
    allEl.addEventListener("click", () => selectApp(null));
    container.appendChild(allEl);

    apps.forEach(app => {
        const isSelected = app.slug === selectedSlug;
        const isOnline   = app.clientId ? (agents.get(app.clientId)?.isOnline ?? false) : false;
        const unread     = unreadCounts.get(app.slug) || 0;

        const el = document.createElement("div");
        el.className = "agent-item" + (isSelected ? " selected" : "");
        el.innerHTML = `
            <span class="agent-dot ${isOnline ? "online" : "offline"}"></span>
            <div class="agent-info">
                <span class="agent-slug" title="${esc(app.slug)}">${esc(app.slug)}</span>
                <span class="agent-status">${esc(app.label)}</span>
            </div>
            ${unread > 0 ? `<span class="unread-badge">${unread > 99 ? "99+" : unread}</span>` : ""}
            <button class="agent-copy-btn" title="Copy slug">
                <svg width="11" height="11" viewBox="0 0 24 24" fill="none">
                    <rect x="9" y="9" width="13" height="13" rx="2" stroke="currentColor" stroke-width="1.9"/>
                    <path d="M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1" stroke="currentColor" stroke-width="1.9"/>
                </svg>
            </button>`;
        el.addEventListener("click", () => selectApp(app.slug));

        const copyBtn = el.querySelector(".agent-copy-btn");
        copyBtn.addEventListener("click", async (e) => {
            e.stopPropagation();
            await navigator.clipboard.writeText(app.slug);
            copyBtn.classList.add("copied");
            copyBtn.innerHTML = `<svg width="11" height="11" viewBox="0 0 24 24" fill="none"><path d="M5 13l4 4L19 7" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>`;
            setTimeout(() => {
                copyBtn.classList.remove("copied");
                copyBtn.innerHTML = `<svg width="11" height="11" viewBox="0 0 24 24" fill="none"><rect x="9" y="9" width="13" height="13" rx="2" stroke="currentColor" stroke-width="1.9"/><path d="M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1" stroke="currentColor" stroke-width="1.9"/></svg>`;
            }, 2000);
        });

        container.appendChild(el);
    });
}

function selectApp(slug) {
    selectedSlug = slug;
    if (slug !== null) unreadCounts.set(slug, 0);
    renderSidebar();
    renderCallbacks();
}

// ── Helpers ────────────────────────────────────────────────────────────────────
function updateCount() {
    const total   = allCallbacks.length;
    const shown   = applyFilters(allCallbacks).length;
    const label   = shown === total
        ? `${total} callback${total !== 1 ? "s" : ""}`
        : `${shown} of ${total} callbacks`;
    document.getElementById("callback-count").textContent = label;
}

function syncSlugFilter() {
    const select  = document.getElementById("filter-slug");
    const current = select.value;
    const slugs   = [...new Set(allCallbacks.map(cb => cb.slug).filter(Boolean))];
    select.innerHTML = '<option value="">All listeners</option>';
    slugs.forEach(s => {
        const opt = document.createElement("option");
        opt.value = s;
        opt.textContent = s;
        if (s === current) opt.selected = true;
        select.appendChild(opt);
    });
}

function applyFilters(list) {
    const slug   = selectedSlug || document.getElementById("filter-slug").value;
    const status = document.getElementById("filter-status").value;
    const search = document.getElementById("search").value.toLowerCase();
    return list.filter(cb => matchesFilter(cb, slug, status, search));
}

function matchesFilter(cb, slug, status, search) {
    if (slug   && cb.slug   !== slug)   return false;
    if (status && cb.status !== status) return false;
    if (search) {
        const hay = (cb.slug + cb.subPath + cb.sourceIp + cb.rawBody + cb.method).toLowerCase();
        if (!hay.includes(search)) return false;
    }
    return true;
}

function formatBody(cb) {
    if (!cb.rawBody) return "(empty body)";
    if (cb.isJsonBody) {
        try { return esc(JSON.stringify(JSON.parse(cb.rawBody), null, 2)); } catch { /* fall through */ }
    }
    return esc(cb.rawBody);
}

function copyText(cb) {
    if (!cb.rawBody) return "";
    if (cb.isJsonBody) {
        try { return JSON.stringify(JSON.parse(cb.rawBody), null, 2); } catch { /* fall through */ }
    }
    return cb.rawBody;
}

function esc(str) {
    return String(str ?? "")
        .replace(/&/g, "&amp;").replace(/</g, "&lt;")
        .replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

function relTime(isoStr) {
    const secs = Math.floor((Date.now() - new Date(isoStr)) / 1000);
    if (secs < 60)   return "just now";
    if (secs < 3600) return `${Math.floor(secs / 60)}m ago`;
    return `${Math.floor(secs / 3600)}h ago`;
}

// ── Events ──────────────────────────────────────────────────────────────────────
document.getElementById("search")        .addEventListener("input",  renderCallbacks);
document.getElementById("filter-slug")   .addEventListener("change", renderCallbacks);
document.getElementById("filter-status") .addEventListener("change", renderCallbacks);
document.getElementById("clear-btn")     .addEventListener("click",  async () => {
    const res = await fetch("/api/callbacks", { method: "DELETE" });
    if (!res.ok) return;
    allCallbacks = [];
    expandedIds.clear();
    renderCallbacks();
    syncSlugFilter();
    // Reconnect so server sends fresh (empty) History, keeping client in sync.
    await connection.stop();
});
