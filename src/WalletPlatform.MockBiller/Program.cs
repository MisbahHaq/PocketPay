using WalletPlatform.MockBiller.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<MockBillerDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("MockBillerDb")));
builder.Services.AddOpenApi();
var app = builder.Build();
app.MapOpenApi();

// validate consumer
app.MapPost("/api/consumers/validate", (ValidateConsumerRequest req) =>
{
    if (!System.Text.RegularExpressions.Regex.IsMatch(req.ConsumerNumber, @"^\d{10,14}$"))
        return Results.BadRequest(new { code = "INVALID_FORMAT" });
    var name = $"Consumer {req.ConsumerNumber[^4..]}";
    var due = 50000L;
    if (req.ConsumerNumber.EndsWith("0000")) return Results.Ok(new { valid = false, code = "INVALID_ACCOUNT" });
    return Results.Ok(new { valid = true, consumerName = name, amountDueMinorUnits = due });
});

// payments
app.MapPost("/api/payments", async (HttpContext ctx, PaymentRequest req, MockBillerDbContext db) =>
{
    var key = ctx.Request.Headers["Idempotency-Key"].ToString();
    if (string.IsNullOrWhiteSpace(key)) return Results.BadRequest(new { code = "IDEMPOTENCY_KEY_REQUIRED" });
    var existing = await db.Transactions.FirstOrDefaultAsync(t => t.ClientReference == key);
    if (existing is not null) return Results.Ok(new { billerReference = existing.BillerReference, status = existing.Status });
    // deterministic behaviors
    var consumer = req.ConsumerNumber;
    if (consumer.EndsWith("0000"))
    {
        var t = new BillerTransaction { BillerReference = $"BLR-{Guid.NewGuid():N}", ClientReference = key, BillerCode = req.BillerCode, ConsumerNumber = consumer, AmountMinorUnits = req.AmountMinorUnits, Status = "Declined" };
        db.Transactions.Add(t); await db.SaveChangesAsync();
        return Results.UnprocessableEntity(new { code = "INVALID_ACCOUNT", billerReference = t.BillerReference });
    }
    if (consumer.EndsWith("1111")) { await Task.Delay(TimeSpan.FromSeconds(10)); }
    if (consumer.EndsWith("3333")) { await Task.Delay(TimeSpan.FromMinutes(10)); }
    if (consumer.EndsWith("2222"))
    {
        // handled by attempt counter not tracked here simply; always succeed after
    }
    if (consumer.EndsWith("4444"))
    {
        var t = new BillerTransaction { BillerReference = $"BLR-{Guid.NewGuid():N}", ClientReference = key, BillerCode = req.BillerCode, ConsumerNumber = consumer, AmountMinorUnits = req.AmountMinorUnits, Status = "Success" };
        db.Transactions.Add(t); await db.SaveChangesAsync();
        // drop connection: return no response by throwing
        throw new IOException("connection dropped");
    }
    await Task.Delay(Random.Shared.Next(50, 300));
    var ok = new BillerTransaction { BillerReference = $"BLR-{Guid.NewGuid():N}", ClientReference = key, BillerCode = req.BillerCode, ConsumerNumber = consumer, AmountMinorUnits = req.AmountMinorUnits, Status = "Success" };
    db.Transactions.Add(ok); await db.SaveChangesAsync();
    return Results.Ok(new { billerReference = ok.BillerReference, status = ok.Status });
});

app.MapGet("/api/payments/{clientReference}", async (string clientReference, MockBillerDbContext db) =>
{
    var t = await db.Transactions.FirstOrDefaultAsync(x => x.ClientReference == clientReference);
    if (t is null) return Results.NotFound();
    return Results.Ok(new { billerReference = t.BillerReference, status = t.Status });
});

app.Run();

public record ValidateConsumerRequest(string ConsumerNumber);
public record PaymentRequest(string BillerCode, string ConsumerNumber, long AmountMinorUnits);
