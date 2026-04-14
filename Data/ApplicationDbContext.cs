using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nexus_backend.Models;

namespace Nexus_backend.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Database Tables (DbSets)
        public DbSet<Entrepreneur> Entrepreneurs { get; set; }
        public DbSet<Investor> Investors { get; set; }
        public DbSet<CollaborationRequest> CollaborationRequests { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Meeting> Meetings { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ==============================================
            // Entrepreneur Configuration (One-to-One with ApplicationUser)
            // ==============================================
            builder.Entity<Entrepreneur>()
                .HasOne(e => e.User)
                .WithOne(u => u.Entrepreneur)
                .HasForeignKey<Entrepreneur>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ==============================================
            // Investor Configuration (One-to-One with ApplicationUser)
            // ==============================================
            builder.Entity<Investor>()
                .HasOne(i => i.User)
                .WithOne(u => u.Investor)
                .HasForeignKey<Investor>(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ==============================================
            // CollaborationRequest Configuration
            // ==============================================
            builder.Entity<CollaborationRequest>()
                .HasOne(r => r.Investor)
                .WithMany()
                .HasForeignKey(r => r.InvestorId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<CollaborationRequest>()
                .HasOne(r => r.Entrepreneur)
                .WithMany()
                .HasForeignKey(r => r.EntrepreneurId)
                .OnDelete(DeleteBehavior.Cascade);

            // ==============================================
            // Message Configuration
            // ==============================================
            builder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            // ==============================================
            // Document Configuration
            // ==============================================
            builder.Entity<Document>()
                .HasOne(d => d.Owner)
                .WithMany()
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            // ==============================================
            // Meeting Configuration
            // ==============================================
            builder.Entity<Meeting>()
                .HasOne(m => m.Organizer)
                .WithMany()
                .HasForeignKey(m => m.OrganizerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Meeting>()
                .HasOne(m => m.Participant)
                .WithMany()
                .HasForeignKey(m => m.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);

            // ==============================================
            // Transaction Configuration
            // ==============================================
            builder.Entity<Transaction>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Transaction>()
                .HasOne(t => t.ToUser)
                .WithMany()
                .HasForeignKey(t => t.ToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ==============================================
            // Notification Configuration
            // ==============================================
            builder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ==============================================
            // JSON Serialization for List<string> properties in Investor
            // ==============================================
            builder.Entity<Investor>()
                .Property(i => i.InvestmentInterests)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

            builder.Entity<Investor>()
                .Property(i => i.InvestmentStage)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

            builder.Entity<Investor>()
                .Property(i => i.PortfolioCompanies)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
        }
    }
}