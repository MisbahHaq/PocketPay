using System.Net.Http.Json;
using WalletPlatform.Application.Common;

namespace WalletPlatform.Infrastructure.Services;

public sealed class BillerGateway(HttpClient http) : IBillerGateway
{
    public async Task<BillerResult> SubmitPaymentAsync(Guid paymentId, string billerCode, string consumerNumber, long amountMinorUnits, string correlationId, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
            {
                Content = JsonContent.Create(new { BillerCode = billerCode, ConsumerNumber = consumerNumber, AmountMinorUnits = amountMinorUnits })
            };
            req.Headers.Add("Idempotency-Key", paymentId.ToString());
            req.Headers.Add("X-Correlation-Id", correlationId);
            var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>(ct);
                var br = body != null && body.TryGetValue("billerReference", out var v) ? v?.ToString() : null;
                return new BillerResult(BillerOutcome.Success, br, null, null);
            }
            if ((int)resp.StatusCode == 422)
            {
                var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>(ct);
                var code = body != null && body.TryGetValue("code", out var v) ? v?.ToString() : "DECLINED";
                return new BillerResult(BillerOutcome.Declined, null, code, code);
            }
            return new BillerResult(BillerOutcome.Unknown, null, null, $"HTTP {(int)resp.StatusCode}");
        }
        catch (TaskCanceledException) { return new BillerResult(BillerOutcome.Unknown, null, null, "timeout"); }
        catch (HttpRequestException ex) { return new BillerResult(BillerOutcome.Unknown, null, null, ex.Message); }
        catch (IOException ex) { return new BillerResult(BillerOutcome.Unknown, null, null, ex.Message); }
    }

    public async Task<BillerResult> GetStatusAsync(Guid paymentId, CancellationToken ct)
    {
        try
        {
            var resp = await http.GetAsync($"/api/payments/{paymentId}", ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return new BillerResult(BillerOutcome.Unknown, null, null, "no record");
            if (!resp.IsSuccessStatusCode) return new BillerResult(BillerOutcome.Unknown, null, null, $"HTTP {(int)resp.StatusCode}");
            var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>(ct);
            var status = body != null && body.TryGetValue("status", out var v) ? v?.ToString() : null;
            var br = body != null && body.TryGetValue("billerReference", out var v2) ? v2?.ToString() : null;
            return status == "Success" ? new BillerResult(BillerOutcome.Success, br, null, null)
                : status == "Declined" ? new BillerResult(BillerOutcome.Declined, br, "DECLINED", null)
                : new BillerResult(BillerOutcome.Unknown, br, null, status);
        }
        catch { return new BillerResult(BillerOutcome.Unknown, null, null, "lookup failed"); }
    }

    public async Task<(bool Valid, string? ConsumerName, long? AmountDueMinorUnits)> ValidateConsumerAsync(string billerCode, string consumerNumber, CancellationToken ct)
    {
        try
        {
            var resp = await http.PostAsJsonAsync("/api/consumers/validate", new { ConsumerNumber = consumerNumber }, ct);
            if (!resp.IsSuccessStatusCode) return (false, null, null);
            var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>(ct);
            var valid = body != null && body.TryGetValue("valid", out var v) && v is true;
            var name = body != null && body.TryGetValue("consumerName", out var n) ? n?.ToString() : null;
            long? due = null;
            if (body != null && body.TryGetValue("amountDueMinorUnits", out var d) && long.TryParse(d?.ToString(), out var dd)) due = dd;
            return (valid, name, due);
        }
        catch { return (false, null, null); }
    }
}
