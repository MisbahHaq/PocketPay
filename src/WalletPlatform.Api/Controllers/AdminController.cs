using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletPlatform.Api.Authorization;
using WalletPlatform.Application.Admin.FundWallet;
using WalletPlatform.Application.Admin.GetTransfers;
using WalletPlatform.Application.Admin.GetTrialBalance;
using WalletPlatform.Application.Admin.GetUsersPage;
using WalletPlatform.Application.Admin.ReverseJournalEntry;
using WalletPlatform.Application.Admin.ReverseTransfer;
using WalletPlatform.Application.Transfers;

namespace WalletPlatform.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/admin")]
public sealed class AdminController(
    GetUsersPageHandler getUsersPageHandler,
    FundWalletHandler fundWalletHandler,
    ReverseJournalEntryHandler reverseJournalEntryHandler,
    GetTrialBalanceHandler getTrialBalanceHandler,
    GetAdminTransfersHandler getAdminTransfersHandler,
    ReverseTransferHandler reverseTransferHandler) : ControllerBase
{
    [HttpGet("users")]
    [ProducesResponseType<PagedResult<UserSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<UserSummary>>> GetUsers(
        [FromQuery] GetUsersPageQuery query,
        CancellationToken cancellationToken)
    {
        var response = await getUsersPageHandler.HandleAsync(query, cancellationToken);
        return Ok(response);
    }

    [HttpPost("wallets/{walletId:guid}/fund")]
    [ProducesResponseType<FundWalletResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FundWalletResponse>> FundWallet(
        Guid walletId,
        FundWalletRequest request,
        CancellationToken cancellationToken)
    {
        var response = await fundWalletHandler.HandleAsync(
            walletId,
            request,
            User.GetRequiredUserName(),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("journal-entries/{journalEntryId:guid}/reverse")]
    [ProducesResponseType<ReverseJournalEntryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReverseJournalEntryResponse>> ReverseJournalEntry(
        Guid journalEntryId,
        ReverseJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var response = await reverseJournalEntryHandler.HandleAsync(
            journalEntryId,
            request,
            User.GetRequiredUserName(),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet("ledger/trial-balance")]
    [ProducesResponseType<TrialBalanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TrialBalanceResponse>> GetTrialBalance(CancellationToken cancellationToken)
    {
        var response = await getTrialBalanceHandler.HandleAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("transfers")]
    [ProducesResponseType<TransfersPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TransfersPageResponse>> GetTransfers(
        [FromQuery] GetAdminTransfersQuery query,
        CancellationToken cancellationToken)
    {
        var response = await getAdminTransfersHandler.HandleAsync(query, cancellationToken);
        return Ok(response);
    }

    [HttpPost("transfers/{transferId:guid}/reverse")]
    [ProducesResponseType<TransferDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransferDetail>> ReverseTransfer(
        Guid transferId,
        ReverseTransferRequest request,
        CancellationToken cancellationToken)
    {
        var response = await reverseTransferHandler.HandleAsync(
            transferId,
            request,
            User.GetRequiredUserName(),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }
}