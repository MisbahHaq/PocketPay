using System.Data;
using System.Net.NetworkInformation;
using Microsoft.EntityFrameworkCore;
using server.Models;

namespace server.Data
{
    public class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext _dbContext)
        {
            await _dbContext.Database.MigrateAsync();

            //Seed Roles
            if (!_dbContext.Roles.Any())
            {
                var roles = new List<Role> { 
                    new Role{ RoleName = "Admin"},
                    new Role{ RoleName = "Developer"},
                    new Role{RoleName = "Manager",
                    }
                };

                await _dbContext.AddRangeAsync(roles);
                await _dbContext.SaveChangesAsync();   
            }

            //Seed Users
            if (!_dbContext.Users.Any())
            {
                var admin = new User
                {
                    UserName = "Admin",
                    UserEmail= "admin@system.com"
                };
                await _dbContext.AddAsync(admin);

                var adminRole = await _dbContext.Roles.FirstOrDefaultAsync(role => role.RoleName == "Admin");
             

                //var newRole = new UserRole { 
                //   UserId = admin.UserId,
                //   RoleId = adminRole!.RoleId
                //};

                var newRole = new UserRole
                {
                    User = admin,
                    Role = adminRole
                };

                await _dbContext.UserRoles.AddAsync(newRole);
                await _dbContext.SaveChangesAsync();

            }

        }
    }
}
