using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.TestSupport;

namespace SPWPOS.UnitTests;

public sealed class WeekAvailabilityTests
{
    [Fact]
    public async Task Selected_week_replaces_recurring_hours_and_reset_keeps_dated_busy_time()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = await ServiceHarness.CreateAsync();
        var user = await harness.RegisterAsync();
        var week = new DateOnly(2026, 12, 14);
        var start = new DateTimeOffset(2026, 12, 14, 0, 0, 0, TimeSpan.Zero);

        async Task Change(Func<IWorkspaceService, Task> action)
        {
            await using var scope = harness.Provider.CreateAsyncScope();
            await action(scope.ServiceProvider.GetRequiredService<IWorkspaceService>());
        }

        Task<TimeSlot[]> FreeSlots() => harness.DbAsync(async db =>
        {
            var state = new PlanningState([], [], await db.WeeklyAvailability.ToArrayAsync(ct),
                await db.AvailabilityExceptions.ToArrayAsync(ct), [], [], new WorkPreference { TimeZoneId = "UTC" });
            return new PlanningEngine().FreeSlots(state, start, start.AddDays(14));
        });

        await Change(s => s.SetAvailabilityAsync(user.Id, new([new(1, 540, 1020), new(2, 540, 1020)]), ct));
        await Change(s => s.SaveExceptionAsync(user.Id, null, new(week, 840, 900, false, "Bận"), ct));
        await Change(s => s.SetAvailabilityForWeekAsync(user.Id, week, new([new(1, 780, 960)]), ct));

        Assert.Equal(new TimeSlot[]
        {
            new(start.AddHours(13), start.AddHours(14)),
            new(start.AddHours(15), start.AddHours(16)),
            new(start.AddDays(7).AddHours(9), start.AddDays(7).AddHours(17)),
            new(start.AddDays(8).AddHours(9), start.AddDays(8).AddHours(17))
        }, await FreeSlots());

        await Change(s => s.SetAvailabilityForWeekAsync(user.Id, week, new([]), ct));
        var followingWeek = await FreeSlots();
        Assert.Equal(2, followingWeek.Length);
        Assert.All(followingWeek, slot => Assert.True(slot.Start >= start.AddDays(7)));

        await Change(s => s.SetAvailabilityForWeekAsync(user.Id, week, null, ct));
        Assert.Equal(new TimeSlot(start.AddHours(9), start.AddHours(14)), (await FreeSlots())[0]);
        await harness.DbAsync(async db => Assert.False((await db.AvailabilityExceptions.SingleAsync(ct)).IsAvailable));
    }
}
