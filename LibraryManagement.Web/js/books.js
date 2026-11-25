import { apiRequest, copyStatusBadge, debounce, escapeHtml, formatDate, showToast } from "../js/api.js";
import { initLayout, updateCacheBadge } from "../js/layout.js";

initLayout({ activePage: "books", pageTitle: "Books", isSubPage: true });

const state = {
  page: 1,
  pageSize: 10,
  search: "",
  categoryId: "",
  publisherId: "",
  sortBy: "title",
  sortDirection: "ASC",
  books: []
};

const bookModal = new bootstrap.Modal(document.getElementById("bookModal"));
const detailsModal = new bootstrap.Modal(document.getElementById("bookDetailsModal"));

init();

async function init() {
  bindEvents();
  await loadLookups();
  await loadBooks();
}

function bindEvents() {
  document.getElementById("book-search").addEventListener("input", debounce(e => {
    state.search = e.target.value;
    state.page = 1;
    loadBooks();
  }, 250));

  document.getElementById("book-filter-category").addEventListener("change", e => {
    state.categoryId = e.target.value;
    state.page = 1;
    loadBooks();
  });

  document.getElementById("book-filter-publisher").addEventListener("change", e => {
    state.publisherId = e.target.value;
    state.page = 1;
    loadBooks();
  });

  document.getElementById("book-sort").addEventListener("change", e => {
    const [col, dir] = e.target.value.split(":");
    state.sortBy = col;
    state.sortDirection = dir;
    state.page = 1;
    loadBooks();
  });

  document.getElementById("book-reset-btn").addEventListener("click", () => {
    state.search = "";
    state.categoryId = "";
    state.publisherId = "";
    state.sortBy = "title";
    state.sortDirection = "ASC";
    state.page = 1;
    document.getElementById("book-search").value = "";
    document.getElementById("book-filter-category").value = "";
    document.getElementById("book-filter-publisher").value = "";
    document.getElementById("book-sort").value = "title:ASC";
    loadBooks();
  });

  document.getElementById("add-book-btn").addEventListener("click", openCreateModal);
  document.getElementById("book-form").addEventListener("submit", handleSaveBook);
}

async function loadLookups() {
  try {
    const [catRes, pubRes, authRes] = await Promise.all([
      apiRequest("/categories"),
      apiRequest("/publishers"),
      apiRequest("/authors")
    ]);
    const catOpts = (catRes.data || []).map(c => `<option value="${c.categoryId}">${escapeHtml(c.categoryName)}</option>`).join("");
    const pubOpts = (pubRes.data || []).map(p => `<option value="${p.publisherId}">${escapeHtml(p.publisherName)}</option>`).join("");
    const authOpts = (authRes.data || []).map(a => `<option value="${a.authorId}">${escapeHtml(a.authorName)}</option>`).join("");

    document.getElementById("book-filter-category").innerHTML = `<option value="">All Categories</option>${catOpts}`;
    document.getElementById("book-category").innerHTML = `<option value="">Select category...</option>${catOpts}`;
    document.getElementById("book-filter-publisher").innerHTML = `<option value="">All Publishers</option>${pubOpts}`;
    document.getElementById("book-publisher").innerHTML = `<option value="">None / Unassigned</option>${pubOpts}`;
    document.getElementById("book-authors").innerHTML = authOpts;
  } catch (err) {
    showToast(err.message, "error");
  }
}

async function loadBooks() {
  const tbody = document.getElementById("books-tbody");
  tbody.innerHTML = `<tr><td colspan="8" class="loading-state">Loading books...</td></tr>`;

  const params = new URLSearchParams({
    page: String(state.page),
    pageSize: String(state.pageSize),
    sortBy: state.sortBy,
    sortDirection: state.sortDirection
  });
  if (state.search.trim()) params.set("search", state.search.trim());
  if (state.categoryId) params.set("categoryId", state.categoryId);
  if (state.publisherId) params.set("publisherId", state.publisherId);

  try {
    const { data, cacheStatus } = await apiRequest(`/books?${params.toString()}`);
    updateCacheBadge(cacheStatus);
    state.books = data.items || [];
    renderBooks(state.books);
    renderPagination(data);
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="8" class="empty-state text-danger">${escapeHtml(err.message)}</td></tr>`;
  }
}

function renderBooks(books) {
  const tbody = document.getElementById("books-tbody");
  if (!books.length) {
    tbody.innerHTML = `<tr><td colspan="8" class="empty-state"><i class="bi bi-book"></i>No books match your filters.</td></tr>`;
    return;
  }

  tbody.innerHTML = books.map(b => `
    <tr>
      <td class="fw-medium">${escapeHtml(b.title)}</td>
      <td><code>${escapeHtml(b.isbn)}</code></td>
      <td>${escapeHtml((b.authors || []).map(a => a.authorName).join(", ") || "—")}</td>
      <td>${escapeHtml(b.categoryName)}</td>
      <td class="text-secondary">${escapeHtml(b.publisherName || "—")}</td>
      <td class="text-end tabular-nums">${b.publicationYear}</td>
      <td class="text-end tabular-nums">
        <strong>${b.availableCopies}</strong> <span class="text-secondary">/ ${b.totalCopies}</span>
      </td>
      <td class="text-end text-nowrap">
        <button type="button" class="btn btn-outline-secondary btn-sm" data-action="view" data-id="${b.bookId}" title="Details">
          <i class="bi bi-eye"></i>
        </button>
        <button type="button" class="btn btn-outline-secondary btn-sm" data-action="edit" data-id="${b.bookId}" title="Edit">
          <i class="bi bi-pencil"></i>
        </button>
        <button type="button" class="btn btn-outline-danger btn-sm" data-action="delete" data-id="${b.bookId}" title="Delete / Deactivate">
          <i class="bi bi-trash"></i>
        </button>
      </td>
    </tr>
  `).join("");

  tbody.querySelectorAll("button[data-action]").forEach(btn => {
    btn.addEventListener("click", () => {
      const id = Number(btn.dataset.id);
      if (btn.dataset.action === "view") openDetailsModal(id);
      else if (btn.dataset.action === "edit") openEditModal(id);
      else handleDeleteBook(id);
    });
  });
}

function renderPagination(paged) {
  const container = document.getElementById("books-pagination");
  const start = paged.totalCount === 0 ? 0 : (paged.page - 1) * paged.pageSize + 1;
  const end = Math.min(paged.page * paged.pageSize, paged.totalCount);

  container.innerHTML = `
    <div>Showing <strong>${start}–${end}</strong> of <strong>${paged.totalCount}</strong> titles</div>
    <div class="d-flex align-items-center gap-2">
      <button type="button" class="btn btn-outline-secondary btn-sm" id="books-prev" ${paged.page <= 1 ? "disabled" : ""}>Prev</button>
      <span>Page ${paged.page} of ${Math.max(1, paged.totalPages)}</span>
      <button type="button" class="btn btn-outline-secondary btn-sm" id="books-next" ${paged.page >= paged.totalPages ? "disabled" : ""}>Next</button>
    </div>
  `;
  document.getElementById("books-prev")?.addEventListener("click", () => { state.page--; loadBooks(); });
  document.getElementById("books-next")?.addEventListener("click", () => { state.page++; loadBooks(); });
}

function openCreateModal() {
  document.getElementById("bookModalTitle").textContent = "Add Book";
  document.getElementById("book-form").reset();
  document.getElementById("book-id").value = "";
  document.getElementById("book-year").value = String(new Date().getFullYear());
  document.getElementById("book-language").value = "English";
  document.getElementById("book-initial-copies").value = "1";
  document.querySelectorAll(".initial-copies-field").forEach(el => el.classList.remove("d-none"));
  document.getElementById("book-form-error").classList.add("d-none");
  bookModal.show();
}

async function openEditModal(id) {
  try {
    const { data } = await apiRequest(`/books/${id}`);
    const b = data.book;
    document.getElementById("bookModalTitle").textContent = `Edit Book — ${b.isbn}`;
    document.getElementById("book-id").value = b.bookId;
    document.getElementById("book-title").value = b.title;
    document.getElementById("book-isbn").value = b.isbn;
    document.getElementById("book-category").value = b.categoryId;
    document.getElementById("book-publisher").value = b.publisherId ?? "";
    document.getElementById("book-year").value = b.publicationYear;
    document.getElementById("book-edition").value = b.edition || "";
    document.getElementById("book-language").value = b.language;
    document.getElementById("book-shelf").value = b.shelfLocation || "";
    document.getElementById("book-description").value = b.description || "";

    const authorSet = new Set((b.authors || []).map(a => String(a.authorId)));
    Array.from(document.getElementById("book-authors").options).forEach(opt => {
      opt.selected = authorSet.has(opt.value);
    });

    document.querySelectorAll(".initial-copies-field").forEach(el => el.classList.add("d-none"));
    document.getElementById("book-form-error").classList.add("d-none");
    bookModal.show();
  } catch (err) {
    showToast(err.message, "error");
  }
}

async function openDetailsModal(id) {
  try {
    const { data } = await apiRequest(`/books/${id}`);
    const b = data.book;
    const copies = data.copies || [];
    document.getElementById("book-details-body").innerHTML = `
      <dl class="row mb-3">
        <dt class="col-sm-3 text-secondary">Title</dt><dd class="col-sm-9 fw-medium">${escapeHtml(b.title)}</dd>
        <dt class="col-sm-3 text-secondary">ISBN</dt><dd class="col-sm-9"><code>${escapeHtml(b.isbn)}</code></dd>
        <dt class="col-sm-3 text-secondary">Author(s)</dt><dd class="col-sm-9">${escapeHtml((b.authors || []).map(a => a.authorName).join(", "))}</dd>
        <dt class="col-sm-3 text-secondary">Category</dt><dd class="col-sm-9">${escapeHtml(b.categoryName)}</dd>
        <dt class="col-sm-3 text-secondary">Publisher</dt><dd class="col-sm-9">${escapeHtml(b.publisherName || "—")}</dd>
        <dt class="col-sm-3 text-secondary">Year / Edition</dt><dd class="col-sm-9">${b.publicationYear} ${b.edition ? `(${escapeHtml(b.edition)})` : ""}</dd>
        <dt class="col-sm-3 text-secondary">Shelf</dt><dd class="col-sm-9">${escapeHtml(b.shelfLocation || "—")}</dd>
        <dt class="col-sm-3 text-secondary">Description</dt><dd class="col-sm-9">${escapeHtml(b.description || "—")}</dd>
      </dl>
      <h6 class="fw-semibold mb-2">Physical Copies (${copies.length})</h6>
      <div class="table-responsive border rounded">
        <table class="data-table">
          <thead><tr><th>Accession #</th><th>Shelf</th><th>Acquired</th><th>Status</th><th>Notes</th></tr></thead>
          <tbody>
            ${!copies.length ? `<tr><td colspan="5" class="empty-state">No physical copies recorded.</td></tr>` : copies.map(c => `
              <tr>
                <td><code>${escapeHtml(c.accessionNumber)}</code></td>
                <td>${escapeHtml(c.shelfLocation || "—")}</td>
                <td class="text-secondary">${formatDate(c.acquisitionDate)}</td>
                <td>${copyStatusBadge(c.copyStatus)}</td>
                <td class="text-secondary">${escapeHtml(c.notes || "—")}</td>
              </tr>
            `).join("")}
          </tbody>
        </table>
      </div>
    `;
    detailsModal.show();
  } catch (err) {
    showToast(err.message, "error");
  }
}

async function handleSaveBook(e) {
  e.preventDefault();
  const errorBox = document.getElementById("book-form-error");
  errorBox.classList.add("d-none");

  const id = document.getElementById("book-id").value;
  const title = document.getElementById("book-title").value.trim();
  const isbn = document.getElementById("book-isbn").value.trim();
  const categoryId = Number(document.getElementById("book-category").value);
  const pubRaw = document.getElementById("book-publisher").value;
  const publisherId = pubRaw ? Number(pubRaw) : null;
  const publicationYear = Number(document.getElementById("book-year").value);
  const edition = document.getElementById("book-edition").value.trim();
  const language = document.getElementById("book-language").value.trim();
  const shelfLocation = document.getElementById("book-shelf").value.trim();
  const description = document.getElementById("book-description").value.trim();
  const authorIds = Array.from(document.getElementById("book-authors").selectedOptions).map(o => Number(o.value));

  if (!title || !isbn || !categoryId || !authorIds.length) {
    errorBox.textContent = "Title, ISBN, category, and at least one author are required.";
    errorBox.classList.remove("d-none");
    return;
  }

  const payload = {
    title,
    isbn,
    categoryId,
    publisherId,
    publicationYear,
    edition: edition || null,
    language: language || "English",
    shelfLocation: shelfLocation || null,
    description: description || null,
    authorIds
  };

  if (!id) {
    payload.initialCopies = Number(document.getElementById("book-initial-copies").value || 0);
    payload.accessionPrefix = document.getElementById("book-acc-prefix").value.trim() || null;
  } else {
    payload.isActive = true;
  }

  try {
    if (id) {
      await apiRequest(`/books/${id}`, { method: "PUT", body: payload });
      showToast("Book updated successfully.");
    } else {
      await apiRequest("/books", { method: "POST", body: payload });
      showToast("Book added successfully.");
    }
    bookModal.hide();
    await loadBooks();
  } catch (err) {
    errorBox.textContent = err.message;
    errorBox.classList.remove("d-none");
  }
}

async function handleDeleteBook(id) {
  const b = state.books.find(x => x.bookId === id);
  if (!confirm(`Remove or deactivate "${b?.title || id}"? Books with loan history will be marked inactive.`)) return;
  try {
    await apiRequest(`/books/${id}`, { method: "DELETE" });
    showToast("Book removed or deactivated.");
    await loadBooks();
  } catch (err) {
    showToast(err.message, "error");
  }
}
