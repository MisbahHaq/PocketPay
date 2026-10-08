using System.Text.Json;
using server.Services;

namespace server.Services
{
    public interface IAuditLogService
    {
        Task WriteAsync(string action, string? targetType = null, string? targetId = null, object? details = null);

        //for flows where the acting user's organization is being assigned in the same request
        Task WriteForAsync(int organizationId, int actorUserId, string action, string? targetType = null, string? targetId = null, object? details = null);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly Data.AppDbContext _db;
        private readonly ICurrentUser _current;

        public AuditLogService(Data.AppDbContext db, ICurrentUser current)
        {
            _db = db;
            _current = current;
        }

        public async Task WriteAsync(string action, string? targetType = null, string? targetId = null, object? details = null)
        {
            var orgId = _current.OrgId;
            var actorId = _current.UserId;
            await AppendAsync(orgId, actorId, action, targetType, targetId, details);
        }

        public Task WriteForAsync(int organizationId, int actorUserId, string action, string? targetType = null, string? targetId = null, object? details = null)
        {
            return AppendAsync(organizationId, actorUserId, action, targetType, targetId, details);
        }

        private async Task AppendAsync(int? organizationId, int? actorUserId, string action, string? targetType, string? targetId, object? details)
        {
            _db.AuditLogs.Add(new Models.AuditLog
            {
                OrganizationId = organizationId,
                ActorUserId = actorUserId,
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                Details = details is null ? null : JsonSerializer.Serialize(details),
                CreatedAt = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync();
        }
    }
}
