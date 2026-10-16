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
