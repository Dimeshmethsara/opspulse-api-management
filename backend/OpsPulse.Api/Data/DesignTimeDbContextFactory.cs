using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpsPulse.Api.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OpsPulseDbContext>
{
    public OpsPulseDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<OpsPulseDbContext>().UseSqlite("Data Source=storage/opspulse.db").Options);
}
