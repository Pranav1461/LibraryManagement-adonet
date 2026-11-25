import { apiRequest, debounce, escapeHtml, formatDate, formatFine, loanStatusBadge, membershipBadge, showToast } from "../js/api.js";
import { initLayout } from "../js/layout.js";

initLayout({ activePage: "members", pageTitle: "Members", isSubPage: true });

const state = {
  page: 1,
  pageSize: 10,
  search: "",
  status: "",
  members: []
};

const memberModal = new bootstrap.Modal(document.getElementById("memberModal"));
const detailsModal = new bootstrap.Modal(document.getElementById("memberDetailsModal"));

init();

async function init() {
  document.getElementById("member-search").addEventListener("input", debounce(e => {
    state.search = e.target.value;
    state.page = 1;
    loadMembers();
  }, 250));

  document.getElementById("member-filter-status").addEventListener("change", e => {
    state.status = e.target.value;
    state.page = 1;
    loadMembers();
  });

  document.getElementById("member-reset-btn").addEventListener("click", () => {
    state.search = "";
    state.status = "";
    state.page = 1;
    document.getElementById("member-search").value = "";
    document.getElementById("member-filter-status").value = "";
    loadMembers();
  });

  document.getElementById("add-member-btn").addEventListener("click", openCreate);
  document.getElementById("member-form").addEventListener("submit", handleSave);

  await loadMembers();
}

async function loadMembers() {
  const tbody = document.getElementById("members-tbody");
  tbody.innerHTML = `<tr><td colspan="8" class="loading-state">Loading members...</td></tr>`;

  const params = new URLSearchParams({ page: String(state.page), pageSize: String(state.pageSize) });
  if (state.search.trim()) params.set("search", state.search.trim());
  if (state.status) params.set("membershipStatus", state.status);

  try {
    const { data } = await apiRequest(`/members?${params.toString()}`);
    state.members = data.items || [];
    if (!state.members.length) {
      tbody.innerHTML = `<tr><td colspan="8" class="empty-state"><i class="bi bi-people"></i>No members match your filter.</td></tr>`;
    } else {
      tbody.innerHTML = state.members.map(m => `
        <tr>
          <td><code>${escapeHtml(m.membershipNumber)}</code></td>
          <td class="fw-medium">${escapeHtml(m.fullName)}</td>
          <td>${escapeHtml(m.email)}</td>
          <td class="text-nowrap">${escapeHtml(m.phone)}</td>
          <td class="text-secondary text-nowrap">${formatDate(m.membershipExpiryDate)}</td>
          <td class="text-end tabular-nums">
            ${m.activeLoanCount}
            ${m.overdueLoanCount > 0 ? `<span class="text-danger fw-semibold">(${m.overdueLoanCount} overdue)</span>` : ""}
          </td>
          <td>${membershipBadge(m.membershipStatus)}</td>
          <td class="text-end text-nowrap">
            <button type="button" class="btn btn-outline-secondary btn-sm" data-action="view" data-id="${m.memberId}" title="Borrowing History">
              <i class="bi bi-eye"></i>
            </button>
            <button type="button" class="btn btn-outline-secondary btn-sm" data-action="edit" data-id="${m.memberId}" title="Edit">
              <i class="bi bi-pencil"></i>
            </button>
            <button type="button" class="btn btn-outline-danger btn-sm" data-action="delete" data-id="${m.memberId}" title="Delete / Deactivate">
              <i class="bi bi-trash"></i>
            </button>
          </td>
        </tr>
      `).join("");

      tbody.querySelectorAll("button[data-action]").forEach(btn => {
        btn.addEventListener("click", () => {
          const id = Number(btn.dataset.id);
          if (btn.dataset.action === "view") openDetails(id);
          else if (btn.dataset.action === "edit") openEdit(id);
          else handleDelete(id);
        });
      });
    }

    const pager = document.getElementById("members-pagination");
    pager.innerHTML = `
      <div>Total: <strong>${data.totalCount}</strong> member(s)</div>
      <div class="d-flex align-items-center gap-2">
        <button type="button" class="btn btn-outline-secondary btn-sm" id="mem-prev" ${data.page <= 1 ? "disabled" : ""}>Prev</button>
        <span>Page ${data.page} of ${Math.max(1, data.totalPages)}</span>
        <button type="button" class="btn btn-outline-secondary btn-sm" id="mem-next" ${data.page >= data.totalPages ? "disabled" : ""}>Next</button>
      </div>
    `;
    document.getElementById("mem-prev")?.addEventListener("click", () => { state.page--; loadMembers(); });
    document.getElementById("mem-next")?.addEventListener("click", () => { state.page++; loadMembers(); });
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="8" class="empty-state text-danger">${escapeHtml(err.message)}</td></tr>`;
  }
}

function openCreate() {
  document.getElementById("memberModalTitle").textContent = "Register Member";
  document.getElementById("member-form").reset();
  document.getElementById("member-id").value = "";
  const nextYear = new Date();
  nextYear.setFullYear(nextYear.getFullYear() + 1);
  document.getElementById("member-expiry").value = nextYear.toISOString().slice(0, 10);
  document.getElementById("member-status").value = "Active";
  document.getElementById("member-form-error").classList.add("d-none");
  memberModal.show();
}

function openEdit(id) {
  const m = state.members.find(x => x.memberId === id);
  if (!m) return;
  document.getElementById("memberModalTitle").textContent = `Edit Member — ${m.membershipNumber}`;
  document.getElementById("member-id").value = m.memberId;
  document.getElementById("member-number").value = m.membershipNumber;
  document.getElementById("member-name").value = m.fullName;
  document.getElementById("member-email").value = m.email;
  document.getElementById("member-phone").value = m.phone;
  document.getElementById("member-expiry").value = m.membershipExpiryDate ? m.membershipExpiryDate.slice(0, 10) : "";
  document.getElementById("member-status").value = m.membershipStatus;
  document.getElementById("member-address").value = m.address || "";
  document.getElementById("member-form-error").classList.add("d-none");
  memberModal.show();
}

async function openDetails(id) {
  try {
    const { data } = await apiRequest(`/members/${id}`);
    const m = data.member;
    const history = data.borrowingHistory || [];

    document.getElementById("memberDetailsTitle").textContent = `${m.fullName} (${m.membershipNumber})`;
    document.getElementById("member-details-body").innerHTML = `
      <div class="row g-3 mb-4 small">
        <div class="col-md-4"><span class="text-secondary">Email:</span> <strong>${escapeHtml(m.email)}</strong></div>
        <div class="col-md-4"><span class="text-secondary">Phone:</span> <strong>${escapeHtml(m.phone)}</strong></div>
        <div class="col-md-4"><span class="text-secondary">Status:</span> ${membershipBadge(m.membershipStatus)}</div>
        <div class="col-md-4"><span class="text-secondary">Registered:</span> ${formatDate(m.registrationDate)}</div>
        <div class="col-md-4"><span class="text-secondary">Expires:</span> ${formatDate(m.membershipExpiryDate)}</div>
        <div class="col-md-4"><span class="text-secondary">Active / Overdue:</span> <strong>${data.currentLoans.length}</strong> / <strong class="text-danger">${data.overdueLoans.length}</strong></div>
      </div>

      <h6 class="fw-semibold mb-2">Borrowing History (${history.length})</h6>
      <div class="table-responsive border rounded">
        <table class="data-table">
          <thead><tr><th>Loan #</th><th>Book</th><th>Accession</th><th>Issued</th><th>Due</th><th>Returned</th><th class="text-end">Fine</th><th>Status</th></tr></thead>
          <tbody>
            ${!history.length ? `<tr><td colspan="8" class="empty-state">No borrowing history for this member.</td></tr>` : history.map(l => `
              <tr>
                <td>#${l.loanId}</td>
                <td class="fw-medium">${escapeHtml(l.bookTitle)}</td>
                <td><code>${escapeHtml(l.accessionNumber)}</code></td>
                <td class="text-secondary">${formatDate(l.issueDate)}</td>
                <td class="text-secondary">${formatDate(l.dueDate)}</td>
                <td class="text-secondary">${formatDate(l.returnDate)}</td>
                <td class="text-end tabular-nums">${formatFine(l.fineAmount)}</td>
                <td>${loanStatusBadge(l)}</td>
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

async function handleSave(e) {
  e.preventDefault();
  const errorBox = document.getElementById("member-form-error");
  errorBox.classList.add("d-none");

  const id = document.getElementById("member-id").value;
  const membershipNumber = document.getElementById("member-number").value.trim();
  const fullName = document.getElementById("member-name").value.trim();
  const email = document.getElementById("member-email").value.trim();
  const phone = document.getElementById("member-phone").value.trim();
  const expiryRaw = document.getElementById("member-expiry").value;
  const membershipStatus = document.getElementById("member-status").value;
  const address = document.getElementById("member-address").value.trim();

  if (!membershipNumber || !fullName || !email || !phone) {
    errorBox.textContent = "Membership number, full name, email, and phone are required.";
    errorBox.classList.remove("d-none");
    return;
  }

  const payload = {
    membershipNumber,
    fullName,
    email,
    phone,
    address: address || null,
    membershipExpiryDate: expiryRaw ? new Date(expiryRaw).toISOString() : null,
    membershipStatus
  };

  try {
    if (id) {
      await apiRequest(`/members/${id}`, { method: "PUT", body: payload });
      showToast("Member updated.");
    } else {
      await apiRequest("/members", { method: "POST", body: payload });
      showToast("Member registered.");
    }
    memberModal.hide();
    await loadMembers();
  } catch (err) {
    errorBox.textContent = err.message;
    errorBox.classList.remove("d-none");
  }
}

async function handleDelete(id) {
  const m = state.members.find(x => x.memberId === id);
  if (!confirm(`Deactivate or delete "${m?.fullName || id}"? Members with loan history are set to Inactive.`)) return;
  try {
    await apiRequest(`/members/${id}`, { method: "DELETE" });
    showToast("Member updated / removed.");
    await loadMembers();
  } catch (err) {
    showToast(err.message, "error");
  }
}
