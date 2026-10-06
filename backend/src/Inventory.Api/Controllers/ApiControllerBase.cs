using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

/// <summary>
/// Base for every controller. Exists so the cross-cutting API conventions are declared
/// once rather than repeated on twenty controllers.
/// </summary>
/// <remarks>
/// Controllers are intentionally almost empty: they bind, they delegate to MediatR, and
/// they translate the result into a status code. Any <c>if</c> statement containing a
/// business rule in a controller is a rule that has escaped the domain layer, because
/// controllers are the one place with no unit tests and no way to reuse the logic.
/// </remarks>
[ApiController]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public abstract class ApiControllerBase : ControllerBase
{
}