document.addEventListener("change", (event) => {
  const select = event.target;
  if (!(select instanceof HTMLSelectElement) || !select.dataset.enables) {
    return;
  }

  const target = document.getElementById(select.dataset.enables);
  if (target) {
    target.disabled = select.value !== select.dataset.enablesWhen;
  }
});

document.addEventListener("click", async (event) => {
  const button = event.target.closest("button[data-copy]");
  const source = button && document.getElementById(button.dataset.copy);
  if (!source) {
    return;
  }

  try {
    await navigator.clipboard.writeText(source.value);
    button.textContent = "Copied";
  } catch {
    source.select();
  }
});

document.addEventListener("click", (event) => {
  for (const menu of document.querySelectorAll("details[data-menu][open]")) {
    if (!menu.contains(event.target)) {
      menu.open = false;
    }
  }
});

document.addEventListener("submit", (event) => {
  const message = event.target.dataset?.confirm;
  if (message && !confirm(message)) {
    event.preventDefault();
  }
});

document.addEventListener("submit", async (event) => {
  const form = event.target;
  if (!(form instanceof HTMLFormElement) || !form.hasAttribute("data-upload")) {
    return;
  }

  event.preventDefault();
  const file = form.elements.file.files[0];
  const status = form.querySelector("[data-upload-status]");
  const button = form.querySelector("button[type=submit]");
  if (!file) {
    return;
  }

  if (file.size > Number(form.dataset.maxSize)) {
    status.textContent = "File is too large.";
    return;
  }

  button.disabled = true;
  status.textContent = `Uploading ${file.name}…`;

  try {
    const antiforgery = form.elements.__RequestVerificationToken.value;
    const contentType = file.type || "application/octet-stream";
    const customExpiresAt = form.elements.CustomExpiresAt;

    const started = await postForm(form.action, antiforgery, {
      FileName: file.name,
      ContentType: contentType,
      Size: file.size,
      Lifetime: form.elements.Lifetime.value,
      TimeZone: form.elements.TimeZone.value,
      ...(customExpiresAt.disabled || !customExpiresAt.value
        ? {}
        : { CustomExpiresAt: customExpiresAt.value }),
    });
    const { id, uploadUrl } = await started.json();

    const uploaded = await fetch(uploadUrl, {
      method: "PUT",
      headers: { "x-ms-blob-type": "BlockBlob", "content-type": contentType },
      body: file,
    });
    if (!uploaded.ok) {
      throw new Error(`Upload failed with ${uploaded.status}`);
    }

    await postForm(`${form.action}/${id}/complete`, antiforgery, {});
    location.reload();
  } catch (error) {
    status.textContent =
      error.status === 409
        ? "Not enough storage space left for this file."
        : "Upload failed. Check the expiration date and try again.";
    button.disabled = false;
  }
});

async function postForm(url, antiforgery, fields) {
  const body = new FormData();
  body.append("__RequestVerificationToken", antiforgery);
  for (const [name, value] of Object.entries(fields)) {
    body.append(name, value);
  }

  const response = await fetch(url, {
    method: "POST",
    body,
    redirect: "error",
  });
  if (!response.ok) {
    throw Object.assign(new Error(`Request failed with ${response.status}`), {
      status: response.status,
    });
  }

  return response;
}

const localTimeFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
});
const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;

function applyLocalTime() {
  for (const element of document.querySelectorAll("time[data-local-time]")) {
    element.textContent = localTimeFormat.format(new Date(element.dateTime));
  }

  for (const input of document.querySelectorAll("input[data-time-zone]")) {
    input.value = timeZone;
  }
}

document.addEventListener("DOMContentLoaded", applyLocalTime);
Blazor.addEventListener("enhancedload", applyLocalTime);

const minute = 60_000;
const hour = 60 * minute;
const day = 24 * hour;

function formatTimeLeft(left) {
  const minutes = Math.max(1, Math.round(left / minute));
  if (minutes < 60) {
    return { label: `${minutes} min`, soon: true };
  }

  const hours = Math.round(left / hour);
  const label = hours < 24 ? `${hours} h` : `${Math.round(left / day)} d`;
  return { label, soon: false };
}

function countDown() {
  let next = 30_000;
  for (const item of document.querySelectorAll("[data-expires]")) {
    const left = Date.parse(item.dataset.expires) - Date.now();
    if (left <= 0) {
      vanish(item);
      continue;
    }

    const { label, soon } = formatTimeLeft(left);
    item.classList.toggle("soon", soon);
    const badge = item.querySelector("[data-time-left]");
    if (badge) {
      badge.textContent = label;
    }

    const stamp = item.querySelector("[data-time-left-stamp]");
    if (stamp) {
      stamp.textContent = `${label} left`;
      stamp.classList.toggle("stamp-soon", soon);
    }

    next = Math.min(next, left);
  }

  setTimeout(countDown, Math.max(1_000, next));
}

function vanish(item) {
  item.removeAttribute("data-expires");
  item.classList.add("gone");
  setTimeout(() => item.remove(), 600);
}

countDown();
