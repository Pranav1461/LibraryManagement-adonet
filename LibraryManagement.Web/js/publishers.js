import { apiRequest, debounce, escapeHtml, showToast } from "../js/api.js";
import { initLayout, updateCacheBadge } from "../js/layout.js";

initLayout({ activePage: "publishers", pageTitle: "Publishers", isSubPage: true });

let publishers = [];
let search = "";

const pubModal = new bootstrap.Modal(document.getElementById("pubModal"));
const booksModal = new bootstrap.Modal(document.getElementById("pubBooksModal"));

document.getElementById("pub-search").addEventListener("input", debounce(e => {
  search = e.target.value;
  loadPublishers();
}, 250));
document.getElementById("add-pub-btn").addEventListener("click", openCreate);
document.getElementById("pub-form").addEventListener("submit", handleSave);

loadPublishers();

async function loadPublishers() {
  const tbody = document.getElementById("pub-tbody");
  tbody.innerHTML = `<tr><td colspan="6" class="loading-state">Loading publishers...</td></tr>`;
  const qs = search.trim() ? `?search=${encodeURIComponent(search.trim())}` : "";

  try {
    const { data, cacheStatus } = await apiRequest(`/publishers${qs}`);
    updateCacheBadge(cacheStatus);
    publishers = data || [];
    if (!publishers.length) {
      tbody.innerHTML = `<tr><td colspan="6" class="empty-state"><i class="bi bi-building"></i>No publishers found.</td></tr>`;
      return;
    }

    tbody.innerHTML = publishers.map(p => `
      <tr>
        <td class="fw-medium">${escapeHtml(p.publisherName)}</td>
        <td class="text-secondary">${escapeHtml(p.contactEmail || "—")}</td>
        <td class="text-nowrap">${escapeHtml(p.phone || "—")}</td>
        <td class="text-secondary">${escapeHtml(p.address || "—")}</td>
        <td class="text-end tabular-nums">${p.bookCount}</td>
        <td class="text-end text-nowrap">
          <button type="button" class="btn btn-outline-secondary btn-sm" data-action="books" data-id="${p.publisherId}" title="View Books">
            <i class="bi bi-book"></i>
          </button>
          <button type="button" class="btn btn-outline-secondary btn-sm" data-action="edit" data-id="${p.publisherId}" title="Edit">
            <i class="bi bi-pencil"></i>
          </button>
          <button type="button" class="btn btn-outline-danger btn-sm" data-action="delete" data-id="${p.publisherId}" title="Delete">
            <i class="bi bi-trash"></i>
          </button>
        </td>
      </tr>
    `).join("");

    tbody.querySelectorAll("button[data-action]").forEach(btn => {
      btn.addEventListener("click", () => {
        const id = Number(btn.dataset.id);
        if (btn.dataset.action === "books") viewBooks(id);
        else if (btn.dataset.action === "edit") openEdit(id);
        else handleDelete(id);
      });
    });
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="6" class="empty-state text-danger">${escapeHtml(err.message)}</td></tr>`;
  }
}

async function viewBooks(id) {
  try {
    const { data } = await apiRequest(`/publishers/${id}/books`);
    document.getElementById("pubBooksTitle").textContent = `Books by ${data.publisher.publisherName}`;
    const books = data.books || [];
    document.getElementById("pub-books-body").innerHTML = !books.length
      ? `<div class="empty-state">No books assigned to this publisher yet.</div>`
      : `<div class="table-responsive border rounded">
          <table class="data-table">
            <thead><tr><th>Title</th><th>ISBN</th><th>Category</th><th class="text-end">Year</th><th class="text-end">Available</th></tr></thead>
            <tbody>
              ${books.map(b => `
                <tr>
                  <td class="fw-medium">${escapeHtml(b.title)}</td>
                  <td><code>${escapeHtml(b.isbn)}</code></td>
                  <td>${escapeHtml(b.categoryName)}</td>
                  <td class="text-end tabular-nums">${b.publicationYear}</td>
                  <td class="text-end tabular-nums">${b.availableCopies} / ${b.totalCopies}</td>
                </tr>
              `).join("")}
            </tbody>
          </table>
        </div>`;
    booksModal.show();
  } catch (err) {
    showToast(err.message, "error");
  }
}

function openCreate() {
  document.getElementById("pubModalTitle").textContent = "Add Publisher";
  document.getElementById("pub-form").reset();
  document.getElementById("pub-id").value = "";
  document.getElementById("pub-form-error").classList.add("d-none");
  pubModal.show();
}

function openEdit(id) {
  const p = publishers.find(x => x.publisherId === id);
  if (!p) return;
  document.getElementById("pubModalTitle").textContent = "Edit Publisher";
  document.getElementById("pub-id").value = p.publisherId;
  document.getElementById("pub-name").value = p.publisherName;
  document.getElementById("pub-email").value = p.contactEmail || "";
  document.getElementById("pub-phone").value = p.phone || "";
  document.getElementById("pub-address").value = p.address || "";
  document.getElementById("pub-form-error").classList.add("d-none");
  pubModal.show();
}

async function handleSave(e) {
  e.preventDefault();
  const errorBox = document.getElementById("pub-form-error");
  errorBox.classList.add("d-none");

  const id = document.getElementById("pub-id").value;
  const publisherName = document.getElementById("pub-name").value.trim();
  const contactEmail = document.getElementById("pub-email").value.trim();
  const phone = document.getElementById("pub-phone").value.trim();
  const address = document.getElementById("pub-address").value.trim();

  if (publisherName.length < 2) {
    errorBox.textContent = "Publisher name must be at least 2 characters.";
    errorBox.classList.remove("d-none");
    return;
  }

  const payload = {
    publisherName,
    contactEmail: contactEmail || null,
    phone: phone || null,
    address: address || null
  };

  try {
    if (id) {
      await apiRequest(`/publishers/${id}`, { method: "PUT", body: payload });
      showToast("Publisher updated.");
    } else {
      await apiRequest("/publishers", { method: "POST", body: payload });
      showToast("Publisher added.");
    }
    pubModal.hide();
    await loadPublishers();
  } catch (err) {
    errorBox.textContent = err.message;
    errorBox.classList.remove("d-none");
  }
}

async function handleDelete(id) {
  const p = publishers.find(x => x.publisherId === id);
  if (!confirm(`Delete publisher "${p?.publisherName || id}"? Publishers referenced by books cannot be deleted.`)) return;
  try {
    await apiRequest(`/publishers/${id}`, { method: "DELETE" });
    showToast("Publisher deleted.");
    await loadPublishers();
  } catch (err) {
    showToast(err.message, "error");
  }
}
