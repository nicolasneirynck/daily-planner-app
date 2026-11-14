using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Planning.Domain.PlannedActivities;
using Planning.Domain.Products;
using Planning.Domain.Projects;

namespace Planning.Persistence;
/// <summary>
/// Seeds the database
/// </summary>
/// <param name="dbContext"></param>
/// <param name="roleManager"></param>
/// <param name="userManager"></param>
public class DbSeeder(ApplicationDbContext dbContext, RoleManager<IdentityRole> roleManager, UserManager<IdentityUser> userManager)
{
    const string PasswordDefault = "A1b2C3!";
    public async Task SeedAsync()
    {
        await RolesAsync();
        await UsersAsync();
        await ProductsAsync();
        await ProjectsAsync();
    }

    private async Task RolesAsync()
    {
        foreach (var role in new[] { "Administrator", "Technician", "User" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
    
    private async Task  UsersAsync()
    {
        if (dbContext.Users.Any())
        {
            var existingUser = await userManager.FindByEmailAsync("user@example.com");
            if (existingUser is not null && !await userManager.IsInRoleAsync(existingUser, "User"))
                await userManager.AddToRoleAsync(existingUser, "User");
            return;
        }
        
        await dbContext.Roles.ToListAsync();

        var admin = new IdentityUser
        {
            UserName = "admin@example.com",
            Email = "admin@example.com",
            EmailConfirmed = true,
        };
        await userManager.CreateAsync(admin, PasswordDefault);
        
        var technicianAccount1 = new IdentityUser
        {
            UserName = "technician1@example.com",
            Email = "technician1@example.com",
            EmailConfirmed = true,
        };
        await userManager.CreateAsync(technicianAccount1, PasswordDefault);
        
        var technicianAccount2 = new IdentityUser
        {
            UserName = "technician2@example.com",
            Email = "technician2@example.com",
            EmailConfirmed = true,
        };
        await userManager.CreateAsync(technicianAccount2, PasswordDefault);
                
        var user = new IdentityUser
        {
            UserName = "user@example.com",
            Email = "user@example.com",
            EmailConfirmed = true,
        };
        await userManager.CreateAsync(user, PasswordDefault);
        
        await userManager.AddToRoleAsync(admin, "Administrator");
        await userManager.AddToRoleAsync(technicianAccount1, "Technician");
        await userManager.AddToRoleAsync(technicianAccount2, "Technician");
        await userManager.AddToRoleAsync(user, "User");

        dbContext.Technicians.AddRange(
            new Technician("Tech 1", "Awesome", technicianAccount1.Id),
            new Technician("Tech 2", "Less Awesome", technicianAccount2.Id));
        
        await dbContext.SaveChangesAsync();
    }
    

    
    private async Task  ProductsAsync()
    {
        if (dbContext.Products.Any())
            return;
        
        dbContext.Products.AddRange(
            new Product{ Name = "Laptop", Description = "15-inch display, 16GB RAM" },
            new Product{ Name = "Smartphone", Description = "6.5-inch screen, 128GB storage" },
            new Product{ Name = "Headphones", Description = "Wireless noise-cancelling" },
            new Product{ Name = "Keyboard", Description = "Mechanical RGB backlit" },
            new Product{ Name = "Mouse", Description = "Ergonomic wireless mouse" },
            new Product{ Name = "Monitor", Description = "27-inch 4K UHD display" },
            new Product{ Name = "Printer", Description = "All-in-one inkjet printer" },
            new Product{ Name = "Camera", Description = "Mirrorless 24MP with 4K video" },
            new Product{ Name = "Smartwatch", Description = "Heart rate monitor, GPS" },
            new Product{ Name = "Speaker", Description = "Bluetooth portable speaker" }
        );

        await dbContext.SaveChangesAsync();
    }
    
    private async Task  ProjectsAsync()
    {
        if (dbContext.Projects.Any())
            return;
        
        var technicians = await dbContext.Technicians.ToListAsync();
        
        if (!technicians.Any())
            return;
        
        var addresses = new List<Address>
        {
            new Address("Koningstraat 12", "Bus 3A", "Brussel", "1000"),
            new Address("Meir 45", "", "Antwerpen", "2000"),
            new Address("Veldstraat 78", "2e verdieping", "Gent", "9000"),
            new Address("Rue de la Loi 175", "", "Bruxelles", "1040"),
            new Address("Place Saint-Lambert 8", "Bureau 12", "Liège", "4000"),
        };

        var rnd = new Random(123); // Using a seed so the random is always the same.
        
        var projects = new List<Project>
        {
            new("Website Redesign", technicians[rnd.Next(technicians.Count)], addresses[0]),
            new("Mobile App Development", technicians[rnd.Next(technicians.Count)], addresses[1]),
            new("Database Migration", technicians[rnd.Next(technicians.Count)], addresses[2]),
            new("E-commerce Platform", technicians[rnd.Next(technicians.Count)], addresses[3]),
            new("CRM Integration", technicians[rnd.Next(technicians.Count)], addresses[4])
        };

        dbContext.Projects.AddRange(projects);
        await dbContext.SaveChangesAsync();
    }

    public async Task SeedPlannedActivitiesAsync()
    {
        var user = await userManager.FindByEmailAsync("user@example.com");
        if (user is null)
            return;

        var blocks = await dbContext.TimeBlocks
            .Where(block => block.UserId == user.Id && !block.IsDeleted)
            .ToDictionaryAsync(block => block.SortOrder, block => block.Id);

        if (!Enumerable.Range(1, 6).All(blocks.ContainsKey))
            return;

        string[][] activityNames =
        [
            ["Ochtendwandeling", "Sporten", "Ontbijten", "De dag voorbereiden"],
            ["Administratie bijwerken", "Weekplanning maken", "E-mails beantwoorden", "Concentratiewerk"],
            ["Lunch met een vriend", "Lunchpauze", "Korte wandeling", "Boodschappenlijst maken"],
            ["Boodschappen doen", "Familiebezoek", "Afspraak bij de kapper", "Buiten wandelen"],
            ["Huis opruimen", "Koken", "De was doen", "Planten water geven"],
            ["Boek lezen", "Film kijken", "Spelletje spelen", "Ontspannen"]
        ];

        var today = DateOnly.FromDateTime(DateTime.Today);
        var existing = (await dbContext.PlannedActivities
            .Where(activity =>
                activity.UserId == user.Id &&
                activity.Date >= today &&
                activity.Date <= today.AddDays(6) &&
                !activity.IsDeleted)
            .Select(activity => new
            {
                activity.Date,
                activity.TimeBlockId,
                activity.Name
            })
            .ToListAsync())
            .ToLookup(activity => (activity.Date, activity.TimeBlockId));

        for (var day = 0; day < 7; day++)
        {
            var date = today.AddDays(day);
            var random = new Random(date.DayNumber);
            var counts = new[] { 0, 1, 2, 3, 4, random.Next(5) };

            for (var i = counts.Length - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                (counts[i], counts[swapIndex]) = (counts[swapIndex], counts[i]);
            }

            for (var block = 1; block <= 6; block++)
            {
                var blockId = blocks[block];
                var existingActivities = existing[(date, blockId)].ToList();
                var existingNames = existingActivities.Select(activity => activity.Name).ToHashSet();
                var namesToAdd = activityNames[block - 1]
                    .Where(name => !existingNames.Contains(name))
                    .Take(Math.Max(0, counts[block - 1] - existingActivities.Count));

                foreach (var name in namesToAdd)
                {
                    dbContext.PlannedActivities.Add(new PlannedActivity
                    {
                        UserId = user.Id,
                        Date = date,
                        TimeBlockId = blockId,
                        Name = name
                    });
                }
            }
        }

        await dbContext.SaveChangesAsync();
    }
}
