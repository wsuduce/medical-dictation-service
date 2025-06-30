using Microsoft.AspNetCore.Identity;

namespace MedicalDictationService.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        
        // Check if admin user already exists
        var adminUser = await userManager.FindByNameAsync("admin");
        if (adminUser == null)
        {
            // Create admin user
            adminUser = new IdentityUser
            {
                UserName = "admin",
                Email = "admin@medicaldictation.local",
                EmailConfirmed = true
            };
            
            var result = await userManager.CreateAsync(adminUser, "Admin123!");
            
            if (result.Succeeded)
            {
                Console.WriteLine("✅ Admin user created successfully!");
                Console.WriteLine("   Username: admin");
                Console.WriteLine("   Password: Admin123!");
            }
            else
            {
                Console.WriteLine("❌ Failed to create admin user:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"   - {error.Description}");
                }
            }
        }
        else
        {
            Console.WriteLine("ℹ️  Admin user already exists - skipping creation");
        }
    }
} 