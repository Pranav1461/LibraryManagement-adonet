import { apiRequest, getApiBaseUrl, setApiBaseUrl, showToast } from "./api.js";

const NAV_GROUPS = [
  {
    label: "Overview",
    items: [
      { id: "dashboard", label: "Dashboard", icon: "bi-grid", href: "index.html" }
    ]
  },
  {
    label: "Catalogue",
    items: [
      { id: "books", label: "Books", icon: "bi-book", href: "pages/books.html" },
      { id: "authors", label: "Authors", icon: "bi-person-lines-fill", href: "pages/authors.html" },
      { id: "categories", label: "Categories", icon: "bi-tags", href: "pages/categories.html" },
      { id: "publishers", label: "Publishers", icon: "bi-building", href: "pages/publishers.html" },
      { id: "copies", label: "Book Copies", icon: "bi-upc-scan", href: "pages/book-copies.html" }
    ]
  },
  {
    label: "Circulation",
    items: [
      { id: "members", label: "Members", icon: "bi-people", href: "pages/members.html" },
      { id: "issue", label: "Issue Book", icon: "bi-journal-arrow-up", href: "pages/issue-book.html" },
      { id: "return", label: "Return Book", icon: "bi-journal-arrow-down", href: "pages/return-book.html" },
      { id: "history", label: "Borrowing History", icon: "bi-clock-history", href: "pages/borrowing-history.html" },
      { id: "overdue", label: "Overdue Books", icon: "bi-exclamation-circle", href: "pages/overdue-books.html" }
    ]
  },
  {
    label: "Analytics",
    items: [
      { id: "reports", label: "Reports", icon: "bi-bar-chart-line", href: "pages/reports.html" }
    ]
  }
];

const THEME_KEY = "lms_theme";

function applyTheme(theme) {
  const normalized = theme === "dark" ? "dark" : "light";
  document.documentElement.setAttribute("data-bs-theme", normalized);
  localStorage.setItem(THEME_KEY, normalized);
  const btn = document.getElementById("theme-toggle-btn");
  if (btn) {
    btn.innerHTML = normalized === "dark"
      ? `<i class="bi bi-sun"></i> Light`
      : `<i class="bi bi-moon"></i> Dark`;
    btn.title = normalized === "dark" ? "Switch to normal light mode" : "Switch to dark mode";
  }
  return normalized;
}

// Apply saved theme immediately on module load
applyTheme(localStorage.getItem(THEME_KEY) || "light");

export function initLayout({ activePage, pageTitle, isSubPage = false }) {
  const prefix = isSubPage ? "../" : "";
  const shell = document.getElementById("app-shell");
  if (!shell) return;

  const existingContent = document.getElementById("page-content");
  const navHtml = NAV_GROUPS.map(group => `
    <div class="nav-section-label">${group.label}</div>
    ${group.items.map(item => `
      <a class="sidebar-link ${item.id === activePage ? "active" : ""}" href="${prefix}${item.href}">
        <i class="bi ${item.icon}"></i>
        <span>${item.label}</span>
      </a>
    `).join("")}
  `).join("");

  const sidebar = document.createElement("aside");
  sidebar.className = "app-sidebar";
  sidebar.id = "app-sidebar";
  sidebar.innerHTML = `
    <a class="sidebar-brand" href="${prefix}index.html">
      <span class="sidebar-brand-mark"><i class="bi bi-journal-bookmark"></i></span>
      <span class="sidebar-brand-text">LibraDesk</span>
    </a>
    <nav class="sidebar-nav" aria-label="Library Navigation">
      ${navHtml}
    </nav>
    <div class="sidebar-footer">
      <div>ADO.NET + SQL Server</div>
      <div class="text-truncate">${getApiBaseUrl()}</div>
    </div>
  `;

  const backdrop = document.createElement("div");
  backdrop.className = "sidebar-backdrop";

  const main = document.createElement("div");
  main.className = "app-main";

  const topbar = document.createElement("header");
  topbar.className = "app-topbar";
  topbar.innerHTML = `
    <div class="topbar-left">
      <button type="button" class="btn btn-outline-secondary btn-sm d-lg-none" id="mobile-menu-btn" aria-label="Toggle menu">
        <i class="bi bi-list"></i>
      </button>
      <nav class="breadcrumb-nav" aria-label="Breadcrumb">
        <a href="${prefix}index.html">Library</a>
        <i class="bi bi-chevron-right" style="font-size: 11px;"></i>
        <span class="breadcrumb-current">${pageTitle}</span>
      </nav>
    </div>
    <div class="topbar-right">
      <span id="cache-indicator" class="status-badge status-neutral d-none"></span>
      <button type="button" class="btn btn-outline-secondary btn-sm" id="theme-toggle-btn" title="Toggle theme"></button>
      <button type="button" class="api-status-pill" id="api-config-btn" title="Configure API URL">
        <span class="api-status-dot" id="api-status-dot"></span>
        <span id="api-status-text">Checking API</span>
        <i class="bi bi-gear ms-1"></i>
      </button>
    </div>
  `;

  const contentWrap = document.createElement("main");
  contentWrap.className = "app-content";
  if (existingContent) contentWrap.appendChild(existingContent);

  main.appendChild(topbar);
  main.appendChild(contentWrap);

  shell.innerHTML = "";
  shell.appendChild(sidebar);
  shell.appendChild(backdrop);
  shell.appendChild(main);

  document.getElementById("mobile-menu-btn")?.addEventListener("click", () => {
    sidebar.classList.toggle("open");
    backdrop.classList.toggle("open");
  });
  backdrop.addEventListener("click", () => {
    sidebar.classList.remove("open");
    backdrop.classList.remove("open");
  });

  applyTheme(localStorage.getItem(THEME_KEY) || "light");
  document.getElementById("theme-toggle-btn")?.addEventListener("click", () => {
    const current = document.documentElement.getAttribute("data-bs-theme") === "dark" ? "dark" : "light";
    const next = applyTheme(current === "dark" ? "light" : "dark");
    window.dispatchEvent(new CustomEvent("lms-theme-change", { detail: { theme: next } }));
  });

  injectApiModal();
  document.getElementById("api-config-btn")?.addEventListener("click", () => {
    const input = document.getElementById("api-url-input");
    if (input) input.value = getApiBaseUrl();
    new bootstrap.Modal(document.getElementById("apiConfigModal")).show();
  });

  checkConnectivity();
}

export function updateCacheBadge(cacheStatus) {
  const el = document.getElementById("cache-indicator");
  if (!el) return;
  if (!cacheStatus) {
    el.classList.add("d-none");
    return;
  }
  el.classList.remove("d-none", "status-success", "status-neutral");
  el.classList.add(cacheStatus === "HIT" ? "status-success" : "status-neutral");
  el.innerHTML = `<i class="bi bi-lightning-charge"></i> Cache: ${cacheStatus}`;
}

async function checkConnectivity() {
  const dot = document.getElementById("api-status-dot");
  const text = document.getElementById("api-status-text");
  try {
    await apiRequest("/categories");
    dot?.classList.remove("offline");
    dot?.classList.add("online");
    if (text) text.textContent = "API Connected";
  } catch {
    dot?.classList.remove("online");
    dot?.classList.add("offline");
    if (text) text.textContent = "API Offline";
  }
}

function injectApiModal() {
  if (document.getElementById("apiConfigModal")) return;
  const div = document.createElement("div");
  div.innerHTML = `
    <div class="modal fade" id="apiConfigModal" tabindex="-1" aria-hidden="true">
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title">API Connection Settings</h5>
            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
          </div>
          <form id="api-config-form">
            <div class="modal-body">
              <div class="mb-3">
                <label class="form-label" for="api-url-input">Backend Base URL</label>
                <input type="url" class="form-control" id="api-url-input" required placeholder="http://localhost:5056/api">
              </div>
              <button type="button" class="btn btn-outline-secondary btn-sm" id="clear-cache-btn">
                <i class="bi bi-arrow-clockwise"></i> Clear Server IMemoryCache
              </button>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Cancel</button>
              <button type="submit" class="btn btn-primary">Save & Reload</button>
            </div>
          </form>
        </div>
      </div>
    </div>
  `;
  document.body.appendChild(div.firstElementChild);

  document.getElementById("api-config-form").addEventListener("submit", e => {
    e.preventDefault();
    setApiBaseUrl(document.getElementById("api-url-input").value);
    window.location.reload();
  });

  document.getElementById("clear-cache-btn").addEventListener("click", async () => {
    try {
      await apiRequest("/dashboard/cache/clear", { method: "POST" });
      showToast("Server memory cache cleared.");
    } catch (err) {
      showToast(err.message, "error");
    }
  });
}
