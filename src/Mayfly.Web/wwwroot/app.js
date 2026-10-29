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

document.addEventListener("dblclick", (event) => {
  const link = event.target.closest("a[data-download]");
  if (link) {
    location.href = link.dataset.download;
  }
});

const fileDragType = "application/x-mayfly-file";

document.addEventListener("dragstart", (event) => {
  const link = event.target.closest?.("a[data-file-id]");
  if (link) {
    event.dataTransfer.setData(fileDragType, link.dataset.fileId);
    event.dataTransfer.effectAllowed = "move";
  }
});

function folderTarget(event) {
  return event.dataTransfer?.types.includes(fileDragType)
    ? event.target.closest?.("[data-folder-target]")
    : null;
}

document.addEventListener("dragover", (event) => {
  const target = folderTarget(event);
  if (target) {
    event.preventDefault();
    event.dataTransfer.dropEffect = "move";
    target.classList.add("drop-target");
  }
});

document.addEventListener("dragleave", (event) => {
  folderTarget(event)?.classList.remove("drop-target");
});

document.addEventListener("drop", async (event) => {
  const target = folderTarget(event);
  if (!target) {
    return;
  }

  event.preventDefault();
  target.classList.remove("drop-target");
  try {
    await moveFile(
      event.dataTransfer.getData(fileDragType),
      target.dataset.folderTarget,
    );
  } finally {
    location.reload();
  }
});

document.addEventListener("submit", async (event) => {
  const form = event.target;
  if (!(form instanceof HTMLFormElement) || !form.dataset.moveFile) {
    return;
  }

  event.preventDefault();
  const fileId = form.dataset.moveFile;
  const folderId = form.elements.FolderId.value;
  try {
    await moveFile(fileId, folderId);
    location.href = folderId
      ? `desktop/folders/${folderId}/files/${fileId}`
      : `desktop/files/${fileId}`;
  } catch {
    location.reload();
  }
});

async function moveFile(fileId, folderId) {
  const antiforgery = document.querySelector(
    'input[name="__RequestVerificationToken"]',
  ).value;
  await postForm(
    `files/${fileId}/move`,
    antiforgery,
    folderId ? { FolderId: folderId } : {},
  );
}

document.addEventListener("keydown", (event) => {
  if (
    event.key !== "Escape" ||
    event.target.closest("input, select, textarea")
  ) {
    return;
  }

  const menu = document.querySelector("details[data-menu][open]");
  if (menu) {
    menu.open = false;
  } else {
    document.querySelector("[data-window-close]")?.click();
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
  const files = [...form.elements.file.files];
  const status = form.querySelector("[data-upload-status]");
  const button = form.querySelector("button[type=submit]");
  if (files.length === 0) {
    return;
  }

  const tooLarge = files.find(
    (file) => file.size > Number(form.dataset.maxSize),
  );
  if (tooLarge) {
    status.textContent = `${tooLarge.name} is too large.`;
    return;
  }

  button.disabled = true;
  let current = files[0];

  try {
    for (const [index, file] of files.entries()) {
      current = file;
      status.textContent =
        files.length > 1
          ? `Uploading ${index + 1} of ${files.length}: ${file.name}…`
          : `Uploading ${file.name}…`;
      await uploadFile(form, file);
    }

    location.reload();
  } catch (error) {
    status.textContent =
      error.status === 409
        ? `Not enough storage space left for ${current.name}.`
        : `${current.name} failed to upload. Check the expiration date and try again.`;
    button.disabled = false;
  }
});

document.addEventListener("change", (event) => {
  const input = event.target;
  if (
    input instanceof HTMLInputElement &&
    input.type === "file" &&
    input.form?.hasAttribute("data-upload")
  ) {
    showChosenFiles(input.form);
  }
});

document.addEventListener("dragover", (event) => {
  const zone = document.querySelector("[data-drop-zone]");
  if (zone && event.dataTransfer?.types.includes("Files")) {
    event.preventDefault();
    zone.hidden = false;
  }
});

document.addEventListener("dragleave", (event) => {
  const zone = document.querySelector("[data-drop-zone]");
  if (zone && !event.relatedTarget) {
    zone.hidden = true;
  }
});

document.addEventListener("drop", (event) => {
  const zone = document.querySelector("[data-drop-zone]");
  const form = document.querySelector("form[data-upload]");
  if (!zone || !form || !event.dataTransfer?.files.length) {
    return;
  }

  event.preventDefault();
  zone.hidden = true;
  form.elements.file.files = event.dataTransfer.files;
  form.closest("details").open = true;
  showChosenFiles(form);
  form.elements.Lifetime.focus();
});

function showChosenFiles(form) {
  const names = [...form.elements.file.files].map((file) => file.name);
  form.querySelector("[data-upload-status]").textContent =
    names.length > 0 ? `Ready: ${names.join(", ")}` : "";
}

async function uploadFile(form, file) {
  const antiforgery = form.elements.__RequestVerificationToken.value;
  const contentType = file.type || "application/octet-stream";
  const customExpiresAt = form.elements.CustomExpiresAt;
  const folderId = form.elements.FolderId?.value;

  const started = await postForm(form.action, antiforgery, {
    FileName: file.name,
    ContentType: contentType,
    Size: file.size,
    Lifetime: form.elements.Lifetime.value,
    TimeZone: form.elements.TimeZone.value,
    ...(customExpiresAt.disabled || !customExpiresAt.value
      ? {}
      : { CustomExpiresAt: customExpiresAt.value }),
    ...(folderId ? { FolderId: folderId } : {}),
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
}

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
