using BuilderPlatform.Infrastructure.Persistence;
using BuilderPlatform.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ScaffoldEngine, ProjectAwarenessEngine, RuntimePatchEngine — stateless singletons
builder.Services.AddSingleton<ScaffoldEngine>();
builder.Services.AddSingleton<ProjectAwarenessEngine>();
builder.Services.AddSingleton<RuntimePatchEngine>();
builder.Services.AddSingleton<PreviewRunner>();
builder.Services.AddSingleton<RuntimeValidationEngine>();
builder.Services.AddSingleton<AutofixEngine>();
builder.Services.AddSingleton<DeployEngine>();
builder.Services.AddSingleton<RuntimeEventBus>();

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
app.MapControllers();

app.Run();
