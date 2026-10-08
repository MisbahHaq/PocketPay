using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Authorization;
using server.Data;
using server.DTOs.AuditDTO;
using server.Helpers;
using server.Services;

namespace server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuditLogsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;

        public AuditLogsController(AppDbContext db, ICurrentUser current)
        {
            _db = db;
            _current = current;
        }

        [Permission(PermissionKeys.ManageOrganization)]
        [HttpGet]
        public async Task<ActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = _db.AuditLogs.AsNoTracking()
                .Where(a => a.OrganizationId == orgId)
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.AuditLogId);

            var total = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AuditLogResponseDTO
                {
                    AuditLogId = a.AuditLogId,
                    Action = a.Action,
                    ActorName = a.Actor != null ? a.Actor.UserName : null,
                    TargetType = a.TargetType,
                    TargetId = a.TargetId,
                    Details = a.Details,
                    CreatedAt = a.CreatedAt,
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }
    }
}
