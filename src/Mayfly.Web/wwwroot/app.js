document.addEventListener("change", (event) => {
  const select = event.target;
  if (!(select instanceof HTMLSelectElement) || !select.dataset.enables) {
    return;
  }

  const target = document.getElementById(select.dataset.enables);
  if (target) {
    target.disabled = select.value !== select.dataset.enablesWhen;
    if (!target.disabled) {
      target.focus();
    }
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
const itemDragType = "application/x-mayfly-item";

document.addEventListener("dragstart", (event) => {
  const link = event.target.closest?.(".desktop .item > a");
  if (!link || !itemId(link)) {
    return;
  }

  if (link.dataset.fileId) {
    event.dataTransfer.setData(fileDragType, link.dataset.fileId);
  }
  event.dataTransfer.setData(itemDragType, itemId(link));
  event.dataTransfer.effectAllowed = "move";
});

function itemId(link) {
  return link.dataset.fileId ?? link.dataset.folderTarget;
}

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

function layoutTarget(event) {
  return event.dataTransfer?.types.includes(itemDragType) &&
    !folderTarget(event)
    ? event.target.closest?.(".desktop")
    : null;
}

document.addEventListener("dragover", (event) => {
  if (layoutTarget(event)) {
    event.preventDefault();
    event.dataTransfer.dropEffect = "move";
  }
});

document.addEventListener("drop", async (event) => {
  const grid = layoutTarget(event);
  if (!grid) {
    return;
  }

  event.preventDefault();
  const style = getComputedStyle(grid);
  const box = grid.getBoundingClientRect();
  const left = box.left + parseFloat(style.paddingLeft) - grid.scrollLeft;
  const top = box.top + parseFloat(style.paddingTop) - grid.scrollTop;
  const width = parseFloat(style.gridAutoColumns) + parseFloat(style.columnGap);
  const height = parseFloat(style.gridTemplateRows);
  const rows = style.gridTemplateRows.split(" ").length;
  const cellAt = (x, y) => [
    Math.max(1, Math.floor((x - left) / width) + 1),
    Math.min(rows, Math.max(1, Math.floor((y - top) / height) + 1)),
  ];

  const items = [...grid.querySelectorAll(":scope > .item")].map((item) => {
    const rect = item.getBoundingClientRect();
    return {
      item,
      id: itemId(item.querySelector(":scope > a")),
      cell: cellAt(rect.left + rect.width / 2, rect.top + rect.height / 2),
    };
  });
  const moved = items.find(
    ({ id }) => id === event.dataTransfer.getData(itemDragType),
  );
  const target = cellAt(event.clientX, event.clientY);
  const occupant = items.find(
    ({ cell }) => cell[0] === target[0] && cell[1] === target[1],
  );
  if (!moved || occupant === moved) {
    return;
  }

  if (occupant) {
    occupant.cell = moved.cell;
  }
  moved.cell = target;

  const fields = {};
  items.forEach(({ item, id, cell: [column, row] }, index) => {
    item.style.gridArea = `${row} / ${column}`;
    fields[`Items[${index}].Id`] = id;
    fields[`Items[${index}].Column`] = column;
    fields[`Items[${index}].Row`] = row;
  });

  const antiforgery = document.querySelector(
    'input[name="__RequestVerificationToken"]',
  ).value;
  try {
    await postForm("desktop/layout", antiforgery, fields);
  } catch {
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
    event.target.closest("input, select, textarea, [contenteditable]")
  ) {
    return;
  }

  const invoker = closeContextMenu();
  const menu = document.querySelector("details[data-menu][open]");
  if (invoker) {
    invoker.focus();
  } else if (menu) {
    menu.open = false;
  } else {
    document.querySelector("[data-window-close]")?.click();
  }
});

const contextMenuExclusions =
  ".menubar, .window, .path, .context-menu, input, select, textarea, [contenteditable]";
let openContextMenu = null;

function closeContextMenu() {
  if (!openContextMenu) {
    return null;
  }

  const { menu, invoker } = openContextMenu;
  menu.hidden = true;
  openContextMenu = null;
  return invoker;
}

function contextTarget(element) {
  const file = element.closest("a[data-file-id]");
  if (file) {
    return ["file", file];
  }

  const folder = element.closest(".folder > a[data-folder-target]");
  return folder ? ["folder", folder] : ["desktop", null];
}

function fillContextMenu(menu, invoker) {
  for (const item of menu.querySelectorAll("[data-from]")) {
    const value =
      item.dataset.from === "href"
        ? invoker.getAttribute("href")
        : invoker.dataset[item.dataset.from];
    if (item instanceof HTMLInputElement) {
      item.value = value;
    } else {
      item.setAttribute("href", value);
    }
  }
}

document.addEventListener("contextmenu", (event) => {
  if (
    !document.querySelector("[data-context-menu]") ||
    event.target.closest(contextMenuExclusions)
  ) {
    return;
  }

  const [kind, invoker] = contextTarget(event.target);
  const menu = document.querySelector(`[data-context-menu="${kind}"]`);
  if (!menu) {
    return;
  }

  event.preventDefault();
  closeContextMenu();
  if (invoker) {
    fillContextMenu(menu, invoker);
  }

  const anchor =
    event.clientX || event.clientY
      ? { left: event.clientX, bottom: event.clientY }
      : (invoker ?? event.target).getBoundingClientRect();
  menu.hidden = false;
  const { width, height } = menu.getBoundingClientRect();
  menu.style.left = `${Math.max(8, Math.min(anchor.left, innerWidth - width - 8))}px`;
  menu.style.top = `${Math.max(8, Math.min(anchor.bottom, innerHeight - height - 8))}px`;
  openContextMenu = { menu, invoker: invoker ?? document.activeElement };
  menu.querySelector('[role="menuitem"]').focus();
});

document.addEventListener("keydown", (event) => {
  if (!openContextMenu) {
    return;
  }

  const items = [...openContextMenu.menu.querySelectorAll('[role="menuitem"]')];
  const step = { ArrowDown: 1, ArrowUp: -1 }[event.key];
  if (step) {
    event.preventDefault();
    const index = items.indexOf(document.activeElement);
    items[(index + step + items.length) % items.length].focus();
  } else if (event.key === "Tab") {
    closeContextMenu();
  }
});

document.addEventListener("keydown", (event) => {
  if (event.key !== "Delete" || openContextMenu) {
    return;
  }

  const [kind, invoker] = contextTarget(event.target);
  const menu =
    invoker && document.querySelector(`[data-context-menu="${kind}"]`);
  if (menu) {
    event.preventDefault();
    fillContextMenu(menu, invoker);
    menu.querySelector("form").requestSubmit();
  }
});

document.addEventListener("pointerdown", (event) => {
  if (openContextMenu && !openContextMenu.menu.contains(event.target)) {
    closeContextMenu();
  }
});

document.addEventListener("click", (event) => {
  if (openContextMenu?.menu.contains(event.target)) {
    setTimeout(closeContextMenu);
  }
});

document.addEventListener("scroll", closeContextMenu, true);
addEventListener("resize", closeContextMenu);
addEventListener("blur", closeContextMenu);

const desktopActions = {
  "new-folder": () => {
    const details = document.querySelector(".new-folder");
    details.open = true;
    details.querySelector('input:not([type="hidden"])').focus();
  },
  upload: () => openUpload("file"),
  "upload-folder": () => openUpload("folder"),
};

function openUpload(input) {
  const details = document.querySelector(".upload");
  details.open = true;
  details.querySelector(`input[name="${input}"]`).click();
}

document.addEventListener("click", (event) => {
  const action = event.target.closest("[data-action]")?.dataset.action;
  desktopActions[action]?.();
});

const markdownMarks = {
  B: "**",
  STRONG: "**",
  I: "*",
  EM: "*",
  U: "++",
  S: "~~",
  STRIKE: "~~",
  DEL: "~~",
};
const editorShortcuts = { b: "bold", i: "italic", u: "underline" };

function editorMarkdown(node) {
  let text = "";
  for (const child of node.childNodes) {
    text +=
      child.nodeType === Node.TEXT_NODE
        ? child.data.replaceAll("\xa0", " ")
        : elementMarkdown(child);
  }

  return text;
}

function elementMarkdown(element) {
  const name = element.nodeName;
  if (name === "BR") {
    return "\n";
  }

  if (name === "CODE") {
    return `\`${element.textContent}\``;
  }

  const inner = editorMarkdown(element);
  if (name === "DIV" || name === "P") {
    return `\n${inner.replace(/\n$/, "")}`;
  }

  if (name === "LI") {
    return `\n- ${inner.replace(/\n$/, "")}`;
  }

  if (name === "UL" || name === "OL") {
    return `\n${inner}\n`;
  }

  const mark = markdownMarks[name];
  const [, before, core, after] = inner.match(/^(\s*)([\s\S]*?)(\s*)$/);
  return mark && core ? before + mark + core + mark + after : inner;
}

function codeAtSelection(editor) {
  const node = getSelection().anchorNode;
  const element = node?.nodeType === Node.TEXT_NODE ? node.parentElement : node;
  const code = element?.closest?.("code");
  return code && editor.contains(code) ? code : null;
}

function runEditorCommand(editor, command) {
  editor.focus();
  const code = command === "code" && codeAtSelection(editor);
  if (code) {
    code.replaceWith(code.textContent);
  } else if (command === "code") {
    const selected = getSelection().toString();
    if (selected) {
      const element = document.createElement("code");
      element.textContent = selected;
      document.execCommand("insertHTML", false, element.outerHTML);
    }
  } else {
    document.execCommand(command);
  }

  updateEditorTools(editor);
}

function updateEditorTools(editor) {
  for (const button of editor
    .closest("form")
    .querySelectorAll("button[data-command]")) {
    const { command } = button.dataset;
    const pressed =
      command === "code"
        ? Boolean(codeAtSelection(editor))
        : document.queryCommandState(command);
    button.setAttribute("aria-pressed", String(pressed));
  }
}

document.addEventListener("mousedown", (event) => {
  if (event.target.closest?.("button[data-command]")) {
    event.preventDefault();
  }
});

document.addEventListener("click", (event) => {
  const button = event.target.closest("button[data-command]");
  const editor = button?.form?.querySelector("[data-editor]");
  if (editor) {
    runEditorCommand(editor, button.dataset.command);
  }
});

document.addEventListener("keydown", (event) => {
  const editor = event.target.closest?.("[data-editor]");
  if (!editor || !(event.ctrlKey || event.metaKey)) {
    return;
  }

  const command = editorShortcuts[event.key.toLowerCase()];
  if (event.key === "Enter") {
    event.preventDefault();
    editor.closest("form").requestSubmit();
  } else if (command) {
    event.preventDefault();
    runEditorCommand(editor, command);
  }
});

document.addEventListener("selectionchange", () => {
  const editor = document.activeElement?.closest?.("[data-editor]");
  if (editor) {
    updateEditorTools(editor);
  }
});

document.addEventListener("paste", (event) => {
  if (event.target.closest?.("[data-editor]")) {
    event.preventDefault();
    document.execCommand(
      "insertText",
      false,
      event.clipboardData.getData("text/plain"),
    );
  }
});

document.addEventListener("input", (event) => {
  const editor = event.target.closest?.("[data-editor]");
  if (editor && !editor.textContent && !editor.querySelector("li")) {
    editor.replaceChildren();
  }
});

document.addEventListener("submit", (event) => {
  const editor = event.target.querySelector?.("[data-editor]");
  if (!editor) {
    return;
  }

  const content = editorMarkdown(editor).replace(/^\n+|\s+$/g, "");
  if (content) {
    event.target.querySelector("textarea").value = content;
  } else {
    event.preventDefault();
    editor.focus();
  }
});

function restoreEditors() {
  for (const editor of document.querySelectorAll("[data-editor]")) {
    const source = editor.closest("form").querySelector("textarea");
    if (source.value && !editor.textContent) {
      editor.innerText = source.value;
    }
  }
}

document.addEventListener("submit", (event) => {
  const message = event.target.dataset?.confirm;
  if (message && !confirm(message)) {
    event.preventDefault();
  }
});

let droppedUpload = null;

document.addEventListener("submit", async (event) => {
  const form = event.target;
  if (!(form instanceof HTMLFormElement) || !form.hasAttribute("data-upload")) {
    return;
  }

  event.preventDefault();
  const { files, directories } = chosenUpload(form);
  const status = form.querySelector("[data-upload-status]");
  const button = form.querySelector("button[type=submit]");
  if (files.length === 0 && directories.length === 0) {
    return;
  }

  const tooLarge = files.find(
    ({ file }) => file.size > Number(form.dataset.maxSize),
  );
  if (tooLarge) {
    status.textContent = `${tooLarge.file.name} is too large.`;
    return;
  }

  button.disabled = true;
  let current = "";

  try {
    const antiforgery = form.elements.__RequestVerificationToken.value;
    const folderIds = new Map([["", form.elements.FolderId.value]]);
    for (const path of directories) {
      const slash = path.lastIndexOf("/");
      const parentId = folderIds.get(path.slice(0, Math.max(slash, 0)));
      current = path;
      status.textContent = `Creating ${path}…`;
      const created = await postForm("folders", antiforgery, {
        Name: path.slice(slash + 1),
        ...(parentId ? { ParentId: parentId } : {}),
      });
      folderIds.set(path, (await created.json()).id);
    }

    for (const [index, { file, directory }] of files.entries()) {
      current = file.name;
      status.textContent =
        files.length > 1
          ? `Uploading ${index + 1} of ${files.length}: ${file.name}…`
          : `Uploading ${file.name}…`;
      await uploadFile(form, file, folderIds.get(directory));
    }

    location.reload();
  } catch (error) {
    status.textContent =
      error.status === 409
        ? `Not enough storage space left for ${current}.`
        : `${current} failed to upload. Check the expiration date and try again.`;
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
    droppedUpload = null;
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

document.addEventListener("drop", async (event) => {
  const zone = document.querySelector("[data-drop-zone]");
  const form = document.querySelector("form[data-upload]");
  if (!zone || !form || !event.dataTransfer?.files.length) {
    return;
  }

  event.preventDefault();
  zone.hidden = true;
  const files = [...event.dataTransfer.files];
  const entries = [...event.dataTransfer.items]
    .filter((item) => item.kind === "file")
    .map((item) => item.webkitGetAsEntry());
  form.elements.file.value = "";
  form.elements.folder.value = "";
  droppedUpload = entries.every(Boolean)
    ? await readDropped(entries)
    : {
        files: files.map((file) => ({ file, directory: "" })),
        directories: [],
      };
  form.closest("details").open = true;
  showChosenFiles(form);
  form.elements.Lifetime.focus();
});

function chosenUpload(form) {
  if (droppedUpload) {
    return droppedUpload;
  }

  const directories = new Set();
  const chosen = [...form.elements.file.files, ...form.elements.folder.files];
  const files = chosen.map((file) => {
    const parts = file.webkitRelativePath.split("/").slice(0, -1);
    parts.forEach((_, index) =>
      directories.add(parts.slice(0, index + 1).join("/")),
    );
    return { file, directory: parts.join("/") };
  });
  return { files, directories: [...directories] };
}

async function readDropped(entries) {
  const upload = { files: [], directories: [] };
  const walk = async (entry, directory) => {
    if (entry.isFile) {
      const file = await new Promise((resolve, reject) =>
        entry.file(resolve, reject),
      );
      upload.files.push({ file, directory });
      return;
    }

    const path = directory ? `${directory}/${entry.name}` : entry.name;
    upload.directories.push(path);
    const reader = entry.createReader();
    const readBatch = () =>
      new Promise((resolve, reject) => reader.readEntries(resolve, reject));
    for (
      let batch = await readBatch();
      batch.length > 0;
      batch = await readBatch()
    ) {
      for (const child of batch) {
        await walk(child, path);
      }
    }
  };

  for (const entry of entries) {
    await walk(entry, "");
  }

  return upload;
}

function showChosenFiles(form) {
  const { files, directories } = chosenUpload(form);
  const names = [
    ...directories
      .filter((path) => !path.includes("/"))
      .map((path) => `${path}/`),
    ...files.filter(({ directory }) => !directory).map(({ file }) => file.name),
  ];
  form.querySelector("[data-upload-status]").textContent =
    names.length > 0 ? `Ready: ${names.join(", ")}` : "";
}

async function uploadFile(form, file, folderId) {
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

function markTheme() {
  const theme = document.documentElement.dataset.theme ?? "";
  for (const button of document.querySelectorAll("[data-theme-choice]")) {
    button.setAttribute("aria-pressed", button.dataset.themeChoice === theme);
  }
}

document.addEventListener("click", (event) => {
  const choice = event.target.closest("[data-theme-choice]");
  if (!choice) {
    return;
  }

  const theme = choice.dataset.themeChoice;
  document.cookie = `theme=${theme}; path=/; max-age=${theme ? 31_536_000 : 0}; samesite=lax; secure`;
  if (theme) {
    document.documentElement.dataset.theme = theme;
  } else {
    delete document.documentElement.dataset.theme;
  }
  markTheme();
});

document.addEventListener("DOMContentLoaded", markTheme);
Blazor.addEventListener("enhancedload", markTheme);
document.addEventListener("DOMContentLoaded", applyLocalTime);
Blazor.addEventListener("enhancedload", applyLocalTime);
document.addEventListener("DOMContentLoaded", restoreEditors);
Blazor.addEventListener("enhancedload", restoreEditors);

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
