using TurboTaxi.Application;
using TurboTaxi.Infrastructure;
using TurboTaxi.Realtime;
using TurboTaxi.Realtime.Hubs;

try
{
    var builder = WebApplication.CreateBuilder(args);

    Console.WriteLine($"[STARTUP] Environment: {builder.Environment.EnvironmentName}");
    Console.WriteLine($"[STARTUP] ContentRoot: {builder.Environment.ContentRootPath}");

    // ---------------------------------------------------------
    // SERVICES
    // ---------------------------------------------------------
    builder.Services.AddApplication();
    Console.WriteLine("[STARTUP] Application services registered");

    builder.Services.AddInfrastructure(builder.Configuration);
    Console.WriteLine("[STARTUP] Infrastructure services registered");

    builder.Services.AddRealtime(builder.Configuration);
    Console.WriteLine("[STARTUP] Realtime services registered");

    builder.Services.AddControllers();

    // SignalR
    builder.Services.AddSignalR(options =>
    {
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        options.HandshakeTimeout = TimeSpan.FromSeconds(30);
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.MaximumReceiveMessageSize = 102400;
    });
    Console.WriteLine("[STARTUP] SignalR configured");

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.WithOrigins(
                    "https://localhost:7443", 
                    "http://localhost:7442",
                    "https://localhost:44327",
                    "http://localhost:61149"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
                Console.WriteLine("[CORS] Development mode - localhost allowed");
            }
            else
            {
                policy.SetIsOriginAllowed(_ => true)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
                Console.WriteLine("[CORS] Production mode - all origins allowed");
            }
        });
    });

    // Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();
    Console.WriteLine("[STARTUP] Application built successfully");

    // ---------------------------------------------------------
    // MIDDLEWARE
    // ---------------------------------------------------------

    // Static files
    app.UseStaticFiles();

    // Swagger (h?mi?? aktiv)
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.RoutePrefix = string.Empty;
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TurboTaxi API v1");
    });

    // CORS
    app.UseCors("AllowAll");

    // Authentication/Authorization
    app.UseAuthorization();

    // Controllers
    app.MapControllers();

    // SignalR Hub
    app.MapHub<DriverHub>("/hubs/driver").RequireCors("AllowAll");

    Console.WriteLine("[STARTUP] Middleware configured");

    // ---------------------------------------------------------
    // STARTUP LOGS
    // ---------------------------------------------------------
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var env = app.Environment;
        var config = app.Configuration;

        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine($"TurboTaxi API Started - {env.EnvironmentName} Mode");
        Console.WriteLine(new string('=', 80));

        Console.WriteLine("\nServer URLs:");
        foreach (var url in app.Urls)
        {
            Console.WriteLine($"   {url}");
        }

        Console.WriteLine("\nRemote Servers:");
        var sql = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(sql))
        {
            var sqlServer = sql.Split(';')[0].Replace("Data Source=", "");
            Console.WriteLine($"   SQL Server: {sqlServer}");
        }

        var redis = config.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redis))
        {
            var redisHost = redis.Split(':')[0];
            Console.WriteLine($"   Redis: {redisHost}");
        }

        if (env.IsDevelopment())
        {
            Console.WriteLine("\nWeb Interfaces:");
            Console.WriteLine("   Swagger:        /");
            Console.WriteLine("   User Panel:     /user.html");
            Console.WriteLine("   Driver Panel:   /driver.html");
            Console.WriteLine("   Debug Tools:    /debug-connection.html");
        }

        Console.WriteLine("\nAPI Endpoints:");
        Console.WriteLine("   POST   /api/rides              - Create ride");
        Console.WriteLine("   POST   /api/rides/{id}/accept  - Accept ride");
        Console.WriteLine("   POST   /api/rides/{id}/start   - Start ride");
        Console.WriteLine("   POST   /api/rides/{id}/finish  - Finish ride");
        Console.WriteLine("   WS     /hubs/driver            - SignalR Hub");

        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine("Server is ready! All services connected.");
        Console.WriteLine(new string('=', 80) + "\n");
    });

    Console.WriteLine("[STARTUP] Starting application...");
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine("\n" + new string('!', 80));
    Console.WriteLine("FATAL ERROR DURING STARTUP");
    Console.WriteLine(new string('!', 80));
    Console.WriteLine($"Error Type: {ex.GetType().FullName}");
    Console.WriteLine($"Message: {ex.Message}");
    Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");

    if (ex.InnerException != null)
    {
        Console.WriteLine($"\nInner Exception: {ex.InnerException.GetType().FullName}");
        Console.WriteLine($"Inner Message: {ex.InnerException.Message}");
        Console.WriteLine($"\nInner Stack Trace:\n{ex.InnerException.StackTrace}");
    }

    Console.WriteLine(new string('!', 80) + "\n");

    throw;
}
