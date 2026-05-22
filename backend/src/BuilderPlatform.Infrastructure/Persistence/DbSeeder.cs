using BuilderPlatform.Domain.Entities;
using BuilderPlatform.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BuilderPlatform.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        // Seed builder admin user (idempotent)
        if (!await db.BuilderUsers.AnyAsync())
        {
            db.BuilderUsers.Add(new BuilderUser
            {
                Email        = "admin@builder.local",
                PasswordHash = BuilderPasswordHasher.Hash("Builder1234!"),
            });
            await db.SaveChangesAsync();
        }

        // Migrate orphaned products (null OwnerUserId) to admin user
        var admin = await db.BuilderUsers.FirstAsync(u => u.Email == "admin@builder.local");
        var orphans = await db.Products.Where(p => p.OwnerUserId == null).ToListAsync();
        if (orphans.Count > 0)
        {
            foreach (var p in orphans) p.OwnerUserId = admin.Id;
            await db.SaveChangesAsync();
        }

        if (await db.Products.AnyAsync()) return;

        var product = new Product
        {
            Id          = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name        = "QuincenaCR",
            Prompt      = "SaaS de control de personal y planilla quincenal para restaurantes en Costa Rica",
            Status      = ProductStatus.Building,
            OwnerUserId = admin.Id,
            CreatedAt   = DateTime.UtcNow.AddDays(-3),
            UpdatedAt   = DateTime.UtcNow.AddHours(-2),
        };

        var messages = new List<ChatMessage>
        {
            new() { ProductId = product.Id, Role = MessageRole.User,    Content = "Quiero construir un SaaS de planilla quincenal para restaurantes en Costa Rica.", CreatedAt = DateTime.UtcNow.AddDays(-3) },
            new() { ProductId = product.Id, Role = MessageRole.Runtime, Content = "Entendido. Analizando el contexto de negocio y legislación laboral costarricense...", CreatedAt = DateTime.UtcNow.AddDays(-3).AddSeconds(5) },
            new() { ProductId = product.Id, Role = MessageRole.Runtime, Content = "He generado el brief del producto. El sistema gestionará empleados, turnos, horas extra y cálculo de planilla según el Código de Trabajo de Costa Rica. ¿Aprobás este enfoque?", CreatedAt = DateTime.UtcNow.AddDays(-3).AddMinutes(1) },
            new() { ProductId = product.Id, Role = MessageRole.User,    Content = "Sí, aprobado. Incluí también gestión de días libres pagados.", CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new() { ProductId = product.Id, Role = MessageRole.Runtime, Content = "Agregado. Iniciando arquitectura del sistema con módulo de ausencias y días libres.", CreatedAt = DateTime.UtcNow.AddDays(-2).AddSeconds(10) },
        };

        var events = new List<ActivityEvent>
        {
            new() { ProductId = product.Id, EventType = ActivityType.ProductCreated,        Title = "Producto creado",               CreatedAt = DateTime.UtcNow.AddDays(-3) },
            new() { ProductId = product.Id, EventType = ActivityType.DiscoveryStarted,      Title = "Discovery iniciado",            CreatedAt = DateTime.UtcNow.AddDays(-3).AddMinutes(1) },
            new() { ProductId = product.Id, EventType = ActivityType.BriefGenerated,        Title = "Brief generado",                Details = "Planilla quincenal + empleados + turnos + horas extra + días libres", CreatedAt = DateTime.UtcNow.AddDays(-3).AddMinutes(2) },
            new() { ProductId = product.Id, EventType = ActivityType.ArchitectureGenerated, Title = "Arquitectura generada",         Details = ".NET 9 + SQL Server + Next.js 15 + Azure", CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new() { ProductId = product.Id, EventType = ActivityType.SprintStarted,         Title = "Sprint 1 iniciado",             Details = "Módulo de empleados + auth", CreatedAt = DateTime.UtcNow.AddDays(-1) },
        };

        var approvals = new List<Approval>
        {
            new() { ProductId = product.Id, Title = "Aprobar schema de base de datos", Description = "El schema incluye tablas: employees, shifts, payroll_periods, payroll_entries, absences. ¿Aprobás esta estructura antes de generar las migraciones?", Status = ApprovalStatus.Approved, ResolutionNote = "Aprobado con nota: agregar índice en employee_id + period.", CreatedAt = DateTime.UtcNow.AddDays(-2), ResolvedAt = DateTime.UtcNow.AddDays(-2).AddHours(1) },
            new() { ProductId = product.Id, Title = "Aprobar deploy a staging",        Description = "Sprint 1 completado. El runtime quiere hacer deploy a Azure App Service (staging). ¿Confirmás?",                                                           Status = ApprovalStatus.Pending,  CreatedAt = DateTime.UtcNow.AddHours(-3) },
        };

        db.Products.Add(product);
        db.ChatMessages.AddRange(messages);
        db.ActivityEvents.AddRange(events);
        db.Approvals.AddRange(approvals);

        await db.SaveChangesAsync();
    }
}
