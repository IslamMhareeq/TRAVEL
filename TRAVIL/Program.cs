using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TRAVEL.Data;
using TRAVEL.Services;

var builder = WebApplication.CreateBuilder(args);

// Add MVC services
builder.Services.AddControllersWithViews();

// Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TravelDbContext>(options =>
    options.UseNpgsql(connectionString, b => b.MigrationsAssembly("TRAVEL"))
);

// Register Services
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITravelPackageService, TravelPackageService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<ICartService, CartService>();
// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JWT");
var secretKeyString = jwtSettings["SecretKey"];
if (string.IsNullOrEmpty(secretKeyString))
    throw new InvalidOperationException("JWT SecretKey is not configured");

var secretKey = Encoding.UTF8.GetBytes(secretKeyString);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("User", "Admin"));
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", corsBuilder =>
    {
        corsBuilder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure HTTP Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// Map Routes
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<TravelDbContext>();
        context.Database.Migrate();  // Only use Migrate(), not EnsureCreated()
        Console.WriteLine("✅ Database initialized successfully.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database error.");
    }
}
Console.WriteLine("🚀 TRAVIL Application starting...");
Console.WriteLine("📍 HTTP: http://localhost:5255");
Console.WriteLine("📍 HTTPS: https://localhost:7298");
app.Run();

// Seed sample data method
static async Task SeedSampleData(TravelDbContext context)
{
    // Check if already seeded
    if (context.TravelPackages.Any())
        return;

    Console.WriteLine("🌱 Seeding sample travel packages...");

    var packages = new List<TRAVEL.Models.TravelPackage>
    {
        // Family Packages
        new() { Destination = "Disneyland Paris", Country = "France", StartDate = DateTime.UtcNow.AddDays(30), EndDate = DateTime.UtcNow.AddDays(37), Price = 2500, AvailableRooms = 20, PackageType = TRAVEL.Models.PackageType.Family, Description = "Magical family adventure at Disneyland Paris with park tickets, character meet & greets, and themed dining experiences.", Itinerary = "Day 1: Arrival & Hotel Check-in, Day 2-4: Park Days, Day 5-6: Paris City Tour, Day 7: Departure", ImageUrl = "https://images.unsplash.com/photo-1533174072545-7a4b6ad7a6c3?w=800", IsActive = true },
        new() { Destination = "Orlando Theme Parks", Country = "USA", StartDate = DateTime.UtcNow.AddDays(45), EndDate = DateTime.UtcNow.AddDays(52), Price = 3200, AvailableRooms = 15, PackageType = TRAVEL.Models.PackageType.Family, Description = "Ultimate family fun at Universal Studios, Disney World, and SeaWorld with VIP fast passes included.", ImageUrl = "https://images.unsplash.com/photo-1575444758702-4a6b9222336e?w=800", IsActive = true },
        new() { Destination = "Tokyo Family Adventure", Country = "Japan", StartDate = DateTime.UtcNow.AddDays(60), EndDate = DateTime.UtcNow.AddDays(68), Price = 4500, AvailableRooms = 12, PackageType = TRAVEL.Models.PackageType.Family, MinimumAge = 5, Description = "Discover Tokyo Disneyland, teamLab Borderless, and authentic Japanese culture perfect for families.", ImageUrl = "https://images.unsplash.com/photo-1536098561742-ca998e48cbcc?w=800", IsActive = true },
        
        // Honeymoon Packages
        new() { Destination = "Maldives Paradise", Country = "Maldives", StartDate = DateTime.UtcNow.AddDays(20), EndDate = DateTime.UtcNow.AddDays(27), Price = 5500, AvailableRooms = 8, PackageType = TRAVEL.Models.PackageType.Honeymoon, MinimumAge = 18, Description = "Romantic overwater bungalow experience with private beach dinners, couples spa, and sunset cruises.", ImageUrl = "https://images.unsplash.com/photo-1514282401047-d79a71a590e8?w=800", IsActive = true },
        new() { Destination = "Santorini Romance", Country = "Greece", StartDate = DateTime.UtcNow.AddDays(35), EndDate = DateTime.UtcNow.AddDays(42), Price = 3800, AvailableRooms = 10, PackageType = TRAVEL.Models.PackageType.Honeymoon, MinimumAge = 18, Description = "Iconic blue domes, sunset views in Oia, wine tasting, and romantic cave hotel accommodation.", ImageUrl = "https://images.unsplash.com/photo-1570077188670-e3a8d69ac5ff?w=800", IsActive = true },
        new() { Destination = "Bali Bliss", Country = "Indonesia", StartDate = DateTime.UtcNow.AddDays(25), EndDate = DateTime.UtcNow.AddDays(33), Price = 2800, DiscountedPrice = 2400, DiscountStartDate = DateTime.UtcNow, DiscountEndDate = DateTime.UtcNow.AddDays(7), AvailableRooms = 15, PackageType = TRAVEL.Models.PackageType.Honeymoon, MinimumAge = 18, Description = "Tropical paradise with private villa, rice terrace views, temple visits, and traditional spa treatments.", ImageUrl = "https://images.unsplash.com/photo-1537996194471-e657df975ab4?w=800", IsActive = true },
        
        // Adventure Packages
        new() { Destination = "Swiss Alps Adventure", Country = "Switzerland", StartDate = DateTime.UtcNow.AddDays(40), EndDate = DateTime.UtcNow.AddDays(48), Price = 4200, AvailableRooms = 12, PackageType = TRAVEL.Models.PackageType.Adventure, MinimumAge = 12, MaximumAge = 60, Description = "Thrilling alpine experience with hiking, paragliding, and scenic train journeys through breathtaking mountains.", ImageUrl = "https://images.unsplash.com/photo-1531366936337-7c912a4589a7?w=800", IsActive = true },
        new() { Destination = "Costa Rica Expedition", Country = "Costa Rica", StartDate = DateTime.UtcNow.AddDays(50), EndDate = DateTime.UtcNow.AddDays(58), Price = 2900, AvailableRooms = 18, PackageType = TRAVEL.Models.PackageType.Adventure, Description = "Rainforest zip-lining, volcano hiking, wildlife spotting, and white water rafting adventure.", ImageUrl = "https://images.unsplash.com/photo-1518495973542-4542c06a5843?w=800", IsActive = true },
        new() { Destination = "New Zealand Explorer", Country = "New Zealand", StartDate = DateTime.UtcNow.AddDays(70), EndDate = DateTime.UtcNow.AddDays(82), Price = 5800, AvailableRooms = 10, PackageType = TRAVEL.Models.PackageType.Adventure, Description = "Middle Earth adventure with bungee jumping, glacier hiking, and Hobbiton visit included.", ImageUrl = "https://images.unsplash.com/photo-1469521669194-babb45599def?w=800", IsActive = true },
        
        // Cruise Packages
        new() { Destination = "Caribbean Cruise", Country = "Caribbean", StartDate = DateTime.UtcNow.AddDays(55), EndDate = DateTime.UtcNow.AddDays(62), Price = 2200, AvailableRooms = 50, PackageType = TRAVEL.Models.PackageType.Cruise, Description = "7-night luxury cruise visiting Jamaica, Bahamas, and private islands with all-inclusive dining.", ImageUrl = "https://images.unsplash.com/photo-1548574505-5e239809ee19?w=800", IsActive = true },
        new() { Destination = "Mediterranean Voyage", Country = "Mediterranean", StartDate = DateTime.UtcNow.AddDays(65), EndDate = DateTime.UtcNow.AddDays(75), Price = 3500, DiscountedPrice = 3100, DiscountStartDate = DateTime.UtcNow, DiscountEndDate = DateTime.UtcNow.AddDays(5), AvailableRooms = 40, PackageType = TRAVEL.Models.PackageType.Cruise, Description = "10-night cruise visiting Barcelona, Rome, Athens, and Dubrovnik with shore excursions.", ImageUrl = "https://images.unsplash.com/photo-1559825481-12a05cc00344?w=800", IsActive = true },
        new() { Destination = "Alaska Glacier Cruise", Country = "USA", StartDate = DateTime.UtcNow.AddDays(80), EndDate = DateTime.UtcNow.AddDays(88), Price = 4100, AvailableRooms = 35, PackageType = TRAVEL.Models.PackageType.Cruise, Description = "Majestic glacier viewing, whale watching, and wilderness exploration along Alaska's coastline.", ImageUrl = "https://images.unsplash.com/photo-1473448912268-2022ce9509d8?w=800", IsActive = true },
        
        // Luxury Packages
        new() { Destination = "Dubai Luxury", Country = "UAE", StartDate = DateTime.UtcNow.AddDays(15), EndDate = DateTime.UtcNow.AddDays(22), Price = 6500, AvailableRooms = 5, PackageType = TRAVEL.Models.PackageType.Luxury, MinimumAge = 18, Description = "5-star Burj Al Arab experience with private desert safari, yacht charter, and helicopter tour.", ImageUrl = "https://images.unsplash.com/photo-1512453979798-5ea266f8880c?w=800", IsActive = true },
        new() { Destination = "French Riviera", Country = "France", StartDate = DateTime.UtcNow.AddDays(45), EndDate = DateTime.UtcNow.AddDays(52), Price = 7200, AvailableRooms = 6, PackageType = TRAVEL.Models.PackageType.Luxury, MinimumAge = 18, Description = "Monaco, Nice, and Cannes luxury experience with michelin dining and private yacht tours.", ImageUrl = "https://images.unsplash.com/photo-1533929736458-ca588d08c8be?w=800", IsActive = true },
        new() { Destination = "Seychelles Private Island", Country = "Seychelles", StartDate = DateTime.UtcNow.AddDays(90), EndDate = DateTime.UtcNow.AddDays(98), Price = 12000, AvailableRooms = 3, PackageType = TRAVEL.Models.PackageType.Luxury, MinimumAge = 18, Description = "Exclusive private island resort with butler service, private beach, and gourmet dining.", ImageUrl = "https://images.unsplash.com/photo-1589979481223-deb893043163?w=800", IsActive = true },
        
        // Budget Packages
        new() { Destination = "Backpack Thailand", Country = "Thailand", StartDate = DateTime.UtcNow.AddDays(20), EndDate = DateTime.UtcNow.AddDays(30), Price = 1200, AvailableRooms = 30, PackageType = TRAVEL.Models.PackageType.Budget, Description = "10-day budget-friendly adventure through Bangkok, Chiang Mai, and Thai islands.", ImageUrl = "https://images.unsplash.com/photo-1528181304800-259b08848526?w=800", IsActive = true },
        new() { Destination = "Portugal Explorer", Country = "Portugal", StartDate = DateTime.UtcNow.AddDays(35), EndDate = DateTime.UtcNow.AddDays(42), Price = 950, DiscountedPrice = 799, DiscountStartDate = DateTime.UtcNow, DiscountEndDate = DateTime.UtcNow.AddDays(6), AvailableRooms = 25, PackageType = TRAVEL.Models.PackageType.Budget, Description = "Lisbon, Porto, and Algarve on a budget with local experiences and authentic cuisine.", ImageUrl = "https://images.unsplash.com/photo-1555881400-74d7acaacd8b?w=800", IsActive = true },
        new() { Destination = "Vietnam Discovery", Country = "Vietnam", StartDate = DateTime.UtcNow.AddDays(40), EndDate = DateTime.UtcNow.AddDays(50), Price = 1100, AvailableRooms = 28, PackageType = TRAVEL.Models.PackageType.Budget, Description = "Hanoi to Ho Chi Minh City adventure with Ha Long Bay cruise and Hoi An exploration.", ImageUrl = "https://images.unsplash.com/photo-1557750255-c76072a7aad1?w=800", IsActive = true },
        
        // Cultural Packages
        new() { Destination = "Ancient Egypt", Country = "Egypt", StartDate = DateTime.UtcNow.AddDays(30), EndDate = DateTime.UtcNow.AddDays(38), Price = 2400, AvailableRooms = 20, PackageType = TRAVEL.Models.PackageType.Cultural, Description = "Pyramids, Sphinx, Luxor temples, and Nile cruise through ancient Egyptian civilization.", ImageUrl = "https://images.unsplash.com/photo-1539650116574-8efeb43e2750?w=800", IsActive = true },
        new() { Destination = "Kyoto Heritage", Country = "Japan", StartDate = DateTime.UtcNow.AddDays(50), EndDate = DateTime.UtcNow.AddDays(58), Price = 3600, AvailableRooms = 14, PackageType = TRAVEL.Models.PackageType.Cultural, Description = "Traditional tea ceremonies, geisha district walks, ancient temples, and zen garden meditation.", ImageUrl = "https://images.unsplash.com/photo-1493976040374-85c8e12f0c0e?w=800", IsActive = true },
        new() { Destination = "Peruvian Wonders", Country = "Peru", StartDate = DateTime.UtcNow.AddDays(60), EndDate = DateTime.UtcNow.AddDays(70), Price = 3100, AvailableRooms = 16, PackageType = TRAVEL.Models.PackageType.Cultural, Description = "Machu Picchu, Sacred Valley, Lake Titicaca, and immersive Incan culture experience.", ImageUrl = "https://images.unsplash.com/photo-1526392060635-9d6019884377?w=800", IsActive = true },
        
        // Beach Packages
        new() { Destination = "Phuket Paradise", Country = "Thailand", StartDate = DateTime.UtcNow.AddDays(25), EndDate = DateTime.UtcNow.AddDays(32), Price = 1800, AvailableRooms = 22, PackageType = TRAVEL.Models.PackageType.Beach, Description = "Crystal clear waters, island hopping, Thai massage, and beachfront resort relaxation.", ImageUrl = "https://images.unsplash.com/photo-1589394815804-964ed0be2eb5?w=800", IsActive = true },
        new() { Destination = "Cancun Escape", Country = "Mexico", StartDate = DateTime.UtcNow.AddDays(35), EndDate = DateTime.UtcNow.AddDays(42), Price = 2100, AvailableRooms = 25, PackageType = TRAVEL.Models.PackageType.Beach, Description = "All-inclusive beach resort with Mayan ruins visit, cenote swimming, and nightlife.", ImageUrl = "https://images.unsplash.com/photo-1552074284-5e88ef1aef18?w=800", IsActive = true },
        new() { Destination = "Mauritius Beach", Country = "Mauritius", StartDate = DateTime.UtcNow.AddDays(55), EndDate = DateTime.UtcNow.AddDays(62), Price = 3200, DiscountedPrice = 2800, DiscountStartDate = DateTime.UtcNow, DiscountEndDate = DateTime.UtcNow.AddDays(7), AvailableRooms = 12, PackageType = TRAVEL.Models.PackageType.Beach, Description = "Turquoise lagoons, water sports, botanical gardens, and underwater waterfall views.", ImageUrl = "https://images.unsplash.com/photo-1544551763-46a013bb70d5?w=800", IsActive = true },
        
        // Mountain Packages
        new() { Destination = "Himalayas Trek", Country = "Nepal", StartDate = DateTime.UtcNow.AddDays(45), EndDate = DateTime.UtcNow.AddDays(58), Price = 2800, AvailableRooms = 10, PackageType = TRAVEL.Models.PackageType.Mountain, MinimumAge = 16, MaximumAge = 55, Description = "Everest Base Camp trek with experienced guides, teahouse stays, and breathtaking views.", ImageUrl = "https://images.unsplash.com/photo-1544735716-392fe2489ffa?w=800", IsActive = true },
        new() { Destination = "Canadian Rockies", Country = "Canada", StartDate = DateTime.UtcNow.AddDays(70), EndDate = DateTime.UtcNow.AddDays(78), Price = 3400, AvailableRooms = 15, PackageType = TRAVEL.Models.PackageType.Mountain, Description = "Banff and Jasper national parks with glacier walks, wildlife spotting, and mountain lodges.", ImageUrl = "https://images.unsplash.com/photo-1503614472-8c93d56e92ce?w=800", IsActive = true },
        new() { Destination = "Patagonia Explorer", Country = "Argentina", StartDate = DateTime.UtcNow.AddDays(85), EndDate = DateTime.UtcNow.AddDays(96), Price = 4500, AvailableRooms = 8, PackageType = TRAVEL.Models.PackageType.Mountain, Description = "Torres del Paine, Perito Moreno glacier, and remote wilderness adventure.", ImageUrl = "https://images.unsplash.com/photo-1531761535209-180857e963b9?w=800", IsActive = true },
    };

    context.TravelPackages.AddRange(packages);
    await context.SaveChangesAsync();

    // Create admin user if not exists
    if (!context.Users.Any(u => u.Role == TRAVEL.Models.UserRole.Admin))
    {
        var authService = new AuthenticationService(context,
            new ConfigurationBuilder().AddJsonFile("appsettings.json").Build(),
            null!, // Email service not needed for seeding
            null!); // Logger not needed for seeding

        var adminUser = new TRAVEL.Models.User
        {
            FirstName = "Admin",
            LastName = "User",
            Email = "admin@travil.com",
            PasswordHash = authService.HashPassword("Admin123!"),
            Role = TRAVEL.Models.UserRole.Admin,
            Status = TRAVEL.Models.UserStatus.Active,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(adminUser);
        await context.SaveChangesAsync();
        Console.WriteLine("👤 Admin user created: admin@travil.com / Admin123!");
    }

    Console.WriteLine($"✅ Seeded {packages.Count} travel packages");
}
