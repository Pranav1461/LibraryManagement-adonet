import { apiRequest, escapeHtml, formatDate, formatFine, formatNumber, loanStatusBadge } from "./api.js";
import { initLayout, updateCacheBadge } from "./layout.js";

let activityChart = null;
let mostBorrowedChart = null;
let categoryChart = null;
let lastDashboardData = null;

initLayout({ activePage: "dashboard", pageTitle: "Dashboard", isSubPage: false });
document.getElementById("refresh-dashboard-btn").addEventListener("click", loadDashboard);
window.addEventListener("lms-theme-change", () => {
  if (lastDashboardData) renderCharts(lastDashboardData);
});

loadDashboard();

async function loadDashboard() {
  const errBox = document.getElementById("dashboard-error");
  errBox.classList.add("d-none");

  try {
    const { data, cacheStatus } = await apiRequest("/dashboard");
    lastDashboardData = data;
    updateCacheBadge(cacheStatus);
    renderKpis(data.summary);
    renderCharts(data);
    renderTables(data);
  } catch (err) {
    errBox.textContent = err.message;
    errBox.classList.remove("d-none");
    document.getElementById("kpi-grid").innerHTML = `<div class="empty-state surface-panel">Unable to load dashboard.</div>`;
  }
}

function renderKpis(s) {
  const items = [
    { label: "Book Titles", value: formatNumber(s.totalBooks), meta: "Active catalog titles" },
    { label: "Physical Copies", value: formatNumber(s.totalCopies), meta: `${formatNumber(s.availableCopies)} available` },
    { label: "Available Copies", value: formatNumber(s.availableCopies), meta: "Ready for checkout", cls: "text-success" },
    { label: "Borrowed Copies", value: formatNumber(s.borrowedCopies), meta: "Currently checked out" },
    { label: "Registered Members", value: formatNumber(s.totalMembers), meta: `${formatNumber(s.activeMembers)} active` },
    { label: "Overdue Loans", value: formatNumber(s.overdueLoans), meta: "Past due date", cls: s.overdueLoans > 0 ? "text-danger" : "" },
    { label: "Due Today", value: formatNumber(s.booksDueToday), meta: "Scheduled return today", cls: s.booksDueToday > 0 ? "text-warning" : "" },
    { label: "Total Borrowings", value: formatNumber(s.totalBorrowings), meta: `${formatNumber(s.totalReturns)} returned` }
  ];

  document.getElementById("kpi-grid").innerHTML = items.map(m => `
    <div class="kpi-tile">
      <div class="kpi-label">${escapeHtml(m.label)}</div>
      <div class="kpi-value tabular-nums ${m.cls || ""}">${escapeHtml(m.value)}</div>
      <div class="kpi-meta">${escapeHtml(m.meta)}</div>
    </div>
  `).join("");
}

function renderCharts(data) {
  const isDark = document.documentElement.getAttribute("data-bs-theme") === "dark";
  const textColor = isDark ? "#9CA3AF" : "#6B7280";
  const gridColor = isDark ? "#253042" : "#F3F4F6";
  const borderColor = isDark ? "#2A3446" : "#E5E7EB";
  const surfaceColor = isDark ? "#171E2B" : "#FFFFFF";

  Chart.defaults.font.family = '"Inter", -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
  Chart.defaults.font.size = 12;
  Chart.defaults.color = textColor;

  // Merge monthly labels chronologically
  const monthSet = new Set([
    ...data.monthlyBorrowing.map(m => m.monthLabel),
    ...data.monthlyReturns.map(m => m.monthLabel)
  ]);
  const months = Array.from(monthSet).sort();
  const borrowMap = new Map(data.monthlyBorrowing.map(m => [m.monthLabel, m.count]));
  const returnMap = new Map(data.monthlyReturns.map(m => [m.monthLabel, m.count]));

  if (activityChart) activityChart.destroy();
  activityChart = new Chart(document.getElementById("monthlyActivityChart"), {
    type: "bar",
    data: {
      labels: months,
      datasets: [
        {
          label: "Borrowed",
          data: months.map(m => borrowMap.get(m) || 0),
          backgroundColor: "#2563EB",
          borderRadius: 4,
          maxBarThickness: 22
        },
        {
          label: "Returned",
          data: months.map(m => returnMap.get(m) || 0),
          backgroundColor: "#0F766E",
          borderRadius: 4,
          maxBarThickness: 22
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      animation: false,
      plugins: { legend: { position: "top", align: "end" } },
      scales: {
        x: { grid: { display: false }, border: { color: borderColor } },
        y: { beginAtZero: true, ticks: { precision: 0 }, grid: { color: gridColor }, border: { display: false } }
      }
    }
  });

  if (mostBorrowedChart) mostBorrowedChart.destroy();
  mostBorrowedChart = new Chart(document.getElementById("mostBorrowedChart"), {
    type: "bar",
    data: {
      labels: data.mostBorrowedBooks.map(b => b.title.length > 26 ? `${b.title.slice(0, 26)}...` : b.title),
      datasets: [{
        label: "Loans",
        data: data.mostBorrowedBooks.map(b => b.loanCount),
        backgroundColor: "#2563EB",
        borderRadius: 4,
        maxBarThickness: 20
      }]
    },
    options: {
      indexAxis: "y",
      responsive: true,
      maintainAspectRatio: false,
      animation: false,
      plugins: { legend: { display: false } },
      scales: {
        x: { beginAtZero: true, ticks: { precision: 0 }, grid: { color: gridColor }, border: { display: false } },
        y: { grid: { display: false }, border: { color: borderColor } }
      }
    }
  });

  if (categoryChart) categoryChart.destroy();
  categoryChart = new Chart(document.getElementById("booksByCategoryChart"), {
    type: "doughnut",
    data: {
      labels: data.booksByCategory.map(c => c.categoryName),
      datasets: [{
        data: data.booksByCategory.map(c => c.copyCount),
        backgroundColor: ["#2563EB", "#0F766E", "#475569", "#0284C7", "#16A34A", "#D97706", "#64748B", "#94A3B8"],
        borderWidth: 2,
        borderColor: surfaceColor
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      animation: false,
      cutout: "60%",
      plugins: {
        legend: { position: "right", labels: { boxWidth: 10, padding: 8, usePointStyle: true } }
      }
    }
  });
}

function renderTables(data) {
  const overdueTbody = document.getElementById("overdue-alerts-tbody");
  overdueTbody.innerHTML = !data.overdueAlerts.length
    ? `<tr><td colspan="5" class="empty-state">No overdue loans.</td></tr>`
    : data.overdueAlerts.map(l => `
      <tr>
        <td class="fw-medium">${escapeHtml(l.bookTitle)}</td>
        <td>${escapeHtml(l.memberName)}</td>
        <td><code>${escapeHtml(l.accessionNumber)}</code></td>
        <td class="text-secondary text-nowrap">${formatDate(l.dueDate)}</td>
        <td class="text-end tabular-nums text-danger fw-semibold">${l.overdueDays}d</td>
      </tr>
    `).join("");

  const dueSoonTbody = document.getElementById("due-soon-tbody");
  dueSoonTbody.innerHTML = !data.dueSoonLoans.length
    ? `<tr><td colspan="4" class="empty-state">No books due in the next 3 days.</td></tr>`
    : data.dueSoonLoans.map(l => `
      <tr>
        <td class="fw-medium">${escapeHtml(l.bookTitle)}</td>
        <td>${escapeHtml(l.memberName)}</td>
        <td><code>${escapeHtml(l.accessionNumber)}</code></td>
        <td class="text-warning fw-medium text-nowrap">${formatDate(l.dueDate)}</td>
      </tr>
    `).join("");

  const returnsTbody = document.getElementById("recent-returns-tbody");
  returnsTbody.innerHTML = !data.recentReturns.length
    ? `<tr><td colspan="4" class="empty-state">No recent returns.</td></tr>`
    : data.recentReturns.map(l => `
      <tr>
        <td class="fw-medium">${escapeHtml(l.bookTitle)}</td>
        <td>${escapeHtml(l.memberName)}</td>
        <td class="text-secondary text-nowrap">${formatDate(l.returnDate)}</td>
        <td class="text-end tabular-nums">${formatFine(l.fineAmount)}</td>
      </tr>
    `).join("");

  const recentTbody = document.getElementById("recent-borrowings-tbody");
  recentTbody.innerHTML = !data.recentBorrowings.length
    ? `<tr><td colspan="7" class="empty-state">No borrowing activity yet.</td></tr>`
    : data.recentBorrowings.map(l => `
      <tr>
        <td class="fw-medium">#${l.loanId}</td>
        <td class="fw-medium">${escapeHtml(l.bookTitle)}</td>
        <td><code>${escapeHtml(l.accessionNumber)}</code></td>
        <td>${escapeHtml(l.memberName)} <span class="text-secondary small">(${escapeHtml(l.membershipNumber)})</span></td>
        <td class="text-secondary text-nowrap">${formatDate(l.issueDate)}</td>
        <td class="text-secondary text-nowrap">${formatDate(l.dueDate)}</td>
        <td>${loanStatusBadge(l)}</td>
      </tr>
    `).join("");
}
