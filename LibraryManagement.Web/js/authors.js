import { apiRequest, debounce, escapeHtml, showToast } from "../js/api.js";
import { initLayout, updateCacheBadge } from "../js/layout.js";

initLayout({ activePage: "authors", pageTitle: "Authors", isSubPage: true });

let authors = [];
let search = "";
const booksModal = new bootstrap.Modal(document.getElementById("authorBooksModal"));

document.getElementById("author-search").addEventListener("input", debounce(e => {
  search = e.target.value;
  loadAuthors();
}, 250));

document.getElementById("author-form").addEventListener("submit", handleSave);
document.getElementById("author-cancel-btn").addEventListener("click", resetForm);

loadAuthors();

async function loadAuthors() {
  const tbody = document.getElementById("authors-tbody");
  tbody.innerHTML = `<tr><td colspan="4" class="loading-state">Loading authors...</td></tr>`;
  const qs = search.trim() ? `?search=${encodeURIComponent(search.trim())}` : "";

  try {
    const { data, cacheStatus } = await apiRequest(`/authors${qs}`);
    updateCacheBadge(cacheStatus);
    authors = data || [];
    if (!authors.length) {
      tbody.innerHTML = `<tr><td colspan="4" class="empty-state"><i class="bi bi-person-lines-fill"></i>No authors found.</td></tr>`;
      return;
    }

    tbody.innerHTML = authors.map(a => `
      <tr>
        <td class="fw-medium">${escapeHtml(a.authorName)}</td>
        <td class="text-secondary">${escapeHtml(a.biography || "—")}</td>
        <td class="text-end tabular-nums">${a.bookCount}</td>
        <td class="text-end text-nowrap">
          <button type="button" class="btn btn-outline-secondary btn-sm" data-action="books" data-id="${a.authorId}" title="View Books">
            <i class="bi bi-book"></i>
          </button>
          <button type="button" class="btn btn-outline-secondary btn-sm" data-action="edit" data-id="${a.authorId}" title="Edit">
            <i class="bi bi-pencil"></i>
          </button>
          <button type="button" class="btn btn-outline-danger btn-sm" data-action="delete" data-id="${a.authorId}" title="Delete">
            <i class="bi bi-trash"></i>
          </button>
        </td>
      </tr>
    `).join("");

    tbody.querySelectorAll("button[data-action]").forEach(btn => {
      btn.addEventListener("click", () => {
        const id = Number(btn.dataset.id);
        const action = btn.dataset.action;
        if (action === "books") viewAuthorBooks(id);
        else if (action === "edit") startEdit(id);
        else handleDelete(id);
      });
    });
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="4" class="empty-state text-danger">${escapeHtml(err.message)}</td></tr>`;
  }
}

async function viewAuthorBooks(id) {
  try {
    const { data } = await apiRequest(`/authors/${id}/books`);
    document.getElementById("authorBooksTitle").textContent = `Books by ${data.author.authorName}`;
    const books = data.books || [];
    document.getElementById("author-books-body").innerHTML = !books.length
      ? `<div class="empty-state">No books linked to this author yet.</div>`
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

function startEdit(id) {
  const a = authors.find(x => x.authorId === id);
  if (!a) return;
  document.getElementById("author-id").value = a.authorId;
  document.getElementById("author-name").value = a.authorName;
  document.getElementById("author-bio").value = a.biography || "";
  document.getElementById("author-form-title").textContent = `Edit Author #${a.authorId}`;
  document.getElementById("author-cancel-btn").classList.remove("d-none");
  document.getElementById("author-form-error").classList.add("d-none");
}

function resetForm() {
  document.getElementById("author-form").reset();
  document.getElementById("author-id").value = "";
  document.getElementById("author-form-title").textContent = "New Author";
  document.getElementById("author-cancel-btn").classList.add("d-none");
  document.getElementById("author-form-error").classList.add("d-none");
}

async function handleSave(e) {
  e.preventDefault();
  const errorBox = document.getElementById("author-form-error");
  errorBox.classList.add("d-none");

  const id = document.getElementById("author-id").value;
  const authorName = document.getElementById("author-name").value.trim();
  const biography = document.getElementById("author-bio").value.trim();

  if (authorName.length < 2) {
    errorBox.textContent = "Author name must be at least 2 characters.";
    errorBox.classList.remove("d-none");
    return;
  }

  try {
    if (id) {
      await apiRequest(`/authors/${id}`, { method: "PUT", body: { authorName, biography: biography || null } });
      showToast("Author updated.");
    } else {
      await apiRequest("/authors", { method: "POST", body: { authorName, biography: biography || null } });
      showToast("Author added.");
    }
    resetForm();
    await loadAuthors();
  } catch (err) {
    errorBox.textContent = err.message;
    errorBox.classList.remove("d-none");
  }
}

async function handleDelete(id) {
  const a = authors.find(x => x.authorId === id);
  if (!confirm(`Delete author "${a?.authorName || id}"? Authors linked to books cannot be deleted.`)) return;
  try {
    await apiRequest(`/authors/${id}`, { method: "DELETE" });
    showToast("Author deleted.");
    if (Number(document.getElementById("author-id").value) === id) resetForm();
    await loadAuthors();
  } catch (err) {
    showToast(err.message, "error");
  }
}
