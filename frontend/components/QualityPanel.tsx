"use client";

import { useState, useEffect } from "react";
import { ProductDetail, ValidationRunDetail, GateResult, RuntimeHealth } from "@/lib/types";
import { ShieldCheck, ShieldAlert, ShieldX, RefreshCw, Play, ChevronDown, ChevronUp } from "lucide-react";

const API = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5238";

interface QualityPanelProps {
  product: ProductDetail;
}

const HEALTH_COLORS: Record<RuntimeHealth, { bg: string; text: string; label: string }> = {
  healthy:    { bg: "var(--status-active-bg)",  text: "var(--status-active-text)",  label: "Saludable"    },
  degraded:   { bg: "var(--status-warn-bg)",    text: "var(--status-warn-text)",    label: "Degradado"    },
  broken:     { bg: "var(--status-danger-bg)",  text: "var(--status-danger-text)",  label: "Roto"         },
  recovering: { bg: "var(--status-info-bg)",    text: "var(--status-info-text)",    label: "Recuperando"  },
};

const GATE_CATEGORIES: Record<string, string> = {
  registry: "Registry",
  runtime:  "Runtime",
  build:    "Build",
};

const GATE_LABELS: Record<string, string> = {
  registry_nav:       "Nav Registry",
  registry_dashboard: "Dashboard Registry",
  registry_modules:   "Modules Registry",
  preview_running:    "Preview Activo",
  dashboard_route:    "Ruta /dashboard",
  frontend_typecheck: "TypeScript Check",
  backend_build:      "Backend Build",
};

function GateBadge({ gate }: { gate: GateResult }) {
  const [open, setOpen] = useState(false);
  const label   = GATE_LABELS[gate.gate] ?? gate.gate;
  const color   = gate.skipped
    ? "var(--muted)"
    : gate.passed
    ? "var(--status-active-text)"
    : "var(--status-danger-text)";
  const marker  = gate.skipped ? "—" : gate.passed ? "✓" : "✗";

  return (
    <div
      style={{
        padding:      "8px 12px",
        background:   "var(--surface)",
        borderRadius: "8px",
        border:       `1px solid var(--border)`,
        marginBottom: "4px",
      }}
    >
      <div
        style={{ display: "flex", alignItems: "center", gap: "10px", cursor: gate.detail ? "pointer" : "default" }}
        onClick={() => gate.detail && setOpen(v => !v)}
      >
        <span style={{ fontSize: "13px", fontWeight: "700", color, flexShrink: 0 }}>{marker}</span>
        <div style={{ flex: 1, minWidth: 0 }}>
          <span style={{ fontSize: "12px", color: "var(--foreground)", fontWeight: "500" }}>{label}</span>
          <span style={{ fontSize: "10px", color: "var(--muted)", marginLeft: "8px" }}>{gate.message}</span>
        </div>
        {gate.detail && (
          open ? <ChevronUp size={12} style={{ color: "var(--muted)", flexShrink: 0 }} />
               : <ChevronDown size={12} style={{ color: "var(--muted)", flexShrink: 0 }} />
        )}
      </div>

      {open && gate.detail && (
        <pre style={{
          marginTop: "8px", padding: "8px", background: "var(--bg)", borderRadius: "6px",
          fontSize: "10px", lineHeight: "1.5", color: "var(--status-danger-text)",
          whiteSpace: "pre-wrap", wordBreak: "break-all", overflow: "auto", maxHeight: "200px",
        }}>
          {gate.detail}
        </pre>
      )}
    </div>
  );
}

export default function QualityPanel({ product }: QualityPanelProps) {
  const [runDetail, setRunDetail] = useState<ValidationRunDetail | null>(null);
  const [loading,   setLoading]   = useState(false);
  const [running,   setRunning]   = useState(false);
  const [logsOpen,  setLogsOpen]  = useState(false);

  const health = (product.runtimeHealth ?? "healthy") as RuntimeHealth;
  const hc     = HEALTH_COLORS[health] ?? HEALTH_COLORS.healthy;

  const latestRun = product.validationRuns?.[0];

  const loadRunDetail = async (runId: string) => {
    setLoading(true);
    try {
      const res = await fetch(`${API}/api/products/${product.id}/validations/${runId}`);
      if (res.ok) setRunDetail(await res.json());
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (latestRun?.id) loadRunDetail(latestRun.id);
  }, [latestRun?.id]);

  const triggerValidation = async () => {
    setRunning(true);
    try {
      await fetch(`${API}/api/products/${product.id}/validate`, { method: "POST" });
    } finally {
      setTimeout(() => setRunning(false), 2000);
    }
  };

  const byCategory = runDetail?.gateResults.reduce<Record<string, GateResult[]>>((acc, g) => {
    const cat = GATE_CATEGORIES[g.category] ?? g.category;
    if (!acc[cat]) acc[cat] = [];
    acc[cat].push(g);
    return acc;
  }, {}) ?? {};

  return (
    <div style={{ padding: "16px" }}>

      {/* Health banner */}
      <div style={{
        display: "flex", alignItems: "center", justifyContent: "space-between",
        padding: "12px 16px", background: hc.bg, borderRadius: "10px",
        border: "1px solid var(--border)", marginBottom: "20px",
      }}>
        <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
          {health === "healthy"    && <ShieldCheck size={18} style={{ color: hc.text }} />}
          {health === "degraded"   && <ShieldAlert size={18} style={{ color: hc.text }} />}
          {health === "broken"     && <ShieldX     size={18} style={{ color: hc.text }} />}
          {health === "recovering" && <RefreshCw   size={18} style={{ color: hc.text }} />}
          <div>
            <p style={{ fontSize: "13px", fontWeight: "700", color: hc.text }}>{hc.label}</p>
            <p style={{ fontSize: "10px", color: hc.text, opacity: 0.8 }}>Runtime health del proyecto</p>
          </div>
        </div>

        <button
          onClick={triggerValidation}
          disabled={running || product.isProcessing}
          style={{
            display: "flex", alignItems: "center", gap: "6px",
            padding: "6px 14px", borderRadius: "8px", fontSize: "11px", fontWeight: "700",
            background: "var(--accent)", color: "#000", border: "none", cursor: "pointer",
            opacity: running || product.isProcessing ? 0.5 : 1,
          }}
        >
          <Play size={11} />
          {running ? "Iniciando…" : "Validar"}
        </button>
      </div>

      {/* Latest run summary */}
      {latestRun && (
        <div style={{ marginBottom: "20px" }}>
          <p style={{ fontSize: "11px", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.5px", fontWeight: "600", marginBottom: "10px" }}>
            Último Run
          </p>
          <div style={{
            padding: "10px 14px", background: "var(--surface)", borderRadius: "8px",
            border: "1px solid var(--border)", display: "flex", alignItems: "center", gap: "12px",
          }}>
            <span style={{
              fontSize: "10px", fontWeight: "700", padding: "2px 8px", borderRadius: "4px",
              background: latestRun.status === "passed" ? "var(--status-active-bg)" : "var(--status-danger-bg)",
              color: latestRun.status === "passed" ? "var(--status-active-text)" : "var(--status-danger-text)",
              textTransform: "uppercase",
            }}>
              {latestRun.status}
            </span>
            <span style={{ fontSize: "11px", color: "var(--status-active-text)" }}>
              {latestRun.gatesPassed}✓
            </span>
            {latestRun.gatesFailed > 0 && (
              <span style={{ fontSize: "11px", color: "var(--status-danger-text)" }}>
                {latestRun.gatesFailed}✗
              </span>
            )}
            {latestRun.autofixAttempts > 0 && (
              <span style={{ fontSize: "10px", color: "var(--status-info-text)" }}>
                {latestRun.autofixAttempts} autofix
              </span>
            )}
            <span style={{ fontSize: "10px", color: "var(--muted)", marginLeft: "auto" }}>
              {new Date(latestRun.startedAt).toLocaleString("es-CR", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })}
            </span>
          </div>
        </div>
      )}

      {/* Gate results */}
      {loading ? (
        <p style={{ fontSize: "12px", color: "var(--muted)", padding: "8px 0" }}>Cargando resultados…</p>
      ) : runDetail ? (
        <div style={{ marginBottom: "20px" }}>
          <p style={{ fontSize: "11px", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.5px", fontWeight: "600", marginBottom: "10px" }}>
            Quality Gates
          </p>

          {Object.entries(byCategory).map(([cat, gates]) => (
            <div key={cat} style={{ marginBottom: "12px" }}>
              <p style={{ fontSize: "10px", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.5px", marginBottom: "6px", fontWeight: "600" }}>
                {cat}
              </p>
              {gates.map(g => <GateBadge key={g.gate} gate={g} />)}
            </div>
          ))}

          {/* Logs */}
          {(runDetail.logs || runDetail.errors) && (
            <div style={{ marginTop: "12px" }}>
              <button
                onClick={() => setLogsOpen(v => !v)}
                style={{
                  display: "flex", alignItems: "center", gap: "6px",
                  background: "none", border: "none", color: "var(--muted)",
                  fontSize: "11px", cursor: "pointer", padding: 0,
                }}
              >
                {logsOpen ? <ChevronUp size={11} /> : <ChevronDown size={11} />}
                Ver logs del run
              </button>

              {logsOpen && (
                <pre style={{
                  marginTop: "8px", padding: "10px", background: "var(--bg)", borderRadius: "8px",
                  fontSize: "10px", lineHeight: "1.5", color: "var(--foreground)",
                  whiteSpace: "pre-wrap", wordBreak: "break-all", overflow: "auto", maxHeight: "260px",
                  border: "1px solid var(--border)",
                }}>
                  {runDetail.logs}
                  {runDetail.errors && `\n--- ERRORS ---\n${runDetail.errors}`}
                </pre>
              )}
            </div>
          )}
        </div>
      ) : !latestRun ? (
        <InfoBox type="info">
          No hay validaciones aún. Hacé clic en <strong>Validar</strong> o enviá &ldquo;valida el proyecto&rdquo; en el chat.
        </InfoBox>
      ) : null}

      {/* Run history */}
      {product.validationRuns.length > 1 && (
        <div>
          <p style={{ fontSize: "11px", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.5px", fontWeight: "600", marginBottom: "8px" }}>
            Historial ({product.validationRuns.length})
          </p>
          <div style={{ display: "flex", flexDirection: "column", gap: "4px" }}>
            {product.validationRuns.map(r => (
              <div
                key={r.id}
                onClick={() => loadRunDetail(r.id)}
                style={{
                  display: "flex", alignItems: "center", gap: "8px",
                  padding: "6px 10px", background: "var(--surface)", borderRadius: "6px",
                  border: r.id === runDetail?.id ? "1px solid var(--accent)" : "1px solid var(--border)",
                  cursor: "pointer",
                }}
              >
                <span style={{
                  fontSize: "9px", fontWeight: "700", padding: "1px 5px", borderRadius: "3px",
                  background: r.status === "passed" ? "var(--status-active-bg)" : "var(--status-danger-bg)",
                  color: r.status === "passed" ? "var(--status-active-text)" : "var(--status-danger-text)",
                  textTransform: "uppercase",
                }}>
                  {r.status}
                </span>
                <span style={{ fontSize: "10px", color: "var(--status-active-text)" }}>{r.gatesPassed}✓</span>
                {r.gatesFailed > 0 && <span style={{ fontSize: "10px", color: "var(--status-danger-text)" }}>{r.gatesFailed}✗</span>}
                {r.autofixAttempts > 0 && <span style={{ fontSize: "9px", color: "var(--status-info-text)" }}>{r.autofixAttempts} fix</span>}
                <span style={{ fontSize: "9px", color: "var(--muted)", marginLeft: "auto" }}>
                  {new Date(r.startedAt).toLocaleTimeString("es-CR", { hour: "2-digit", minute: "2-digit" })}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function InfoBox({ type, children }: { type: "info" | "warning"; children: React.ReactNode }) {
  const colors = type === "warning"
    ? { bg: "var(--status-warn-bg)",  border: "rgba(251,191,36,0.2)",  text: "var(--status-warn-text)"  }
    : { bg: "var(--status-info-bg)",  border: "rgba(96,165,250,0.2)",  text: "var(--status-info-text)"  };
  return (
    <div style={{ padding: "10px 14px", background: colors.bg, border: `1px solid ${colors.border}`, borderRadius: "8px" }}>
      <p style={{ fontSize: "12px", color: colors.text, lineHeight: "1.6" }}>{children}</p>
    </div>
  );
}
