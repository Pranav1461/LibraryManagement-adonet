import { apiRequest, escapeHtml, formatDate, formatFine, loanStatusBadge, showToast } from "../js/api.js";
import { initLayout } from "../js/layout.js";

initLayout({ activePage: "return", pageTitle: "Return Book", isSubPage: true });

let activeLoans = [];
let dailyFineRate = 5.0;

init();

async function init() {
  document.getElementById("return-date").value = new Date().toISOString().slice(0, 10);
  document.getElementById("return-loan-select").addEventListener("change", () => {
    const id = Number(document.getElementById("return-loan-select").value);
    const loan = activeLoans.find(l => l.loanId === id);
    if (loan) document.getElementById("return-accession").value = loan.accessionNumber;
    updateFinePreview();
  });
  document.getElementById("return-accession").addEventListener("input", updateFinePreview);
  document.getElementById("return-date").addEventListener("change", updateFinePreview);
  document.getElementById("return-form").addEventListener("submit", handleReturn);

  await loadPolicy();
  await loadActiveLoans();
}

async function loadPolicy() {
  try {
    const { data } = await apiRequest("/dashboard/policy");
    dailyFineRate = Number(data.dailyFineRate || 5);
    document.getElementById("fine-rate-label").textContent = `${formatFine(dailyFineRate)} / overdue day`;
  } catch { /* fallback */ }
}

async function loadActiveLoans() {
  const tbody = document.getElementById("return-loans-tbody");
  tbody.innerHTML = `<tr><td colspan="7" class="loading-state">Loading active loans...</td></tr>`;
  try {
    const { data } = await apiRequest("/loans?loanStatus=Active&page=1&pageSize=100");
    activeLoans = data.items || [];

    const select = document.getElementById("return-loan-select");
    select.innerHTML = `<option value="">Choose an active loan...</option>` +
      activeLoans.map(l => `<option value="${l.loanId}">#${l.loanId} — ${escapeHtml(l.accessionNumber)} (${escapeHtml(l.bookTitle)}) — ${escapeHtml(l.memberName)}</option>`).join("");

    if (!activeLoans.length) {
      tbody.innerHTML = `<tr><td colspan="7" class="empty-state">All books are currently returned.</td></tr>`;
      return;
    }

    tbody.innerHTML = activeLoans.map(l => `
      <tr>
        <td class="fw-medium">#${l.loanId}</td>
        <td><code>${escapeHtml(l.accessionNumber)}</code></td>
        <td class="fw-medium">${escapeHtml(l.bookTitle)}</td>
        <td>${escapeHtml(l.memberName)}</td>
        <td class="text-secondary text-nowrap">${formatDate(l.dueDate)}</td>
        <td>${loanStatusBadge(l)}</td>
        <td class="text-end">
          <button type="button" class="btn btn-outline-secondary btn-sm select-return-btn" data-id="${l.loanId}">Select</button>
        </td>
      </tr>
    `).join("");

    tbody.querySelectorAll(".select-return-btn").forEach(btn => {
      btn.addEventListener("click", () => {
        const id = Number(btn.dataset.id);
        select.value = String(id);
        const loan = activeLoans.find(x => x.loanId === id);
        if (loan) document.getElementById("return-accession").value = loan.accessionNumber;
        updateFinePreview();
      });
    });
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="7" class="empty-state text-danger">${escapeHtml(err.message)}</td></tr>`;
  }
}

function updateFinePreview() {
  const box = document.getElementById("fine-preview-box");
  const loanId = Number(document.getElementById("return-loan-select").value);
  const acc = document.getElementById("return-accession").value.trim().toUpperCase();
  const loan = activeLoans.find(l => l.loanId === loanId || (acc && l.accessionNumber.toUpperCase() === acc));

  if (!loan) {
    box.textContent = "Select a loan to preview overdue days and calculated fine.";
    return;
  }

  const returnDateStr = document.getElementById("return-date").value;
  const ret = returnDateStr ? new Date(returnDateStr + "T00:00:00") : new Date();
  const due = new Date(loan.dueDate.slice(0, 10) + "T00:00:00");
  const diffDays = Math.max(0, Math.round((ret - due) / (1000 * 60 * 60 * 24)));
  const fine = diffDays * dailyFineRate;

  box.innerHTML = `
    <div><strong>Book:</strong> ${escapeHtml(loan.bookTitle)} (<code>${escapeHtml(loan.accessionNumber)}</code>)</div>
    <div><strong>Due Date:</strong> ${formatDate(loan.dueDate)} | <strong>Overdue Days:</strong> <span class="${diffDays > 0 ? "text-danger fw-semibold" : "text-success"}">${diffDays}</span></div>
    <div><strong>Estimated Fine:</strong> <span class="tabular-nums fw-semibold ${fine > 0 ? "text-danger" : "text-success"}">${formatFine(fine)}</span></div>
  `;
}

async function handleReturn(e) {
  e.preventDefault();
  const errBox = document.getElementById("return-error");
  errBox.classList.add("d-none");

  const loanIdVal = document.getElementById("return-loan-select").value;
  const accessionNumber = document.getElementById("return-accession").value.trim();
  const returnDate = document.getElementById("return-date").value;
  const returnCondition = document.getElementById("return-condition").value;
  const notes = document.getElementById("return-notes").value.trim();

  if (!loanIdVal && !accessionNumber) {
    errBox.textContent = "Select an active loan or enter an accession number.";
    errBox.classList.remove("d-none");
    return;
  }

  const btn = document.getElementById("submit-return-btn");
  btn.disabled = true;

  try {
    const { data } = await apiRequest("/loans/return", {
      method: "POST",
      body: {
        loanId: loanIdVal ? Number(loanIdVal) : null,
        accessionNumber: loanIdVal ? null : accessionNumber,
        returnDate: returnDate ? new Date(returnDate).toISOString() : null,
        returnCondition,
        notes: notes || null
      }
    });
    showToast(`Book returned. Fine recorded: ${formatFine(data.fineAmount)}.`);
    document.getElementById("return-loan-select").value = "";
    document.getElementById("return-accession").value = "";
    document.getElementById("return-notes").value = "";
    updateFinePreview();
    await loadActiveLoans();
  } catch (err) {
    errBox.textContent = err.message;
    errBox.classList.remove("d-none");
  } finally {
    btn.disabled = false;
  }
}
