import { apiRequest, debounce, escapeHtml, formatDate, formatFine, formatNumber, showToast } from "../js/api.js";
import { initLayout } from "../js/layout.js";

initLayout({ activePage: "overdue", pageTitle: "Overdue Books", isSubPage: true });

let search = "";
let dailyFineRate = 5.0;

init();

async function init() {
  document.getElementById("overdue-search").addEventListener("input", debounce(e => {
    search = e.target.value;
    loadOverdue();
  }, 250));

  try {
    const { data } = await apiRequest("/dashboard/policy");
    dailyFineRate = Number(data.dailyFineRate || 5);
  } catch { /* default */ }

  await loadOverdue();
}

async function loadOverdue() {
  const tbody = document.getElementById("overdue-tbody");
  tbody.innerHTML = `<tr><td colspan="9" class="loading-state">Loading overdue books...</td></tr>`;

  const params = new URLSearchParams({ overdueOnly: "true", page: "1", pageSize: "100" });
  if (search.trim()) params.set("search", search.trim());

  try {
    const { data } = await apiRequest(`/loans?${params.toString()}`);
    const items = data.items || [];

    const totalDays = items.reduce((acc, l) => acc + l.overdueDays, 0);
    const totalAccrued = items.reduce((acc, l) => acc + l.overdueDays * dailyFineRate, 0);

    document.getElementById("overdue-summary").innerHTML = `
      <div class="kpi-tile">
        <div class="kpi-label">Overdue Loans</div>
        <div class="kpi-value tabular-nums ${items.length > 0 ? "text-danger" : ""}">${formatNumber(items.length)}</div>
      </div>
      <div class="kpi-tile">
        <div class="kpi-label">Combined Overdue Days</div>
        <div class="kpi-value tabular-nums">${formatNumber(totalDays)}</div>
      </div>
      <div class="kpi-tile">
        <div class="kpi-label">Estimated Fines (${formatFine(dailyFineRate)}/day)</div>
        <div class="kpi-value tabular-nums text-danger">${formatFine(totalAccrued)}</div>
      </div>
    `;

    if (!items.length) {
      tbody.innerHTML = `<tr><td colspan="9" class="empty-state"><i class="bi bi-check-circle"></i>No overdue books right now.</td></tr>`;
      return;
    }

    tbody.innerHTML = items.map(l => {
      const accrued = l.overdueDays * dailyFineRate;
      return `
        <tr>
          <td class="fw-medium">#${l.loanId}</td>
          <td class="fw-medium">${escapeHtml(l.bookTitle)}</td>
          <td><code>${escapeHtml(l.accessionNumber)}</code></td>
          <td>${escapeHtml(l.memberName)} <span class="text-secondary small">(${escapeHtml(l.membershipNumber)})</span></td>
          <td class="text-secondary text-nowrap">${formatDate(l.issueDate)}</td>
          <td class="text-danger text-nowrap fw-medium">${formatDate(l.dueDate)}</td>
          <td class="text-end tabular-nums text-danger fw-semibold">${l.overdueDays}d</td>
          <td class="text-end tabular-nums fw-semibold">${formatFine(accrued)}</td>
          <td class="text-end">
            <button type="button" class="btn btn-primary btn-sm quick-return-btn" data-id="${l.loanId}">
              Return Now
            </button>
          </td>
        </tr>
      `;
    }).join("");

    tbody.querySelectorAll(".quick-return-btn").forEach(btn => {
      btn.addEventListener("click", async () => {
        const id = Number(btn.dataset.id);
        if (!confirm(`Process return for Loan #${id} today and record its overdue fine?`)) return;
        try {
          const { data: returned } = await apiRequest("/loans/return", {
            method: "POST",
            body: { loanId: id, returnCondition: "Available" }
          });
          showToast(`Loan #${id} returned. Fine recorded: ${formatFine(returned.fineAmount)}.`);
          await loadOverdue();
        } catch (err) {
          showToast(err.message, "error");
        }
      });
    });
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="9" class="empty-state text-danger">${escapeHtml(err.message)}</td></tr>`;
  }
}
