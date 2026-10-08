using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Data;
using server.DTOs.AuthDTO;
using server.Helpers;
using server.Models;

namespace server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly JwtHelper _jwtHelper;

        public AuthController(AppDbContext dbContext, JwtHelper jwtHelper)
        {
            _dbContext = dbContext;
            _jwtHelper = jwtHelper;
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
                .Where(ur => ur.UserId == user.UserId)
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


        [HttpPost("register")]
        public async Task<ActionResult> Register(RegisterDTO userDTO)
        {
            //unique email
            if (await _dbContext.Users.AnyAsync(u => u.UserEmail == userDTO.Email))
            {
                return BadRequest("User Already Exists");
            }


            var user = new User
            {
                UserEmail = userDTO.Email,
                UserName = userDTO.UserName,
                HashedPassword = HashPassword(userDTO.Password),
            };

            await _dbContext.Users.AddAsync(user);

            var defaultRole = await _dbContext.Roles.FirstOrDefaultAsync(role => role.RoleName == "Developer");

            if (defaultRole != null) {
                var newUserRole = new UserRole
                {
                    User = user,
                    Role = defaultRole
                };
                await _dbContext.UserRoles.AddAsync(newUserRole);
            }


            await _dbContext.SaveChangesAsync();

            return Ok(user);
        }




        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult> GetCurrentUser()
        {
            //token -> userid
            var userid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

            

            if(userid == null)
            {
                return Unauthorized();
            }

            int id = int.Parse(userid.Value);

            var user = await _dbContext.Users.FirstOrDefaultAsync(user => user.UserId == id);
            return  Ok(user);
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
