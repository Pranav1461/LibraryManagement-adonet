const DEFAULT_API_BASE = "http://localhost:5056/api";

export function getApiBaseUrl() {
  return localStorage.getItem("lms_api_base_url") || DEFAULT_API_BASE;
}

export function setApiBaseUrl(url) {
  const cleaned = (url || DEFAULT_API_BASE).trim().replace(/\/+$/, "");
  localStorage.setItem("lms_api_base_url", cleaned);
  return cleaned;
}

export async function apiRequest(path, options = {}) {
  const baseUrl = getApiBaseUrl();
  const normalizedPath = path.startsWith("/") ? path : `/${path}`;
  const url = `${baseUrl}${normalizedPath}`;

  const config = {
    method: options.method || "GET",
    headers: {
      "Content-Type": "application/json",
      ...(options.headers || {})
    }
  };

  if (options.body !== undefined) {
    config.body = typeof options.body === "string" ? options.body : JSON.stringify(options.body);
  }

  let response;
  try {
    response = await fetch(url, config);
  } catch {
    throw new Error(`Unable to reach API at ${baseUrl}. Ensure LibraryManagement.API is running.`);
  }

  const cacheHeader = response.headers.get("X-Cache");
  const contentType = response.headers.get("content-type") || "";
  const isJson = contentType.includes("application/json");
  const payload = isJson ? await response.json() : await response.text();

  if (!response.ok) {
    const errorMessage = isJson && payload?.message
      ? payload.message
      : `Request failed with status ${response.status}.`;
    const err = new Error(errorMessage);
    err.status = response.status;
    err.errors = isJson ? payload?.errors : null;
    throw err;
  }

  return {
    data: isJson && payload && "data" in payload ? payload.data : payload,
    message: isJson ? payload?.message : "",
    cacheStatus: cacheHeader || (isJson && payload?.cacheHit === true ? "HIT" : isJson && payload?.cacheHit === false ? "MISS" : null)
  };
}

export function formatFine(amount) {
  const num = Number(amount || 0);
  return `₹${num.toLocaleString("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

export function formatNumber(val) {
  return new Intl.NumberFormat("en-US").format(Number(val || 0));
}

export function formatDate(iso) {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";
  return d.toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric" });
}

export function formatDateTime(iso) {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";
  return d.toLocaleString("en-US", { year: "numeric", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}

export function escapeHtml(str) {
  if (str === null || str === undefined) return "";
  return String(str)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

export function copyStatusBadge(status) {
  const map = {
    Available: "status-success",
    Borrowed: "status-info",
    Reserved: "status-warning",
    Damaged: "status-warning",
    Lost: "status-danger",
    Inactive: "status-neutral"
  };
  return `<span class="status-badge ${map[status] || "status-neutral"}">${escapeHtml(status)}</span>`;
}

export function membershipBadge(status) {
  const map = {
    Active: "status-success",
    Inactive: "status-neutral",
    Expired: "status-danger"
  };
  return `<span class="status-badge ${map[status] || "status-neutral"}">${escapeHtml(status)}</span>`;
}

export function loanStatusBadge(loan) {
  if (loan.returnDate) return `<span class="status-badge status-success">Returned</span>`;
  if (loan.isOverdue || loan.loanStatus === "Overdue") {
    return `<span class="status-badge status-danger">Overdue (${loan.overdueDays}d)</span>`;
  }
  return `<span class="status-badge status-info">Issued</span>`;
}

export function showToast(message, type = "success") {
  let stack = document.getElementById("toast-stack");
  if (!stack) {
    stack = document.createElement("div");
    stack.id = "toast-stack";
    stack.className = "toast-stack";
    document.body.appendChild(stack);
  }

  const iconClass = type === "error"
    ? "bi-exclamation-circle text-danger"
    : type === "warning"
      ? "bi-exclamation-triangle text-warning"
      : "bi-check-circle text-success";

  const toast = document.createElement("div");
  toast.className = "app-toast";
  toast.innerHTML = `
    <i class="bi ${iconClass} mt-1"></i>
    <div class="flex-grow-1">${escapeHtml(message)}</div>
    <button type="button" class="btn-close btn-sm" aria-label="Close"></button>
  `;
  toast.querySelector(".btn-close").addEventListener("click", () => toast.remove());
  stack.appendChild(toast);

  setTimeout(() => {
    if (toast.isConnected) toast.remove();
  }, 4500);
}

export function debounce(fn, delayMs = 250) {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), delayMs);
  };
}
