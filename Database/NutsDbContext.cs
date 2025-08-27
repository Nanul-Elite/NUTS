using Microsoft.EntityFrameworkCore;

namespace NUTS.Database
{
    public class NutsDbContext : DbContext
    {
        public DbSet<TargetData> TargetDatas { get; set; }
        public DbSet<KillRecord> KillRecords { get; set; }
        public DbSet<LeaderboardPost> LeaderboardPosts { get; set; }
        public DbSet<Settings> Settings { get; set; }

        public NutsDbContext(DbContextOptions<NutsDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}