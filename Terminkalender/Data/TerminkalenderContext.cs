using Microsoft.EntityFrameworkCore;
using Terminkalender.Models;

namespace Terminkalender.Data
{
    public class TerminkalenderContext : DbContext
    {
        public TerminkalenderContext(DbContextOptions<TerminkalenderContext> options)
            : base(options)
        {
        }

        public DbSet<Reservation> Reservations { get; set; }
    }
}
