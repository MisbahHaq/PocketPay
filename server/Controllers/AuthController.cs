using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Data;
using server.DTOs.AuthDTO;
using server.Helpers;
using server.Models;
using server.Services;

namespace server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly JwtHelper _jwtHelper;
        private readonly ICurrentUser _current;

        public AuthController(AppDbContext dbContext, JwtHelper jwtHelper, ICurrentUser current)
        {
            _dbContext = dbContext;
            _jwtHelper = jwtHelper;
            _current = current;
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login(LoginDTO loginDTO) {
            var user = await _dbContext.Users.FirstOrDefaultAsync(user => user.UserName == loginDTO.UserName);

            if (user == null) {
                return BadRequest("User does not exist");
            }


            if (!VerifyPassword(loginDTO.Password, user.HashedPassword)) {
                return BadRequest("Username or Password is Invalid");
            }

            var roles = await _dbContext.UserRoles
                .Where(ur => ur.UserId == user.UserId && ur.Role!.OrganizationId == user.OrganizationId)
                .Select(ur => ur.Role!.RoleName)
                .ToListAsync();
            string token = _jwtHelper.GenerateToken(user, roles);

            var response = new AuthResponseDTO
            {
                Token=token,
                Username = user.UserName,
                Email=user.UserEmail,
                ExpiresIn = DateTime.UtcNow.AddMinutes(120),
                Roles = roles
            };

            return Ok(response);
        }


        //open account creation: the account starts without an organization.
        //users only join an organization through an invitation or by creating their own.
        [HttpPost("register")]
        public async Task<ActionResult> Register(RegisterDTO userDTO)
        {
            if (await _dbContext.Users.AnyAsync(u => u.UserEmail == userDTO.Email))
            {
                return BadRequest("User Already Exists");
            }

            if (string.IsNullOrWhiteSpace(userDTO.Password) || userDTO.Password.Length < 6)
            {
                return BadRequest("Password must be at least 6 characters");
            }

            var user = new User
            {
                UserEmail = userDTO.Email,
                UserName = userDTO.UserName,
                HashedPassword = HashPassword(userDTO.Password),
                Status = UserStatus.Active,
            };

            await _dbContext.Users.AddAsync(user);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Registration successful", user.UserId });
        }




        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult> GetCurrentUser()
        {
            var user = await _current.LoadAsync();
            if (user is null) return Unauthorized();

            string? organizationName = null;
            var roles = new List<string>();

            if (user.OrganizationId is not null)
            {
                organizationName = await _dbContext.Organizations.AsNoTracking()
                    .Where(o => o.OrganizationId == user.OrganizationId)
                    .Select(o => o.Name)
                    .FirstOrDefaultAsync();

                roles = await (
                    from ur in _dbContext.UserRoles.AsNoTracking()
                    join r in _dbContext.Roles.AsNoTracking() on ur.RoleId equals r.RoleId
                    where ur.UserId == user.UserId && r.OrganizationId == user.OrganizationId
                    select r.RoleName
                ).ToListAsync();
            }

            return Ok(new
            {
                user.UserId,
                user.UserName,
                user.UserEmail,
                user.Status,
                Organization = organizationName == null
                    ? null
                    : new { user.OrganizationId, Name = organizationName },
                Roles = roles,
                Permissions = _current.Permissions.ToList(),
            });
        }

        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        private bool VerifyPassword(string text,string hash)
        {
            return BCrypt.Net.BCrypt.Verify(text, hash);
        }
    }
}
