using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Identity;
using AppleStore.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddMemoryCache();

// ASP.NET Core Identity over the report's own Users table (see UserStore).
// Sign-in state lives in Identity's application cookie.
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.AddAppleStoreIdentityCore()
    .AddSignInManager();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IProductCatalogService, ProductCatalogService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<SeedAdminOptions>().Bind(builder.Configuration.GetSection(SeedAdminOptions.Section));
builder.Services.AddScoped<AdminSeeder>();
// Real SMTP when the "Smtp" section is configured, otherwise log-only.
builder.Services.AddAppleStoreEmail(builder.Configuration);

var app = builder.Build();

// The first admin account, from the SeedAdmin settings (see AdminSeeder).
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<AdminSeeder>().SeedAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// Area routes first, so /Admin reaches the Admin area's Dashboard.
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

// Lets the integration tests start this app with WebApplicationFactory<Program>.
public partial class Program;
