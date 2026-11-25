import { apiRequest, copyStatusBadge, debounce, escapeHtml, formatDate, formatFine, formatNumber, getApiBaseUrl, showToast } from "../js/api.js";
import { initLayout, updateCacheBadge } from "../js/layout.js";

initLayout({ activePage: "reports", pageTitle: "Reports", isSubPage: true });

const state = {
  report: "books",
  search: "",
  categoryId: "",
  authorId: "",
  bookId: "",
  memberId: "",
  copyStatus: "",
  startDate: "",
  endDate: "",
  page: 1,
  pageSize: 25
};

init();

async function init() {
  bindEvents();
  await loadLookups();
  configureFilters();
  await loadReport();
}

function bindEvents() {
  document.querySelectorAll("#report-tabs .segmented-tab").forEach(tab => {
    tab.addEventListener("click", () => {
      state.report = tab.dataset.report;
      state.page = 1;
      document.querySelectorAll("#report-tabs .segmented-tab").forEach(t => t.classList.remove("active"));
      tab.classList.add("active");
      configureFilters();
      loadReport();
    });
  });

  document.getElementById("rep-search").addEventListener("input", debounce(e => {
    state.search = e.target.value;
    state.page = 1;
    loadReport();
  }, 250));

  ["rep-category", "rep-author", "rep-book", "rep-member", "rep-copy-status", "rep-start", "rep-end"].forEach(id => {
    document.getElementById(id).addEventListener("change", () => {
      state.categoryId = document.getElementById("rep-category").value;
      state.authorId = document.getElementById("rep-author").value;
      state.bookId = document.getElementById("rep-book").value;
      state.memberId = document.getElementById("rep-member").value;
      state.copyStatus = document.getElementById("rep-copy-status").value;
      state.startDate = document.getElementById("rep-start").value;
      state.endDate = document.getElementById("rep-end").value;
      state.page = 1;
      loadReport();
    });
  });

  document.getElementById("rep-reset-btn").addEventListener("click", () => {
    state.search = "";
    state.categoryId = "";
    state.authorId = "";
    state.bookId = "";
    state.memberId = "";
    state.copyStatus = "";
    state.startDate = "";
    state.endDate = "";
    state.page = 1;
    ["rep-search", "rep-category", "rep-author", "rep-book", "rep-member", "rep-copy-status", "rep-start", "rep-end"].forEach(id => {
      document.getElementById(id).value = "";
    });
    loadReport();
  });

  document.getElementById("export-csv-btn").addEventListener("click", () => {
    const qs = buildParams();
    qs.set("format", "csv");
    window.open(`${getApiBaseUrl()}/reports/${state.report}?${qs.toString()}`, "_blank");
  });
}

function configureFilters() {
  const r = state.report;
  const showDates = ["overdue", "fines", "members", "most-borrowed", "monthly-borrowing", "monthly-returns"].includes(r);
  const showCategory = ["books", "available-books", "borrowed-books", "overdue", "fines", "most-borrowed", "categories", "copies"].includes(r);
  const showAuthor = ["books", "available-books", "borrowed-books", "authors"].includes(r);
  const showBook = ["books", "available-books", "borrowed-books", "overdue", "fines", "copies"].includes(r);
  const showMember = ["overdue", "fines", "members"].includes(r);
  const showCopyStatus = r === "copies";

  document.getElementById("rep-start").classList.toggle("d-none", !showDates);
  document.getElementById("rep-end").classList.toggle("d-none", !showDates);
  document.getElementById("rep-category").classList.toggle("d-none", !showCategory);
  document.getElementById("rep-author").classList.toggle("d-none", !showAuthor);
  document.getElementById("rep-book").classList.toggle("d-none", !showBook);
  document.getElementById("rep-member").classList.toggle("d-none", !showMember);
  document.getElementById("rep-copy-status").classList.toggle("d-none", !showCopyStatus);
}

async function loadLookups() {
  try {
    const [catRes, authRes, bookRes, memRes] = await Promise.all([
      apiRequest("/categories"),
      apiRequest("/authors"),
      apiRequest("/books?page=1&pageSize=100"),
      apiRequest("/members?page=1&pageSize=100")
    ]);
    document.getElementById("rep-category").innerHTML = `<option value="">All Categories</option>` +
      (catRes.data || []).map(c => `<option value="${c.categoryId}">${escapeHtml(c.categoryName)}</option>`).join("");
    document.getElementById("rep-author").innerHTML = `<option value="">All Authors</option>` +
      (authRes.data || []).map(a => `<option value="${a.authorId}">${escapeHtml(a.authorName)}</option>`).join("");
    document.getElementById("rep-book").innerHTML = `<option value="">All Books</option>` +
      (bookRes.data?.items || []).map(b => `<option value="${b.bookId}">${escapeHtml(b.title)}</option>`).join("");
    document.getElementById("rep-member").innerHTML = `<option value="">All Members</option>` +
      (memRes.data?.items || []).map(m => `<option value="${m.memberId}">${escapeHtml(m.fullName)} (${escapeHtml(m.membershipNumber)})</option>`).join("");
  } catch (err) {
    showToast(err.message, "error");
  }
}

function buildParams() {
  const params = new URLSearchParams({ page: String(state.page), pageSize: String(state.pageSize) });
  if (state.search.trim()) params.set("search", state.search.trim());
  if (state.categoryId) params.set("categoryId", state.categoryId);
  if (state.authorId) params.set("authorId", state.authorId);
  if (state.bookId) params.set("bookId", state.bookId);
  if (state.memberId) params.set("memberId", state.memberId);
  if (state.copyStatus) params.set("copyStatus", state.copyStatus);
  if (state.startDate) params.set("startDate", state.startDate);
  if (state.endDate) params.set("endDate", state.endDate);
  return params;
}

async function loadReport() {
  const tbody = document.getElementById("report-tbody");
  tbody.innerHTML = `<tr><td colspan="10" class="loading-state">Generating report...</td></tr>`;

  try {
    const { data, cacheStatus } = await apiRequest(`/reports/${state.report}?${buildParams().toString()}`);
    updateCacheBadge(cacheStatus);
    renderReport(state.report, data);
    renderPagination(data);
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="10" class="empty-state text-danger">${escapeHtml(err.message)}</td></tr>`;
  }
}

function renderSummary(tiles) {
  document.getElementById("report-summary-grid").innerHTML = tiles.map(t => `
    <div class="kpi-tile">
      <div class="kpi-label">${escapeHtml(t.label)}</div>
      <div class="kpi-value tabular-nums">${escapeHtml(t.value)}</div>
    </div>
  `).join("");
}

function renderPagination(res) {
  const container = document.getElementById("report-pagination");
  container.innerHTML = `
    <div>Total rows: <strong>${res.totalCount}</strong></div>
    <div class="d-flex align-items-center gap-2">
      <button type="button" class="btn btn-outline-secondary btn-sm" id="rep-prev" ${res.page <= 1 ? "disabled" : ""}>Prev</button>
      <span>Page ${res.page} of ${Math.max(1, res.totalPages)}</span>
      <button type="button" class="btn btn-outline-secondary btn-sm" id="rep-next" ${res.page >= res.totalPages ? "disabled" : ""}>Next</button>
    </div>
  `;
  document.getElementById("rep-prev")?.addEventListener("click", () => { state.page--; loadReport(); });
  document.getElementById("rep-next")?.addEventListener("click", () => { state.page++; loadReport(); });
}

function renderReport(type, res) {
  const rows = res.rows || [];
  const s = res.summary || {};
  const thead = document.getElementById("report-thead");
  const tbody = document.getElementById("report-tbody");

  if (["books", "available-books", "borrowed-books"].includes(type)) {
    renderSummary([
      { label: "Book Titles", value: formatNumber(s.totalTitles) },
      { label: "Total Copies", value: formatNumber(s.totalCopies) },
      { label: "Available", value: formatNumber(s.availableCopies) },
      { label: "Borrowed", value: formatNumber(s.borrowedCopies) }
    ]);
    thead.innerHTML = `<tr>
      <th>Title</th><th>ISBN</th><th>Authors</th><th>Category</th><th>Publisher</th>
      <th class="text-end">Year</th><th class="text-end">Total</th><th class="text-end">Avail</th><th class="text-end">Borrowed</th>
    </tr>`;
    tbody.innerHTML = !rows.length ? emptyRow(9) : rows.map(r => `<tr>
      <td class="fw-medium">${escapeHtml(r.title)}</td>
      <td><code>${escapeHtml(r.isbn)}</code></td>
      <td>${escapeHtml(r.authors || "—")}</td>
      <td>${escapeHtml(r.categoryName)}</td>
      <td class="text-secondary">${escapeHtml(r.publisherName)}</td>
      <td class="text-end tabular-nums">${r.publicationYear}</td>
      <td class="text-end tabular-nums">${r.totalCopies}</td>
      <td class="text-end tabular-nums text-success fw-semibold">${r.availableCopies}</td>
      <td class="text-end tabular-nums">${r.borrowedCopies}</td>
    </tr>`).join("");
  } else if (type === "overdue" || type === "fines") {
    renderSummary([
      { label: "Loans", value: formatNumber(s.totalLoans) },
      { label: "Active", value: formatNumber(s.activeLoans) },
      { label: "Overdue", value: formatNumber(s.overdueLoans) },
      { label: "Recorded Fines", value: formatFine(s.totalFines) }
    ]);
    thead.innerHTML = `<tr>
      <th>Loan #</th><th>Member</th><th>Accession</th><th>Book Title</th><th>Issued</th><th>Due</th><th>Returned</th><th class="text-end">Overdue</th><th class="text-end">Fine</th><th>Status</th>
    </tr>`;
    tbody.innerHTML = !rows.length ? emptyRow(10) : rows.map(r => `<tr>
      <td>#${r.loanId}</td>
      <td>${escapeHtml(r.memberName)} <span class="text-secondary small">(${escapeHtml(r.membershipNumber)})</span></td>
      <td><code>${escapeHtml(r.accessionNumber)}</code></td>
      <td class="fw-medium">${escapeHtml(r.bookTitle)}</td>
      <td class="text-secondary">${formatDate(r.issueDate)}</td>
      <td class="text-secondary">${formatDate(r.dueDate)}</td>
      <td class="text-secondary">${formatDate(r.returnDate)}</td>
      <td class="text-end tabular-nums text-danger">${r.overdueDays}d</td>
      <td class="text-end tabular-nums fw-medium">${formatFine(r.fineAmount)}</td>
      <td>${escapeHtml(r.loanStatus)}</td>
    </tr>`).join("");
  } else if (type === "members") {
    renderSummary([
      { label: "Members", value: formatNumber(s.totalMembers) },
      { label: "Total Borrows", value: formatNumber(s.totalLoans) },
      { label: "Currently Borrowed", value: formatNumber(s.activeLoans) },
      { label: "Total Fines", value: formatFine(s.totalFines) }
    ]);
    thead.innerHTML = `<tr>
      <th>Membership #</th><th>Member Name</th><th>Status</th><th class="text-end">Total Borrowed</th><th class="text-end">Active</th><th class="text-end">Overdue</th><th class="text-end">Fines</th><th>Last Borrowed</th>
    </tr>`;
    tbody.innerHTML = !rows.length ? emptyRow(8) : rows.map(r => `<tr>
      <td><code>${escapeHtml(r.membershipNumber)}</code></td>
      <td class="fw-medium">${escapeHtml(r.fullName)}</td>
      <td>${escapeHtml(r.membershipStatus)}</td>
      <td class="text-end tabular-nums">${r.totalBorrowed}</td>
      <td class="text-end tabular-nums">${r.currentlyBorrowed}</td>
      <td class="text-end tabular-nums text-danger">${r.overdueCount}</td>
      <td class="text-end tabular-nums">${formatFine(r.totalFines)}</td>
      <td class="text-secondary">${formatDate(r.lastBorrowDate)}</td>
    </tr>`).join("");
  } else if (type === "most-borrowed") {
    renderSummary([{ label: "Total Checkouts", value: formatNumber(s) }]);
    thead.innerHTML = `<tr><th>Book Title</th><th>ISBN</th><th class="text-end">Total Checkouts</th></tr>`;
    tbody.innerHTML = !rows.length ? emptyRow(3) : rows.map(r => `<tr>
      <td class="fw-medium">${escapeHtml(r.title)}</td>
      <td><code>${escapeHtml(r.isbn)}</code></td>
      <td class="text-end tabular-nums fw-semibold">${r.loanCount}</td>
    </tr>`).join("");
  } else if (type === "categories") {
    renderSummary([
      { label: "Categories", value: formatNumber(s.totalCategories) },
      { label: "Books", value: formatNumber(s.totalBooks) },
      { label: "Physical Copies", value: formatNumber(s.totalCopies) },
      { label: "Total Loans", value: formatNumber(s.totalLoans) }
    ]);
    thead.innerHTML = `<tr><th>Category</th><th class="text-end">Titles</th><th class="text-end">Copies</th><th class="text-end">Available</th><th class="text-end">Borrowed</th><th class="text-end">Total Loans</th></tr>`;
    tbody.innerHTML = !rows.length ? emptyRow(6) : rows.map(r => `<tr>
      <td class="fw-medium">${escapeHtml(r.categoryName)}</td>
      <td class="text-end tabular-nums">${r.bookCount}</td>
      <td class="text-end tabular-nums">${r.totalCopies}</td>
      <td class="text-end tabular-nums text-success">${r.availableCopies}</td>
      <td class="text-end tabular-nums">${r.borrowedCopies}</td>
      <td class="text-end tabular-nums fw-medium">${r.totalLoans}</td>
    </tr>`).join("");
  } else if (type === "authors") {
    renderSummary([
      { label: "Authors", value: formatNumber(s.totalAuthors) },
      { label: "Book Links", value: formatNumber(s.totalBookLinks) },
      { label: "Total Borrows", value: formatNumber(s.totalBorrows) }
    ]);
    thead.innerHTML = `<tr><th>Author</th><th class="text-end">Books</th><th class="text-end">Physical Copies</th><th class="text-end">Total Borrows</th></tr>`;
    tbody.innerHTML = !rows.length ? emptyRow(4) : rows.map(r => `<tr>
      <td class="fw-medium">${escapeHtml(r.authorName)}</td>
      <td class="text-end tabular-nums">${r.bookCount}</td>
      <td class="text-end tabular-nums">${r.totalCopies}</td>
      <td class="text-end tabular-nums fw-medium">${r.totalBorrows}</td>
    </tr>`).join("");
  } else if (type === "monthly-borrowing" || type === "monthly-returns") {
    renderSummary([
      { label: "Months", value: formatNumber(s.totalMonths) },
      { label: "Transactions", value: formatNumber(s.totalTransactions) },
      { label: "Fines", value: formatFine(s.totalFines) }
    ]);
    thead.innerHTML = `<tr><th>Month</th><th class="text-end">Transactions</th><th class="text-end">Unique Members</th><th class="text-end">Unique Titles</th><th class="text-end">Fines</th></tr>`;
    tbody.innerHTML = !rows.length ? emptyRow(5) : rows.map(r => `<tr>
      <td class="fw-medium">${escapeHtml(r.monthLabel)}</td>
      <td class="text-end tabular-nums fw-semibold">${r.transactionCount}</td>
      <td class="text-end tabular-nums">${r.uniqueMembers}</td>
      <td class="text-end tabular-nums">${r.uniqueBooks}</td>
      <td class="text-end tabular-nums">${formatFine(r.finesCollected)}</td>
    </tr>`).join("");
  } else if (type === "copies") {
    renderSummary([
      { label: "Total Copies", value: formatNumber(s.totalCopies) },
      { label: "Available", value: formatNumber(s.available) },
      { label: "Borrowed", value: formatNumber(s.borrowed) },
      { label: "Other Status", value: formatNumber(s.unavailableOrOther) }
    ]);
    thead.innerHTML = `<tr><th>Accession #</th><th>Book Title</th><th>ISBN</th><th>Category</th><th>Shelf</th><th>Status</th><th>Acquired</th><th class="text-end">Total Loans</th></tr>`;
    tbody.innerHTML = !rows.length ? emptyRow(8) : rows.map(r => `<tr>
      <td><code>${escapeHtml(r.accessionNumber)}</code></td>
      <td class="fw-medium">${escapeHtml(r.bookTitle)}</td>
      <td><code>${escapeHtml(r.isbn)}</code></td>
      <td>${escapeHtml(r.categoryName)}</td>
      <td>${escapeHtml(r.shelfLocation || "—")}</td>
      <td>${copyStatusBadge(r.CopyStatus || r.copyStatus)}</td>
      <td class="text-secondary">${formatDate(r.acquisitionDate)}</td>
      <td class="text-end tabular-nums">${r.totalLoans}</td>
    </tr>`).join("");
  }
}

function emptyRow(colspan) {
  return `<tr><td colspan="${colspan}" class="empty-state"><i class="bi bi-file-earmark-bar-graph"></i>No matching rows found.</td></tr>`;
}
