using Microsoft.EntityFrameworkCore;
using SPWPOS.Application.Authentication;
using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;
using SPWPOS.Infrastructure.Persistence;
namespace SPWPOS.Infrastructure.Commerce;

// Read-only reporting. The verified payment service remains the only writer of payment status.
public sealed class CommerceAdminService(ApplicationDbContext db, TimeProvider clock)
{
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);
    private sealed record Daily(DateTime Date, long AmountVnd, int PaidOrders);
    private static AuthException Invalid(string message) => new("INVALID_REPORT_FILTER", message, 400);
    private static DateTimeOffset Start(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), Vietnam);
    private static DateOnly LocalDate(DateTimeOffset time) => DateOnly.FromDateTime(time.ToOffset(Vietnam).DateTime);
    private static DateOnly Monday(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    private static DateOnly MonthStart(DateOnly date) => new(date.Year, date.Month, 1);
    private (DateOnly From, DateOnly To) Range(DateOnly? from, DateOnly? to)
    {
        var today = LocalDate(clock.GetUtcNow());
        var first = from ?? MonthStart(today); var last = to ?? today;
        if (first.Year < 2000 || last.Year > 2100 || last < first || last.DayNumber - first.DayNumber >= 366)
            throw Invalid("Chọn khoảng ngày hợp lệ, tối đa 366 ngày (từ năm 2000 đến 2100).");
        return (first, last);
    }
    private IQueryable<SkinPurchase> Paid => db.SkinPurchases.AsNoTracking().Where(x => x.Status == "PAID" && x.PaidAt != null);
    private static async Task<RevenueTotal> Total(IQueryable<SkinPurchase> orders, CancellationToken ct) =>
        await orders.GroupBy(x => 1).Select(g => new RevenueTotal(g.Sum(x => (long)x.AmountVnd), g.Count())).SingleOrDefaultAsync(ct) ?? new(0, 0);
    public async Task<RevenueReport> RevenueAsync(DateOnly? from, DateOnly? to, string groupBy, CancellationToken ct)
    {
        var range = Range(from, to);
        if (groupBy is not ("day" or "week" or "month")) throw Invalid("Chọn thống kê theo ngày, tuần hoặc tháng.");
        var now = clock.GetUtcNow(); var today = LocalDate(now);
        var tomorrow = Start(today.AddDays(1)); var todayStart = Start(today); var weekStart = Start(Monday(today)); var monthStart = Start(MonthStart(today));
        var dailyTotal = await Total(Paid.Where(x => x.PaidAt >= todayStart && x.PaidAt < tomorrow), ct);
        var weeklyTotal = await Total(Paid.Where(x => x.PaidAt >= weekStart && x.PaidAt < tomorrow), ct);
        var monthlyTotal = await Total(Paid.Where(x => x.PaidAt >= monthStart && x.PaidAt < tomorrow), ct);
        var allTotal = await Total(Paid, ct);
        var start = Start(range.From); var end = Start(range.To.AddDays(1));
        var selected = Paid.Where(x => x.PaidAt >= start && x.PaidAt < end);
        var selectedTotal = await Total(selected, ct);
        var customers = await selected.Select(x => x.UserId).Distinct().CountAsync(ct);
        Daily[] days;
        if (db.Database.IsSqlServer())
        {
            // Aggregate on SQL Server, transfer at most 366 daily totals, not every purchase.
            days = await selected.GroupBy(x => EF.Functions.AtTimeZone(x.PaidAt!.Value, "SE Asia Standard Time").Date)
                .Select(g => new Daily(g.Key, g.Sum(x => (long)x.AmountVnd), g.Count())).ToArrayAsync(ct);
        }
        else
        {
            // SQLite test harness stores DateTimeOffset as ticks and has no offset date functions.
            var rows = await selected.Select(x => new { x.PaidAt, x.AmountVnd }).ToArrayAsync(ct);
            days = rows.GroupBy(x => x.PaidAt!.Value.ToOffset(Vietnam).Date)
                .Select(g => new Daily(g.Key, g.Sum(x => (long)x.AmountVnd), g.Count())).ToArray();
        }
        DateOnly Bucket(DateOnly d) => groupBy == "week" ? Monday(d) : groupBy == "month" ? MonthStart(d) : d;
        var buckets = days.GroupBy(x => Bucket(DateOnly.FromDateTime(x.Date)))
            .ToDictionary(g => g.Key, g => new RevenueTotal(g.Sum(x => x.AmountVnd), g.Sum(x => x.PaidOrders)));
        var points = new List<RevenuePoint>();
        for (var day = Bucket(range.From); day <= range.To;)
        {
            var next = groupBy == "month" ? day.AddMonths(1) : day.AddDays(groupBy == "week" ? 7 : 1);
            var value = buckets.GetValueOrDefault(day, new(0, 0));
            points.Add(new(day < range.From ? range.From : day, next.AddDays(-1) > range.To ? range.To : next.AddDays(-1), value.AmountVnd, value.PaidOrders));
            day = next;
        }
        return new(range.From, range.To, groupBy, "Asia/Ho_Chi_Minh", now, dailyTotal, weeklyTotal, monthlyTotal, allTotal, selectedTotal, customers, points.ToArray());
    }
    public async Task<PurchaseHistory> PurchasesAsync(DateOnly? from, DateOnly? to, string? status, string? search, int page, int pageSize, CancellationToken ct)
    {
        var range = Range(from, to);
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100) throw Invalid("Phân trang không hợp lệ.");
        if (search?.Length > 100) throw Invalid("Từ khóa tìm kiếm tối đa 100 ký tự.");
        if (status is not (null or "" or "PAID" or "PENDING" or "CREATING" or "EXPIRED" or "CANCELLED")) throw Invalid("Trạng thái đơn hàng không hợp lệ.");
        var start = Start(range.From); var end = Start(range.To.AddDays(1)); var now = clock.GetUtcNow();
        // Settled orders use recognition time; other orders use their creation time.
        var query = from p in db.SkinPurchases.AsNoTracking()
                    join u in db.Users.AsNoTracking() on p.UserId equals u.Id
                    let recorded = p.Status == "PAID" ? p.PaidAt ?? p.CreatedAt : p.CreatedAt
                    let effectiveStatus = (p.Status == "PENDING" || p.Status == "CREATING") && p.ExpiresAt <= now ? "EXPIRED" : p.Status
                    where recorded >= start && recorded < end
                    select new { Purchase = p, u.Email, BuyerName = (u.FirstName + " " + u.LastName).Trim(), Status = effectiveStatus, Recorded = recorded };
        if (!string.IsNullOrEmpty(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim(); var isCode = long.TryParse(term, out var code);
            query = query.Where(x => (x.Email != null && x.Email.Contains(term)) || x.BuyerName.Contains(term) ||
                (isCode && x.Purchase.OrderCode == code) || (x.Purchase.ReceiptReference != null && x.Purchase.ReceiptReference.Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.Recorded).ThenByDescending(x => x.Purchase.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminPurchase(x.Purchase.Id, x.Purchase.OrderCode, x.Purchase.UserId, x.Email ?? "", x.BuyerName,
                x.Purchase.SkinCode, x.Purchase.SkinCode == "fox" ? "Skin MILO" : x.Purchase.SkinCode, x.Purchase.AmountVnd,
                x.Status, x.Purchase.CreatedAt, x.Purchase.ExpiresAt, x.Purchase.PaidAt, x.Purchase.ReceiptReference)).ToArrayAsync(ct);
        return new(range.From, range.To, page, pageSize, total, items);
    }
}
