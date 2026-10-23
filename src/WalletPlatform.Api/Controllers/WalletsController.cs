using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletPlatform.Api.Authorization;
using WalletPlatform.Application.Ledger;
using WalletPlatform.Application.Ledger.GetWalletBalance;
using WalletPlatform.Application.Ledger.GetWalletStatement;
using WalletPlatform.Application.Transfers;
using WalletPlatform.Application.Transfers.CreateTransfer;
using WalletPlatform.Application.Transfers.GetTransfer;
using WalletPlatform.Application.Transfers.GetTransfers;
using WalletPlatform.Application.Transfers.LookupWallet;
using WalletPlatform.Domain.Enums;

namespace WalletPlatform.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
[Route("api/wallets")]
public sealed class WalletsController(
    GetWalletBalanceHandler getWalletBalanceHandler,
    GetWalletStatementHandler getWalletStatementHandler,
    LookupWalletHandler lookupWalletHandler,
    CreateTransferHandler createTransferHandler,
    GetTransfersHandler getTransfersHandler,
    GetTransferHandler getTransferHandler) : ControllerBase
{
    [HttpGet("{walletId:guid}/balance")]
    [ProducesResponseType<BalanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BalanceResponse>> GetBalance(
        Guid walletId,
        CancellationToken cancellationToken)
    {
        var query = new GetWalletBalanceQuery(walletId, User.GetUserId());
        var response = await getWalletBalanceHandler.HandleAsync(query, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{walletId:guid}/statement")]
    [ProducesResponseType<StatementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StatementResponse>> GetStatement(
        Guid walletId,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var query = new GetWalletStatementQuery(
            walletId,
            User.GetUserId(),
            page == 0 ? 1 : page,
            pageSize == 0 ? 20 : pageSize);

        var response = await getWalletStatementHandler.HandleAsync(query, cancellationToken);
        return Ok(response);
    }

    [HttpGet("lookup")]
    [ProducesResponseType<WalletLookupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WalletLookupResponse>> LookupWallet(
        [FromQuery] string walletNumber,
        CancellationToken cancellationToken)
    {
        var response = await lookupWalletHandler.HandleAsync(walletNumber, cancellationToken);
        return Ok(response);
    }

    [HttpPost("transfers")]
    [ProducesResponseType<TransferSummary>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransferSummary>> CreateTransfer(
        CreateTransferRequest request,
        CancellationToken cancellationToken)
    {
        var response = await createTransferHandler.HandleAsync(User.GetUserId(), request, cancellationToken);

        // If transfer failed, return 422 with error code in ProblemDetails extensions
        if (response.Status == TransferStatus.Failed)
        {
            var problem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.21",
                Title = "Transfer failed",
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = response.FailureReason,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["code"] = response.FailureReason;
            return new ObjectResult(problem) { StatusCode = StatusCodes.Status422UnprocessableEntity };
        }

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet("transfers")]
    [ProducesResponseType<TransfersPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransfersPageResponse>> GetTransfers(
        [FromQuery] GetTransfersQuery query,
        CancellationToken cancellationToken)
    {
        var response = await getTransfersHandler.HandleAsync(User.GetUserId(), query, cancellationToken);
        return Ok(response);
    }

    [HttpGet("transfers/{transferId:guid}")]
    [ProducesResponseType<TransferDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransferDetail>> GetTransfer(
        Guid transferId,
        CancellationToken cancellationToken)
    {
        var response = await getTransferHandler.HandleAsync(User.GetUserId(), transferId, User.IsInRole("Admin"), cancellationToken);
        return Ok(response);
    }
}