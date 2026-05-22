"use client";
import { useState, useEffect, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Cpu, Lock, ArrowRight, AlertCircle, CheckCircle2 } from "lucide-react";

export default function LoginPage() {
  return (
    <Suspense>
      <LoginForm />
    </Suspense>
  );
}

function LoginForm() {
  const router       = useRouter();
  const searchParams = useSearchParams();
  const from         = searchParams.get("from") ?? "/workspace";

  const [email,    setEmail]    = useState("admin@builder.local");
  const [password, setPassword] = useState("Builder1234!");
  const [loading,  setLoading]  = useState(false);
  const [error,    setError]    = useState<string | null>(null);
  const [success,  setSuccess]  = useState(false);

  // If already authenticated, go straight to workspace
  useEffect(() => {
    const token = document.cookie.match(/(?:^|; )bp-token=([^;]*)/)?.[1];
    if (token) router.replace("/workspace");
  }, [router]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const res = await fetch("/api/auth/login", {
        method:  "POST",
        headers: { "Content-Type": "application/json" },
        body:    JSON.stringify({ email: email.trim(), password }),
      });
      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        setError((data as { error?: string }).error ?? "Credenciales incorrectas");
        return;
      }
      setSuccess(true);
      setTimeout(() => router.replace(from), 400);
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
      {/* bg grid decoration */}
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
            Plataforma de construcción de productos SaaS
          </p>
        </div>

        {/* Card */}
        <div
          className="card"
          style={{
            borderRadius: "18px",
            boxShadow: "0 24px 64px rgba(0,0,0,0.4)",
            padding: "32px",
          }}
        >
          {/* Demo credentials notice */}
          <div
            style={{
              background: "var(--status-info-bg)", border: "1px solid var(--border)",
              borderRadius: "10px", padding: "12px 14px", marginBottom: "24px",
              display: "flex", alignItems: "flex-start", gap: "10px",
            }}
          >
            <Lock size={14} style={{ color: "var(--status-info-text)", marginTop: "2px", flexShrink: 0 }} />
            <div>
              <div style={{ fontSize: "11px", fontWeight: "700", color: "var(--status-info-text)", marginBottom: "4px", letterSpacing: "0.06em" }}>
                ACCESO DEMO
              </div>
              <div style={{ fontSize: "12px", color: "var(--muted)", lineHeight: "1.5" }}>
                <span style={{ color: "var(--foreground)" }}>admin@builder.local</span>
                {" / "}
                <span style={{ color: "var(--foreground)" }}>Builder1234!</span>
              </div>
            </div>
          </div>

          <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
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
                autoComplete="email"
                style={{
                  width: "100%", padding: "10px 14px", borderRadius: "10px",
                  border: "1px solid var(--border)", background: "var(--surface-elevated)",
                  color: "var(--foreground)", fontSize: "14px", outline: "none",
                  boxSizing: "border-box",
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
                autoComplete="current-password"
                style={{
                  width: "100%", padding: "10px 14px", borderRadius: "10px",
                  border: "1px solid var(--border)", background: "var(--surface-elevated)",
                  color: "var(--foreground)", fontSize: "14px", outline: "none",
                  boxSizing: "border-box",
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
                <><CheckCircle2 size={16} /> Accediendo...</>
              ) : loading ? (
                "Verificando..."
              ) : (
                <><ArrowRight size={16} /> Entrar al Builder OS</>
              )}
            </button>
          </form>

          <div style={{ marginTop: "20px", textAlign: "center" }}>
            <span style={{ fontSize: "13px", color: "var(--muted)" }}>¿No tenés cuenta?{" "}</span>
            <a
              href="/register"
              style={{ fontSize: "13px", color: "var(--accent)", fontWeight: "600", textDecoration: "none" }}
            >
              Crear cuenta
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
