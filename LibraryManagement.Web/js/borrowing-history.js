import { apiRequest, debounce, escapeHtml, formatDate, formatFine, loanStatusBadge, showToast } from "../js/api.js";
import { initLayout } from "../js/layout.js";

initLayout({ activePage: "history", pageTitle: "Borrowing History", isSubPage: true });

const state = {
  page: 1,
  pageSize: 15,
  search: "",
  loanStatus: "",
  startDate: "",
  endDate: ""
};

const loanModal = new bootstrap.Modal(document.getElementById("loanDetailsModal"));
const resModal = new bootstrap.Modal(document.getElementById("resModal"));

init();

async function init() {
  document.getElementById("loan-search").addEventListener("input", debounce(e => {
    state.search = e.target.value;
    state.page = 1;
    loadLoans();
  }, 250));

  document.getElementById("loan-status-filter").addEventListener("change", e => {
    state.loanStatus = e.target.value;
    state.page = 1;
    loadLoans();
  });

  document.getElementById("loan-start-date").addEventListener("change", e => {
    state.startDate = e.target.value;
    state.page = 1;
    loadLoans();
  });

  document.getElementById("loan-end-date").addEventListener("change", e => {
    state.endDate = e.target.value;
    state.page = 1;
    loadLoans();
  });

  document.getElementById("loan-reset-btn").addEventListener("click", () => {
    state.search = "";
    state.loanStatus = "";
    state.startDate = "";
    state.endDate = "";
    state.page = 1;
    document.getElementById("loan-search").value = "";
    document.getElementById("loan-status-filter").value = "";
    document.getElementById("loan-start-date").value = "";
    document.getElementById("loan-end-date").value = "";
    loadLoans();
  });

  document.getElementById("new-res-btn").addEventListener("click", openReservationModal);
  document.getElementById("res-form").addEventListener("submit", handleCreateReservation);

  await Promise.all([loadLoans(), loadReservations()]);
}

async function loadLoans() {
  const tbody = document.getElementById("loans-tbody");
  tbody.innerHTML = `<tr><td colspan="11" class="loading-state">Loading loans...</td></tr>`;

  const params = new URLSearchParams({ page: String(state.page), pageSize: String(state.pageSize) });
  if (state.search.trim()) params.set("search", state.search.trim());
  if (state.loanStatus) params.set("loanStatus", state.loanStatus);
  if (state.startDate) params.set("startDate", state.startDate);
  if (state.endDate) params.set("endDate", state.endDate);

  try {
    const { data } = await apiRequest(`/loans?${params.toString()}`);
    const items = data.items || [];
    if (!items.length) {
      tbody.innerHTML = `<tr><td colspan="11" class="empty-state"><i class="bi bi-clock-history"></i>No loans match your filters.</td></tr>`;
    } else {
      tbody.innerHTML = items.map(l => {
        const canRenew = !l.returnDate && !l.isOverdue && l.loanStatus !== "Overdue";
        return `
          <tr>
            <td class="fw-medium">#${l.loanId}</td>
            <td class="fw-medium">${escapeHtml(l.bookTitle)}</td>
            <td><code>${escapeHtml(l.accessionNumber)}</code></td>
            <td>${escapeHtml(l.memberName)} <span class="text-secondary small">(${escapeHtml(l.membershipNumber)})</span></td>
            <td class="text-secondary text-nowrap">${formatDate(l.issueDate)}</td>
            <td class="text-secondary text-nowrap">${formatDate(l.dueDate)}</td>
            <td class="text-secondary text-nowrap">${formatDate(l.returnDate)}</td>
            <td class="text-end tabular-nums">${l.renewalCount}</td>
            <td class="text-end tabular-nums">${formatFine(l.fineAmount)}</td>
            <td>${loanStatusBadge(l)}</td>
            <td class="text-end text-nowrap">
              <button type="button" class="btn btn-outline-secondary btn-sm" data-action="view" data-id="${l.loanId}" title="Details & Renewals">
                <i class="bi bi-eye"></i>
              </button>
              ${canRenew ? `
                <button type="button" class="btn btn-outline-secondary btn-sm" data-action="renew" data-id="${l.loanId}" title="Renew (+7 days)">
                  <i class="bi bi-arrow-repeat"></i> Renew
                </button>` : ""}
            </td>
          </tr>
        `;
      }).join("");

      tbody.querySelectorAll("button[data-action]").forEach(btn => {
        btn.addEventListener("click", () => {
          const id = Number(btn.dataset.id);
          if (btn.dataset.action === "view") openLoanDetails(id);
          else handleRenew(id);
        });
      });
    }

    const pager = document.getElementById("loans-pagination");
    pager.innerHTML = `
      <div>Total: <strong>${data.totalCount}</strong> loan(s)</div>
      <div class="d-flex align-items-center gap-2">
        <button type="button" class="btn btn-outline-secondary btn-sm" id="loans-prev" ${data.page <= 1 ? "disabled" : ""}>Prev</button>
        <span>Page ${data.page} of ${Math.max(1, data.totalPages)}</span>
        <button type="button" class="btn btn-outline-secondary btn-sm" id="loans-next" ${data.page >= data.totalPages ? "disabled" : ""}>Next</button>
      </div>
    `;
    document.getElementById("loans-prev")?.addEventListener("click", () => { state.page--; loadLoans(); });
    document.getElementById("loans-next")?.addEventListener("click", () => { state.page++; loadLoans(); });
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="11" class="empty-state text-danger">${escapeHtml(err.message)}</td></tr>`;
  }
}

async function handleRenew(id) {
  try {
    const { data } = await apiRequest(`/loans/${id}/renew`, { method: "POST", body: {} });
    showToast(`Loan #${id} renewed. New due date: ${formatDate(data.loan.dueDate)}.`);
    await loadLoans();
  } catch (err) {
    showToast(err.message, "error");
  }
}

async function openLoanDetails(id) {
  try {
    const { data } = await apiRequest(`/loans/${id}`);
    const l = data.loan;
    const renewals = data.renewals || [];
    document.getElementById("loanDetailsTitle").textContent = `Loan #${l.loanId} — ${l.bookTitle}`;
    document.getElementById("loan-details-body").innerHTML = `
      <div class="row g-2 mb-3 small">
        <div class="col-md-4"><span class="text-secondary">Member:</span> <strong>${escapeHtml(l.memberName)}</strong></div>
        <div class="col-md-4"><span class="text-secondary">Accession:</span> <code>${escapeHtml(l.accessionNumber)}</code></div>
        <div class="col-md-4"><span class="text-secondary">Status:</span> ${loanStatusBadge(l)}</div>
        <div class="col-md-4"><span class="text-secondary">Issued:</span> ${formatDate(l.issueDate)}</div>
        <div class="col-md-4"><span class="text-secondary">Due:</span> ${formatDate(l.dueDate)}</div>
        <div class="col-md-4"><span class="text-secondary">Fine:</span> <strong>${formatFine(l.fineAmount)}</strong></div>
      </div>
      <h6 class="fw-semibold mb-2">Renewal History (${renewals.length})</h6>
      <div class="table-responsive border rounded">
        <table class="data-table">
          <thead><tr><th>Renewal #</th><th>Previous Due Date</th><th>New Due Date</th><th>Renewed On</th></tr></thead>
          <tbody>
            ${!renewals.length ? `<tr><td colspan="4" class="empty-state">No renewals recorded for this loan.</td></tr>` : renewals.map(r => `
              <tr>
                <td>#${r.renewalId}</td>
                <td class="text-secondary">${formatDate(r.previousDueDate)}</td>
                <td class="fw-medium">${formatDate(r.newDueDate)}</td>
                <td class="text-secondary">${formatDate(r.renewalDate)}</td>
              </tr>
            `).join("")}
          </tbody>
        </table>
      </div>
    `;
    loanModal.show();
  } catch (err) {
    showToast(err.message, "error");
  }
}

async function loadReservations() {
  const tbody = document.getElementById("reservations-tbody");
  try {
    const { data } = await apiRequest("/reservations");
    const items = data || [];
    if (!items.length) {
      tbody.innerHTML = `<tr><td colspan="7" class="empty-state">No reservations in queue.</td></tr>`;
      return;
    }

    tbody.innerHTML = items.map(r => {
      const badge = r.reservationStatus === "Pending"
        ? "status-warning"
        : r.reservationStatus === "Fulfilled"
          ? "status-success"
          : "status-neutral";
      return `
        <tr>
          <td>#${r.reservationId}</td>
          <td class="fw-medium">${escapeHtml(r.bookTitle)}</td>
          <td><code>${escapeHtml(r.isbn)}</code></td>
          <td>${escapeHtml(r.memberName)} <span class="text-secondary small">(${escapeHtml(r.membershipNumber)})</span></td>
          <td class="text-secondary">${formatDate(r.reservationDate)}</td>
          <td><span class="status-badge ${badge}">${escapeHtml(r.reservationStatus)}</span></td>
          <td class="text-end text-nowrap">
            ${r.reservationStatus === "Pending" ? `
              <button type="button" class="btn btn-outline-secondary btn-sm" data-res-action="fulfill" data-id="${r.reservationId}">Fulfill</button>
              <button type="button" class="btn btn-outline-danger btn-sm" data-res-action="cancel" data-id="${r.reservationId}">Cancel</button>
            ` : "—"}
          </td>
        </tr>
      `;
    }).join("");

    tbody.querySelectorAll("button[data-res-action]").forEach(btn => {
      btn.addEventListener("click", async () => {
        const id = Number(btn.dataset.id);
        const act = btn.dataset.resAction;
        try {
          await apiRequest(`/reservations/${id}/${act}`, { method: "PUT" });
          showToast(`Reservation #${id} ${act}ed.`);
          await loadReservations();
        } catch (err) {
          showToast(err.message, "error");
        }
      });
    });
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="7" class="empty-state text-danger">${escapeHtml(err.message)}</td></tr>`;
  }
}

async function openReservationModal() {
  document.getElementById("res-error").classList.add("d-none");
  try {
    const [memRes, bookRes] = await Promise.all([
      apiRequest("/members?membershipStatus=Active&page=1&pageSize=100"),
      apiRequest("/books?page=1&pageSize=100")
    ]);
    document.getElementById("res-member").innerHTML = `<option value="">Select active member...</option>` +
      (memRes.data?.items || []).map(m => `<option value="${m.memberId}">${escapeHtml(m.fullName)} (${escapeHtml(m.membershipNumber)})</option>`).join("");
    document.getElementById("res-book").innerHTML = `<option value="">Select book...</option>` +
      (bookRes.data?.items || []).map(b => `<option value="${b.bookId}">${escapeHtml(b.title)} (Avail: ${b.availableCopies})</option>`).join("");
    resModal.show();
  } catch (err) {
    showToast(err.message, "error");
  }
}

async function handleCreateReservation(e) {
  e.preventDefault();
  const errBox = document.getElementById("res-error");
  errBox.classList.add("d-none");
  const memberId = Number(document.getElementById("res-member").value);
  const bookId = Number(document.getElementById("res-book").value);
  if (!memberId || !bookId) {
    errBox.textContent = "Select both a member and a book.";
    errBox.classList.remove("d-none");
    return;
  }

  try {
    await apiRequest("/reservations", { method: "POST", body: { memberId, bookId } });
    showToast("Reservation created.");
    resModal.hide();
    await loadReservations();
  } catch (err) {
    errBox.textContent = err.message;
    errBox.classList.remove("d-none");
  }
}
