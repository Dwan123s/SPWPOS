using System.Text;
using System.Text.RegularExpressions;

namespace SPWPOS.Application.Workspace;

// An interpretation aid, not an IME or a general spelling corrector. Only
// recognized Vietnamese planning vocabulary is decoded; unknown names stay raw.
public static class VietnameseTelex
{
    private static readonly HashSet<string> Vocabulary = new(("""
        tôi mình bạn tớ em anh chị chúng ta có muốn cần làm việc viết bài báo cáo công việc dự án
        lập lên xếp lịch sắp đặt thêm tạo sửa đổi cập nhật điều chỉnh dời chuyển lại giữ nguyên
        hôm nay ngày mai kia tuần tháng năm thứ hai ba tư bốn năm sáu bảy tám chín mười một
        chủ nhật đầu cuối sáng trưa chiều tối đêm khuya buổi lúc vào từ đến giờ phút tiếng rưỡi
        rảnh bận nghỉ ngơi trống học ôn tập đọc sách tài liệu học bài tiếng anh lập trình thiết kế
        họp cuộc lớp hẹn gặp thi kiểm tra chuẩn bị nội dung thảo luận báo cáo trả lời gửi tin nhắn
        không đừng chưa chỉ xem thử trước sau hãy giúp cho với của và hoặc nhưng rồi nhé nha được
        lưu áp dụng tự động xác nhận đồng ý hủy bỏ xóa dừng toàn bộ tất cả mỗi từng riêng cùng
        hạn chót hoàn thành xong kết thúc bắt đầu thời gian lượng khoảng trong ngoài liên tục
        chia nhỏ ưu tiên khẩn cấp gấp đột xuất mềm cứng tối đa tối thiểu ít nhất nhiều hơn bớt
        bao nhiêu khi nào còn đủ thiếu phù hợp khả năng phân tích đánh giá gợi ý phương án
        công ty nhà trường phòng chạy ngủ ăn uống tập thể dục đi về nghỉ lễ du lịch sinh nhật
        soạn nộp sửa luyện dự kiến tiến độ nhắc nhở thông báo mục tiêu kế hoạch danh sách ghi chú
        đó này khác mới cũ giờ rảnh khung cửa sổ thay thế yêu cầu thông tin tiếp tục tạm dừng
        lỗi hiểu đúng sai rõ chính xác xác định lựa chọn chồng trùng đã sẽ đang cũng vẫn thôi
        cảm ơn xin chào hỗ trợ mở đóng nội bộ ôn tiếng Việt giải bài toán mẫu chương trình
        người khách nửa lăm mốt bữa rửa dọn tưới cây đường trường lượng lương hưởng hướng
        """).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> English = new(
        "as is of or for has was us bus class pass test tests case cases file files save status focus tasks work hours now own cow show low new row rows json url http https".Split(' '),
        StringComparer.OrdinalIgnoreCase);
    private static readonly Regex Tokens = new(
        @"(?<protected>https?://[^\s]+|[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}|`[^`\r\n]*`|(?:[A-Za-z]:\\|/)[^\s]+|[\w.-]+\.(?:cs|jsx?|tsx?|json|md|txt|csv|pdf|docx|xlsx)\b)|(?<word>(?<![\p{L}\p{N}_])[A-Za-z]{2,24}(?![\p{L}\p{N}_]))",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
    private const string ToneKeys = "sfrxj";
    private const string ToneMarks = "\u0301\u0300\u0309\u0303\u0323";

    public static string Normalize(string text) => Tokens.Replace(text, match =>
        match.Groups["word"].Success ? Decode(match.Value) : match.Value);

    private static string Decode(string token)
    {
        if (English.Contains(token)) return token;
        var lower = token.ToLowerInvariant();
        // Tolerate an accidentally repeated tone key, e.g. vieetss -> vieets.
        var word = Regex.Replace(lower, @"([sfrxj])\1+(?=(?:ng|nh|ch|[cmnpt])?$)", "$1",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        var candidates = new HashSet<string>(StringComparer.Ordinal);
        void Consider(string body, char? tone)
        {
            var shaped = Shapes(body);
            var candidate = tone is { } key ? Accent(shaped, key) : shaped;
            if (Vocabulary.Contains(candidate)) candidates.Add(candidate);
        }
        Consider(word, null);
        var vowelSeen = false;
        for (var i = 0; i < word.Length; i++)
        {
            if ("aeiouy".Contains(word[i])) vowelSeen = true;
            else if (vowelSeen && ToneKeys.Contains(word[i])) Consider(word.Remove(i, 1), word[i]);
        }
        if (candidates.Count != 1) return token;
        var result = candidates.Single();
        if (result == lower) return token;
        return token.All(char.IsUpper) ? result.ToUpperInvariant() : char.IsUpper(token[0])
            ? char.ToUpperInvariant(result[0]) + result[1..] : result;
    }

    private static string Shapes(string word)
    {
        var result = word.Replace("dd", "đ").Replace("uwow", "ươ").Replace("uow", "ươ")
            .Replace("uuw", "ưu").Replace("uaw", "ưa")
            .Replace("aa", "â").Replace("aw", "ă").Replace("ee", "ê").Replace("oo", "ô")
            .Replace("ow", "ơ").Replace("uw", "ư");
        // A modifier entered after the coda is also common: ddungwf -> đừng.
        return Regex.Replace(result, @"(?<vowel>uo|[auo])(?<coda>ng|nh|ch|[cmnpt]?)w$", match =>
            (match.Groups["vowel"].Value switch { "uo" => "ươ", "a" => "ă", "u" => "ư", _ => "ơ" }) + match.Groups["coda"].Value,
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    private static string Accent(string word, char tone)
    {
        var vowels = Enumerable.Range(0, word.Length).Where(i => "aeiouyâăêôơư".Contains(word[i])).ToList();
        if (vowels.Count == 0) return word;
        if (vowels.Count > 1 && (word.StartsWith("qu", StringComparison.Ordinal) || word.StartsWith("gi", StringComparison.Ordinal)))
            vowels.RemoveAt(0);
        var shaped = vowels.Where(i => "âăêôơư".Contains(word[i])).ToArray();
        var target = shaped.Length > 0 ? shaped[^1] : vowels.Count == 1 ? vowels[0] :
            vowels[^1] == word.Length - 1 ? vowels[^2] : vowels[^1];
        return word.Insert(target + 1, ToneMarks[ToneKeys.IndexOf(tone)].ToString()).Normalize(NormalizationForm.FormC);
    }
}
