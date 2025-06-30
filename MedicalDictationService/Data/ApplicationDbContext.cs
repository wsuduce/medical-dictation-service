using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MedicalDictationService.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Future medical dictation entities will be added here
    // Example: public DbSet<Patient> Patients { get; set; }
    // Example: public DbSet<Transcription> Transcriptions { get; set; }
    // Example: public DbSet<Template> Templates { get; set; }
} 