using BuilderPlatform.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace BuilderPlatform.Infrastructure.Services;

public class ScaffoldEngine(IConfiguration config, ILogger<ScaffoldEngine> logger)
{
    private readonly string _basePath = config["Scaffold:OutputPath"]
        ?? Path.Combine(Directory.GetCurrentDirectory(), "scaffold-output");

    // ── Public entry point ─────────────────────────────────────────────────────

    public Task<(string projectPath, List<ScaffoldEntry> entries)> GenerateAsync(
        Product product, ProductProfile profile, CancellationToken ct = default)
    {
        var slug        = ToSlug(product.Name);
        var safeName    = ToSafeName(product.Name);
        var projectPath = Path.Combine(_basePath, slug);

        if (Directory.Exists(projectPath) && Directory.EnumerateFileSystemEntries(projectPath).Any())
        {
            logger.LogWarning("Scaffold path already exists, appending timestamp: {Path}", projectPath);
            projectPath = Path.Combine(_basePath, $"{slug}-{DateTime.UtcNow:yyyyMMddHHmmss}");
        }

        Directory.CreateDirectory(projectPath);
        logger.LogInformation("Generating scaffold at {Path}", projectPath);

        var entries = new List<ScaffoldEntry>();
        int order   = 0;

        GenerateBackend(safeName,  product.Name, profile, projectPath, entries, ref order);
        GenerateFrontend(safeName, product.Name, profile, projectPath, entries, ref order);
        GenerateDocs(product.Name, profile, projectPath, entries, ref order);

        logger.LogInformation("Scaffold complete: {Count} files", entries.Count(e => e.EntryType == "file"));
        return Task.FromResult((projectPath, entries));
    }

    // ── Backend scaffold ───────────────────────────────────────────────────────

    private static void GenerateBackend(string safeName, string displayName, ProductProfile profile,
        string root, List<ScaffoldEntry> entries, ref int order)
    {
        var backendRoot = Path.Combine(root, "backend");
        var srcRoot     = Path.Combine(backendRoot, "src");

        WriteFile(backendRoot, $"{safeName}.sln",
            SlnContent(safeName), "solution", entries, ref order);

        // Domain
        var domainRoot   = Path.Combine(srcRoot, $"{safeName}.Domain");
        var entitiesRoot = Path.Combine(domainRoot, "Entities");

        WriteFile(domainRoot, $"{safeName}.Domain.csproj",
            DomainCsproj(safeName), "xml", entries, ref order);

        foreach (var entity in profile.DbEntities.Take(8))
            WriteFile(entitiesRoot, $"{entity}.cs",
                EntityClass(safeName, entity), "csharp", entries, ref order);

        // Infrastructure
        var infraRoot       = Path.Combine(srcRoot, $"{safeName}.Infrastructure");
        var persistenceRoot = Path.Combine(infraRoot, "Persistence");

        WriteFile(infraRoot, $"{safeName}.Infrastructure.csproj",
            InfrastructureCsproj(safeName), "xml", entries, ref order);

        WriteFile(persistenceRoot, "AppDbContext.cs",
            DbContextClass(safeName, profile.DbEntities.Take(8).ToArray()), "csharp", entries, ref order);

        // API
        var apiRoot         = Path.Combine(srcRoot, $"{safeName}.API");
        var controllersRoot = Path.Combine(apiRoot, "Controllers");

        WriteFile(apiRoot, $"{safeName}.API.csproj",    ApiCsproj(safeName),         "xml",    entries, ref order);
        WriteFile(apiRoot, "Program.cs",                ProgramCs(safeName),          "csharp", entries, ref order);
        WriteFile(apiRoot, "appsettings.json",          AppSettings(displayName),     "json",   entries, ref order);
        WriteFile(apiRoot, "appsettings.Development.json", DevAppSettings(),          "json",   entries, ref order);

        foreach (var entity in profile.DbEntities.Take(5))
            WriteFile(controllersRoot, $"{entity}Controller.cs",
                ControllerClass(safeName, entity), "csharp", entries, ref order);
    }

    // ── Frontend scaffold ──────────────────────────────────────────────────────

    private static void GenerateFrontend(string safeName, string displayName, ProductProfile profile,
        string root, List<ScaffoldEntry> entries, ref int order)
    {
        var feRoot = Path.Combine(root, "frontend");

        WriteFile(feRoot, "package.json",        PackageJson(safeName, displayName), "json",       entries, ref order);
        WriteFile(feRoot, "tsconfig.json",        TsConfig(),                         "json",       entries, ref order);
        WriteFile(feRoot, "next.config.ts",       NextConfig(),                       "typescript", entries, ref order);
        WriteFile(feRoot, "postcss.config.mjs",   PostcssConfig(),                    "javascript", entries, ref order);
        WriteFile(feRoot, "instrumentation.ts",   InstrumentationHook(),              "typescript", entries, ref order);
        WriteFile(feRoot, "tailwind.config.ts",   TailwindConfig(),                   "typescript", entries, ref order);
        WriteFile(feRoot, ".gitignore",            FrontendGitIgnore(),                null,         entries, ref order);
        WriteFile(feRoot, "middleware.ts",         Middleware(),                       "typescript", entries, ref order);

        var appRoot = Path.Combine(feRoot, "app");
        WriteFile(appRoot, "globals.css",  GlobalsCss(),                 "css",        entries, ref order);
        WriteFile(appRoot, "layout.tsx",   RootLayout(displayName),      "typescript", entries, ref order);
        WriteFile(appRoot, "page.tsx",     RootPage(),                   "typescript", entries, ref order);

        WriteFile(Path.Combine(appRoot, "dashboard"), "page.tsx",
            DashboardPage(displayName, profile), "typescript", entries, ref order);

        var demoEmail    = $"admin@{ToSlug(displayName)}.com";
        const string demoPassword = "Demo1234!";
        WriteFile(Path.Combine(appRoot, "login"), "page.tsx",
            LoginPage(displayName, demoEmail, demoPassword), "typescript", entries, ref order);

        foreach (var feature in profile.CoreFeatures.Take(5))
            WriteFile(Path.Combine(appRoot, ToRoute(feature)), "page.tsx",
                FeaturePage(feature, ToRoute(feature), profile), "typescript", entries, ref order);

        // Auth API routes
        WriteFile(Path.Combine(appRoot, "api", "auth", "login"),  "route.ts",
            ApiAuthLoginRoute(demoEmail, demoPassword), "typescript", entries, ref order);
        WriteFile(Path.Combine(appRoot, "api", "auth", "logout"), "route.ts",
            ApiAuthLogoutRoute(), "typescript", entries, ref order);

        // Data persistence API route (dynamic [module] segment)
        WriteFile(Path.Combine(appRoot, "api", "data", "[module]"), "route.ts",
            ApiDataRoute(), "typescript", entries, ref order);

        // Workflow events API route
        WriteFile(Path.Combine(appRoot, "api", "workflow"), "route.ts",
            WorkflowApiRoute(profile.Industry), "typescript", entries, ref order);

        var componentsRoot = Path.Combine(feRoot, "components");
        WriteFile(componentsRoot, "Sidebar.tsx",            SidebarServer(displayName),          "typescript", entries, ref order);
        WriteFile(componentsRoot, "SidebarClient.tsx",      SidebarClientComponent(displayName), "typescript", entries, ref order);
        WriteFile(componentsRoot, "AppShell.tsx",           AppShellComponent(),                 "typescript", entries, ref order);
        WriteFile(componentsRoot, "Header.tsx",             HeaderComponent(),                   "typescript", entries, ref order);
        WriteFile(componentsRoot, "DashboardRefresher.tsx", DashboardRefresherComponent(),       "typescript", entries, ref order);

        var libRoot = Path.Combine(feRoot, "lib");
        WriteFile(libRoot, "types.ts",  LibTypes(profile), "typescript", entries, ref order);
        WriteFile(libRoot, "api.ts",    LibApi(),          "typescript", entries, ref order);
        WriteFile(libRoot, "utils.ts",  LibUtils(),        "typescript", entries, ref order);

        // Registry seed — entity labels for human-readable sidebar nav
        var ctx        = ContentGenerator.GetDomainContext(profile.Industry);
        var labelsJson = System.Text.Json.JsonSerializer.Serialize(
            ctx.EntityLabels, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        WriteFile(Path.Combine(feRoot, "registry"), "entity-labels.json", labelsJson, "json", entries, ref order);

        // Registry seed — nav-items.json with correct hrefs matching scaffolded page directories
        var navOpts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        var navSeed = profile.CoreFeatures.Take(5)
            .Select(f => new { label = f, href = "/" + ToRoute(f), icon = "Grid" })
            .ToList();
        WriteFile(Path.Combine(feRoot, "registry"), "nav-items.json",
            System.Text.Json.JsonSerializer.Serialize(navSeed, navOpts), "json", entries, ref order);
    }

    // ── Docs scaffold ──────────────────────────────────────────────────────────

    private static void GenerateDocs(string displayName, ProductProfile profile,
        string root, List<ScaffoldEntry> entries, ref int order)
    {
        var docsRoot = Path.Combine(root, "docs");

        WriteFile(root,     "README.md",            Readme(displayName, profile),       "markdown", entries, ref order);
        WriteFile(docsRoot, "ARCHITECTURE.md",      ArchitectureDoc(displayName, profile), "markdown", entries, ref order);
        WriteFile(docsRoot, "DEVELOPMENT_SETUP.md", DevSetupDoc(displayName),           "markdown", entries, ref order);
    }

    // ── C# / XML templates (use $$ raw strings — { } are literal, {{expr}} interpolates) ──

    private static string SlnContent(string n) => $$"""

        Microsoft Visual Studio Solution File, Format Version 12.00
        # Visual Studio Version 17
        VisualStudioVersion = 17.0.31903.59
        MinimumVisualStudioVersion = 10.0.40219.1
        Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{n}}.API", "src\{{n}}.API\{{n}}.API.csproj", "{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"
        EndProject
        Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{n}}.Domain", "src\{{n}}.Domain\{{n}}.Domain.csproj", "{B2C3D4E5-F6A7-8901-BCDE-F12345678901}"
        EndProject
        Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{n}}.Infrastructure", "src\{{n}}.Infrastructure\{{n}}.Infrastructure.csproj", "{C3D4E5F6-A7B8-9012-CDEF-123456789012}"
        EndProject
        Global
          GlobalSection(SolutionConfigurationPlatforms) = preSolution
            Debug|Any CPU = Debug|Any CPU
            Release|Any CPU = Release|Any CPU
          EndGlobalSection
        EndGlobal
        """;

    private static string DomainCsproj(string n) => $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net9.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>{{n}}.Domain</RootNamespace>
          </PropertyGroup>
        </Project>
        """;

    private static string InfrastructureCsproj(string n) => $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net9.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>{{n}}.Infrastructure</RootNamespace>
          </PropertyGroup>
          <ItemGroup>
            <ProjectReference Include="..\{{n}}.Domain\{{n}}.Domain.csproj" />
          </ItemGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.0" />
            <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="9.0.0">
              <PrivateAssets>all</PrivateAssets>
            </PackageReference>
          </ItemGroup>
        </Project>
        """;

    private static string ApiCsproj(string n) => $$"""
        <Project Sdk="Microsoft.NET.Sdk.Web">
          <PropertyGroup>
            <TargetFramework>net9.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <RootNamespace>{{n}}.API</RootNamespace>
          </PropertyGroup>
          <ItemGroup>
            <ProjectReference Include="..\{{n}}.Domain\{{n}}.Domain.csproj" />
            <ProjectReference Include="..\{{n}}.Infrastructure\{{n}}.Infrastructure.csproj" />
          </ItemGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.0" />
            <PackageReference Include="Swashbuckle.AspNetCore" Version="7.2.0" />
          </ItemGroup>
        </Project>
        """;

    private static string EntityClass(string ns, string entity) => $$"""
        namespace {{ns}}.Domain.Entities;

        public class {{entity}}
        {
            public Guid Id { get; set; } = Guid.NewGuid();
        {{GetEntityFields(entity)}}
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
            public bool IsActive { get; set; } = true;
        }
        """;

    private static string GetEntityFields(string entity)
    {
        var lower = entity.ToLowerInvariant();
        if (ContainsAny(lower, "user", "customer", "employee", "owner", "doctor", "vet", "driver", "operator", "teacher", "student"))
            return "    public string Name { get; set; } = string.Empty;\n    public string Email { get; set; } = string.Empty;\n    public string Phone { get; set; } = string.Empty;";
        if (ContainsAny(lower, "appointment", "reservation", "booking", "schedule"))
            return "    public Guid CustomerId { get; set; }\n    public DateTime Date { get; set; }\n    public TimeSpan Duration { get; set; } = TimeSpan.FromHours(1);\n    public string Status { get; set; } = \"pending\";\n    public string? Notes { get; set; }";
        if (ContainsAny(lower, "invoice", "order", "payment", "transaction", "payroll"))
            return "    public decimal Amount { get; set; }\n    public string Currency { get; set; } = \"USD\";\n    public string Status { get; set; } = \"pending\";\n    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;\n    public DateTime? PaidAt { get; set; }";
        if (ContainsAny(lower, "product", "item", "menu", "dish", "machine"))
            return "    public string Name { get; set; } = string.Empty;\n    public string Description { get; set; } = string.Empty;\n    public decimal Price { get; set; }\n    public string Category { get; set; } = string.Empty;\n    public int Stock { get; set; }";
        if (ContainsAny(lower, "record", "history", "log", "note"))
            return "    public Guid SubjectId { get; set; }\n    public string Content { get; set; } = string.Empty;\n    public string RecordType { get; set; } = string.Empty;\n    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;\n    public string? AuthorId { get; set; }";
        if (ContainsAny(lower, "organization", "company", "branch", "clinic", "school", "restaurant", "store"))
            return "    public string Name { get; set; } = string.Empty;\n    public string Slug { get; set; } = string.Empty;\n    public string? Address { get; set; }\n    public string? Phone { get; set; }\n    public string? Email { get; set; }\n    public string Plan { get; set; } = \"free\";";
        return "    public string Name { get; set; } = string.Empty;\n    public string Description { get; set; } = string.Empty;";
    }

    private static string DbContextClass(string ns, string[] entities)
    {
        var dbSets = string.Join("\n    ", entities.Select(e => $"public DbSet<{e}> {e}s => Set<{e}>();"));
        var keys   = string.Join("\n        ", entities.Select(e => $"modelBuilder.Entity<{e}>().HasKey(e => e.Id);"));
        return $$"""
            using {{ns}}.Domain.Entities;
            using Microsoft.EntityFrameworkCore;

            namespace {{ns}}.Infrastructure.Persistence;

            public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
            {
                {{dbSets}}

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    {{keys}}
                }
            }
            """;
    }

    private static string ProgramCs(string ns) => $$"""
        using {{ns}}.Infrastructure.Persistence;
        using Microsoft.EntityFrameworkCore;

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddCors(opt =>
            opt.AddDefaultPolicy(p =>
                p.WithOrigins("http://localhost:3000")
                 .AllowAnyHeader()
                 .AllowAnyMethod()));

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseCors();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
        """;

    private static string AppSettings(string displayName) => $$"""
        {
          "Logging": {
            "LogLevel": {
              "Default": "Information",
              "Microsoft.AspNetCore": "Warning"
            }
          },
          "AllowedHosts": "*",
          "ConnectionStrings": {
            "DefaultConnection": "Server=localhost;Database={{displayName.Replace(" ", "")}};Trusted_Connection=True;TrustServerCertificate=True"
          },
          "Jwt": {
            "Key": "REPLACE_WITH_SECURE_KEY_MIN_32_CHARS",
            "Issuer": "{{displayName}}",
            "Audience": "{{displayName}}-users"
          }
        }
        """;

    private static string DevAppSettings() => """
        {
          "Logging": {
            "LogLevel": {
              "Default": "Debug",
              "Microsoft.AspNetCore": "Information"
            }
          }
        }
        """;

    private static string ControllerClass(string ns, string entity) => $$"""
        using Microsoft.AspNetCore.Mvc;

        namespace {{ns}}.API.Controllers;

        [ApiController]
        [Route("api/[controller]")]
        public class {{entity}}Controller : ControllerBase
        {
            [HttpGet]
            public IActionResult GetAll()
                => Ok(new { message = "TODO: Return all {{entity}} records", data = Array.Empty<object>() });

            [HttpGet("{id:guid}")]
            public IActionResult GetById(Guid id)
                => Ok(new { id, message = "TODO: Return {{entity}} by id" });

            [HttpPost]
            public IActionResult Create([FromBody] object request)
                => StatusCode(201, new { message = "TODO: Create {{entity}}" });

            [HttpPut("{id:guid}")]
            public IActionResult Update(Guid id, [FromBody] object request)
                => Ok(new { id, message = "TODO: Update {{entity}}" });

            [HttpDelete("{id:guid}")]
            public IActionResult Delete(Guid id) => NoContent();
        }
        """;

    // ── Frontend templates (use """...""" + Replace to avoid JSX brace conflicts) ──

    private static string PackageJson(string safeName, string displayName) => $$"""
        {
          "name": "{{safeName.ToLowerInvariant()}}",
          "version": "0.1.0",
          "private": true,
          "scripts": {
            "dev": "next dev",
            "build": "next build",
            "start": "next start",
            "lint": "next lint",
            "type-check": "tsc --noEmit"
          },
          "dependencies": {
            "next": "15.3.9",
            "react": "^19.0.0",
            "react-dom": "^19.0.0",
            "lucide-react": "^0.511.0"
          },
          "devDependencies": {
            "@types/node": "^20",
            "@types/react": "^19",
            "@types/react-dom": "^19",
            "typescript": "^5",
            "tailwindcss": "^4",
            "@tailwindcss/postcss": "^4",
            "postcss": "^8"
          }
        }
        """;

    private static string TsConfig() => """
        {
          "compilerOptions": {
            "target": "ES2017",
            "lib": ["dom", "dom.iterable", "esnext"],
            "allowJs": true,
            "skipLibCheck": true,
            "strict": true,
            "noEmit": true,
            "esModuleInterop": true,
            "module": "esnext",
            "moduleResolution": "bundler",
            "resolveJsonModule": true,
            "isolatedModules": true,
            "jsx": "preserve",
            "incremental": true,
            "plugins": [{ "name": "next" }],
            "paths": { "@/*": ["./*"] }
          },
          "include": ["next-env.d.ts", "**/*.ts", "**/*.tsx", ".next/types/**/*.ts"],
          "exclude": ["node_modules"]
        }
        """;

    private static string NextConfig() => """
        import type { NextConfig } from "next";
        const nextConfig: NextConfig = {};
        export default nextConfig;
        """;

    private static string PostcssConfig() => """
        export default { plugins: { "@tailwindcss/postcss": {} } };
        """;

    private static string InstrumentationHook() => """
        export async function register() {
          if (
            typeof globalThis.localStorage !== "undefined" &&
            typeof (globalThis.localStorage as Storage).getItem !== "function"
          ) {
            (globalThis as Record<string, unknown>).localStorage = {
              getItem: () => null,
              setItem: () => {},
              removeItem: () => {},
              clear: () => {},
              key: () => null,
              length: 0,
            } as Storage;
          }
        }
        """;

    private static string TailwindConfig() => """
        import type { Config } from "tailwindcss";
        const config: Config = {
          content: ["./pages/**/*.{js,ts,jsx,tsx,mdx}", "./components/**/*.{js,ts,jsx,tsx,mdx}", "./app/**/*.{js,ts,jsx,tsx,mdx}"],
          theme: { extend: {} },
          plugins: [],
        };
        export default config;
        """;

    private static string GlobalsCss() => """
        @import "tailwindcss";

        :root {
          --background:        #0d0f14;
          --surface:           #13161e;
          --surface-elevated:  #1c2030;
          --border:            rgba(255,255,255,0.08);
          --foreground:        #e8eaf0;
          --foreground-muted:  #8b92a5;
          --muted:             #525870;
          --accent:            #4f6ef7;
          --status-active-bg:   rgba(52,211,153,0.12);
          --status-active-text: #34d399;
          --status-warn-bg:     rgba(251,191,36,0.12);
          --status-warn-text:   #fbbf24;
          --status-danger-bg:   rgba(248,113,113,0.12);
          --status-danger-text: #f87171;
          --status-info-bg:     rgba(96,165,250,0.12);
          --status-info-text:   #60a5fa;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { background: var(--background); color: var(--foreground); font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; font-size: 14px; -webkit-font-smoothing: antialiased; }
        a { color: inherit; text-decoration: none; }
        ::-webkit-scrollbar { width: 5px; height: 5px; }
        ::-webkit-scrollbar-track { background: transparent; }
        ::-webkit-scrollbar-thumb { background: var(--border); border-radius: 4px; }
        """;

    // TSX templates: plain string with __PLACEHOLDER__ substitution to avoid {{ }} conflicts
    private static string RootLayout(string displayName) =>
        """
        import type { Metadata } from "next";
        import "./globals.css";
        import { Sidebar } from "@/components/Sidebar";
        import { AppShell } from "@/components/AppShell";

        export const metadata: Metadata = {
          title: "__DISPLAY_NAME__",
          description: "Generated by Builder Platform",
        };

        export default function RootLayout({ children }: { children: React.ReactNode }) {
          return (
            <html lang="es">
              <body>
                <AppShell sidebar={<Sidebar />}>
                  {children}
                </AppShell>
              </body>
            </html>
          );
        }
        """.Replace("__DISPLAY_NAME__", displayName);

    private static string RootPage() => """
        import { redirect } from "next/navigation";
        export default function RootPage() { redirect("/dashboard"); }
        """;

    private static string DashboardPage(string displayName, ProductProfile profile)
    {
        var ctx = ContentGenerator.GetDomainContext(profile.Industry);

        var kpisJs = string.Join(",\n    ", ctx.Kpis.Select(k =>
            $"{{ label: \"{EscapeJs(k.Label)}\", value: \"{EscapeJs(k.Value)}\", trend: \"{EscapeJs(k.Trend)}\", trendColor: \"{k.TrendColor}\" }}"));

        var activityJs = string.Join(",\n    ", ctx.RecentActivity.Select(r =>
            $"{{ desc: \"{EscapeJs(r.Description)}\", when: \"{EscapeJs(r.When)}\", status: \"{EscapeJs(r.Status)}\", statusBg: \"{StatusBgVar(r.StatusColor)}\", statusColor: \"{StatusTextVar(r.StatusColor)}\" }}"));

        var (fsImports, computeBody) = profile.Industry == "restaurant"
            ? (
                "readFileSync, existsSync",
                """
                  const dir     = join(process.cwd(), ".data");
                  const pedidos = loadModule(dir, "pedidos-comandas");
                  const mesas   = loadModule(dir, "mesas");
                  const inv     = loadModule(dir, "inventario-compras");
                  const hasData = pedidos.length > 0 || mesas.length > 0 || inv.length > 0;
                  if (!hasData) return { kpis: DEMO_KPIS, isRealData: false };
                  const active    = pedidos.filter(r => ["Pendiente","Preparando","En camino"].includes(r["Estado"] ?? "")).length;
                  const inKitchen = pedidos.filter(r => r["Estado"] === "Preparando").length;
                  const done      = pedidos.filter(r => r["Estado"] === "Entregado").length;
                  const rawV      = pedidos.filter(r => r["Estado"] === "Entregado")
                                     .reduce((s, r) => { const v = (r["Total"] ?? "").replace(/[^0-9]/g, ""); return s + (parseInt(v, 10) || 0); }, 0);
                  const ventas    = `₡${rawV.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",")}`;
                  const occ       = mesas.filter(r => r["Estado"] === "Ocupada").length;
                  const totM      = mesas.length;
                  const lowSt     = inv.filter(r => { const st = (r["Estado"] ?? "").toLowerCase(); return st.includes("bajo") || st.includes("sin stock"); }).length;
                  return {
                    isRealData: true,
                    kpis: [
                      { label: "Ventas del día",  value: ventas,                                       trend: `${done} pedido${done !== 1 ? "s" : ""} completado${done !== 1 ? "s" : ""}`, trendColor: "var(--status-active-text)" },
                      { label: "Órdenes activas", value: String(active),                               trend: inKitchen > 0 ? `${inKitchen} en cocina ahora` : "Cocina libre",              trendColor: active > 0 ? "var(--status-warn-text)" : "var(--foreground-muted)" },
                      { label: "Mesas ocupadas",  value: totM > 0 ? `${occ} / ${totM}` : String(occ), trend: totM > 0 ? `${totM - occ} disponible${(totM - occ) !== 1 ? "s" : ""}` : "Sin datos de mesas", trendColor: "var(--foreground-muted)" },
                      { label: "Stock bajo",      value: String(lowSt),                                trend: lowSt > 0 ? "Revisar inventario" : "Inventario OK",                          trendColor: lowSt > 0 ? "var(--status-danger-text)" : "var(--status-active-text)" },
                    ]
                  };
                """
              )
            : (
                "readFileSync, readdirSync, existsSync",
                """
                  const dir = join(process.cwd(), ".data");
                  if (!existsSync(dir)) return { kpis: DEMO_KPIS, isRealData: false };
                  const allFiles = readdirSync(dir).filter(f => f.endsWith(".json") && f !== "activity.json");
                  const total = allFiles.reduce((s: number, f: string) => {
                    try { return s + (JSON.parse(readFileSync(join(dir, f), "utf-8")) as unknown[]).length; } catch { return s; }
                  }, 0);
                  if (total === 0) return { kpis: DEMO_KPIS, isRealData: false };
                  return {
                    isRealData: true,
                    kpis: [{ ...DEMO_KPIS[0], value: String(total), trend: "registros activos" }, ...DEMO_KPIS.slice(1)],
                  };
                """
              );

        return """
            import DashboardRefresher from "@/components/DashboardRefresher";
            import { __FS_IMPORTS__ } from "fs";
            import { join } from "path";

            export const dynamic = "force-dynamic";

            type KpiRow = { label: string; value: string; trend: string; trendColor: string };
            type ARow   = { desc: string; when: string; status: string; statusBg: string; statusColor: string };

            const DEMO_KPIS: KpiRow[] = [
              __KPIS_DEMO__
            ];

            function loadModule(dir: string, name: string): Array<Record<string, string>> {
              try {
                const f = join(dir, `${name}.json`);
                return existsSync(f) ? (JSON.parse(readFileSync(f, "utf-8")) as Array<Record<string, string>>) : [];
              } catch { return []; }
            }

            function computeKpis(): { kpis: KpiRow[]; isRealData: boolean } {
              try {
                __COMPUTE_KPIS_BODY__
              } catch { return { kpis: DEMO_KPIS, isRealData: false }; }
            }

            function fmtWhen(iso: string): string {
              try {
                const d = Math.floor((Date.now() - new Date(iso).getTime()) / 60000);
                if (d < 1) return "ahora mismo";
                if (d < 60) return `hace ${d} min`;
                const h = Math.floor(d / 60);
                return h < 24 ? `hace ${h}h` : "ayer";
              } catch { return "hace un momento"; }
            }
            function lBg(c: string): string {
              return c === "active" ? "var(--status-active-bg)" : c === "warn" ? "var(--status-warn-bg)" : c === "danger" ? "var(--status-danger-bg)" : "var(--status-info-bg)";
            }
            function lTxt(c: string): string {
              return c === "active" ? "var(--status-active-text)" : c === "warn" ? "var(--status-warn-text)" : c === "danger" ? "var(--status-danger-text)" : "var(--status-info-text)";
            }
            function loadLive(): { rows: ARow[]; lastWhen: string | null } {
              try {
                const file = join(process.cwd(), ".data", "activity.json");
                if (!existsSync(file)) return { rows: [], lastWhen: null };
                const raw = JSON.parse(readFileSync(file, "utf-8")) as Array<Record<string, string>>;
                const rows = raw.slice(0, 8).map(r => ({
                  desc: r.desc ?? "",
                  when: fmtWhen(r.when ?? ""),
                  status: r.status ?? "Actualizado",
                  statusBg:    lBg(r.statusColor  ?? "info"),
                  statusColor: lTxt(r.statusColor ?? "info"),
                }));
                const lastWhen = raw[0]?.when ? fmtWhen(raw[0].when) : null;
                return { rows, lastWhen };
              } catch { return { rows: [], lastWhen: null }; }
            }

            export default function DashboardPage() {
              const { kpis, isRealData }     = computeKpis();
              const { rows: live, lastWhen } = loadLive();
              const demo: ARow[] = [
                __ACTIVITY__
              ];
              const rows   = live.length > 0 ? live : demo;
              const isLive = live.length > 0;

              return (
                <div style={{ padding: "28px", maxWidth: "1200px" }}>
                  <DashboardRefresher intervalMs={5000} />
                  <div style={{ marginBottom: "32px" }}>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "4px" }}>
                      <h1 style={{ fontSize: "22px", fontWeight: "700", color: "var(--foreground)" }}>
                        __DISPLAY_NAME__
                      </h1>
                      {isRealData && (
                        <span style={{ fontSize: "11px", color: "var(--status-active-text)", padding: "3px 10px", borderRadius: "99px", background: "var(--status-active-bg)", border: "1px solid var(--border)", marginTop: "2px", flexShrink: 0 }}>
                          DATOS EN VIVO
                        </span>
                      )}
                    </div>
                    <p style={{ fontSize: "13px", color: "var(--foreground-muted)" }}>
                      Panel de control · {isRealData ? "calculado desde registros reales" : "datos demo"}
                      {lastWhen && <span style={{ marginLeft: "8px", color: "var(--muted)" }}>· últ. actividad {lastWhen}</span>}
                    </p>
                  </div>

                  <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))", gap: "16px", marginBottom: "32px" }}>
                    {kpis.map((kpi) => (
                      <div key={kpi.label} style={{ padding: "22px", background: "var(--surface)", borderRadius: "12px", border: isRealData ? "1px solid var(--status-active-bg)" : "1px solid var(--border)", position: "relative" }}>
                        {isRealData && <span style={{ position: "absolute", top: "14px", right: "14px", width: "6px", height: "6px", borderRadius: "50%", background: "var(--status-active-text)" }} />}
                        <p style={{ fontSize: "11px", fontWeight: "500", color: "var(--foreground-muted)", marginBottom: "10px", textTransform: "uppercase", letterSpacing: "0.06em" }}>{kpi.label}</p>
                        <p style={{ fontSize: "26px", fontWeight: "700", color: "var(--foreground)", marginBottom: "6px", letterSpacing: "-0.02em" }}>{kpi.value}</p>
                        <p style={{ fontSize: "11px", color: kpi.trendColor }}>{kpi.trend}</p>
                      </div>
                    ))}
                  </div>

                  <div style={{ background: "var(--surface)", borderRadius: "12px", border: "1px solid var(--border)", overflow: "hidden" }}>
                    <div style={{ padding: "16px 20px", borderBottom: "1px solid var(--border)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                      <span style={{ fontSize: "14px", fontWeight: "600", color: "var(--foreground)" }}>Actividad reciente</span>
                      {isLive
                        ? <span style={{ fontSize: "11px", color: "var(--status-active-text)", padding: "3px 10px", borderRadius: "99px", background: "var(--status-active-bg)", border: "1px solid var(--border)" }}>EN VIVO</span>
                        : <span style={{ fontSize: "11px", color: "var(--muted)", padding: "3px 10px", borderRadius: "99px", background: "var(--surface-elevated)", border: "1px solid var(--border)" }}>Datos demo</span>
                      }
                    </div>
                    <table style={{ width: "100%", borderCollapse: "collapse" }}>
                      <thead>
                        <tr style={{ background: "var(--surface-elevated)" }}>
                          <th style={{ padding: "10px 20px", textAlign: "left", fontSize: "11px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Descripción</th>
                          <th style={{ padding: "10px 20px", textAlign: "left", fontSize: "11px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Cuándo</th>
                          <th style={{ padding: "10px 20px", textAlign: "right", fontSize: "11px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Estado</th>
                        </tr>
                      </thead>
                      <tbody>
                        {rows.map((row, i) => (
                          <tr key={i} style={{ borderTop: "1px solid var(--border)" }}>
                            <td style={{ padding: "13px 20px", fontSize: "13px", color: "var(--foreground)" }}>{row.desc}</td>
                            <td style={{ padding: "13px 20px", fontSize: "12px", color: "var(--foreground-muted)" }}>{row.when}</td>
                            <td style={{ padding: "13px 20px", textAlign: "right" }}>
                              <span style={{ fontSize: "11px", fontWeight: "500", padding: "3px 10px", borderRadius: "99px", background: row.statusBg, color: row.statusColor }}>{row.status}</span>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              );
            }
            """.Replace("__FS_IMPORTS__", fsImports)
               .Replace("__KPIS_DEMO__", kpisJs)
               .Replace("__ACTIVITY__", activityJs)
               .Replace("__DISPLAY_NAME__", displayName)
               .Replace("__COMPUTE_KPIS_BODY__", computeBody);
    }

    private static string DashboardRefresherComponent() => """
        "use client";
        import { useEffect } from "react";
        import { useRouter } from "next/navigation";

        export default function DashboardRefresher({ intervalMs }: { intervalMs: number }) {
          const router = useRouter();
          useEffect(() => {
            const refresh = () => router.refresh();
            const id = setInterval(refresh, intervalMs);
            const onVis = () => { if (document.visibilityState === "visible") refresh(); };
            document.addEventListener("visibilitychange", onVis);
            return () => { clearInterval(id); document.removeEventListener("visibilitychange", onVis); };
          }, [router, intervalMs]);
          return null;
        }
        """;

    private static string LoginPage(string displayName, string demoEmail, string demoPassword) =>
        """
        "use client";
        import { useState } from "react";
        import { useRouter } from "next/navigation";

        const APP_NAME    = "__DISPLAY_NAME__";
        const APP_INITIAL = APP_NAME.charAt(0).toUpperCase();
        const DEMO_EMAIL  = "__EMAIL__";
        const DEMO_PASS   = "__PASSWORD__";

        export default function LoginPage() {
          const [email,    setEmail]    = useState(DEMO_EMAIL);
          const [password, setPassword] = useState(DEMO_PASS);
          const [error,    setError]    = useState("");
          const [loading,  setLoading]  = useState(false);
          const router = useRouter();

          const handleLogin = async (e: React.FormEvent) => {
            e.preventDefault();
            setLoading(true);
            setError("");
            try {
              const res = await fetch("/api/auth/login", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ email, password }),
              });
              if (!res.ok) {
                setError("Credenciales incorrectas. Usá las credenciales demo.");
                return;
              }
              router.push("/dashboard");
              router.refresh();
            } catch {
              setError("Error de conexión. Intentá de nuevo.");
            } finally {
              setLoading(false);
            }
          };

          return (
            <div style={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh", background: "var(--background)" }}>
              <div style={{ width: "100%", maxWidth: "400px", padding: "0 20px" }}>
                <div style={{ background: "var(--surface)", borderRadius: "20px", border: "1px solid var(--border)", padding: "40px", boxShadow: "0 24px 64px rgba(0,0,0,0.35)" }}>

                  <div style={{ display: "flex", alignItems: "center", gap: "14px", marginBottom: "28px" }}>
                    <div style={{ width: "46px", height: "46px", borderRadius: "12px", background: "var(--accent)", display: "flex", alignItems: "center", justifyContent: "center", fontSize: "22px", fontWeight: "700", color: "#fff", flexShrink: 0 }}>
                      {APP_INITIAL}
                    </div>
                    <div>
                      <h1 style={{ fontSize: "18px", fontWeight: "700", color: "var(--foreground)", lineHeight: "1.2", marginBottom: "2px" }}>{APP_NAME}</h1>
                      <p style={{ color: "var(--foreground-muted)", fontSize: "12px" }}>Iniciá sesión para continuar</p>
                    </div>
                  </div>

                  <div style={{ padding: "12px 14px", background: "var(--surface-elevated)", border: "1px solid var(--border)", borderRadius: "10px", marginBottom: "24px" }}>
                    <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "8px" }}>
                      <p style={{ fontSize: "10px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Acceso demo</p>
                      <span style={{ fontSize: "10px", fontWeight: "600", padding: "2px 8px", borderRadius: "4px", background: "var(--status-info-bg)", color: "var(--status-info-text)" }}>DEMO</span>
                    </div>
                    <p style={{ fontSize: "12px", fontFamily: "monospace", color: "var(--foreground)", marginBottom: "2px" }}>{DEMO_EMAIL}</p>
                    <p style={{ fontSize: "12px", fontFamily: "monospace", color: "var(--foreground)" }}>{DEMO_PASS}</p>
                  </div>

                  <form onSubmit={handleLogin}>
                    <div style={{ marginBottom: "16px" }}>
                      <label style={{ display: "block", fontSize: "11px", fontWeight: "600", color: "var(--muted)", marginBottom: "6px", textTransform: "uppercase", letterSpacing: "0.05em" }}>Email</label>
                      <input type="email" value={email} onChange={e => setEmail(e.target.value)} required autoFocus
                        style={{ width: "100%", padding: "11px 14px", background: "var(--surface-elevated)", border: "1px solid var(--border)", borderRadius: "8px", color: "var(--foreground)", fontSize: "14px", outline: "none" }} />
                    </div>
                    <div style={{ marginBottom: "20px" }}>
                      <label style={{ display: "block", fontSize: "11px", fontWeight: "600", color: "var(--muted)", marginBottom: "6px", textTransform: "uppercase", letterSpacing: "0.05em" }}>Contraseña</label>
                      <input type="password" value={password} onChange={e => setPassword(e.target.value)} required
                        style={{ width: "100%", padding: "11px 14px", background: "var(--surface-elevated)", border: "1px solid var(--border)", borderRadius: "8px", color: "var(--foreground)", fontSize: "14px", outline: "none" }} />
                    </div>
                    {error && (
                      <p style={{ fontSize: "12px", color: "var(--status-danger-text)", background: "var(--status-danger-bg)", padding: "9px 12px", borderRadius: "6px", marginBottom: "16px" }}>{error}</p>
                    )}
                    <button type="submit" disabled={loading}
                      style={{ width: "100%", padding: "12px", background: loading ? "var(--surface-elevated)" : "var(--accent)", color: loading ? "var(--muted)" : "#fff", border: "none", borderRadius: "8px", fontSize: "14px", fontWeight: "600", cursor: loading ? "not-allowed" : "pointer", transition: "background 0.15s" }}>
                      {loading ? "Entrando..." : "Entrar"}
                    </button>
                  </form>
                </div>
                <p style={{ textAlign: "center", fontSize: "11px", color: "var(--muted)", marginTop: "16px", opacity: 0.6 }}>
                  Generado por Builder Platform
                </p>
              </div>
            </div>
          );
        }
        """.Replace("__DISPLAY_NAME__", displayName)
           .Replace("__EMAIL__",        demoEmail)
           .Replace("__PASSWORD__",     demoPassword);

    private static string FeaturePage(string feature, string route, ProductProfile profile)
    {
        var template = ContentGenerator.GetModuleTemplate(feature, route, profile.Industry);
        if (template is null)
            return GenericFeaturePage(feature, route);

        var colsJs = string.Join(", ", template.Columns.Select(c => $"\"{EscapeJs(c)}\""));
        var rowsJs = string.Join(",\n  ", template.Rows.Select(r =>
        {
            var cells = string.Join(", ", r.Cells.Select(c => $"\"{EscapeJs(c)}\""));
            return $"{{ cells: [{cells}], sc: \"{r.StatusColor}\" }}";
        }));
        var transitionsJs = template.Transitions?.Length > 0
            ? string.Join(",\n  ", template.Transitions.Select(t =>
                $"{{ from: \"{EscapeJs(t.From)}\", to: \"{EscapeJs(t.To)}\", label: \"{EscapeJs(t.Label)}\", color: \"{t.ActionColor}\" }}"))
            : "";

        return
            """
            "use client";
            import { useState, useEffect, useCallback } from "react";
            import { Inbox, CheckCircle, Plus } from "lucide-react";

            const TITLE      = "__TITLE__";
            const ACTION     = "__ACTION__";
            const KPI        = "__KPI_BAR__";
            const STATUS_COL = __STATUS_COL__ as number;
            const COLS       = [__COLS__];
            const DEMO_ROWS  = [
              __ROWS__
            ] as { cells: string[]; sc: string }[];

            type Row        = { id?: string; cells: string[]; sc: string };
            type Transition = { from: string; to: string; label: string; color: string };
            const TRANSITIONS: Transition[] = [__TRANSITIONS__];
            const INITIAL_STATUS = TRANSITIONS.length > 0 ? TRANSITIONS[0].from  : "";
            const INITIAL_SC     = TRANSITIONS.length > 0 ? TRANSITIONS[0].color : "info";

            const bg  = (c: string): string =>
              c === "active" ? "var(--status-active-bg)"   :
              c === "warn"   ? "var(--status-warn-bg)"     :
              c === "danger" ? "var(--status-danger-bg)"   : "var(--status-info-bg)";
            const txt = (c: string): string =>
              c === "active" ? "var(--status-active-text)" :
              c === "warn"   ? "var(--status-warn-text)"   :
              c === "danger" ? "var(--status-danger-text)" : "var(--status-info-text)";

            export default function __PASCAL__Page() {
              const [savedRows,  setSavedRows]  = useState<Row[]>([]);
              const [loadDone,   setLoadDone]   = useState(false);
              const [showModal,  setShowModal]  = useState(false);
              const [form,       setForm]       = useState<Record<string, string>>({});
              const [saving,     setSaving]     = useState(false);
              const [toast,      setToast]      = useState<string | null>(null);
              const [hoveredRow, setHoveredRow] = useState<string | null>(null);

              const load = useCallback(async () => {
                try {
                  const res = await fetch("/api/data/__ROUTE__");
                  if (res.ok) {
                    const data = await res.json() as Array<Record<string, string>>;
                    setSavedRows(data.map(r => ({
                      id:    r.id,
                      cells: COLS.map(c => r[c] || ""),
                      sc:    r["_sc"] || "info",
                    })));
                  }
                } catch {}
                finally { setLoadDone(true); }
              }, []);

              useEffect(() => { void load(); }, [load]);

              useEffect(() => {
                const id = setInterval(() => { void load(); }, 5000);
                return () => clearInterval(id);
              }, [load]);

              useEffect(() => {
                if (!toast) return;
                const t = setTimeout(() => setToast(null), 3000);
                return () => clearTimeout(t);
              }, [toast]);

              const formCols = COLS.filter((_, ci) => ci !== STATUS_COL);
              const canSave  = !saving && (formCols.length === 0 || !!form[formCols[0]]?.trim());
              const hasSaved    = savedRows.length > 0;
              const hasDemoOnly = !hasSaved && DEMO_ROWS.length > 0;

              const save = async () => {
                if (!canSave) return;
                setSaving(true);
                try {
                  const payload: Record<string, string> = { ...form, _sc: INITIAL_SC };
                  const sf = STATUS_COL >= 0 ? COLS[STATUS_COL] : undefined;
                  if (sf && INITIAL_STATUS) payload[sf] = INITIAL_STATUS;
                  await fetch("/api/data/__ROUTE__", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(payload),
                  });
                  setShowModal(false);
                  setForm({});
                  await load();
                  setToast(`${TITLE} agregado correctamente`);
                } finally { setSaving(false); }
              };

              const updateRecord = async (id: string, row: Row, t: Transition) => {
                const sf = STATUS_COL >= 0 ? COLS[STATUS_COL] : undefined;
                if (!sf) return;
                await fetch("/api/data/__ROUTE__", {
                  method: "PATCH",
                  headers: { "Content-Type": "application/json" },
                  body: JSON.stringify({ id, [sf]: t.to, _sc: t.color }),
                });
                void fetch("/api/workflow", {
                  method: "POST",
                  headers: { "Content-Type": "application/json" },
                  body: JSON.stringify({
                    event: "transition",
                    module: "__ROUTE__",
                    row: Object.fromEntries(COLS.map((c, i) => [c, row.cells[i] ?? ""])),
                    from: t.from,
                    to:   t.to,
                  }),
                });
                await load();
                setToast(`${t.label} — estado actualizado`);
              };

              return (
                <div style={{ padding: "28px", maxWidth: "1280px" }}>
                  <div style={{ marginBottom: "24px", display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
                    <div>
                      <h1 style={{ fontSize: "22px", fontWeight: "700", color: "var(--foreground)", marginBottom: "4px" }}>{TITLE}</h1>
                      <p style={{ color: "var(--foreground-muted)", fontSize: "13px" }}>
                        {KPI}
                        {hasSaved && <span style={{ marginLeft: "10px", color: "var(--status-active-text)", fontSize: "12px" }}>· {savedRows.length} registros reales</span>}
                        {hasDemoOnly && <span style={{ marginLeft: "10px", color: "var(--muted)", fontSize: "12px" }}>· datos de ejemplo</span>}
                      </p>
                    </div>
                    <button onClick={() => setShowModal(true)}
                      style={{ display: "flex", alignItems: "center", gap: "6px", padding: "9px 18px", borderRadius: "8px", background: "var(--accent)", color: "#fff", fontSize: "13px", fontWeight: "600", border: "none", cursor: "pointer" }}>
                      <Plus size={14} />
                      {ACTION}
                    </button>
                  </div>

                  {loadDone && !hasSaved && DEMO_ROWS.length === 0 ? (
                    <div style={{ background: "var(--surface)", borderRadius: "12px", border: "1px solid var(--border)", padding: "64px 20px", textAlign: "center" }}>
                      <Inbox size={40} style={{ color: "var(--muted)", display: "block", margin: "0 auto 12px" }} />
                      <p style={{ fontSize: "16px", fontWeight: "600", color: "var(--foreground)", marginBottom: "6px" }}>Sin registros aún</p>
                      <p style={{ fontSize: "13px", color: "var(--foreground-muted)", marginBottom: "24px" }}>Creá el primer {TITLE} para empezar a operar.</p>
                      <button onClick={() => setShowModal(true)}
                        style={{ display: "inline-flex", alignItems: "center", gap: "6px", padding: "10px 22px", borderRadius: "8px", background: "var(--accent)", color: "#fff", fontSize: "13px", fontWeight: "600", border: "none", cursor: "pointer" }}>
                        <Plus size={14} />
                        {ACTION}
                      </button>
                    </div>
                  ) : (
                    <div style={{ background: "var(--surface)", borderRadius: "12px", border: "1px solid var(--border)", overflow: "hidden" }}>
                      {hasDemoOnly && (
                        <div style={{ padding: "8px 16px", background: "var(--surface-elevated)", borderBottom: "1px solid var(--border)", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                          <span style={{ fontSize: "11px", color: "var(--muted)" }}>Datos de ejemplo — creá registros reales con <strong>{ACTION}</strong></span>
                          <span style={{ fontSize: "10px", fontWeight: "600", padding: "2px 8px", borderRadius: "4px", background: "var(--status-info-bg)", color: "var(--status-info-text)" }}>DEMO</span>
                        </div>
                      )}
                      <table style={{ width: "100%", borderCollapse: "collapse" }}>
                        <thead>
                          <tr style={{ background: "var(--surface-elevated)", borderBottom: "1px solid var(--border)" }}>
                            {COLS.map((col) => (
                              <th key={col} style={{ padding: "12px 16px", textAlign: "left", fontSize: "11px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em", whiteSpace: "nowrap" }}>{col}</th>
                            ))}
                          </tr>
                        </thead>
                        <tbody>
                          {!loadDone && (
                            <tr><td colSpan={COLS.length} style={{ padding: "40px", textAlign: "center", color: "var(--muted)", fontSize: "13px" }}>Cargando...</td></tr>
                          )}
                          {loadDone && savedRows.map((row, ri) => (
                            <tr key={ri}
                              onMouseEnter={() => setHoveredRow(`s${ri}`)}
                              onMouseLeave={() => setHoveredRow(null)}
                              style={{ borderTop: "1px solid var(--border)", background: hoveredRow === `s${ri}` ? "var(--surface-elevated)" : "transparent", transition: "background 0.1s" }}>
                              {row.cells.map((cell, ci) => (
                                <td key={ci} style={{ padding: "13px 16px" }}>
                                  {STATUS_COL >= 0 && ci === STATUS_COL ? (
                                    <div style={{ display: "flex", alignItems: "center", gap: "8px", flexWrap: "wrap" }}>
                                      <span style={{ fontSize: "11px", fontWeight: "500", padding: "3px 10px", borderRadius: "99px", background: bg(row.sc), color: txt(row.sc) }}>{cell}</span>
                                      {TRANSITIONS.length > 0 && (() => {
                                        const tr = TRANSITIONS.find(t => t.from === cell);
                                        return tr && row.id ? (
                                          <button onClick={() => void updateRecord(row.id!, row, tr)}
                                            style={{ fontSize: "11px", padding: "3px 10px", borderRadius: "99px", background: "var(--surface-elevated)", color: "var(--foreground-muted)", border: "1px solid var(--border)", cursor: "pointer", whiteSpace: "nowrap" }}>
                                            {tr.label} →
                                          </button>
                                        ) : null;
                                      })()}
                                    </div>
                                  ) : (
                                    <span style={{ fontSize: ci === 0 ? "13px" : "12px", fontWeight: ci === 0 ? "500" : "400", color: ci === 0 ? "var(--foreground)" : "var(--foreground-muted)" }}>{cell}</span>
                                  )}
                                </td>
                              ))}
                            </tr>
                          ))}
                          {loadDone && hasSaved && DEMO_ROWS.length > 0 && (
                            <tr>
                              <td colSpan={COLS.length} style={{ padding: "5px 16px", background: "var(--surface-elevated)", fontSize: "10px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>
                                Datos demo
                              </td>
                            </tr>
                          )}
                          {loadDone && DEMO_ROWS.map((row, ri) => (
                            <tr key={"d" + ri}
                              onMouseEnter={() => setHoveredRow(`d${ri}`)}
                              onMouseLeave={() => setHoveredRow(null)}
                              style={{ borderTop: "1px solid var(--border)", opacity: hasSaved ? 0.35 : 1, background: hoveredRow === `d${ri}` ? "var(--surface-elevated)" : "transparent", transition: "background 0.1s" }}>
                              {row.cells.map((cell, ci) => (
                                <td key={ci} style={{ padding: "13px 16px" }}>
                                  {STATUS_COL >= 0 && ci === STATUS_COL ? (
                                    <span style={{ fontSize: "11px", fontWeight: "500", padding: "3px 10px", borderRadius: "99px", background: bg(row.sc), color: txt(row.sc) }}>{cell}</span>
                                  ) : (
                                    <span style={{ fontSize: ci === 0 ? "13px" : "12px", fontWeight: ci === 0 ? "500" : "400", color: ci === 0 ? "var(--foreground)" : "var(--foreground-muted)" }}>{cell}</span>
                                  )}
                                </td>
                              ))}
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}

                  {showModal && (
                    <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.7)", display: "flex", alignItems: "center", justifyContent: "center", zIndex: 100 }}
                      onClick={() => setShowModal(false)}>
                      <div style={{ background: "var(--surface)", borderRadius: "16px", padding: "28px", width: "440px", maxWidth: "90vw", border: "1px solid var(--border)", boxShadow: "0 20px 60px rgba(0,0,0,0.5)" }}
                        onClick={e => e.stopPropagation()}>
                        <h2 style={{ fontSize: "16px", fontWeight: "700", color: "var(--foreground)", marginBottom: "6px" }}>{ACTION}</h2>
                        <p style={{ fontSize: "12px", color: "var(--foreground-muted)", marginBottom: "20px" }}>Completá los campos y guardá.</p>
                        {formCols.map((col, idx) => (
                          <div key={col} style={{ marginBottom: "14px" }}>
                            <label style={{ display: "block", fontSize: "11px", fontWeight: "600", color: "var(--muted)", marginBottom: "5px", textTransform: "uppercase", letterSpacing: "0.05em" }}>
                              {col}{idx === 0 && <span style={{ color: "var(--status-danger-text)", marginLeft: "4px" }}>*</span>}
                            </label>
                            <input
                              value={form[col] ?? ""}
                              onChange={e => setForm(prev => ({ ...prev, [col]: e.target.value }))}
                              placeholder={`Ingresá ${col.toLowerCase()}...`}
                              autoFocus={idx === 0}
                              style={{ width: "100%", padding: "10px 12px", background: "var(--surface-elevated)", border: "1px solid var(--border)", borderRadius: "8px", color: "var(--foreground)", fontSize: "13px", outline: "none" }}
                            />
                          </div>
                        ))}
                        <div style={{ display: "flex", gap: "8px", marginTop: "24px", justifyContent: "flex-end" }}>
                          <button onClick={() => { setShowModal(false); setForm({}); }}
                            style={{ padding: "9px 16px", borderRadius: "8px", background: "var(--surface-elevated)", color: "var(--foreground-muted)", border: "1px solid var(--border)", fontSize: "13px", cursor: "pointer" }}>
                            Cancelar
                          </button>
                          <button onClick={save} disabled={!canSave}
                            style={{ display: "flex", alignItems: "center", gap: "6px", padding: "9px 20px", borderRadius: "8px", background: canSave ? "var(--accent)" : "var(--surface-elevated)", color: canSave ? "#fff" : "var(--muted)", border: "none", fontSize: "13px", fontWeight: "600", cursor: canSave ? "pointer" : "not-allowed" }}>
                            {saving ? "Guardando..." : "Guardar"}
                          </button>
                        </div>
                      </div>
                    </div>
                  )}

                  {toast && (
                    <div style={{ position: "fixed", bottom: "24px", right: "24px", background: "var(--surface)", border: "1px solid var(--status-active-bg)", borderRadius: "10px", padding: "12px 16px", display: "flex", alignItems: "center", gap: "10px", zIndex: 200, boxShadow: "0 4px 24px rgba(0,0,0,0.4)" }}>
                      <CheckCircle size={16} style={{ color: "var(--status-active-text)", flexShrink: 0 }} />
                      <span style={{ fontSize: "13px", color: "var(--foreground)", fontWeight: "500" }}>{toast}</span>
                    </div>
                  )}
                </div>
              );
            }
            """.Replace("__PASCAL__",      ToPascalCase(route))
               .Replace("__TITLE__",       EscapeJs(template.Title))
               .Replace("__ACTION__",      EscapeJs(template.ActionLabel))
               .Replace("__KPI_BAR__",     EscapeJs(template.KpiBar))
               .Replace("__STATUS_COL__",  template.StatusColumnIndex.ToString())
               .Replace("__COLS__",        colsJs)
               .Replace("__ROWS__",        rowsJs)
               .Replace("__TRANSITIONS__", transitionsJs)
               .Replace("__ROUTE__",       route);
    }

    private static string GenericFeaturePage(string feature, string route) =>
        ("""
        "use client";
        import { useState, useEffect, useCallback } from "react";
        import { Inbox, CheckCircle, Plus } from "lucide-react";

        const DEMO_ROWS = [
          { name: "Demo Item 1", status: "Activo",     date: "2026-05-15", sc: "active" },
          { name: "Demo Item 2", status: "Pendiente",  date: "2026-05-14", sc: "warn" },
          { name: "Demo Item 3", status: "Completado", date: "2026-05-13", sc: "info" },
        ];
        const bg   = (c: string) => c === "active" ? "var(--status-active-bg)"   : c === "warn" ? "var(--status-warn-bg)"   : "var(--status-info-bg)";
        const text = (c: string) => c === "active" ? "var(--status-active-text)" : c === "warn" ? "var(--status-warn-text)" : "var(--status-info-text)";

        type GRow = { name: string; status: string; date: string; sc: string };

        export default function __PASCAL__Page() {
          const [savedRows,  setSavedRows]  = useState<GRow[]>([]);
          const [loadDone,   setLoadDone]   = useState(false);
          const [showModal,  setShowModal]  = useState(false);
          const [form,       setForm]       = useState({ name: "", status: "Activo" });
          const [saving,     setSaving]     = useState(false);
          const [toast,      setToast]      = useState<string | null>(null);
          const [hoveredRow, setHoveredRow] = useState<string | null>(null);

          const load = useCallback(async () => {
            try {
              const res = await fetch("/api/data/__ROUTE__");
              if (res.ok) {
                const data = await res.json() as GRow[];
                setSavedRows(data);
              }
            } catch {}
            finally { setLoadDone(true); }
          }, []);

          useEffect(() => { void load(); }, [load]);

          useEffect(() => {
            const id = setInterval(() => { void load(); }, 5000);
            return () => clearInterval(id);
          }, [load]);

          useEffect(() => {
            if (!toast) return;
            const t = setTimeout(() => setToast(null), 3000);
            return () => clearTimeout(t);
          }, [toast]);

          const canSave = !saving && !!form.name.trim();
          const hasSaved = savedRows.length > 0;

          const save = async () => {
            if (!canSave) return;
            setSaving(true);
            try {
              await fetch("/api/data/__ROUTE__", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ ...form, date: new Date().toISOString().slice(0, 10), sc: "active" }),
              });
              setShowModal(false);
              setForm({ name: "", status: "Activo" });
              await load();
              setToast("Registro agregado correctamente");
            } finally { setSaving(false); }
          };

          return (
            <div style={{ padding: "28px" }}>
              <div style={{ marginBottom: "24px", display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
                <div>
                  <h1 style={{ fontSize: "22px", fontWeight: "700", color: "var(--foreground)", marginBottom: "4px" }}>__FEATURE__</h1>
                  <p style={{ color: "var(--foreground-muted)", fontSize: "13px" }}>
                    Módulo generado · datos persistentes
                    {hasSaved && <span style={{ marginLeft: "10px", color: "var(--status-active-text)", fontSize: "12px" }}>· {savedRows.length} registros</span>}
                  </p>
                </div>
                <button onClick={() => setShowModal(true)}
                  style={{ display: "flex", alignItems: "center", gap: "6px", padding: "9px 18px", borderRadius: "8px", background: "var(--accent)", color: "#fff", fontSize: "13px", fontWeight: "600", border: "none", cursor: "pointer" }}>
                  <Plus size={14} />
                  Nuevo
                </button>
              </div>

              {loadDone && !hasSaved ? (
                <div style={{ background: "var(--surface)", borderRadius: "12px", border: "1px solid var(--border)", padding: "64px 20px", textAlign: "center" }}>
                  <Inbox size={40} style={{ color: "var(--muted)", display: "block", margin: "0 auto 12px" }} />
                  <p style={{ fontSize: "16px", fontWeight: "600", color: "var(--foreground)", marginBottom: "6px" }}>Sin registros aún</p>
                  <p style={{ fontSize: "13px", color: "var(--foreground-muted)", marginBottom: "24px" }}>Creá el primer registro de __FEATURE__.</p>
                  <button onClick={() => setShowModal(true)}
                    style={{ display: "inline-flex", alignItems: "center", gap: "6px", padding: "10px 22px", borderRadius: "8px", background: "var(--accent)", color: "#fff", fontSize: "13px", fontWeight: "600", border: "none", cursor: "pointer" }}>
                    <Plus size={14} />
                    Nuevo registro
                  </button>
                </div>
              ) : (
                <div style={{ background: "var(--surface)", borderRadius: "12px", border: "1px solid var(--border)", overflow: "hidden" }}>
                  <table style={{ width: "100%", borderCollapse: "collapse" }}>
                    <thead>
                      <tr style={{ background: "var(--surface-elevated)", borderBottom: "1px solid var(--border)" }}>
                        <th style={{ padding: "12px 16px", textAlign: "left", fontSize: "11px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Nombre</th>
                        <th style={{ padding: "12px 16px", textAlign: "left", fontSize: "11px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Estado</th>
                        <th style={{ padding: "12px 16px", textAlign: "left", fontSize: "11px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Fecha</th>
                      </tr>
                    </thead>
                    <tbody>
                      {!loadDone && (
                        <tr><td colSpan={3} style={{ padding: "40px", textAlign: "center", color: "var(--muted)", fontSize: "13px" }}>Cargando...</td></tr>
                      )}
                      {loadDone && savedRows.map((row, i) => (
                        <tr key={i}
                          onMouseEnter={() => setHoveredRow(`s${i}`)}
                          onMouseLeave={() => setHoveredRow(null)}
                          style={{ borderTop: "1px solid var(--border)", background: hoveredRow === `s${i}` ? "var(--surface-elevated)" : "transparent", transition: "background 0.1s" }}>
                          <td style={{ padding: "14px 16px", fontSize: "13px", color: "var(--foreground)", fontWeight: "500" }}>{row.name}</td>
                          <td style={{ padding: "14px 16px" }}>
                            <span style={{ fontSize: "11px", fontWeight: "500", padding: "3px 10px", borderRadius: "99px", background: bg(row.sc), color: text(row.sc) }}>{row.status}</span>
                          </td>
                          <td style={{ padding: "14px 16px", fontSize: "12px", color: "var(--foreground-muted)" }}>{row.date}</td>
                        </tr>
                      ))}
                      {loadDone && hasSaved && (
                        <tr><td colSpan={3} style={{ padding: "5px 16px", background: "var(--surface-elevated)", fontSize: "10px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Datos demo</td></tr>
                      )}
                      {loadDone && DEMO_ROWS.map((row, i) => (
                        <tr key={row.name}
                          onMouseEnter={() => setHoveredRow(`d${i}`)}
                          onMouseLeave={() => setHoveredRow(null)}
                          style={{ borderTop: "1px solid var(--border)", opacity: hasSaved ? 0.35 : 1, background: hoveredRow === `d${i}` ? "var(--surface-elevated)" : "transparent", transition: "background 0.1s" }}>
                          <td style={{ padding: "14px 16px", fontSize: "13px", color: "var(--foreground)", fontWeight: "500" }}>{row.name}</td>
                          <td style={{ padding: "14px 16px" }}>
                            <span style={{ fontSize: "11px", fontWeight: "500", padding: "3px 10px", borderRadius: "99px", background: bg(row.sc), color: text(row.sc) }}>{row.status}</span>
                          </td>
                          <td style={{ padding: "14px 16px", fontSize: "12px", color: "var(--foreground-muted)" }}>{row.date}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {showModal && (
                <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.7)", display: "flex", alignItems: "center", justifyContent: "center", zIndex: 100 }}
                  onClick={() => setShowModal(false)}>
                  <div style={{ background: "var(--surface)", borderRadius: "16px", padding: "28px", width: "400px", maxWidth: "90vw", border: "1px solid var(--border)", boxShadow: "0 20px 60px rgba(0,0,0,0.5)" }}
                    onClick={e => e.stopPropagation()}>
                    <h2 style={{ fontSize: "16px", fontWeight: "700", color: "var(--foreground)", marginBottom: "6px" }}>Nuevo registro</h2>
                    <p style={{ fontSize: "12px", color: "var(--foreground-muted)", marginBottom: "20px" }}>__FEATURE__ · completá los campos y guardá.</p>
                    <div style={{ marginBottom: "14px" }}>
                      <label style={{ display: "block", fontSize: "11px", fontWeight: "600", color: "var(--muted)", marginBottom: "5px", textTransform: "uppercase", letterSpacing: "0.05em" }}>
                        Nombre <span style={{ color: "var(--status-danger-text)" }}>*</span>
                      </label>
                      <input value={form.name} onChange={e => setForm(p => ({ ...p, name: e.target.value }))}
                        placeholder="Ingresá un nombre..."
                        autoFocus
                        style={{ width: "100%", padding: "10px 12px", background: "var(--surface-elevated)", border: "1px solid var(--border)", borderRadius: "8px", color: "var(--foreground)", fontSize: "13px", outline: "none" }} />
                    </div>
                    <div style={{ marginBottom: "14px" }}>
                      <label style={{ display: "block", fontSize: "11px", fontWeight: "600", color: "var(--muted)", marginBottom: "5px", textTransform: "uppercase", letterSpacing: "0.05em" }}>Estado</label>
                      <select value={form.status} onChange={e => setForm(p => ({ ...p, status: e.target.value }))}
                        style={{ width: "100%", padding: "10px 12px", background: "var(--surface-elevated)", border: "1px solid var(--border)", borderRadius: "8px", color: "var(--foreground)", fontSize: "13px", outline: "none" }}>
                        <option>Activo</option>
                        <option>Pendiente</option>
                        <option>Completado</option>
                      </select>
                    </div>
                    <div style={{ display: "flex", gap: "8px", marginTop: "24px", justifyContent: "flex-end" }}>
                      <button onClick={() => { setShowModal(false); setForm({ name: "", status: "Activo" }); }}
                        style={{ padding: "9px 16px", borderRadius: "8px", background: "var(--surface-elevated)", color: "var(--foreground-muted)", border: "1px solid var(--border)", fontSize: "13px", cursor: "pointer" }}>
                        Cancelar
                      </button>
                      <button onClick={save} disabled={!canSave}
                        style={{ display: "flex", alignItems: "center", gap: "6px", padding: "9px 20px", borderRadius: "8px", background: canSave ? "var(--accent)" : "var(--surface-elevated)", color: canSave ? "#fff" : "var(--muted)", border: "none", fontSize: "13px", fontWeight: "600", cursor: canSave ? "pointer" : "not-allowed" }}>
                        {saving ? "Guardando..." : "Guardar"}
                      </button>
                    </div>
                  </div>
                </div>
              )}

              {toast && (
                <div style={{ position: "fixed", bottom: "24px", right: "24px", background: "var(--surface)", border: "1px solid var(--status-active-bg)", borderRadius: "10px", padding: "12px 16px", display: "flex", alignItems: "center", gap: "10px", zIndex: 200, boxShadow: "0 4px 24px rgba(0,0,0,0.4)" }}>
                  <CheckCircle size={16} style={{ color: "var(--status-active-text)", flexShrink: 0 }} />
                  <span style={{ fontSize: "13px", color: "var(--foreground)", fontWeight: "500" }}>{toast}</span>
                </div>
              )}
            </div>
          );
        }
        """).Replace("__PASCAL__", ToPascalCase(route))
            .Replace("__FEATURE__", feature)
            .Replace("__ROUTE__", route);

    // Server component — reads registry/nav-items.json at request time so delta features appear automatically
    private static string SidebarServer(string displayName) =>
        ("""
        import fs from "fs";
        import path from "path";
        import { SidebarClient } from "./SidebarClient";

        function readNavItems(): { href: string; label: string }[] {
          try {
            const filePath = path.join(process.cwd(), "registry", "nav-items.json");
            const raw = fs.readFileSync(filePath, "utf-8");
            const data = JSON.parse(raw);
            const items: { href: string; label: string }[] = Array.isArray(data) ? data : (data.value ?? []);
            const hasDash = items.some((i) => i.href === "/dashboard");
            return hasDash ? items : [{ href: "/dashboard", label: "Dashboard" }, ...items];
          } catch {
            return [{ href: "/dashboard", label: "Dashboard" }];
          }
        }

        export function Sidebar() {
          const navItems = readNavItems();
          return <SidebarClient displayName="__DISPLAY_NAME__" navItems={navItems} />;
        }
        """).Replace("__DISPLAY_NAME__", displayName);

    private static string SidebarClientComponent(string displayName) =>
        ("""
        "use client";
        import Link from "next/link";
        import { usePathname } from "next/navigation";
        import { useState } from "react";

        interface NavItem { href: string; label: string; }

        export function SidebarClient({ displayName, navItems }: { displayName: string; navItems: NavItem[] }) {
          const pathname    = usePathname();
          const [hovered, setHovered] = useState<string | null>(null);
          return (
            <aside style={{ width: "220px", flexShrink: 0, background: "var(--surface)", borderRight: "1px solid var(--border)", display: "flex", flexDirection: "column" }}>
              <div style={{ padding: "18px 16px 14px", borderBottom: "1px solid var(--border)" }}>
                <span style={{ fontSize: "14px", fontWeight: "700", color: "var(--foreground)" }}>{displayName}</span>
              </div>
              <nav style={{ padding: "8px", flex: 1 }}>
                {navItems.map(({ href, label }) => {
                  const active  = pathname === href || (href !== "/" && pathname.startsWith(href));
                  const isHover = hovered === href && !active;
                  return (
                    <Link key={href} href={href}
                      onMouseEnter={() => setHovered(href)}
                      onMouseLeave={() => setHovered(null)}
                      style={{ display: "flex", alignItems: "center", padding: "8px 12px", borderRadius: "8px", marginBottom: "2px",
                        fontSize: "13px", fontWeight: active ? "600" : "400",
                        background: active ? "var(--surface-elevated)" : isHover ? "rgba(255,255,255,0.04)" : "transparent",
                        color: active ? "var(--foreground)" : isHover ? "var(--foreground)" : "var(--muted)",
                        borderLeft: active ? "3px solid var(--accent)" : "3px solid transparent",
                        paddingLeft: active ? "9px" : "12px",
                        transition: "background 0.1s, color 0.1s" }}>
                      {label}
                    </Link>
                  );
                })}
              </nav>
              <div style={{ padding: "8px", borderTop: "1px solid var(--border)" }}>
                <button
                  onClick={async () => {
                    await fetch("/api/auth/logout", { method: "POST" });
                    window.location.href = "/login";
                  }}
                  style={{ width: "100%", padding: "8px 12px", borderRadius: "8px", background: "none", border: "none", cursor: "pointer", fontSize: "12px", color: "var(--muted)", textAlign: "left" }}
                >
                  Cerrar sesión
                </button>
                <p style={{ padding: "4px 12px 8px", fontSize: "10px", color: "var(--muted)", opacity: 0.6 }}>
                  Built with Builder Platform
                </p>
              </div>
            </aside>
          );
        }
        """).Replace("__DISPLAY_NAME__", displayName);


    private static string HeaderComponent() => """
        "use client";
        export function Header({ title }: { title: string }) {
          return (
            <header style={{ height: "52px", display: "flex", alignItems: "center", padding: "0 24px",
              borderBottom: "1px solid var(--border)", background: "var(--surface)", flexShrink: 0 }}>
              <span style={{ fontSize: "14px", fontWeight: "600", color: "var(--foreground)" }}>{title}</span>
            </header>
          );
        }
        """;

    private static string AppShellComponent() => """
        "use client";
        import { usePathname } from "next/navigation";

        export function AppShell({
          children,
          sidebar,
        }: {
          children: React.ReactNode;
          sidebar: React.ReactNode;
        }) {
          const pathname = usePathname();
          if (pathname === "/login") {
            return (
              <div style={{ background: "var(--background)", minHeight: "100vh", color: "var(--foreground)" }}>
                {children}
              </div>
            );
          }
          return (
            <div style={{ display: "flex", height: "100vh", background: "var(--background)" }}>
              {sidebar}
              <main style={{ flex: 1, overflow: "auto" }}>{children}</main>
            </div>
          );
        }
        """;

    private static string Middleware() => """
        import { NextResponse } from "next/server";
        import type { NextRequest } from "next/server";

        export function middleware(request: NextRequest) {
          const { pathname } = request.nextUrl;
          const session  = request.cookies.get("bp-session");
          const isPublic = pathname === "/login" || pathname.startsWith("/api/auth");
          const isApi    = pathname.startsWith("/api/");

          if (!session && !isPublic) {
            if (isApi) {
              return new NextResponse(JSON.stringify({ error: "Unauthorized" }), {
                status: 401,
                headers: { "Content-Type": "application/json" },
              });
            }
            return NextResponse.redirect(new URL("/login", request.url));
          }
          if (session && pathname === "/login") {
            return NextResponse.redirect(new URL("/dashboard", request.url));
          }
          return NextResponse.next();
        }

        export const config = {
          matcher: ["/((?!_next/static|_next/image|favicon\\.ico).*)"],
        };
        """;

    private static string ApiAuthLoginRoute(string demoEmail, string demoPassword) =>
        """
        import { NextResponse } from "next/server";

        const DEMO_EMAIL = "__EMAIL__";
        const DEMO_PASS  = "__PASSWORD__";

        export async function POST(req: Request) {
          const { email, password } = await req.json() as { email: string; password: string };
          if (email.toLowerCase().trim() === DEMO_EMAIL && password === DEMO_PASS) {
            const res = NextResponse.json({ ok: true });
            res.cookies.set("bp-session", "demo-ok", {
              httpOnly: true,
              maxAge: 60 * 60 * 24 * 7,
              path: "/",
              sameSite: "lax",
            });
            return res;
          }
          return NextResponse.json({ error: "Credenciales incorrectas" }, { status: 401 });
        }
        """.Replace("__EMAIL__",    demoEmail)
           .Replace("__PASSWORD__", demoPassword);

    private static string ApiAuthLogoutRoute() => """
        import { NextResponse } from "next/server";

        export async function POST() {
          const res = NextResponse.json({ ok: true });
          res.cookies.set("bp-session", "", { maxAge: 0, path: "/" });
          return res;
        }
        """;

    private static string ApiDataRoute() => """
        import { NextResponse } from "next/server";
        import { readFileSync, writeFileSync, mkdirSync, existsSync } from "fs";
        import { join } from "path";

        function dataFile(module: string): string {
          const safe = module.replace(/[^a-z0-9-]/g, "").slice(0, 50) || "default";
          const dir  = join(process.cwd(), ".data");
          if (!existsSync(dir)) mkdirSync(dir, { recursive: true });
          return join(dir, `${safe}.json`);
        }

        export async function GET(
          _req: Request,
          context: { params: Promise<{ module: string }> }
        ) {
          const { module } = await context.params;
          const file = dataFile(module);
          if (!existsSync(file)) return NextResponse.json([]);
          return NextResponse.json(JSON.parse(readFileSync(file, "utf-8")) as unknown[]);
        }

        export async function POST(
          req: Request,
          context: { params: Promise<{ module: string }> }
        ) {
          const { module } = await context.params;
          const body    = await req.json() as Record<string, string>;
          const file    = dataFile(module);
          const records = existsSync(file)
            ? JSON.parse(readFileSync(file, "utf-8")) as unknown[]
            : [];
          const record = { id: Date.now().toString(), ...body, _createdAt: new Date().toISOString() };
          records.push(record);
          writeFileSync(file, JSON.stringify(records, null, 2));
          return NextResponse.json(record, { status: 201 });
        }

        export async function PATCH(
          req: Request,
          context: { params: Promise<{ module: string }> }
        ) {
          const { module } = await context.params;
          const body    = await req.json() as { id: string } & Record<string, string>;
          const file    = dataFile(module);
          if (!existsSync(file)) return NextResponse.json({ error: "Not found" }, { status: 404 });
          const records = JSON.parse(readFileSync(file, "utf-8")) as Array<Record<string, string>>;
          const idx     = records.findIndex(r => r.id === body.id);
          if (idx === -1) return NextResponse.json({ error: "Not found" }, { status: 404 });
          records[idx] = { ...records[idx], ...body };
          writeFileSync(file, JSON.stringify(records, null, 2));
          return NextResponse.json(records[idx]);
        }
        """;

    private static string WorkflowApiRoute(string industry)
    {
        var handlers = industry switch
        {
            "restaurant" => """
                if (module === "pedidos-comandas" && from === "Pendiente" && to === "Preparando")
                  appendActivity(`Orden ${row["Orden"] ?? "#?"} · Mesa ${row["Mesa"] ?? "?"} — enviada a cocina`, "En preparación", "warn");
                else if (module === "pedidos-comandas" && from === "Preparando" && to === "En camino")
                  appendActivity(`Orden ${row["Orden"] ?? "#?"} · ${row["Ítems"] ?? ""} — lista para servir`, "Lista", "active");
                else if (module === "pedidos-comandas" && from === "En camino" && to === "Entregado")
                  appendActivity(`Orden ${row["Orden"] ?? "#?"} · Mesa ${row["Mesa"] ?? "?"} — entregada al cliente`, "Entregado", "active");
                else if (module === "display" && from === "Recibida" && to === "En preparación")
                  appendActivity(`KDS · Orden ${row["Orden"] ?? "#?"} — en preparación`, "Preparando", "warn");
                else if (module === "display" && from === "En preparación" && to === "Lista")
                  appendActivity(`KDS · Orden ${row["Orden"] ?? "#?"} — lista para entregar`, "Lista", "active");
                else if (module === "display" && from === "Lista" && to === "Entregada")
                  appendActivity(`KDS · Orden ${row["Orden"] ?? "#?"} — entregada`, "Entregado", "active");
                else
                  appendActivity(`${module} — ${from} → ${to}`, "Actualizado", "info");
                """,
            "veterinary" => """
                if (module === "citas" && from === "Programada" && to === "En consulta")
                  appendActivity(`Cita · ${row["Paciente"] ?? "?"} — en consulta`, "En consulta", "info");
                else if (module === "citas" && from === "En consulta" && to === "Completada")
                  appendActivity(`Cita · ${row["Paciente"] ?? "?"} — completada`, "Completado", "active");
                else
                  appendActivity(`${module} — ${from} → ${to}`, "Actualizado", "info");
                """,
            "hr_payroll" => """
                if (from && to)
                  appendActivity(`${module} · ${row["Nombre"] ?? row["Empleado"] ?? "?"} — ${from} → ${to}`, "Actualizado", "info");
                else
                  appendActivity(`${module} — registrado`, "Activo", "active");
                """,
            _ => """
                appendActivity(`${module} — ${from} → ${to}`, "Actualizado", "info");
                """
        };

        return ("""
            import { NextResponse } from "next/server";
            import { readFileSync, writeFileSync, mkdirSync, existsSync } from "fs";
            import { join } from "path";

            function ensureDir(): string {
              const dir = join(process.cwd(), ".data");
              if (!existsSync(dir)) mkdirSync(dir, { recursive: true });
              return dir;
            }

            function appendActivity(desc: string, status: string, statusColor: string): void {
              const file  = join(ensureDir(), "activity.json");
              const items = (existsSync(file)
                ? JSON.parse(readFileSync(file, "utf-8")) as unknown[]
                : []) as Array<Record<string, string>>;
              items.unshift({ desc, status, statusColor, when: new Date().toISOString(), id: Date.now().toString() });
              if (items.length > 50) items.splice(50);
              writeFileSync(file, JSON.stringify(items, null, 2));
            }

            type WfBody = { event: string; module: string; row: Record<string, string>; from: string; to: string };

            export async function POST(req: Request) {
              const { module, row, from, to } = await req.json() as WfBody;
              __HANDLERS__
              return NextResponse.json({ ok: true });
            }
            """).Replace("__HANDLERS__", handlers);
    }

    private static string FrontendGitIgnore() => """
        .next/
        node_modules/
        .data/
        *.local
        out/
        """;

    private static string LibTypes(ProductProfile profile)
    {
        var ifaces = string.Join("\n\n", profile.DbEntities.Take(8)
            .Select(e => "export interface " + e + " {\n  id: string;\n  createdAt: string;\n  updatedAt: string;\n  isActive: boolean;\n}"));
        return """
            // Auto-generated by Builder Platform — extend with your specific fields

            __INTERFACES__

            export interface PaginatedResponse<T> {
              data: T[]; total: number; page: number; pageSize: number;
            }

            export interface ApiError { message: string; code?: string; }
            """.Replace("__INTERFACES__", ifaces);
    }

    private static string LibApi() => """
        const BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

        async function request<T>(path: string, init?: RequestInit): Promise<T> {
          const res = await fetch(`${BASE}${path}`, {
            headers: { "Content-Type": "application/json", ...init?.headers },
            ...init,
          });
          if (!res.ok) throw new Error(`${res.status}: ${await res.text().catch(() => "")}`);
          if (res.status === 204) return undefined as T;
          return res.json();
        }

        export const api = {
          get:    <T>(path: string)                => request<T>(path),
          post:   <T>(path: string, body: unknown) => request<T>(path, { method: "POST",   body: JSON.stringify(body) }),
          put:    <T>(path: string, body: unknown) => request<T>(path, { method: "PUT",    body: JSON.stringify(body) }),
          patch:  <T>(path: string, body: unknown) => request<T>(path, { method: "PATCH",  body: JSON.stringify(body) }),
          delete: <T>(path: string)                => request<T>(path, { method: "DELETE" }),
        };
        """;

    private static string LibUtils() => """
        export const cn = (...classes: (string | undefined | false | null)[]): string =>
          classes.filter(Boolean).join(" ");

        export const formatDate = (iso: string): string =>
          new Date(iso).toLocaleDateString("es-CR");

        export const formatCurrency = (amount: number, currency = "CRC"): string =>
          new Intl.NumberFormat("es-CR", { style: "currency", currency }).format(amount);

        export const truncate = (str: string, maxLen: number): string =>
          str.length > maxLen ? str.slice(0, maxLen) + "…" : str;
        """;

    // ── Markdown templates ─────────────────────────────────────────────────────

    private static string Readme(string n, ProductProfile p) => $$"""
        # {{n}}

        > {{p.IndustryLabel}} · {{p.SaasType}}
        > Generated by Builder Platform

        ## Stack

        | Layer    | Technology                  |
        |----------|-----------------------------|
        | Backend  | .NET 9 · Clean Architecture |
        | Database | SQL Server · EF Core 9      |
        | Frontend | Next.js 15 · React 19       |
        | Styling  | Tailwind CSS v4             |

        ## Quick Start

        ### Backend
        ```bash
        cd backend && dotnet restore
        dotnet ef database update --project src/{{n}}.Infrastructure --startup-project src/{{n}}.API
        dotnet run --project src/{{n}}.API
        ```

        ### Frontend
        ```bash
        cd frontend && npm install && npm run dev
        ```

        ## Modules
        {{string.Join("\n", p.CoreFeatures.Select((f, i) => $"- **Sprint {(i / 2) + 1}**: {f}"))}}

        ## Domain Entities
        {{string.Join(" · ", p.DbEntities)}}
        """;

    private static string ArchitectureDoc(string n, ProductProfile p) => $$"""
        # Architecture — {{n}}

        ## Pattern
        {{p.ArchitecturePattern}}

        ## Domain Entities

        | Entity | Description |
        |--------|-------------|
        {{string.Join("\n", p.DbEntities.Select(e => $"| `{e}` | Core domain entity |"))}}

        ## Sprint Plan
        {{string.Join("\n\n", p.SprintPlan.Select((s, i) => $"### Sprint {i + 1}\n{s}"))}}

        ## Technical Risks
        {{p.TechnicalRisk}}
        """;

    private static string DevSetupDoc(string n) => $$"""
        # Development Setup — {{n}}

        ## Prerequisites
        - .NET 9 SDK · Node.js 20+ · SQL Server (local or Docker)

        ## Environment
        Configure `appsettings.Development.json`:
        - `ConnectionStrings:DefaultConnection` — SQL Server connection string
        - `Jwt:Key` — Minimum 32 characters

        ## Database
        ```bash
        dotnet ef migrations add InitialCreate --project src/{{n}}.Infrastructure --startup-project src/{{n}}.API
        dotnet ef database update --project src/{{n}}.Infrastructure --startup-project src/{{n}}.API
        ```

        ## Tests
        ```bash
        dotnet test
        ```
        """;

    // ── File writer ────────────────────────────────────────────────────────────

    private static void WriteFile(string dir, string fileName, string content,
        string? language, List<ScaffoldEntry> entries, ref int order)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), content, new System.Text.UTF8Encoding(false));

        // Compute relative path: strip the projectRoot (3 levels up from leaf dirs)
        var projectRoot  = dir;
        for (int i = 0; i < 6; i++) projectRoot = Directory.GetParent(projectRoot)?.FullName ?? projectRoot;
        var fullPath     = Path.Combine(dir, fileName);
        var relativePath = Path.GetRelativePath(projectRoot, fullPath).Replace('\\', '/');

        entries.Add(new ScaffoldEntry
        {
            RelativePath = relativePath,
            EntryType    = "file",
            Language     = language,
            SortOrder    = order++,
        });
    }

    // ── String utilities ───────────────────────────────────────────────────────

    private static string ToSlug(string name) =>
        Regex.Replace(name.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');

    private static string ToSafeName(string name) =>
        string.Concat(name.Split(' ', '-', '_', '.').Where(w => w.Length > 0)
                          .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    private static string ToRoute(string feature) =>
        Regex.Replace(
            NormalizeForPath(feature.ToLowerInvariant())
                   .Replace("gestion de ", "").Replace("gestion ", "")
                   .Replace(" y ", "-").Split(' ')
                   .FirstOrDefault(w => w.Length >= 3) ?? "module",
            @"[^a-z0-9-]", "");

    private static string ToPascalCase(string route) =>
        string.Concat(route.Split('-').Select(w => w.Length > 0 ? char.ToUpperInvariant(w[0]) + w[1..] : ""));

    private static string Truncate(string s, int maxLen) =>
        s.Length > maxLen ? s[..maxLen] + "…" : s;

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(text.Contains);

    private static string EscapeJs(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string StatusBgVar(string color) => color switch
    {
        "active" => "var(--status-active-bg)",
        "warn"   => "var(--status-warn-bg)",
        "info"   => "var(--status-info-bg)",
        "danger" => "var(--status-danger-bg)",
        _        => "var(--surface-elevated)",
    };

    private static string StatusTextVar(string color) => color switch
    {
        "active" => "var(--status-active-text)",
        "warn"   => "var(--status-warn-text)",
        "info"   => "var(--status-info-text)",
        "danger" => "var(--status-danger-text)",
        _        => "var(--foreground-muted)",
    };

    // ── Delta / incremental generation ────────────────────────────────────────

    public async Task<IReadOnlyList<ScaffoldChange>> GenerateDeltaAsync(
        Product product, string featureName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(product.ProjectPath) || !Directory.Exists(product.ProjectPath))
        {
            logger.LogWarning("Delta skipped — no project path for product {Id}", product.Id);
            return [];
        }

        var safeName = ToSafeName(product.Name);
        var pascal   = ToPascalCase(ToSlug(featureName));
        var route    = ToDeltaRoute(featureName);
        var changes  = new List<ScaffoldChange>();

        ct.ThrowIfCancellationRequested();

        // Backend: entity
        var entityPath = Path.Combine(product.ProjectPath,
            "backend", "src", $"{safeName}.Domain", "Entities", $"{pascal}.cs");
        await WriteDelta(changes, product.Id, featureName, "backend", entityPath,
            DeltaEntityClass(safeName, pascal));

        // Backend: controller
        var ctrlPath = Path.Combine(product.ProjectPath,
            "backend", "src", $"{safeName}.API", "Controllers", $"{pascal}Controller.cs");
        await WriteDelta(changes, product.Id, featureName, "backend", ctrlPath,
            DeltaControllerClass(safeName, pascal));

        // Frontend: page
        var pagePath = Path.Combine(product.ProjectPath,
            "frontend", "app", route, "page.tsx");
        await WriteDelta(changes, product.Id, featureName, "frontend", pagePath,
            DeltaFeaturePage(featureName, pascal, route));

        logger.LogInformation("Delta complete for '{Feature}': {Created} created, {Skipped} skipped",
            featureName,
            changes.Count(c => c.ChangeType == "created"),
            changes.Count(c => c.ChangeType == "skipped"));

        return changes;
    }

    private static async Task WriteDelta(List<ScaffoldChange> changes, Guid productId,
        string moduleLabel, string layer, string fullPath, string content)
    {
        var change = new ScaffoldChange
        {
            ProductId   = productId,
            ModuleLabel = moduleLabel,
            Layer       = layer,
            TargetPath  = fullPath,
        };

        if (File.Exists(fullPath))
        {
            change.ChangeType = "skipped";
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, content, new System.Text.UTF8Encoding(false));
            change.ChangeType = "created";
        }

        changes.Add(change);
    }

    // ── Widget generation ──────────────────────────────────────────────────────

    public async Task<string> GenerateWidgetAsync(
        Product product, string widgetDisplayName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(product.ProjectPath) || !Directory.Exists(product.ProjectPath))
            return string.Empty;

        var normalized     = NormalizeForPath(widgetDisplayName);
        var pascal         = ToPascalCase(ToSlug(normalized));
        var widgetFileName = $"{pascal}Widget.tsx";
        var widgetDir      = Path.Combine(product.ProjectPath, "frontend", "components", "widgets");
        var widgetPath     = Path.Combine(widgetDir, widgetFileName);

        if (File.Exists(widgetPath))
        {
            logger.LogInformation("Widget already exists, skipping: {Path}", widgetPath);
            return widgetPath;
        }

        Directory.CreateDirectory(widgetDir);
        await File.WriteAllTextAsync(widgetPath, WidgetComponentTemplate(pascal, widgetDisplayName),
            new System.Text.UTF8Encoding(false), ct);

        logger.LogInformation("Widget generated: {Path}", widgetPath);
        return widgetPath;
    }

    private static string NormalizeForPath(string s) =>
        s.Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
         .Replace("Á", "A").Replace("É", "E").Replace("Í", "I").Replace("Ó", "O").Replace("Ú", "U")
         .Replace("ñ", "n").Replace("Ñ", "N").Replace("ü", "u").Replace("Ü", "U");

    private static string WidgetComponentTemplate(string pascal, string displayName) =>
        ("""
        "use client";

        export function __PASCAL__Widget() {
          return (
            <div
              className="rounded-xl p-4 space-y-3"
              style={{ background: "var(--surface-elevated)", border: "1px solid var(--border)" }}
            >
              <div className="flex items-center justify-between">
                <h3 className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>
                  __DISPLAY__
                </h3>
                <span
                  className="text-[10px] font-medium px-2 py-0.5 rounded"
                  style={{ background: "var(--surface)", color: "var(--status-active-text)", border: "1px solid var(--border)" }}
                >
                  Live
                </span>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <WidgetMetric label="Total" value="—" />
                <WidgetMetric label="Hoy"   value="—" />
              </div>
              <p className="text-[11px]" style={{ color: "var(--muted)" }}>
                Implementá la lógica de datos conectando con la API.
              </p>
            </div>
          );
        }

        function WidgetMetric({ label, value }: { label: string; value: string }) {
          return (
            <div
              className="rounded-lg px-3 py-2"
              style={{ background: "var(--surface)", border: "1px solid var(--border)" }}
            >
              <p className="text-[10px] mb-0.5" style={{ color: "var(--muted)" }}>{label}</p>
              <p className="text-[15px] font-mono font-semibold" style={{ color: "var(--foreground)" }}>{value}</p>
            </div>
          );
        }
        """)
        .Replace("__PASCAL__", pascal)
        .Replace("__DISPLAY__", displayName);

    // ── Delta templates ────────────────────────────────────────────────────────

    private static string DeltaEntityClass(string safeName, string pascal) => $$"""
        namespace {{safeName}}.Domain.Entities;

        public class {{pascal}}
        {
            public Guid   Id          { get; set; } = Guid.NewGuid();
            public string Name        { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Status      { get; set; } = "active";
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
            public bool IsActive      { get; set; } = true;
        }
        """;

    private static string DeltaControllerClass(string safeName, string pascal) => $$"""
        using Microsoft.AspNetCore.Mvc;

        namespace {{safeName}}.API.Controllers;

        [ApiController]
        [Route("api/[controller]")]
        public class {{pascal}}Controller : ControllerBase
        {
            // TODO: inject AppDbContext when ready to implement
            [HttpGet]
            public IActionResult GetAll()
                => Ok(new { items = Array.Empty<object>(), message = "{{pascal}} module — implement with AppDbContext" });

            [HttpGet("{id:guid}")]
            public IActionResult GetById(Guid id)
                => Ok(new { id, message = "TODO: Return {{pascal}} by id" });

            [HttpPost]
            public IActionResult Create([FromBody] object request)
                => StatusCode(201, new { message = "TODO: Create {{pascal}}" });

            [HttpPut("{id:guid}")]
            public IActionResult Update(Guid id, [FromBody] object request)
                => Ok(new { id, message = "TODO: Update {{pascal}}" });

            [HttpDelete("{id:guid}")]
            public IActionResult Delete(Guid id) => NoContent();
        }
        """;

    private static string DeltaFeaturePage(string featureName, string pascal, string route) =>
        ("""
        "use client";
        import { useState, useEffect, useCallback } from "react";
        import { Inbox, CheckCircle, Plus } from "lucide-react";

        type DRow = { id?: string; name: string; description: string; date: string };

        export default function __PASCAL__Page() {
          const [rows,       setRows]       = useState<DRow[]>([]);
          const [loadDone,   setLoadDone]   = useState(false);
          const [showModal,  setShowModal]  = useState(false);
          const [form,       setForm]       = useState({ name: "", description: "" });
          const [saving,     setSaving]     = useState(false);
          const [toast,      setToast]      = useState<string | null>(null);
          const [hoveredRow, setHoveredRow] = useState<number | null>(null);

          const load = useCallback(async () => {
            try {
              const res = await fetch("/api/data/__ROUTE__");
              if (res.ok) setRows(await res.json() as DRow[]);
            } catch {}
            finally { setLoadDone(true); }
          }, []);

          useEffect(() => { void load(); }, [load]);

          useEffect(() => {
            const id = setInterval(() => { void load(); }, 5000);
            return () => clearInterval(id);
          }, [load]);

          useEffect(() => {
            if (!toast) return;
            const t = setTimeout(() => setToast(null), 3000);
            return () => clearTimeout(t);
          }, [toast]);

          const canSave = !saving && !!form.name.trim();

          const save = async () => {
            if (!canSave) return;
            setSaving(true);
            try {
              await fetch("/api/data/__ROUTE__", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ ...form, date: new Date().toISOString().slice(0, 10) }),
              });
              setShowModal(false);
              setForm({ name: "", description: "" });
              await load();
              setToast("__FEATURE__ agregado correctamente");
            } finally { setSaving(false); }
          };

          return (
            <div style={{ padding: "28px" }}>
              <div style={{ marginBottom: "24px", display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
                <div>
                  <h1 style={{ fontSize: "22px", fontWeight: "700", color: "var(--foreground)", marginBottom: "4px" }}>__FEATURE__</h1>
                  <p style={{ color: "var(--foreground-muted)", fontSize: "13px" }}>
                    {rows.length > 0 ? `${rows.length} registro${rows.length !== 1 ? "s" : ""}` : "Módulo listo para recibir datos"}
                  </p>
                </div>
                <button onClick={() => setShowModal(true)}
                  style={{ display: "flex", alignItems: "center", gap: "6px", padding: "9px 18px", borderRadius: "8px", background: "var(--accent)", color: "#fff", fontSize: "13px", fontWeight: "600", border: "none", cursor: "pointer" }}>
                  <Plus size={14} />
                  Nuevo __FEATURE__
                </button>
              </div>

              {loadDone && rows.length === 0 ? (
                <div style={{ background: "var(--surface)", borderRadius: "12px", border: "1px solid var(--border)", padding: "64px 20px", textAlign: "center" }}>
                  <Inbox size={40} style={{ color: "var(--muted)", display: "block", margin: "0 auto 12px" }} />
                  <p style={{ fontSize: "16px", fontWeight: "600", color: "var(--foreground)", marginBottom: "6px" }}>Sin registros aún</p>
                  <p style={{ fontSize: "13px", color: "var(--foreground-muted)", marginBottom: "24px" }}>Creá el primer elemento para empezar.</p>
                  <button onClick={() => setShowModal(true)}
                    style={{ display: "inline-flex", alignItems: "center", gap: "6px", padding: "10px 22px", borderRadius: "8px", background: "var(--accent)", color: "#fff", fontSize: "13px", fontWeight: "600", border: "none", cursor: "pointer" }}>
                    <Plus size={14} />
                    Nuevo __FEATURE__
                  </button>
                </div>
              ) : !loadDone ? (
                <div style={{ background: "var(--surface)", borderRadius: "12px", border: "1px solid var(--border)", padding: "40px", textAlign: "center", color: "var(--muted)", fontSize: "13px" }}>
                  Cargando...
                </div>
              ) : (
                <div style={{ background: "var(--surface)", borderRadius: "12px", border: "1px solid var(--border)", overflow: "hidden" }}>
                  <table style={{ width: "100%", borderCollapse: "collapse" }}>
                    <thead>
                      <tr style={{ background: "var(--surface-elevated)", borderBottom: "1px solid var(--border)" }}>
                        <th style={{ padding: "12px 16px", textAlign: "left", fontSize: "11px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Nombre</th>
                        <th style={{ padding: "12px 16px", textAlign: "left", fontSize: "11px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Descripción</th>
                        <th style={{ padding: "12px 16px", textAlign: "left", fontSize: "11px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Fecha</th>
                      </tr>
                    </thead>
                    <tbody>
                      {rows.map((row, i) => (
                        <tr key={row.id ?? i}
                          onMouseEnter={() => setHoveredRow(i)}
                          onMouseLeave={() => setHoveredRow(null)}
                          style={{ borderTop: "1px solid var(--border)", background: hoveredRow === i ? "var(--surface-elevated)" : "transparent", transition: "background 0.1s" }}>
                          <td style={{ padding: "14px 16px", fontSize: "13px", fontWeight: "500", color: "var(--foreground)" }}>{row.name}</td>
                          <td style={{ padding: "14px 16px", fontSize: "12px", color: "var(--foreground-muted)" }}>{row.description || "—"}</td>
                          <td style={{ padding: "14px 16px", fontSize: "12px", color: "var(--foreground-muted)" }}>{row.date}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {showModal && (
                <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,0.7)", display: "flex", alignItems: "center", justifyContent: "center", zIndex: 100 }}
                  onClick={() => setShowModal(false)}>
                  <div style={{ background: "var(--surface)", borderRadius: "16px", padding: "28px", width: "420px", maxWidth: "90vw", border: "1px solid var(--border)", boxShadow: "0 20px 60px rgba(0,0,0,0.5)" }}
                    onClick={e => e.stopPropagation()}>
                    <h2 style={{ fontSize: "16px", fontWeight: "700", color: "var(--foreground)", marginBottom: "6px" }}>Nuevo __FEATURE__</h2>
                    <p style={{ fontSize: "12px", color: "var(--foreground-muted)", marginBottom: "20px" }}>Completá los campos y guardá.</p>
                    <div style={{ marginBottom: "14px" }}>
                      <label style={{ display: "block", fontSize: "11px", fontWeight: "600", color: "var(--muted)", marginBottom: "5px", textTransform: "uppercase", letterSpacing: "0.05em" }}>
                        Nombre <span style={{ color: "var(--status-danger-text)" }}>*</span>
                      </label>
                      <input value={form.name} onChange={e => setForm(p => ({ ...p, name: e.target.value }))}
                        placeholder="Ingresá un nombre..."
                        autoFocus
                        style={{ width: "100%", padding: "10px 12px", background: "var(--surface-elevated)", border: "1px solid var(--border)", borderRadius: "8px", color: "var(--foreground)", fontSize: "13px", outline: "none" }} />
                    </div>
                    <div style={{ marginBottom: "14px" }}>
                      <label style={{ display: "block", fontSize: "11px", fontWeight: "600", color: "var(--muted)", marginBottom: "5px", textTransform: "uppercase", letterSpacing: "0.05em" }}>Descripción</label>
                      <input value={form.description} onChange={e => setForm(p => ({ ...p, description: e.target.value }))}
                        placeholder="Descripción opcional..."
                        style={{ width: "100%", padding: "10px 12px", background: "var(--surface-elevated)", border: "1px solid var(--border)", borderRadius: "8px", color: "var(--foreground)", fontSize: "13px", outline: "none" }} />
                    </div>
                    <div style={{ display: "flex", gap: "8px", marginTop: "24px", justifyContent: "flex-end" }}>
                      <button onClick={() => { setShowModal(false); setForm({ name: "", description: "" }); }}
                        style={{ padding: "9px 16px", borderRadius: "8px", background: "var(--surface-elevated)", color: "var(--foreground-muted)", border: "1px solid var(--border)", fontSize: "13px", cursor: "pointer" }}>
                        Cancelar
                      </button>
                      <button onClick={save} disabled={!canSave}
                        style={{ display: "flex", alignItems: "center", gap: "6px", padding: "9px 20px", borderRadius: "8px", background: canSave ? "var(--accent)" : "var(--surface-elevated)", color: canSave ? "#fff" : "var(--muted)", border: "none", fontSize: "13px", fontWeight: "600", cursor: canSave ? "pointer" : "not-allowed" }}>
                        {saving ? "Guardando..." : "Guardar"}
                      </button>
                    </div>
                  </div>
                </div>
              )}

              {toast && (
                <div style={{ position: "fixed", bottom: "24px", right: "24px", background: "var(--surface)", border: "1px solid var(--status-active-bg)", borderRadius: "10px", padding: "12px 16px", display: "flex", alignItems: "center", gap: "10px", zIndex: 200, boxShadow: "0 4px 24px rgba(0,0,0,0.4)" }}>
                  <CheckCircle size={16} style={{ color: "var(--status-active-text)", flexShrink: 0 }} />
                  <span style={{ fontSize: "13px", color: "var(--foreground)", fontWeight: "500" }}>{toast}</span>
                </div>
              )}
            </div>
          );
        }
        """)
        .Replace("__PASCAL__", pascal)
        .Replace("__FEATURE__", featureName)
        .Replace("__ROUTE__", route);

    // Feature name → URL route (e.g. "Facturación" → "facturacion")
    private static string ToDeltaRoute(string featureName)
    {
        // Normalize accents first so the final regex doesn't silently drop them
        var lower = featureName.ToLowerInvariant()
            .Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
            .Replace("ñ", "n").Replace("ü", "u");
        var first = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(w => w.Length >= 3) ?? lower;
        return Regex.Replace(first, @"[^a-z0-9-]", "");
    }
}
