using Microsoft.EntityFrameworkCore;
using SimpleNoteNG.Models;

namespace SimpleNoteNG.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Project> Projects { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Укажите вашу строку подключения к SQL Server
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=SimpleNote;Trusted_Connection=True;");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Настройка связи между User и Project (1 ко многим)
            modelBuilder.Entity<Project>()
                .HasOne(p => p.User)          // У проекта есть один пользователь
                .WithMany()                   // У пользователя много проектов
                .HasForeignKey(p => p.UserId)  // Внешний ключ
                .OnDelete(DeleteBehavior.Cascade); // Удаление проектов при удалении пользователя

            // Дополнительные настройки моделей (опционально)

            // Для User
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasDefaultValue("User"); // Значение по умолчанию для Role

            modelBuilder.Entity<User>()
                .Property(u => u.CreatedAt)
                .HasDefaultValueSql("GETDATE()"); // Текущая дата по умолчанию

            // Для Project
            modelBuilder.Entity<Project>()
                .Property(p => p.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Project>()
                .Property(p => p.UpdatedAt)
                .HasDefaultValueSql("GETDATE()");
        }
    }
}