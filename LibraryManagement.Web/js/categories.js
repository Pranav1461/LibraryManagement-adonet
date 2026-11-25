import { apiRequest, debounce, escapeHtml, formatDate, showToast } from "../js/api.js";
import { initLayout, updateCacheBadge } from "../js/layout.js";

initLayout({ activePage: "categories", pageTitle: "Categories", isSubPage: true });

let categories = [];
let search = "";

document.getElementById("cat-search").addEventListener("input", debounce(e => {
  search = e.target.value;
  loadCategories();
}, 250));
document.getElementById("cat-form").addEventListener("submit", handleSave);
document.getElementById("cat-cancel-btn").addEventListener("click", resetForm);

loadCategories();

async function loadCategories() {
  const tbody = document.getElementById("cat-tbody");
  tbody.innerHTML = `<tr><td colspan="5" class="loading-state">Loading categories...</td></tr>`;
  const qs = search.trim() ? `?search=${encodeURIComponent(search.trim())}` : "";

  try {
    const { data, cacheStatus } = await apiRequest(`/categories${qs}`);
    updateCacheBadge(cacheStatus);
    categories = data || [];
    if (!categories.length) {
      tbody.innerHTML = `<tr><td colspan="5" class="empty-state"><i class="bi bi-tags"></i>No categories found.</td></tr>`;
      return;
    }

    tbody.innerHTML = categories.map(c => `
      <tr>
        <td class="fw-medium">${escapeHtml(c.categoryName)}</td>
        <td class="text-secondary">${escapeHtml(c.description || "—")}</td>
        <td class="text-end tabular-nums">${c.bookCount}</td>
        <td class="text-secondary text-nowrap">${formatDate(c.createdAt)}</td>
        <td class="text-end text-nowrap">
          <button type="button" class="btn btn-outline-secondary btn-sm" data-action="edit" data-id="${c.categoryId}">
            <i class="bi bi-pencil"></i>
          </button>
          <button type="button" class="btn btn-outline-danger btn-sm" data-action="delete" data-id="${c.categoryId}">
            <i class="bi bi-trash"></i>
          </button>
        </td>
      </tr>
    `).join("");

    tbody.querySelectorAll("button[data-action]").forEach(btn => {
      btn.addEventListener("click", () => {
        const id = Number(btn.dataset.id);
        if (btn.dataset.action === "edit") startEdit(id);
        else handleDelete(id);
      });
    });
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="5" class="empty-state text-danger">${escapeHtml(err.message)}</td></tr>`;
  }
}

function startEdit(id) {
  const c = categories.find(x => x.categoryId === id);
  if (!c) return;
  document.getElementById("cat-id").value = c.categoryId;
  document.getElementById("cat-name").value = c.categoryName;
  document.getElementById("cat-desc").value = c.description || "";
  document.getElementById("cat-form-title").textContent = `Edit Category #${c.categoryId}`;
  document.getElementById("cat-cancel-btn").classList.remove("d-none");
  document.getElementById("cat-form-error").classList.add("d-none");
}

function resetForm() {
  document.getElementById("cat-form").reset();
  document.getElementById("cat-id").value = "";
  document.getElementById("cat-form-title").textContent = "New Category";
  document.getElementById("cat-cancel-btn").classList.add("d-none");
  document.getElementById("cat-form-error").classList.add("d-none");
}

async function handleSave(e) {
  e.preventDefault();
  const errorBox = document.getElementById("cat-form-error");
  errorBox.classList.add("d-none");

  const id = document.getElementById("cat-id").value;
  const categoryName = document.getElementById("cat-name").value.trim();
  const description = document.getElementById("cat-desc").value.trim();

  if (categoryName.length < 2) {
    errorBox.textContent = "Category name must be at least 2 characters.";
    errorBox.classList.remove("d-none");
    return;
  }

  try {
    if (id) {
      await apiRequest(`/categories/${id}`, { method: "PUT", body: { categoryName, description: description || null } });
      showToast("Category updated.");
    } else {
      await apiRequest("/categories", { method: "POST", body: { categoryName, description: description || null } });
      showToast("Category created.");
    }
    resetForm();
    await loadCategories();
  } catch (err) {
    errorBox.textContent = err.message;
    errorBox.classList.remove("d-none");
  }
}

async function handleDelete(id) {
  const c = categories.find(x => x.categoryId === id);
  if (!confirm(`Delete category "${c?.categoryName || id}"? Categories assigned to books cannot be deleted.`)) return;
  try {
    await apiRequest(`/categories/${id}`, { method: "DELETE" });
    showToast("Category deleted.");
    if (Number(document.getElementById("cat-id").value) === id) resetForm();
    await loadCategories();
  } catch (err) {
    showToast(err.message, "error");
  }
}
