using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using WalletPlatform.MockBiller.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<MockBillerDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("MockBillerDb")));
builder.Services.AddOpenApi();
var app = builder.Build();
app.MapOpenApi();

// ... (existing endpoints kept minimal) ...

// Settlement file endpoint
app.MapGet("/api/settlements", async (string businessDate, string? billerCode, MockBillerDbContext db) =>
{
    if (!DateOnly.TryParseExact(businessDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var bd))
        return Results.BadRequest("bad date");
    var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi");
    var startUtc = TimeZoneInfo.ConvertTimeToUtc(bd.ToDateTime(TimeOnly.MinValue), tz);
    var endUtc = startUtc.AddDays(1);
    var rows = await db.Transactions.Where(t => t.CreatedAt >= startUtc && t.CreatedAt < endUtc).ToListAsync();
    var sb = new StringBuilder();
    sb.AppendLine($"HEADER,1,{billerCode ?? "MOCK"},{businessDate},{DateTimeOffset.UtcNow:O}");
    long total = 0;
    var detailLines = new List<string>();
    foreach (var t in rows)
    {
        detailLines.Add($"{t.BillerReference},{t.ClientReference},{t.ConsumerNumber},{t.AmountMinorUnits},PKR,{t.Status},{t.CreatedAt:O}");
        if (t.Status == "Success") total += t.AmountMinorUnits;
    }
    foreach (var d in detailLines) sb.AppendLine("DETAIL," + d);
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", detailLines)))).ToLowerInvariant();
    sb.AppendLine($"TRAILER,{detailLines.Count},{total},{hash}");
    return Results.Text(sb.ToString(), "text/csv");
});

// Test-only mutation endpoint (Development/Test only)
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Test")
{
    app.MapPost("/api/test/settlement-mutations", () => Results.Ok(new { status = "ok" }));
}

app.Run();
