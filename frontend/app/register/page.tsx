"use client";
import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { Cpu, UserPlus, ArrowRight, AlertCircle, CheckCircle2 } from "lucide-react";

export default function RegisterPage() {
  const router = useRouter();

  const [name,      setName]      = useState("");
  const [email,     setEmail]     = useState("");
  const [password,  setPassword]  = useState("");
  const [confirm,   setConfirm]   = useState("");
  const [loading,   setLoading]   = useState(false);
  const [error,     setError]     = useState<string | null>(null);
  const [success,   setSuccess]   = useState(false);

  // Already logged in → workspace
  useEffect(() => {
    const token = document.cookie.match(/(?:^|; )bp-token=([^;]*)/)?.[1];
    if (token) router.replace("/workspace");
  }, [router]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    if (password.length < 8) {
      setError("La contraseña debe tener al menos 8 caracteres");
      return;
    }
    if (password !== confirm) {
      setError("Las contraseñas no coinciden");
      return;
    }

    setLoading(true);
    try {
      const res = await fetch("/api/auth/register", {
        method:  "POST",
        headers: { "Content-Type": "application/json" },
        body:    JSON.stringify({
          email:    email.trim(),
          password,
          name:     name.trim() || undefined,
        }),
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        setError((data as { error?: string }).error ?? "Error al crear la cuenta");
        return;
      }
      setSuccess(true);
      setTimeout(() => router.replace("/workspace"), 500);
    } catch {
      setError("No se pudo conectar al servidor");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div
      className="min-h-screen flex items-center justify-center"
      style={{ background: "var(--background)" }}
    >
      {/* bg grid */}
      <div
        style={{
          position: "fixed", inset: 0, pointerEvents: "none",
          backgroundImage: "linear-gradient(var(--border) 1px, transparent 1px), linear-gradient(90deg, var(--border) 1px, transparent 1px)",
          backgroundSize: "40px 40px",
          opacity: 0.35,
        }}
      />

      <div style={{ position: "relative", width: "100%", maxWidth: "420px", padding: "0 24px" }}>
        {/* Brand */}
        <div className="flex flex-col items-center mb-8">
          <div
            style={{
              width: "52px", height: "52px", borderRadius: "14px",
              background: "var(--accent)", display: "flex", alignItems: "center",
              justifyContent: "center", marginBottom: "16px",
              boxShadow: "0 0 32px rgba(99,102,241,0.4)",
            }}
          >
            <Cpu size={26} color="#fff" />
          </div>
          <h1
            style={{
              fontSize: "22px", fontWeight: "700", letterSpacing: "-0.03em",
              color: "var(--foreground)", marginBottom: "4px",
            }}
          >
            Builder OS
          </h1>
          <p style={{ fontSize: "13px", color: "var(--muted)" }}>
            Creá tu cuenta y empezá a construir
          </p>
        </div>

        {/* Card */}
        <div
          className="card"
          style={{ borderRadius: "18px", boxShadow: "0 24px 64px rgba(0,0,0,0.4)", padding: "32px" }}
        >
          <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
            {/* Name (optional) */}
            <div>
              <label
                htmlFor="name"
                style={{ display: "block", fontSize: "12px", fontWeight: "600", color: "var(--muted)", marginBottom: "6px", letterSpacing: "0.05em" }}
              >
                NOMBRE <span style={{ fontWeight: "400", opacity: 0.6 }}>(opcional)</span>
              </label>
              <input
                id="name"
                type="text"
                value={name}
                onChange={e => setName(e.target.value)}
                placeholder="Tu nombre"
                autoComplete="name"
                style={{
                  width: "100%", padding: "10px 14px", borderRadius: "10px",
                  border: "1px solid var(--border)", background: "var(--surface-elevated)",
                  color: "var(--foreground)", fontSize: "14px", outline: "none", boxSizing: "border-box",
                }}
                onFocus={e => (e.target.style.borderColor = "var(--accent)")}
                onBlur={e  => (e.target.style.borderColor = "var(--border)")}
              />
            </div>

            {/* Email */}
            <div>
              <label
                htmlFor="email"
                style={{ display: "block", fontSize: "12px", fontWeight: "600", color: "var(--muted)", marginBottom: "6px", letterSpacing: "0.05em" }}
              >
                EMAIL
              </label>
              <input
                id="email"
                type="email"
                value={email}
                onChange={e => setEmail(e.target.value)}
                required
                placeholder="tu@email.com"
                autoComplete="email"
                style={{
                  width: "100%", padding: "10px 14px", borderRadius: "10px",
                  border: "1px solid var(--border)", background: "var(--surface-elevated)",
                  color: "var(--foreground)", fontSize: "14px", outline: "none", boxSizing: "border-box",
                }}
                onFocus={e => (e.target.style.borderColor = "var(--accent)")}
                onBlur={e  => (e.target.style.borderColor = "var(--border)")}
              />
            </div>

            {/* Password */}
            <div>
              <label
                htmlFor="password"
                style={{ display: "block", fontSize: "12px", fontWeight: "600", color: "var(--muted)", marginBottom: "6px", letterSpacing: "0.05em" }}
              >
                CONTRASEÑA
              </label>
              <input
                id="password"
                type="password"
                value={password}
                onChange={e => setPassword(e.target.value)}
                required
                placeholder="Mínimo 8 caracteres"
                autoComplete="new-password"
                style={{
                  width: "100%", padding: "10px 14px", borderRadius: "10px",
                  border: "1px solid var(--border)", background: "var(--surface-elevated)",
                  color: "var(--foreground)", fontSize: "14px", outline: "none", boxSizing: "border-box",
                }}
                onFocus={e => (e.target.style.borderColor = "var(--accent)")}
                onBlur={e  => (e.target.style.borderColor = "var(--border)")}
              />
            </div>

            {/* Confirm password */}
            <div>
              <label
                htmlFor="confirm"
                style={{ display: "block", fontSize: "12px", fontWeight: "600", color: "var(--muted)", marginBottom: "6px", letterSpacing: "0.05em" }}
              >
                CONFIRMAR CONTRASEÑA
              </label>
              <input
                id="confirm"
                type="password"
                value={confirm}
                onChange={e => setConfirm(e.target.value)}
                required
                placeholder="Repetí la contraseña"
                autoComplete="new-password"
                style={{
                  width: "100%", padding: "10px 14px", borderRadius: "10px",
                  border: "1px solid var(--border)", background: "var(--surface-elevated)",
                  color: "var(--foreground)", fontSize: "14px", outline: "none", boxSizing: "border-box",
                }}
                onFocus={e => (e.target.style.borderColor = "var(--accent)")}
                onBlur={e  => (e.target.style.borderColor = "var(--border)")}
              />
            </div>

            {/* Error */}
            {error && (
              <div
                style={{
                  display: "flex", alignItems: "center", gap: "8px",
                  padding: "10px 12px", borderRadius: "8px",
                  background: "var(--status-danger-bg)",
                  color: "var(--status-danger-text)", fontSize: "13px",
                }}
              >
                <AlertCircle size={14} />
                {error}
              </div>
            )}

            {/* Submit */}
            <button
              type="submit"
              disabled={loading || success}
              style={{
                display: "flex", alignItems: "center", justifyContent: "center",
                gap: "8px", padding: "12px", borderRadius: "10px",
                background: success ? "var(--status-active-bg)" : "var(--accent)",
                color: success ? "var(--status-active-text)" : "#fff",
                fontSize: "14px", fontWeight: "600", cursor: loading || success ? "default" : "pointer",
                border: "none", transition: "background 0.2s, opacity 0.2s",
                opacity: loading ? 0.7 : 1,
              }}
            >
              {success ? (
                <><CheckCircle2 size={16} /> Cuenta creada — entrando...</>
              ) : loading ? (
                "Creando cuenta..."
              ) : (
                <><UserPlus size={16} /> Crear cuenta</>
              )}
            </button>
          </form>

          <div style={{ marginTop: "20px", textAlign: "center" }}>
            <span style={{ fontSize: "13px", color: "var(--muted)" }}>¿Ya tenés cuenta?{" "}</span>
            <a
              href="/login"
              style={{ fontSize: "13px", color: "var(--accent)", fontWeight: "600", textDecoration: "none" }}
            >
              Iniciar sesión
            </a>
          </div>
        </div>

        <p
          style={{
            textAlign: "center", marginTop: "20px",
            fontSize: "11px", color: "var(--muted)", opacity: 0.6,
          }}
        >
          Builder Platform · Sprint 24 · User Registration
        </p>
      </div>
    </div>
  );
}
