using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletPlatform.Api.Authorization;
using WalletPlatform.Application.Ledger;
using WalletPlatform.Application.Ledger.GetWalletBalance;
using WalletPlatform.Application.Ledger.GetWalletStatement;

namespace WalletPlatform.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
[Route("api/wallets")]
public sealed class WalletsController(
    GetWalletBalanceHandler getWalletBalanceHandler,
    GetWalletStatementHandler getWalletStatementHandler) : ControllerBase
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
}
