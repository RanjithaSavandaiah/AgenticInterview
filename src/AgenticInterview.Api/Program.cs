using AgenticInterview.Application;
using AgenticInterview.AgenticSystem;
using AgenticInterview.Infrastructure;
using AgenticInterview.Api.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System;

using Serilog;

// Bootstrap Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/agentic-interview-.txt", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting up AgenticInterview API...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("Logs/agentic-interview-.txt", rollingInterval: RollingInterval.Day));

    // Add services to the container.
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddAgenticSystem();

    // Register MediatR for both Application and AgenticSystem assemblies
    builder.Services.AddMediatR(cfg => {
        cfg.RegisterServicesFromAssembly(typeof(AgenticInterview.Application.DependencyInjection).Assembly);
        cfg.RegisterServicesFromAssembly(typeof(AgenticInterview.AgenticSystem.DependencyInjection).Assembly);
    });
    
    // Agentic System dependencies
    builder.Services.AddSingleton<AgenticInterview.AgenticSystem.Memory.IConversationMemoryStore, AgenticInterview.AgenticSystem.Memory.ConversationMemoryStore>();
    builder.Services.AddSingleton<AgenticInterview.AgenticSystem.Core.IBlackboardManager, AgenticInterview.AgenticSystem.Core.BlackboardManager>();
    builder.Services.AddScoped<AgenticInterview.AgenticSystem.Core.ISessionNotifier, AgenticInterview.Api.Hubs.SignalRSessionNotifier>();
    
    builder.Services.AddScoped<System.Collections.Generic.IList<Microsoft.Extensions.AI.AITool>>(sp => 
    {
        var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger(typeof(AgenticInterview.AgenticSystem.McpTools.InterviewMcpToolFactory));
        return AgenticInterview.AgenticSystem.McpTools.InterviewMcpToolFactory.CreateAllTools(logger, sp);
    });

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Disable the automatic model-state validation that [ApiController] adds.
        // It was silently returning ValidationProblemDetails for IFormFile uploads
        // before the controller action body could run its own explicit checks.
        options.SuppressModelStateInvalidFilter = true;
    });

// Configure form options for multipart file uploads
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52_428_800; // 50 MB
    options.ValueLengthLimit = 10_485_760; // 10 MB
    options.MultipartHeadersLengthLimit = 32_768; // 32 KB
});

// Configure Kestrel request body size limit
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 52_428_800; // 50 MB
});


builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.OperationFilter<AgenticInterview.Api.Swagger.IdempotencyHeaderOperationFilter>();
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("StrictCorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("GlobalLimiter", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });
});

var app = builder.Build();

// Serilog request logging should be early in the pipeline to capture all requests
app.UseSerilogRequestLogging();

// Global exception handler — catches unhandled exceptions and returns structured errors
app.UseGlobalExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("StrictCorsPolicy");
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("GlobalLimiter");
app.MapHub<AgenticInterview.Api.Hubs.HrDashboardHub>("/hrhub").RequireRateLimiting("GlobalLimiter");

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AgenticInterview.Infrastructure.Persistence.ApplicationDbContext>();
    context.Database.EnsureCreated();
    await AgenticInterview.Infrastructure.Data.QuestionBankSeeder.SeedAsync(context);
    await AgenticInterview.Infrastructure.DataSeeding.DemoDataSeeder.SeedDemoDataAsync(context);
}

app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "AgenticInterview API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
