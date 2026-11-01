using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
namespace SPWPOS.Infrastructure.Workspace;

internal sealed record TeamBriefDocument(string Name, string? Text, string? PdfData);
internal static class TeamBriefReader
{
    private const int MaxBytes = 8 * 1024 * 1024, MaxText = 60000;
    internal static TeamBriefDocument Read(TeamBriefFile file)
    {
        var name = (file.Name ?? "").Replace('\\', '/').Split('/').Last();
        if (name.Length is < 1 or > 180 || file.Data is null || file.Data.Length > 11184812)
            throw Error("Tài liệu tối đa 8 MB và tên file tối đa 180 ký tự.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(file.Data); }
        catch (FormatException) { throw Error("Không đọc được file đính kèm. Hãy chọn lại file."); }
        if (bytes.Length is 0 or > MaxBytes) throw Error("Tài liệu cần có nội dung và không vượt quá 8 MB.");
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension == ".pdf")
        {
            if (bytes.Length < 5 || Encoding.ASCII.GetString(bytes, 0, 5) != "%PDF-") throw Error("Nội dung file không phải PDF hợp lệ.");
            return new(name, null, Convert.ToBase64String(bytes));
        }
        try
        {
            string text;
            if (extension is ".txt" or ".md") text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            else if (extension == ".docx") text = ReadWord(bytes);
            else throw Error("Hỗ trợ PDF, Word (.docx), TXT và Markdown (.md). Hãy chuyển file sang một trong các định dạng này.");
            if (text.Length > MaxText) throw Error("Nội dung tài liệu vượt quá 60.000 ký tự. Hãy tách tài liệu theo phần công việc để NORA đọc đầy đủ.");
            if (string.IsNullOrWhiteSpace(text) || text.Contains('\0')) throw Error("Không tìm thấy nội dung văn bản trong file. Với Word dạng ảnh, hãy xuất PDF rồi gửi lại.");
            return new(name, text, null);
        }
        catch (Exception e) when (e is InvalidDataException or XmlException or DecoderFallbackException)
        { throw Error("File không hợp lệ hoặc không đọc được. Hãy xuất lại thành PDF, DOCX hoặc TXT (UTF-8)."); }
    }
    private static string ReadWord(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        if (zip.Entries.Count > 1024 || zip.Entries.Any(x => x.Length > 8 * 1024 * 1024) || zip.Entries.Sum(x => x.Length) > 8 * 1024 * 1024 || zip.GetEntry("word/document.xml") is null)
            throw Error("Tài liệu Word quá lớn sau giải nén hoặc không đúng định dạng DOCX.");
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var content = new StringBuilder();
        foreach (var entry in zip.Entries.Where(x => x.FullName == "word/document.xml" ||
            x.FullName is "word/footnotes.xml" or "word/endnotes.xml" or "word/comments.xml" ||
            x.FullName.StartsWith("word/header", StringComparison.Ordinal) && x.FullName.EndsWith(".xml", StringComparison.Ordinal) ||
            x.FullName.StartsWith("word/footer", StringComparison.Ordinal) && x.FullName.EndsWith(".xml", StringComparison.Ordinal)))
        {
            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2000000 });
            var xml = XDocument.Load(reader);
            foreach (var paragraph in xml.Descendants(w + "p"))
            {
                content.AppendLine(string.Concat(paragraph.Descendants().Where(x => x.Name == w + "t" || x.Name == w + "tab" || x.Name == w + "br")
                    .Select(x => x.Name == w + "tab" ? "\t" : x.Name == w + "br" ? "\n" : x.Value)));
                if (content.Length > MaxText) throw Error("Nội dung Word vượt quá 60.000 ký tự. Hãy tách tài liệu để tránh bỏ sót yêu cầu.");
            }
        }
        return content.ToString();
    }
    private static AuthException Error(string message) => new("TEAM_DOCUMENT_INVALID", message, 400);
}
