using SPWPOS.Application.Workspace;
namespace SPWPOS.UnitTests;

public sealed class VietnameseTelexTests
{
    [Theory]
    [InlineData("lamf", "làm")]
    [InlineData("vieets", "viết")]
    [InlineData("vieetss'", "viết'")]
    [InlineData("vieest", "viết")]
    [InlineData("vieejc", "việc")]
    [InlineData("Tooi muoons lamf vieejc", "Tôi muốn làm việc")]
    [InlineData("LAMF VIEETSS", "LÀM VIẾT")]
    [InlineData("ngayf mai", "ngày mai")]
    [InlineData("ngafy mai", "ngày mai")]
    [InlineData("45 phuts", "45 phút")]
    [InlineData("truowsc 21h", "trước 21h")]
    [InlineData("chieeuf mai", "chiều mai")]
    [InlineData("toois nay", "tối nay")]
    [InlineData("ddungwf luuw", "đừng lưu")]
    [InlineData("ddunwgf", "ddunwgf")]
    [InlineData("chuaw ap dungj", "chưa ap dụng")]
    [InlineData("khoong tuwj ddoongj luuw", "không tự động lưu")]
    [InlineData("Chir xem truowsc", "Chỉ xem trước")]
    [InlineData("ruwowix tieengs", "rưỡi tiếng")]
    [InlineData("Tôi muốn vieetss báo cáo 45 phuts", "Tôi muốn viết báo cáo 45 phút")]
    public void Reads_recognizable_Telex_without_changing_numbers_or_other_words(string input, string expected)
    {
        Assert.Equal(expected, VietnameseTelex.Normalize(input));
        Assert.Equal(expected, VietnameseTelex.Normalize(VietnameseTelex.Normalize(input)));
    }

    [Theory]
    [InlineData("class test tests pass files status focus review task work hours now own cow")]
    [InlineData("https://example.com/lamf/vieetss?day=24/10/2026")]
    [InlineData("lamf@example.com `vieetss` task_lamf SWP-123 24/10/2026 21:30")]
    [InlineData("C:\\work\\lamf.txt /tasks/vieets lamf.cs vieetss.json")]
    [InlineData("UNKNOWN_CUSTOM_CODE Xylophone Azure github")]
    [InlineData("Tôi muốn làm việc ngày mai, chưa lưu.")]
    public void Preserves_English_identifiers_addresses_code_paths_and_accented_input(string input)
        => Assert.Equal(input, VietnameseTelex.Normalize(input));

    [Fact]
    public void Ambiguous_clock_still_requires_confirmation_for_Telex_input()
    {
        Assert.NotNull(AgentInputRules.AmbiguousClock("Tooi cos cuoocj hopj lucs 6h ngayf mai"));
        Assert.NotNull(AgentInputRules.AmbiguousClock("Tôi có cuộc họp lúc 6h ngày mai"));
        Assert.Null(AgentInputRules.AmbiguousClock("Tooi cos cuoocj hopj lucs 6h toois ngayf mai"));
        Assert.Equal((420, 720), AgentInputRules.ExplicitTimeRange("tuwf 7h ddeens 12h sangs"));
    }
}
