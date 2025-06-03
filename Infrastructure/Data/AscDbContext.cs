using Domain;
using Microsoft.EntityFrameworkCore;
using System;

namespace Infrastructure.Data
{
    public class AscDbContext : DbContext, IDisposable
    {
        public AscDbContext(DbContextOptions<AscDbContext> options) : base(options) { }

        public virtual DbSet<User> Users { get; set; } 

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity => entity.HasKey(e => e.EmployeeId));
        }
    }
}