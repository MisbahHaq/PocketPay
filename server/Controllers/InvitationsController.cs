using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Authorization;
using server.Data;
using server.DTOs.AuthDTO;
using server.DTOs.InvitationDTO;
using server.Helpers;
using server.Models;
using server.Services;

namespace server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvitationsController : ControllerBase
    {
        private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

        private readonly AppDbContext _db;
        private readonly ICurrentUser _current;
        private readonly IAuditLogService _audit;
        private readonly JwtHelper _jwtHelper;
        private readonly IConfiguration _config;

        public InvitationsController(
            AppDbContext db,
            ICurrentUser current,
            IAuditLogService audit,
            JwtHelper jwtHelper,
            IConfiguration config)
        {
            _db = db;
            _current = current;
            _audit = audit;
            _jwtHelper = jwtHelper;
            _config = config;
        }

        //send an invitation
        [Permission(PermissionKeys.InviteUsers)]
        [HttpPost]
        public async Task<ActionResult> Create(CreateInvitationDTO dto)
        {
            var user = await _current.RequireOrgUserAsync();
            var orgId = user.OrganizationId!.Value;

            var email = dto.Email.Trim();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                return BadRequest("A valid email address is required.");

            var role = await _db.Roles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoleId == dto.RoleId && r.OrganizationId == orgId);
            if (role is null) return BadRequest("Pick a role from this organization.");

            var alreadyMember = await _db.Users.AnyAsync(u =>
                u.UserEmail == email && u.OrganizationId == orgId);
            if (alreadyMember)
                return BadRequest("That person is already a member of this organization.");

            //one live invitation per email
            var existing = await _db.Invitations
                .Where(i => i.OrganizationId == orgId && i.Email == email && i.Status == InvitationStatus.Pending)
                .ToListAsync();
            foreach (var stale in existing)
            {
                stale.Status = InvitationStatus.Revoked;
            }

            var invitation = new Invitation
            {
                OrganizationId = orgId,
                Email = email,
                RoleId = role.RoleId,
                InvitedById = user.UserId,
                Token = GenerateToken(),
                Status = InvitationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(Lifetime),
            };
            _db.Invitations.Add(invitation);
            await _db.SaveChangesAsync();

            await _audit.WriteAsync("invitation.sent", "Invitation", invitation.InvitationId.ToString(),
                new { email, role = role.RoleName, expiresAt = invitation.ExpiresAt });

            return Ok(new InvitationCreatedDTO
            {
                Invitation = await ToDtoAsync(invitation.InvitationId),
                AcceptUrl = BuildAcceptUrl(invitation.Token),
            });
        }

        //pending / past invitations for this organization
        [Permission(PermissionKeys.InviteUsers)]
        [HttpGet]
        public async Task<ActionResult> List()
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;

            var invitations = await _db.Invitations.AsNoTracking()
                .Include(i => i.Role)
                .Include(i => i.InvitedBy)
                .Where(i => i.OrganizationId == orgId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var result = invitations.Select(i => new InvitationResponseDTO
            {
                InvitationId = i.InvitationId,
                Email = i.Email,
                RoleName = i.Role?.RoleName ?? "",
                InvitedBy = i.InvitedBy?.UserName ?? "",
                Status = EffectiveStatus(i),
                CreatedAt = i.CreatedAt,
                ExpiresAt = i.ExpiresAt,
            });

            return Ok(result);
        }

        //fresh accept link for a pending invitation (for copying into chat/email)
        [Permission(PermissionKeys.InviteUsers)]
        [HttpGet("{id:int}/link")]
        public async Task<ActionResult> Link(int id)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var invitation = await _db.Invitations.AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvitationId == id && i.OrganizationId == orgId);

            if (invitation is null) return NotFound();
            if (EffectiveStatus(invitation) != InvitationStatus.Pending)
                return BadRequest("That invitation is no longer pending.");

            return Ok(new { acceptUrl = BuildAcceptUrl(invitation.Token) });
        }

        //public: what does this invite link point at?
        [AllowAnonymous]
        [HttpGet("lookup")]
        public async Task<ActionResult> Lookup([FromQuery] string token)
        {
            var invitation = await _db.Invitations.AsNoTracking()
                .Include(i => i.Organization)
                .Include(i => i.Role)
                .FirstOrDefaultAsync(i => i.Token == token);

            if (invitation is null) return NotFound("Invitation not found.");

            return Ok(new InvitationLookupDTO
            {
                Email = invitation.Email,
                OrganizationName = invitation.Organization?.Name ?? "",
                RoleName = invitation.Role?.RoleName ?? "",
                Status = EffectiveStatus(invitation),
                ExpiresAt = invitation.ExpiresAt,
                HasAccount = await _db.Users.AnyAsync(u => u.UserEmail == invitation.Email),
            });
        }

        //accept: create/activate the account and join the organization
        [AllowAnonymous]
        [HttpPost("accept")]
        public async Task<ActionResult> Accept(AcceptInvitationDTO dto)
        {
            var invitation = await _db.Invitations
                .Include(i => i.Organization)
                .FirstOrDefaultAsync(i => i.Token == dto.Token);

            if (invitation is null) return NotFound("Invitation not found.");

            var status = EffectiveStatus(invitation);
            if (status == InvitationStatus.Revoked) return BadRequest("This invitation was revoked.");
            if (status == InvitationStatus.Accepted) return BadRequest("This invitation has already been used.");
            if (status == InvitationStatus.Expired) return BadRequest("This invitation has expired.");

            var existing = await _db.Users.FirstOrDefaultAsync(u => u.UserEmail == invitation.Email);

            if (existing is not null)
            {
                var current = await _current.LoadAsync();
                if (current is null || current.UserId != existing.UserId)
                    return StatusCode(StatusCodes.Status403Forbidden,
                        $"Sign in as {invitation.Email} to accept this invitation.");

                if (existing.OrganizationId is not null)
                    return BadRequest("This account already belongs to an organization.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(dto.UserName) || string.IsNullOrWhiteSpace(dto.Password))
                    return BadRequest("Choose a username and password to create your account.");
                if (dto.Password.Length < 6)
                    return BadRequest("Password must be at least 6 characters.");
                if (await _db.Users.AnyAsync(u => u.UserName == dto.UserName))
                    return BadRequest("Username is already taken.");

                existing = new User
                {
                    UserName = dto.UserName.Trim(),
                    UserEmail = invitation.Email,
                    HashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                    Status = UserStatus.Active,
                };
                _db.Users.Add(existing);
            }

            existing.OrganizationId = invitation.OrganizationId;
            existing.Status = UserStatus.Active;

            _db.UserRoles.Add(new UserRole { User = existing, RoleId = invitation.RoleId });

            invitation.Status = InvitationStatus.Accepted;
            invitation.AcceptedAt = DateTime.UtcNow;
            invitation.AcceptedByUserId = existing.UserId;

            await _db.SaveChangesAsync();

            var roles = new List<string> { (await _db.Roles.AsNoTracking().FirstAsync(r => r.RoleId == invitation.RoleId)).RoleName };

            await _audit.WriteForAsync(invitation.OrganizationId, existing.UserId,
                "invitation.accepted", "Invitation", invitation.InvitationId.ToString(),
                new { invitation.Email, role = roles[0] });

            var token = _jwtHelper.GenerateToken(existing, roles);

            return Ok(new AuthResponseDTO
            {
                Token = token,
                Username = existing.UserName,
                Email = existing.UserEmail,
                ExpiresIn = DateTime.UtcNow.AddMinutes(120),
                Roles = roles,
            });
        }

        //revoke
        [Permission(PermissionKeys.InviteUsers)]
        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Revoke(int id)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var invitation = await _db.Invitations
                .FirstOrDefaultAsync(i => i.InvitationId == id && i.OrganizationId == orgId);

            if (invitation is null) return NotFound();
            if (invitation.Status == InvitationStatus.Accepted)
                return BadRequest("That invitation has already been accepted.");

            invitation.Status = InvitationStatus.Revoked;
            await _db.SaveChangesAsync();

            await _audit.WriteAsync("invitation.revoked", "Invitation", invitation.InvitationId.ToString(),
                new { invitation.Email });

            return Ok(new { message = "Invitation revoked." });
        }

        //new secure link (old one stops working)
        [Permission(PermissionKeys.InviteUsers)]
        [HttpPost("{id:int}/reissue")]
        public async Task<ActionResult> Reissue(int id)
        {
            var orgId = (await _current.RequireOrgUserAsync()).OrganizationId!.Value;
            var invitation = await _db.Invitations
                .FirstOrDefaultAsync(i => i.InvitationId == id && i.OrganizationId == orgId);

            if (invitation is null) return NotFound();
            if (invitation.Status == InvitationStatus.Accepted)
                return BadRequest("That invitation has already been accepted.");

            invitation.Token = GenerateToken();
            invitation.Status = InvitationStatus.Pending;
            invitation.ExpiresAt = DateTime.UtcNow.Add(Lifetime);
            invitation.CreatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _audit.WriteAsync("invitation.reissued", "Invitation", invitation.InvitationId.ToString(),
                new { invitation.Email, expiresAt = invitation.ExpiresAt });

            return Ok(new { acceptUrl = BuildAcceptUrl(invitation.Token) });
        }

        private async Task<InvitationResponseDTO> ToDtoAsync(int invitationId)
        {
            var i = await _db.Invitations.AsNoTracking()
                .Include(x => x.Role)
                .Include(x => x.InvitedBy)
                .FirstAsync(x => x.InvitationId == invitationId);

            return new InvitationResponseDTO
            {
                InvitationId = i.InvitationId,
                Email = i.Email,
                RoleName = i.Role?.RoleName ?? "",
                InvitedBy = i.InvitedBy?.UserName ?? "",
                Status = EffectiveStatus(i),
                CreatedAt = i.CreatedAt,
                ExpiresAt = i.ExpiresAt,
            };
        }

        private string BuildAcceptUrl(string token) =>
            $"{_config["ClientUrl"] ?? "http://localhost:5173"}/invite/{token}";

        private static string EffectiveStatus(Invitation invitation)
        {
            if (invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt < DateTime.UtcNow)
                return InvitationStatus.Expired;
            return invitation.Status;
        }

        private static string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }
    }
}
