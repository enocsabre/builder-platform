"use client";

import { useState, useEffect, useCallback } from "react";
import { Wrench, AlertTriangle, AlertCircle, Info, CheckCircle, XCircle, ChevronDown, ChevronUp, Play, RotateCcw } from "lucide-react";
import { api } from "@/lib/api";
import type { RefactorRecommendation } from "@/lib/types";

interface Props {
  productId:  string;
  onActivity: () => void;
}

const SEVERITY_CONFIG = {
  high:   { color: "var(--status-danger-text)",  bg: "var(--status-danger-bg)",  icon: AlertTriangle, label: "Alta"  },
  medium: { color: "var(--status-warn-text)",    bg: "var(--status-warn-bg)",    icon: AlertCircle,  label: "Media" },
  low:    { color: "var(--status-info-text)",    bg: "var(--status-info-bg)",    icon: Info,         label: "Baja"  },
} as const;

const TYPE_LABELS: Record<string, string> = {
  duplicate_module:       "Módulo duplicado",
  redundant_name:         "Nombre redundante",
  ugly_route:             "Ruta mejorable",
  contradictory_relation: "Relación contradictoria",
  missing_connection:     "Conexión faltante",
  orphaned_history:       "Historial huérfano",
};

function timeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const m    = Math.floor(diff / 60000);
  if (m < 1)  return "ahora";
  if (m < 60) return `hace ${m}m`;
  const h = Math.floor(m / 60);
  if (h < 24) return `hace ${h}h`;
  return `hace ${Math.floor(h / 24)}d`;
}

export function RefactorPanel({ productId, onActivity }: Props) {
  const [recs, setRecs]         = useState<RefactorRecommendation[]>([]);
  const [loading, setLoading]   = useState(true);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [resolving, setResolving] = useState<string | null>(null);
  const [executing, setExecuting] = useState<string | null>(null);
  const [filter, setFilter]     = useState<"all" | "pending" | "accepted" | "resolved">("pending");

  const load = useCallback(() => {
    api.refactor.list(productId)
      .then(setRecs)
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [productId]);

  useEffect(() => { load(); }, [load]);

  const resolve = async (recId: string, accepted: boolean) => {
    setResolving(recId);
    try {
      const updated = await api.refactor.resolve(productId, recId, accepted);
      setRecs(prev => prev.map(r => r.id === recId ? updated : r));
      onActivity();
    } finally {
      setResolving(null);
    }
  };

  const execute = async (recId: string) => {
    setExecuting(recId);
    try {
      const updated = await api.refactor.execute(productId, recId);
      setRecs(prev => prev.map(r => r.id === recId ? updated : r));
      onActivity();
    } catch {
      // error shown via status on the rec
      await load();
    } finally {
      setExecuting(null);
    }
  };

  if (loading) return (
    <div style={{ padding: "2rem", color: "var(--text-muted)", textAlign: "center" }}>
      Analizando arquitectura...
    </div>
  );

  const pending  = recs.filter(r => r.status === "pending");
  const accepted = recs.filter(r => r.status === "accepted");
  const resolved = recs.filter(r => !["pending", "accepted"].includes(r.status));
  const displayed =
    filter === "pending"  ? pending  :
    filter === "accepted" ? accepted :
    filter === "resolved" ? resolved : recs;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0", padding: "1rem 0" }}>

      {/* Header */}
      <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", marginBottom: "1rem", padding: "0 0.25rem" }}>
        <div style={{ display: "flex", gap: "0.4rem", flexWrap: "wrap" }}>
          {(["pending", "accepted", "resolved", "all"] as const).map(f => (
            <button key={f} onClick={() => setFilter(f)} style={{
              padding: "0.2rem 0.6rem",
              borderRadius: "4px",
              border: "none",
              cursor: "pointer",
              fontSize: "0.75rem",
              fontWeight: 500,
              background: filter === f ? "var(--accent)" : "var(--surface-2)",
              color: filter === f ? "#fff" : "var(--text-muted)",
            }}>
              {f === "pending"  ? `Pendientes (${pending.length})`  :
               f === "accepted" ? `Aceptadas (${accepted.length})`  :
               f === "resolved" ? `Resueltas (${resolved.length})`  : "Todas"}
            </button>
          ))}
        </div>
      </div>

      {displayed.length === 0 && (
        <div style={{ padding: "2rem", textAlign: "center", color: "var(--text-muted)" }}>
          {filter === "pending" ? (
            <>
              <CheckCircle size={28} style={{ marginBottom: "0.5rem", color: "var(--status-active-text)" }} />
              <p style={{ margin: 0, fontSize: "0.85rem" }}>No hay recomendaciones pendientes.</p>
              <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem" }}>
                El runtime analiza la arquitectura cada vez que se agrega un módulo.
              </p>
            </>
          ) : (
            <p style={{ margin: 0, fontSize: "0.85rem" }}>
              Sin recomendaciones {filter === "accepted" ? "aceptadas" : filter === "resolved" ? "resueltas" : ""}.
            </p>
          )}
        </div>
      )}

      <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
        {displayed.map(rec => {
          const sev = SEVERITY_CONFIG[rec.severity] ?? SEVERITY_CONFIG.low;
          const SevIcon = sev.icon;
          const isExpanded  = expanded === rec.id;
          const isPending   = rec.status === "pending";
          const isAccepted  = rec.status === "accepted";
          const isApplied   = rec.status === "applied";
          const isFailed    = rec.status === "failed";

          return (
            <div key={rec.id} style={{
              border: `1px solid ${isPending ? sev.color + "33" : "var(--border)"}`,
              borderRadius: "8px",
              overflow: "hidden",
              background: "var(--surface-1)",
            }}>
              {/* Card header */}
              <button
                onClick={() => setExpanded(isExpanded ? null : rec.id)}
                style={{
                  width: "100%", textAlign: "left",
                  padding: "0.65rem 0.75rem",
                  background: "none", border: "none", cursor: "pointer",
                  display: "flex", alignItems: "flex-start", gap: "0.6rem",
                }}
              >
                <SevIcon size={15} style={{ color: sev.color, flexShrink: 0, marginTop: "1px" }} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: "0.4rem", flexWrap: "wrap" }}>
                    <span style={{ fontWeight: 600, fontSize: "0.83rem" }}>{rec.title}</span>
                    <span style={{
                      padding: "0.1rem 0.35rem", borderRadius: "3px", fontSize: "0.68rem",
                      background: sev.bg, color: sev.color, whiteSpace: "nowrap",
                    }}>
                      {sev.label}
                    </span>
                    <span style={{
                      padding: "0.1rem 0.35rem", borderRadius: "3px", fontSize: "0.68rem",
                      background: "var(--surface-2)", color: "var(--text-muted)", whiteSpace: "nowrap",
                    }}>
                      {TYPE_LABELS[rec.type] ?? rec.type}
                    </span>
                    {rec.status === "accepted" && (
                      <span style={{ padding: "0.1rem 0.35rem", borderRadius: "3px", fontSize: "0.68rem", background: "var(--status-active-bg)", color: "var(--status-active-text)" }}>
                        Aceptada
                      </span>
                    )}
                    {rec.status === "applied" && (
                      <span style={{ padding: "0.1rem 0.35rem", borderRadius: "3px", fontSize: "0.68rem", background: "var(--status-active-bg)", color: "var(--status-active-text)" }}>
                        ✓ Aplicada
                      </span>
                    )}
                    {rec.status === "failed" && (
                      <span style={{ padding: "0.1rem 0.35rem", borderRadius: "3px", fontSize: "0.68rem", background: "var(--status-danger-bg)", color: "var(--status-danger-text)" }}>
                        ✗ Falló
                      </span>
                    )}
                    {rec.status === "rejected" && (
                      <span style={{ padding: "0.1rem 0.35rem", borderRadius: "3px", fontSize: "0.68rem", background: "var(--surface-3)", color: "var(--text-muted)" }}>
                        Rechazada
                      </span>
                    )}
                  </div>
                  <div style={{ fontSize: "0.72rem", color: "var(--text-muted)", marginTop: "0.15rem" }}>
                    {timeAgo(rec.createdAt)}
                  </div>
                </div>
                {isExpanded ? <ChevronUp size={13} style={{ color: "var(--text-muted)", flexShrink: 0 }} /> : <ChevronDown size={13} style={{ color: "var(--text-muted)", flexShrink: 0 }} />}
              </button>

              {/* Expanded detail */}
              {isExpanded && (
                <div style={{ padding: "0.75rem", borderTop: "1px solid var(--border)", fontSize: "0.8rem" }}>
                  <DetailRow label="Por qué" value={rec.reason} />
                  <DetailRow label="Impacto" value={rec.impact} />
                  <DetailRow label="Riesgo"  value={rec.risk}   />

                  {isPending && (
                    <div style={{ display: "flex", gap: "0.5rem", marginTop: "0.75rem" }}>
                      <ActionBtn
                        label={resolving === rec.id ? "Procesando..." : "✓ Aceptar y generar plan"}
                        accent
                        disabled={resolving === rec.id}
                        onClick={() => resolve(rec.id, true)}
                      />
                      <ActionBtn
                        label="Rechazar"
                        disabled={resolving === rec.id}
                        onClick={() => resolve(rec.id, false)}
                      />
                    </div>
                  )}

                  {isAccepted && (
                    <div style={{ marginTop: "0.75rem" }}>
                      {rec.artifactId && (
                        <div style={{ marginBottom: "0.5rem", padding: "0.4rem 0.6rem", background: "var(--status-active-bg)", borderRadius: "4px", fontSize: "0.75rem", color: "var(--status-active-text)" }}>
                          Refactor Plan generado — visible en la pestaña Artefactos.
                        </div>
                      )}
                      <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
                        <ActionBtn
                          label={executing === rec.id ? "Aplicando..." : "▶ Aplicar refactor"}
                          accent
                          disabled={executing === rec.id}
                          onClick={() => execute(rec.id)}
                        />
                        <span style={{ fontSize: "0.7rem", color: "var(--text-muted)" }}>
                          Solo toca archivos de registry — nunca código custom.
                        </span>
                      </div>
                    </div>
                  )}

                  {isApplied && (
                    <div style={{ marginTop: "0.75rem", padding: "0.5rem 0.65rem", background: "var(--status-active-bg)", borderRadius: "4px", fontSize: "0.75rem", color: "var(--status-active-text)" }}>
                      ✓ Refactor aplicado exitosamente. Validación de registry pasó.
                      {rec.executedAt && (
                        <span style={{ marginLeft: "0.4rem", opacity: 0.7 }}>· {timeAgo(rec.executedAt)}</span>
                      )}
                    </div>
                  )}

                  {isFailed && (
                    <div style={{ marginTop: "0.75rem", padding: "0.5rem 0.65rem", background: "var(--status-danger-bg)", borderRadius: "4px", fontSize: "0.75rem", color: "var(--status-danger-text)" }}>
                      ✗ Ejecución falló: {rec.executionError ?? "Error desconocido."}
                    </div>
                  )}

                  {rec.note && (
                    <div style={{ marginTop: "0.4rem", color: "var(--text-muted)", fontStyle: "italic" }}>
                      Nota: {rec.note}
                    </div>
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ marginBottom: "0.5rem" }}>
      <span style={{ fontWeight: 600, color: "var(--text-muted)", marginRight: "0.35rem" }}>{label}:</span>
      <span style={{ color: "var(--text-secondary)" }}>{value}</span>
    </div>
  );
}

function ActionBtn({ label, accent, disabled, onClick }: { label: string; accent?: boolean; disabled?: boolean; onClick: () => void }) {
  return (
    <button onClick={onClick} disabled={disabled} style={{
      padding: "0.35rem 0.75rem",
      borderRadius: "5px",
      border: "none",
      cursor: disabled ? "not-allowed" : "pointer",
      fontSize: "0.78rem",
      fontWeight: 600,
      opacity: disabled ? 0.5 : 1,
      background: accent ? "var(--accent)" : "var(--surface-2)",
      color: accent ? "#fff" : "var(--text-secondary)",
      transition: "opacity 0.15s",
    }}>
      {label}
    </button>
  );
}
