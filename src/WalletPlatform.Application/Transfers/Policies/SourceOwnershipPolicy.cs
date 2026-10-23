using WalletPlatform.Application.Common;
using WalletPlatform.Domain.ValueObjects;

namespace WalletPlatform.Application.Transfers.Policies;

/// <summary>
/// 1. Source wallet must belong to the caller. (Enforced at the handler level by looking
/// up the wallet by id and checking UserId; not a standalone policy class.)
/// </summary>