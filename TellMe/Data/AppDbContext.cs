using Microsoft.EntityFrameworkCore;
using TellMe.Models;

namespace TellMe.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<FacebookMessage> FacebookMessages { get; set; }
    }
}