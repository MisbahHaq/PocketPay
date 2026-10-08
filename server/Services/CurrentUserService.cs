using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using server.Data;
using server.Models;

namespace server.Services
{
    public interface ICurrentUser
    {
        int? UserId { get; }
        User? User { get; }
        int? OrgId { get; }
        IReadOnlySet<string> Permissions { get; }
        Task<User?> LoadAsync();
        Task<User> RequireOrgUserAsync();
        bool Can(string permission);
    }

    //scoped: loads the acting user (and their effective permissions) at most once per request
    public class CurrentUserService : ICurrentUser
    {
        private readonly AppDbContext _db;
        private ClaimsPrincipal? _principal;
        private User? _user;
        private HashSet<string> _permissions = new();
        private bool _loaded;

        public CurrentUserService(AppDbContext db, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _principal = httpContextAccessor.HttpContext?.User;
        }

        public int? UserId
        {
            get
            {
                var raw = _principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                return int.TryParse(raw, out var id) ? id : null;
            }
        }

        public User? User => _user;
        public int? OrgId => _user?.OrganizationId;
        public IReadOnlySet<string> Permissions => _permissions;

        public async Task<User?> LoadAsync()
        {
            if (_loaded) return _user;
            _loaded = true;

            var id = UserId;
            if (id is null) return null;

            _user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == id);
            if (_user is null) return null;

            var orgId = _user.OrganizationId;
            if (orgId is null)
            {
                _permissions = new HashSet<string>();
                return _user;
            }

            var keys = await (
                from rp in _db.RolePermissions.AsNoTracking()
                join r in _db.Roles.AsNoTracking() on rp.RoleId equals r.RoleId
                join ur in _db.UserRoles.AsNoTracking() on r.RoleId equals ur.RoleId
                where ur.UserId == id && r.OrganizationId == orgId
                select rp.Permission!.Key
            ).Distinct().ToListAsync();

            _permissions = new HashSet<string>(keys);
            return _user;
        }

        public async Task<User> RequireOrgUserAsync()
        {
            var user = await LoadAsync();
            if (user is null || user.OrganizationId is null)
                throw new InvalidOperationException("User is not a member of an organization.");
            return user;
        }

        public bool Can(string permission) => _permissions.Contains(permission);
    }
}
