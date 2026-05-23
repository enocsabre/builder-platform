using System.Text;
using BuilderPlatform.Infrastructure.Persistence;
using BuilderPlatform.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Auth ─────────────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = false,
            ValidateAudience         = false,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                = TimeSpan.Zero,
        };
    });
builder.Services.AddAuthorization();

// ScaffoldEngine, ProjectAwarenessEngine, RuntimePatchEngine — stateless singletons
builder.Services.AddSingleton<ScaffoldEngine>();
builder.Services.AddSingleton<ProjectAwarenessEngine>();
builder.Services.AddSingleton<RuntimePatchEngine>();
builder.Services.AddSingleton<PreviewRunner>();
builder.Services.AddSingleton<RuntimeValidationEngine>();
builder.Services.AddSingleton<AutofixEngine>();
builder.Services.AddSingleton<DeployEngine>();
builder.Services.AddSingleton<RuntimeEventBus>();
builder.Services.AddSingleton<ProductEvolutionService>();
builder.Services.AddSingleton<RefactorDetectionService>();
builder.Services.AddSingleton<RefactorExecutionService>();
builder.Services.AddSingleton<SimulationEngine>();
builder.Services.AddSingleton<ProductIntelligenceEngine>();
builder.Services.AddSingleton<ProductRoadmapEngine>();
builder.Services.AddSingleton<ProductOperationalImpactEngine>();
builder.Services.AddSingleton<ProductCapacityEngine>();
builder.Services.AddScoped<DemoResetEngine>();

// RuntimeOrchestrator registered as singleton so controller can inject it,
// then also registered as the hosted service so the framework starts/stops it.
builder.Services.AddSingleton<RuntimeOrchestrator>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RuntimeOrchestrator>());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p =>
        p.WithOrigins("http://localhost:3002")
         .AllowAnyHeader()
         .AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
