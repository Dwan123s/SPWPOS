export const noteFonts = [
  { value: "default", label: "Mặc định", family: "inherit" },
  {
    value: "serif",
    label: "Có chân",
    family: 'Georgia, "Times New Roman", serif',
  },
  {
    value: "rounded",
    label: "Mềm mại",
    family: '"Trebuchet MS", Arial, sans-serif',
  },
  {
    value: "mono",
    label: "Đơn cách",
    family: "ui-monospace, Consolas, monospace",
  },
];
export const noteInkColors = [
  ["#52647d", "Xám xanh"],
  ["#26374d", "Mực đậm"],
  ["#2459a6", "Xanh dương"],
  ["#216a50", "Xanh lá"],
  ["#7145a0", "Tím"],
  ["#ab3545", "Đỏ"],
];
export const notePaperColors = [
  ["#ffffff", "Trắng"],
  ["#fff8e8", "Kem"],
  ["#edf8f2", "Bạc hà"],
  ["#eff6ff", "Xanh nhạt"],
  ["#fff1f4", "Hồng nhạt"],
  ["#f3efff", "Oải hương"],
];
export const noteSizes = [14, 16, 18, 20, 22, 24, 28, 32];
export const defaultNoteAppearance = Object.freeze({
  size: 18,
  font: "default",
  ink: "#52647d",
  paper: "#ffffff",
  ruled: false,
});
const color = (value, fallback) =>
  typeof value === "string" && /^#[0-9a-f]{6}$/i.test(value)
    ? value.toLowerCase()
    : fallback;
export function normalizeNoteAppearance(value) {
  return {
    size: noteSizes.includes(value?.size)
      ? value.size
      : defaultNoteAppearance.size,
    font: noteFonts.some((font) => font.value === value?.font)
      ? value.font
      : defaultNoteAppearance.font,
    ink: color(value?.ink, defaultNoteAppearance.ink),
    paper: color(value?.paper, defaultNoteAppearance.paper),
    ruled: value?.ruled === true,
  };
}
const storageKey = (account, id) =>
  `planora.note-appearance.v1.${account}.${id || "draft"}`;
export function readNoteAppearance(account, id) {
  try {
    return normalizeNoteAppearance(
      JSON.parse(localStorage.getItem(storageKey(account, id))),
    );
  } catch {
    return { ...defaultNoteAppearance };
  }
}
export function saveNoteAppearance(account, id, appearance) {
  try {
    localStorage.setItem(
      storageKey(account, id),
      JSON.stringify(normalizeNoteAppearance(appearance)),
    );
  } catch {
    /* Appearance remains usable when browser storage is unavailable. */
  }
}
export function clearNoteAppearance(account, id) {
  try {
    localStorage.removeItem(storageKey(account, id));
  } catch {
    /* Optional preferences must not interrupt the note workflow. */
  }
}
export function noteAppearanceStyle(appearance) {
  const { size, font, ink, paper } = normalizeNoteAppearance(appearance);
  const rgb = paper.match(/[a-f0-9]{2}/gi).map((part) => parseInt(part, 16));
  const darkPaper = rgb[0] * 0.299 + rgb[1] * 0.587 + rgb[2] * 0.114 < 145;
  return {
    "--note-writing-size": `${size}px`,
    "--note-writing-font": noteFonts.find((item) => item.value === font).family,
    "--note-ink": ink,
    "--note-paper": paper,
    "--note-paper-muted": darkPaper ? "#c8d2e0" : "#7b8ba0",
    "--note-paper-line": darkPaper ? "#ffffff20" : "#29436614",
  };
}
