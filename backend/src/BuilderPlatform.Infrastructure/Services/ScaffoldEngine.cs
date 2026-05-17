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

        var appRoot = Path.Combine(feRoot, "app");
        WriteFile(appRoot, "globals.css",  GlobalsCss(),                 "css",        entries, ref order);
        WriteFile(appRoot, "layout.tsx",   RootLayout(displayName),      "typescript", entries, ref order);
        WriteFile(appRoot, "page.tsx",     RootPage(),                   "typescript", entries, ref order);

        WriteFile(Path.Combine(appRoot, "dashboard"), "page.tsx",
            DashboardPage(displayName, profile), "typescript", entries, ref order);

        WriteFile(Path.Combine(appRoot, "login"), "page.tsx",
            LoginPage(displayName), "typescript", entries, ref order);

        foreach (var feature in profile.CoreFeatures.Take(5))
            WriteFile(Path.Combine(appRoot, ToRoute(feature)), "page.tsx",
                FeaturePage(feature, ToRoute(feature)), "typescript", entries, ref order);

        var componentsRoot = Path.Combine(feRoot, "components");
        WriteFile(componentsRoot, "Sidebar.tsx",       SidebarServer(displayName),       "typescript", entries, ref order);
        WriteFile(componentsRoot, "SidebarClient.tsx", SidebarClientComponent(displayName), "typescript", entries, ref order);
        WriteFile(componentsRoot, "Header.tsx",        HeaderComponent(),                "typescript", entries, ref order);

        var libRoot = Path.Combine(feRoot, "lib");
        WriteFile(libRoot, "types.ts",  LibTypes(profile), "typescript", entries, ref order);
        WriteFile(libRoot, "api.ts",    LibApi(),          "typescript", entries, ref order);
        WriteFile(libRoot, "utils.ts",  LibUtils(),        "typescript", entries, ref order);
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
            "next": "15.3.0",
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

        export const metadata: Metadata = {
          title: "__DISPLAY_NAME__",
          description: "Generated by Builder Platform",
        };

        export default function RootLayout({ children }: { children: React.ReactNode }) {
          return (
            <html lang="es">
              <body>
                <div style={{ display: "flex", height: "100vh", background: "var(--background)" }}>
                  <Sidebar />
                  <main style={{ flex: 1, overflow: "auto" }}>{children}</main>
                </div>
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
        var statsArr = string.Join(",\n  ", profile.DbEntities.Take(4)
            .Select(e => $"{{ label: \"Total {e}\", value: \"—\" }}"));
        return
            """
            export default function DashboardPage() {
              const stats = [__STATS__];
              return (
                <div style={{ padding: "28px" }}>
                  <h1 style={{ fontSize: "22px", fontWeight: "700", color: "var(--foreground)", marginBottom: "24px" }}>
                    __DISPLAY_NAME__ — Dashboard
                  </h1>
                  <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(200px, 1fr))", gap: "16px", marginBottom: "32px" }}>
                    {stats.map((s) => (
                      <div key={s.label} style={{ padding: "20px", background: "var(--surface)", borderRadius: "12px", border: "1px solid var(--border)" }}>
                        <p style={{ fontSize: "11px", color: "var(--foreground-muted)", marginBottom: "8px", textTransform: "uppercase" }}>{s.label}</p>
                        <p style={{ fontSize: "28px", fontWeight: "700", color: "var(--status-info-text)" }}>{s.value}</p>
                      </div>
                    ))}
                  </div>
                  <div style={{ padding: "24px", background: "var(--surface)", borderRadius: "12px", border: "1px solid var(--border)", textAlign: "center" }}>
                    <p style={{ color: "var(--foreground-muted)", fontSize: "13px" }}>Sprint 1 en construcción</p>
                  </div>
                </div>
              );
            }
            """.Replace("__STATS__", statsArr)
               .Replace("__DISPLAY_NAME__", displayName);
    }

    private static string LoginPage(string displayName) =>
        """
        "use client";
        import { useState } from "react";

        export default function LoginPage() {
          const [email, setEmail] = useState("");
          const [password, setPassword] = useState("");
          const [loading, setLoading] = useState(false);

          const handleLogin = async (e: React.FormEvent) => {
            e.preventDefault();
            setLoading(true);
            console.log("Login:", { email, password });
            setLoading(false);
          };

          return (
            <div style={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh" }}>
              <div style={{ width: "100%", maxWidth: "400px", padding: "40px", background: "var(--surface)", borderRadius: "16px", border: "1px solid var(--border)" }}>
                <h1 style={{ fontSize: "20px", fontWeight: "700", marginBottom: "8px", color: "var(--foreground)" }}>__DISPLAY_NAME__</h1>
                <p style={{ color: "var(--foreground-muted)", fontSize: "13px", marginBottom: "28px" }}>Iniciá sesión para continuar</p>
                <form onSubmit={handleLogin}>
                  <div style={{ marginBottom: "16px" }}>
                    <label style={{ display: "block", fontSize: "12px", color: "var(--foreground-muted)", marginBottom: "6px" }}>Email</label>
                    <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required
                      style={{ width: "100%", padding: "10px 14px", background: "var(--surface-elevated)", border: "1px solid var(--border)", borderRadius: "8px", color: "var(--foreground)", fontSize: "14px", outline: "none" }} />
                  </div>
                  <div style={{ marginBottom: "24px" }}>
                    <label style={{ display: "block", fontSize: "12px", color: "var(--foreground-muted)", marginBottom: "6px" }}>Contraseña</label>
                    <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required
                      style={{ width: "100%", padding: "10px 14px", background: "var(--surface-elevated)", border: "1px solid var(--border)", borderRadius: "8px", color: "var(--foreground)", fontSize: "14px", outline: "none" }} />
                  </div>
                  <button type="submit" disabled={loading}
                    style={{ width: "100%", padding: "11px", background: "var(--accent)", color: "#fff", border: "none", borderRadius: "8px", fontSize: "14px", fontWeight: "600", cursor: loading ? "not-allowed" : "pointer" }}>
                    {loading ? "Entrando..." : "Entrar"}
                  </button>
                </form>
              </div>
            </div>
          );
        }
        """.Replace("__DISPLAY_NAME__", displayName);

    private static string FeaturePage(string feature, string route) =>
        ("""
        export default function __PASCAL__Page() {
          return (
            <div style={{ padding: "28px" }}>
              <div style={{ marginBottom: "24px" }}>
                <h1 style={{ fontSize: "22px", fontWeight: "700", color: "var(--foreground)" }}>__FEATURE__</h1>
                <p style={{ color: "var(--foreground-muted)", marginTop: "4px", fontSize: "13px" }}>Módulo en construcción · Sprint 1</p>
              </div>
              <div style={{ padding: "48px", textAlign: "center", background: "var(--surface)", borderRadius: "12px", border: "1px dashed var(--border)" }}>
                <p style={{ fontSize: "14px", color: "var(--foreground-muted)" }}>Este módulo será implementado en el próximo sprint.</p>
                <p style={{ fontSize: "12px", color: "var(--muted)", marginTop: "8px" }}>Ruta: /__ROUTE__</p>
              </div>
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

        interface NavItem { href: string; label: string; }

        export function SidebarClient({ displayName, navItems }: { displayName: string; navItems: NavItem[] }) {
          const pathname = usePathname();
          return (
            <aside style={{ width: "220px", flexShrink: 0, background: "var(--surface)", borderRight: "1px solid var(--border)", display: "flex", flexDirection: "column" }}>
              <div style={{ padding: "18px 16px 14px", borderBottom: "1px solid var(--border)" }}>
                <span style={{ fontSize: "14px", fontWeight: "700", color: "var(--foreground)" }}>{displayName}</span>
              </div>
              <nav style={{ padding: "8px", flex: 1 }}>
                {navItems.map(({ href, label }) => {
                  const active = pathname === href || (href !== "/" && pathname.startsWith(href));
                  return (
                    <Link key={href} href={href}
                      style={{ display: "flex", alignItems: "center", padding: "8px 12px", borderRadius: "8px", marginBottom: "2px",
                        fontSize: "13px", fontWeight: active ? "600" : "400",
                        background: active ? "var(--surface-elevated)" : "transparent",
                        color: active ? "var(--foreground)" : "var(--muted)" }}>
                      {label}
                    </Link>
                  );
                })}
              </nav>
              <div style={{ padding: "12px 16px", borderTop: "1px solid var(--border)", fontSize: "11px", color: "var(--muted)" }}>
                Built with Builder Platform
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
            feature.ToLowerInvariant()
                   .Replace("gestión de ", "").Replace("gestión ", "")
                   .Replace(" y ", "-").Split(' ')
                   .FirstOrDefault(w => w.Length >= 3) ?? "module",
            @"[^a-z0-9-]", "");

    private static string ToPascalCase(string route) =>
        string.Concat(route.Split('-').Select(w => w.Length > 0 ? char.ToUpperInvariant(w[0]) + w[1..] : ""));

    private static string Truncate(string s, int maxLen) =>
        s.Length > maxLen ? s[..maxLen] + "…" : s;

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(text.Contains);

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
        import { Header } from "@/components/Header";

        export default function __PASCAL__Page() {
          return (
            <div style={{ display: "flex", flexDirection: "column", height: "100%" }}>
              <Header title="__FEATURE__" />
              <main style={{ flex: 1, overflow: "auto", padding: "24px" }}>
                <div style={{ marginBottom: "24px", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <div>
                    <h1 style={{ fontSize: "20px", fontWeight: "700", color: "var(--foreground)", marginBottom: "4px" }}>
                      __FEATURE__
                    </h1>
                    <p style={{ fontSize: "13px", color: "var(--muted)" }}>
                      Módulo generado automáticamente — implementá la lógica de negocio aquí.
                    </p>
                  </div>
                  <button style={{ padding: "8px 16px", borderRadius: "8px", background: "var(--accent)", color: "#fff", fontSize: "13px", fontWeight: "600", border: "none", cursor: "pointer" }}>
                    Nuevo __FEATURE__
                  </button>
                </div>

                <div style={{ borderRadius: "12px", border: "1px solid var(--border)", overflow: "hidden" }}>
                  <table style={{ width: "100%", borderCollapse: "collapse" }}>
                    <thead>
                      <tr style={{ background: "var(--surface)", borderBottom: "1px solid var(--border)" }}>
                        <th style={{ padding: "12px 16px", textAlign: "left", fontSize: "11px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Nombre</th>
                        <th style={{ padding: "12px 16px", textAlign: "left", fontSize: "11px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Estado</th>
                        <th style={{ padding: "12px 16px", textAlign: "left", fontSize: "11px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Creado</th>
                        <th style={{ padding: "12px 16px", textAlign: "right", fontSize: "11px", fontWeight: "600", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>Acciones</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr>
                        <td colSpan={4} style={{ padding: "48px 16px", textAlign: "center", color: "var(--muted)", fontSize: "13px" }}>
                          Sin datos — conectá el controller de __FEATURE__ con tu AppDbContext.
                        </td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </main>
            </div>
          );
        }
        """)
        .Replace("__PASCAL__", pascal)
        .Replace("__FEATURE__", featureName)
        .Replace("__ROUTE__", route);

    // Feature name → URL route (e.g. "Gestión de Alertas" → "alertas")
    private static string ToDeltaRoute(string featureName)
    {
        var lower = featureName.ToLowerInvariant()
            .Replace("gestión de ", "").Replace("gestión ", "")
            .Replace("módulo de ", "").Replace("módulo ", "");
        var first = Regex.Replace(lower, @"[^a-záéíóúüñ\s-]", "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(w => w.Length >= 3) ?? Regex.Replace(lower, @"\s+", "-").Trim('-');
        return Regex.Replace(first, @"[^a-z0-9-]", "");
    }
}
