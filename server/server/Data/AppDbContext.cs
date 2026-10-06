using Microsoft.EntityFrameworkCore;
namespace server.Data
{
    public class AppDbContext:DbContext
    {
        public AppDbContext(DbContextOptions options): base(options)
        {
            
        }
    }
}
