export const TEAM_BRIEF_ACCEPT = ".pdf,.docx,.txt,.md";
export function readTeamBriefFile(file) {
  if (!/\.(pdf|docx|txt|md)$/i.test(file.name))
    return Promise.reject(
      new Error("Hỗ trợ PDF, Word (.docx), TXT và Markdown (.md)."),
    );
  if (!file.size || file.size > 8 * 1024 * 1024)
    return Promise.reject(
      new Error("Tài liệu cần có nội dung và không vượt quá 8 MB."),
    );
  const extension = file.name.split(".").pop().toLowerCase();
  const mimeType = {
    pdf: "application/pdf",
    docx: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    txt: "text/plain",
    md: "text/markdown",
  }[extension];
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () =>
      reject(new Error("Không đọc được tài liệu. Hãy chọn lại file."));
    reader.onload = () =>
      resolve({
        name: file.name,
        mimeType,
        data: String(reader.result).split(",")[1],
        size: file.size,
      });
    reader.readAsDataURL(file);
  });
}
