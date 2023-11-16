using Microsoft.EntityFrameworkCore;

namespace user_api_minimal.Models
{
    public class UserDB : DbContext
    {
        public UserDB(DbContextOptions<UserDB> options) : base(options) { }
        public DbSet<User> Users => Set<User>();
    }
}
