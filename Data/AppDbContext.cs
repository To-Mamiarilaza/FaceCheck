using Microsoft.EntityFrameworkCore;
using FaceCheck.Models;

namespace FaceCheck.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Person> Persons { get; set; }
        public DbSet<MonthDay> MonthDays { get; set; }
        public DbSet<AttendanceReport> Attendances { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MonthDay>().HasNoKey();
            modelBuilder.HasDbFunction(() => GetMonthDays(default, default)).HasName("GetMonthDays");

            modelBuilder.Entity<AttendanceReport>().HasNoKey();
            modelBuilder.HasDbFunction(() => GetMonthlyAttendanceReport(default, default)).HasName("GetMonthlyAttendanceReport");

        }

        public IQueryable<MonthDay> GetMonthDays(int year, int month)
            => FromExpression(() => GetMonthDays(year, month));

        public IQueryable<AttendanceReport> GetMonthlyAttendanceReport(int year, int month)
            => FromExpression(() => GetMonthlyAttendanceReport(year, month));
    }
}