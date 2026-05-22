using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BuilderPlatform.API.DTOs;
using BuilderPlatform.Domain.Entities;
using BuilderPlatform.Infrastructure.Persistence;
using BuilderPlatform.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BuilderPlatform.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IConfiguration config) : ControllerBase
{
    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        var user = await db.BuilderUsers
            .FirstOrDefaultAsync(u => u.Email.ToLower() == req.Email.ToLower().Trim());

        if (user is null || !BuilderPasswordHasher.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Credenciales incorrectas" });

        var expiresAt = DateTime.UtcNow.AddDays(30);
        var token     = GenerateToken(user.Id, user.Email, expiresAt);

        return Ok(new LoginResponse(token, user.Email, expiresAt));
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register(RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "Email y contraseña son requeridos" });

        if (req.Password.Length < 8)
            return BadRequest(new { error = "La contraseña debe tener al menos 8 caracteres" });

        var email = req.Email.Trim().ToLower();

        if (await db.BuilderUsers.AnyAsync(u => u.Email.ToLower() == email))
            return Conflict(new { error = "Este email ya está registrado" });

        var user = new BuilderUser
        {
            Email        = req.Email.Trim(),
            PasswordHash = BuilderPasswordHasher.Hash(req.Password),
            Name         = string.IsNullOrWhiteSpace(req.Name) ? null : req.Name.Trim(),
        };
        db.BuilderUsers.Add(user);
        await db.SaveChangesAsync();

        var expiresAt = DateTime.UtcNow.AddDays(30);
        var token     = GenerateToken(user.Id, user.Email, expiresAt);

        return Ok(new LoginResponse(token, user.Email, expiresAt));
    }

    // GET /api/auth/me
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var email  = User.FindFirstValue(ClaimTypes.Email)  ?? "";
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var uid    = Guid.TryParse(userId, out var id) ? id : Guid.Empty;
        var user   = await db.BuilderUsers.FindAsync(uid);
        return Ok(new MeResponse(email, uid, user?.Name));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private string GenerateToken(Guid userId, string email, DateTime expiresAt)
    {
        var key = Encoding.UTF8.GetBytes(config["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey not configured"));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Subject            = new ClaimsIdentity(claims),
            Expires            = expiresAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
