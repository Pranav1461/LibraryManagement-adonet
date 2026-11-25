import { apiRequest, copyStatusBadge, debounce, escapeHtml, formatDate, formatFine, loanStatusBadge, showToast } from "../js/api.js";
import { initLayout } from "../js/layout.js";

initLayout({ activePage: "copies", pageTitle: "Book Copies", isSubPage: true });

const state = {
  page: 1,
  pageSize: 15,
  search: "",
  bookId: "",
  copyStatus: "",
  copies: []
};

const copyModal = new bootstrap.Modal(document.getElementById("copyModal"));
const historyModal = new bootstrap.Modal(document.getElementById("copyHistoryModal"));

init();

async function init() {
  bindEvents();
  await loadBooksLookup();
  await loadCopies();
}

function bindEvents() {
  document.getElementById("copy-search").addEventListener("input", debounce(e => {
    state.search = e.target.value;
    state.page = 1;
    loadCopies();
  }, 250));

  document.getElementById("copy-filter-book").addEventListener("change", e => {
    state.bookId = e.target.value;
    state.page = 1;
    loadCopies();
  });

  document.getElementById("copy-filter-status").addEventListener("change", e => {
    state.copyStatus = e.target.value;
    state.page = 1;
    loadCopies();
  });

  document.getElementById("copy-reset-btn").addEventListener("click", () => {
    state.search = "";
    state.bookId = "";
    state.copyStatus = "";
    state.page = 1;
    document.getElementById("copy-search").value = "";
    document.getElementById("copy-filter-book").value = "";
    document.getElementById("copy-filter-status").value = "";
    loadCopies();
  });

  document.getElementById("add-copy-btn").addEventListener("click", openCreate);
  document.getElementById("copy-form").addEventListener("submit", handleSave);
}

async function loadBooksLookup() {
  try {
    const { data } = await apiRequest("/books?page=1&pageSize=100");
    const opts = (data.items || []).map(b => `<option value="${b.bookId}">${escapeHtml(b.title)} (${escapeHtml(b.isbn)})</option>`).join("");
    document.getElementById("copy-filter-book").innerHTML = `<option value="">All Books</option>${opts}`;
    document.getElementById("copy-book").innerHTML = `<option value="">Select book...</option>${opts}`;
  } catch (err) {
    showToast(err.message, "error");
  }
}

async function loadCopies() {
  const tbody = document.getElementById("copies-tbody");
  tbody.innerHTML = `<tr><td colspan="8" class="loading-state">Loading physical copies...</td></tr>`;

  const params = new URLSearchParams({ page: String(state.page), pageSize: String(state.pageSize) });
  if (state.search.trim()) params.set("search", state.search.trim());
  if (state.bookId) params.set("bookId", state.bookId);
  if (state.copyStatus) params.set("copyStatus", state.copyStatus);

  try {
    const { data } = await apiRequest(`/bookcopies?${params.toString()}`);
    state.copies = data.items || [];
    if (!state.copies.length) {
      tbody.innerHTML = `<tr><td colspan="8" class="empty-state"><i class="bi bi-upc-scan"></i>No physical copies found.</td></tr>`;
    } else {
      tbody.innerHTML = state.copies.map(c => `
        <tr>
          <td><code>${escapeHtml(c.accessionNumber)}</code></td>
          <td class="fw-medium">${escapeHtml(c.bookTitle)}</td>
          <td class="text-secondary"><code>${escapeHtml(c.isbn)}</code></td>
          <td>${escapeHtml(c.shelfLocation || "—")}</td>
          <td class="text-secondary text-nowrap">${formatDate(c.acquisitionDate)}</td>
          <td>${copyStatusBadge(c.copyStatus)}</td>
          <td class="text-secondary">${escapeHtml(c.notes || "—")}</td>
          <td class="text-end text-nowrap">
            <button type="button" class="btn btn-outline-secondary btn-sm" data-action="history" data-id="${c.copyId}" title="Borrowing History">
              <i class="bi bi-clock-history"></i>
            </button>
            <button type="button" class="btn btn-outline-secondary btn-sm" data-action="edit" data-id="${c.copyId}" title="Edit">
              <i class="bi bi-pencil"></i>
            </button>
          </td>
        </tr>
      `).join("");

      tbody.querySelectorAll("button[data-action]").forEach(btn => {
        btn.addEventListener("click", () => {
          const id = Number(btn.dataset.id);
          if (btn.dataset.action === "history") viewHistory(id);
          else openEdit(id);
        });
      });
    }

    const pager = document.getElementById("copies-pagination");
    pager.innerHTML = `
      <div>Total: <strong>${data.totalCount}</strong> physical copy/copies</div>
      <div class="d-flex align-items-center gap-2">
        <button type="button" class="btn btn-outline-secondary btn-sm" id="copy-prev" ${data.page <= 1 ? "disabled" : ""}>Prev</button>
        <span>Page ${data.page} of ${Math.max(1, data.totalPages)}</span>
        <button type="button" class="btn btn-outline-secondary btn-sm" id="copy-next" ${data.page >= data.totalPages ? "disabled" : ""}>Next</button>
      </div>
    `;
    document.getElementById("copy-prev")?.addEventListener("click", () => { state.page--; loadCopies(); });
    document.getElementById("copy-next")?.addEventListener("click", () => { state.page++; loadCopies(); });
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="8" class="empty-state text-danger">${escapeHtml(err.message)}</td></tr>`;
  }
}

function openCreate() {
  document.getElementById("copyModalTitle").textContent = "Add Physical Copy";
  document.getElementById("copy-form").reset();
  document.getElementById("copy-id").value = "";
  document.getElementById("copy-book-group").classList.remove("d-none");
  document.getElementById("copy-acquired").value = new Date().toISOString().slice(0, 10);
  document.getElementById("copy-form-error").classList.add("d-none");
  copyModal.show();
}

function openEdit(id) {
  const c = state.copies.find(x => x.copyId === id);
  if (!c) return;
  document.getElementById("copyModalTitle").textContent = `Edit Copy — ${c.accessionNumber}`;
  document.getElementById("copy-id").value = c.copyId;
  document.getElementById("copy-book").value = c.bookId;
  document.getElementById("copy-book-group").classList.add("d-none");
  document.getElementById("copy-accession").value = c.accessionNumber;
  document.getElementById("copy-shelf").value = c.shelfLocation || "";
  document.getElementById("copy-status").value = c.copyStatus === "Borrowed" ? "Available" : c.copyStatus;
  document.getElementById("copy-acquired").value = c.acquisitionDate ? c.acquisitionDate.slice(0, 10) : "";
  document.getElementById("copy-notes").value = c.notes || "";
  document.getElementById("copy-form-error").classList.add("d-none");
  copyModal.show();
}

async function handleSave(e) {
  e.preventDefault();
  const errorBox = document.getElementById("copy-form-error");
  errorBox.classList.add("d-none");

  const id = document.getElementById("copy-id").value;
  const bookId = Number(document.getElementById("copy-book").value);
  const accessionNumber = document.getElementById("copy-accession").value.trim();
  const shelfLocation = document.getElementById("copy-shelf").value.trim();
  const copyStatus = document.getElementById("copy-status").value;
  const acquired = document.getElementById("copy-acquired").value;
  const notes = document.getElementById("copy-notes").value.trim();

  if ((!id && !bookId) || !accessionNumber) {
    errorBox.textContent = "Book and accession number are required.";
    errorBox.classList.remove("d-none");
    return;
  }

  try {
    if (id) {
      await apiRequest(`/bookcopies/${id}`, {
        method: "PUT",
        body: {
          accessionNumber,
          shelfLocation: shelfLocation || null,
          acquisitionDate: acquired ? new Date(acquired).toISOString() : null,
          copyStatus,
          notes: notes || null
        }
      });
      showToast("Copy updated.");
    } else {
      await apiRequest("/bookcopies", {
        method: "POST",
        body: {
          bookId,
          accessionNumber,
          shelfLocation: shelfLocation || null,
          acquisitionDate: acquired ? new Date(acquired).toISOString() : null,
          copyStatus,
          notes: notes || null
        }
      });
      showToast("Physical copy added.");
    }
    copyModal.hide();
    await loadCopies();
  } catch (err) {
    errorBox.textContent = err.message;
    errorBox.classList.remove("d-none");
  }
}

async function viewHistory(id) {
  try {
    const { data } = await apiRequest(`/bookcopies/${id}`);
    const c = data.copy;
    const loans = data.loanHistory || [];
    document.getElementById("copyHistoryTitle").textContent = `History — ${c.accessionNumber} (${c.bookTitle})`;
    document.getElementById("copy-history-body").innerHTML = !loans.length
      ? `<div class="empty-state">This physical copy has not been borrowed yet.</div>`
      : `<div class="table-responsive border rounded">
          <table class="data-table">
            <thead><tr><th>Loan #</th><th>Member</th><th>Issued</th><th>Due</th><th>Returned</th><th class="text-end">Fine</th><th>Status</th></tr></thead>
            <tbody>
              ${loans.map(l => `
                <tr>
                  <td>#${l.loanId}</td>
                  <td>${escapeHtml(l.memberName)} <span class="text-secondary small">(${escapeHtml(l.membershipNumber)})</span></td>
                  <td class="text-secondary">${formatDate(l.issueDate)}</td>
                  <td class="text-secondary">${formatDate(l.dueDate)}</td>
                  <td class="text-secondary">${formatDate(l.returnDate)}</td>
                  <td class="text-end tabular-nums">${formatFine(l.fineAmount)}</td>
                  <td>${loanStatusBadge(l)}</td>
                </tr>
              `).join("")}
            </tbody>
          </table>
        </div>`;
    historyModal.show();
  } catch (err) {
    showToast(err.message, "error");
  }
}
