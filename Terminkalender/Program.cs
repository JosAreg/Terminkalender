using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql;
using Serilog;
using Serilog.Events;
using Terminkalender.Data;
using Terminkalender.Services;

var builder = WebApplication.CreateBuilder(args);

// Serilog konfigurieren
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog(); // Sicherstellen, dass das Paket installiert ist und der Namespace korrekt eingebunden ist.

// Füge den Datenbankkontext hinzu und konfiguriere die MariaDB-Verbindung
builder.Services.AddDbContext<TerminkalenderContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
    new MySqlServerVersion(new Version(10, 4, 32))));

// Controller und Views hinzufügen
builder.Services.AddControllersWithViews();


// Services 
builder.Services.AddScoped<ReservationService>();


try
{
    var app = builder.Build();

    // Middlewares hinzufügen
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Reservations}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "An error occured while strarting the application");
}
finally
{
    Log.CloseAndFlush();
}
