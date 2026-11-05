using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;
namespace SPWPOS.UnitTests;

public sealed class CalendarPastEventTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task<T> Work<T>(ServiceHarness h, Func<IWorkspaceService, Task<T>> action) {
        await using var scope = h.Provider.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>());
    }
    [Theory] [InlineData(-60)] [InlineData(-1)] [InlineData(0)]
    public async Task Past_and_current_start_are_rejected_without_saving_any_repeated_occurrences(int offset)
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        var start = h.Clock.GetUtcNow().AddMinutes(offset);
        var failure = await Assert.ThrowsAsync<AuthException>(() => Work(h, s => s.SaveEventAsync(user.Id, null, new("Lịch cũ", start, start.AddHours(2), RepeatWeeks: 4), Ct)));
        Assert.Equal("EVENT_IN_PAST", failure.Code);
        Assert.False(await h.DbAsync(db => db.FixedEvents.AnyAsync(Ct)));
    }
    [Fact] public async Task Future_start_can_be_saved_and_cannot_be_edited_back_into_the_past()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        var start = h.Clock.GetUtcNow().AddMinutes(30);
        var id = await Work(h, s => s.SaveEventAsync(user.Id, null, new("Lịch mới", start, start.AddHours(1)), Ct));
        var failure = await Assert.ThrowsAsync<AuthException>(() => Work(h, s => s.SaveEventAsync(user.Id, id, new("Không được dời", start.AddHours(-2), start.AddHours(-1)), Ct)));
        Assert.Equal("EVENT_IN_PAST", failure.Code);
        var entry = await h.DbAsync(db => db.FixedEvents.AsNoTracking().SingleAsync(Ct));
        Assert.Equal(start, entry.Start); Assert.Equal("Lịch mới", entry.Name);
    }
    [Fact] public async Task History_remains_readable_and_metadata_can_be_corrected_without_moving_its_start()
    {
        await using var h = await ServiceHarness.CreateAsync(); var user = await h.RegisterAsync();
        var start = h.Clock.GetUtcNow().AddDays(-1);
        var old = new FixedEvent { UserId = user.Id, Name = "Lịch sử", Start = start, End = start.AddHours(1) };
        await h.DbAsync(async db => { db.FixedEvents.Add(old); await db.SaveChangesAsync(Ct); });
        await Work(h, s => s.SaveEventAsync(user.Id, old.Id, new("Tên đã sửa", start, start.AddHours(1)), Ct));
        var entry = await h.DbAsync(db => db.FixedEvents.AsNoTracking().SingleAsync(Ct));
        Assert.Equal("Tên đã sửa", entry.Name); Assert.Equal(start, entry.Start);
    }
}
