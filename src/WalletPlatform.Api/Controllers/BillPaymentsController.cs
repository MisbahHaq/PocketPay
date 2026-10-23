using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletPlatform.Application.Common;
using WalletPlatform.Domain.Entities;
using WalletPlatform.Infrastructure.Services;

namespace WalletPlatform.Api.Controllers;

[ApiController]
[Route("api/bill-payments")]
[Authorize]
public class BillPaymentsController(BillPaymentService svc) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBillPaymentRequest req, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var payment = await svc.AcceptAsync(req.WalletId, req.BillerCode, req.ConsumerNumber, req.AmountMinorUnits, userId, ct);
        return Accepted(new { payment.Id, payment.Status });
    }

    [HttpGet]
    public IActionResult List() => Ok(Array.Empty<object>());

    [HttpGet("{id:guid}")]
    public IActionResult Get(Guid id) => NotFound();
}

public record CreateBillPaymentRequest(Guid WalletId, string BillerCode, string ConsumerNumber, long AmountMinorUnits);
