using IotWelt.API.Models;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<RaumKlimaLog> RaumKlimaLogs => Set<RaumKlimaLog>();
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
}
