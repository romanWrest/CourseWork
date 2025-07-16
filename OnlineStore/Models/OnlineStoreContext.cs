using Microsoft.EntityFrameworkCore;
using OnlineStore.Models;

namespace OnlineStore.Models
{
    public class OnlineStoreContext : DbContext
    {
        public OnlineStoreContext(DbContextOptions<OnlineStoreContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, Username = "admin", PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"), Role = "Admin" },
                new User { Id = 2, Username = "user", PasswordHash = BCrypt.Net.BCrypt.HashPassword("user"), Role = "User" }
            );

            modelBuilder.Entity<Product>().HasData(
                new Product { Id = 1, Name = "Ноутбук", Price = 1499m, Description = "Последнее слово техники" },
                new Product { Id = 2, Name = "Мышь", Price = 149m, Description = "Беспроводная" }
            );
        }
    }
}