import { apiRequest, escapeHtml, formatDate, loanStatusBadge, showToast } from "../js/api.js";
import { initLayout } from "../js/layout.js";

initLayout({ activePage: "issue", pageTitle: "Issue Book", isSubPage: true });

let members = [];
let policy = { defaultBorrowingDays: 14, maxActiveLoansPerMember: 3 };

init();

async function init() {
  document.getElementById("issue-date").value = new Date().toISOString().slice(0, 10);
  document.getElementById("issue-date").addEventListener("change", updateDuePreview);
  document.getElementById("issue-duration").addEventListener("input", updateDuePreview);
  document.getElementById("issue-member").addEventListener("change", updateMemberHint);
  document.getElementById("issue-form").addEventListener("submit", handleIssue);

  await loadPolicy();
  await Promise.all([loadLookups(), loadActiveLoans()]);
  updateDuePreview();
}

async function loadPolicy() {
  try {
    const { data } = await apiRequest("/dashboard/policy");
    policy = data;
    document.getElementById("issue-duration").value = String(policy.defaultBorrowingDays);
    document.getElementById("policy-badge").textContent = `Max ${policy.maxActiveLoansPerMember} active books`;
  } catch { /* fallback defaults */ }
}

async function loadLookups() {
  try {
    const [memRes, copyRes] = await Promise.all([
      apiRequest("/members?page=1&pageSize=100"),
      apiRequest("/bookcopies?copyStatus=Available&page=1&pageSize=100")
    ]);
    members = memRes.data?.items || [];
    const copies = copyRes.data?.items || [];

    document.getElementById("issue-member").innerHTML = `<option value="">Select member...</option>` +
      members.map(m => `<option value="${m.memberId}">${escapeHtml(m.fullName)} (${escapeHtml(m.membershipNumber)}) — ${escapeHtml(m.membershipStatus)} (${m.activeLoanCount}/${policy.maxActiveLoansPerMember})</option>`).join("");

    document.getElementById("issue-copy").innerHTML = `<option value="">Select available physical copy...</option>` +
      copies.map(c => `<option value="${c.copyId}">${escapeHtml(c.accessionNumber)} — ${escapeHtml(c.bookTitle)}</option>`).join("");
  } catch (err) {
    showToast(err.message, "error");
  }
}

function updateMemberHint() {
  const id = Number(document.getElementById("issue-member").value);
  const hint = document.getElementById("member-eligibility-hint");
  const m = members.find(x => x.memberId === id);
  if (!m) {
    hint.textContent = "";
    return;
  }
  const expired = new Date(m.membershipExpiryDate) < new Date();
  if (m.membershipStatus !== "Active" || expired) {
    hint.innerHTML = `<span class="text-danger">Status: ${escapeHtml(m.membershipStatus)} (Expires ${formatDate(m.membershipExpiryDate)}) — ineligible to borrow.</span>`;
  } else if (m.activeLoanCount >= policy.maxActiveLoansPerMember) {
    hint.innerHTML = `<span class="text-warning">Reached borrowing limit (${m.activeLoanCount}/${policy.maxActiveLoansPerMember}).</span>`;
  } else {
    hint.innerHTML = `<span class="text-success">Eligible (${m.activeLoanCount}/${policy.maxActiveLoansPerMember} active loans).</span>`;
  }
}

function updateDuePreview() {
  const dateStr = document.getElementById("issue-date").value;
  const days = Number(document.getElementById("issue-duration").value) || policy.defaultBorrowingDays;
  if (!dateStr) return;
  const d = new Date(dateStr);
  d.setDate(d.getDate() + days);
  document.getElementById("due-date-preview").textContent = formatDate(d.toISOString());
}

async function handleIssue(e) {
  e.preventDefault();
  const errBox = document.getElementById("issue-error");
  errBox.classList.add("d-none");

  const memberId = Number(document.getElementById("issue-member").value);
  const copyId = Number(document.getElementById("issue-copy").value);
  const issueDate = document.getElementById("issue-date").value;
  const durationDays = Number(document.getElementById("issue-duration").value);
  const notes = document.getElementById("issue-notes").value.trim();

  if (!memberId || !copyId || durationDays < 1) {
    errBox.textContent = "Please select a member, an available copy, and a valid duration.";
    errBox.classList.remove("d-none");
    return;
  }

  const btn = document.getElementById("submit-issue-btn");
  btn.disabled = true;

  try {
    await apiRequest("/loans/issue", {
      method: "POST",
      body: {
        memberId,
        copyId,
        issueDate: issueDate ? new Date(issueDate).toISOString() : null,
        durationDays,
        notes: notes || null
      }
    });
    showToast("Book issued successfully.");
    document.getElementById("issue-notes").value = "";
    document.getElementById("member-eligibility-hint").textContent = "";
    await Promise.all([loadLookups(), loadActiveLoans()]);
  } catch (err) {
    errBox.textContent = err.message;
    errBox.classList.remove("d-none");
  } finally {
    btn.disabled = false;
  }
}

async function loadActiveLoans() {
  const tbody = document.getElementById("active-loans-tbody");
  tbody.innerHTML = `<tr><td colspan="6" class="loading-state">Loading active loans...</td></tr>`;
  try {
    const { data } = await apiRequest("/loans?loanStatus=Active&page=1&pageSize=12");
    const items = data.items || [];
    tbody.innerHTML = !items.length
      ? `<tr><td colspan="6" class="empty-state">No active loans.</td></tr>`
      : items.map(l => `
        <tr>
          <td class="fw-medium">#${l.loanId}</td>
          <td><code>${escapeHtml(l.accessionNumber)}</code></td>
          <td class="fw-medium">${escapeHtml(l.bookTitle)}</td>
          <td>${escapeHtml(l.memberName)}</td>
          <td class="text-secondary text-nowrap">${formatDate(l.dueDate)}</td>
          <td>${loanStatusBadge(l)}</td>
        </tr>
      `).join("");
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="6" class="empty-state text-danger">${escapeHtml(err.message)}</td></tr>`;
  }
}
