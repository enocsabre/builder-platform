"use client";

import { useState, useEffect } from "react";
import { Brain, ArrowRight, GitBranch, Clock, Layers, History } from "lucide-react";
import { api } from "@/lib/api";
import type { EvolutionContext } from "@/lib/types";

interface Props {
  productId: string;
}

function timeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const m = Math.floor(diff / 60000);
  if (m < 1)  return "ahora mismo";
  if (m < 60) return `hace ${m}m`;
  const h = Math.floor(m / 60);
  if (h < 24) return `hace ${h}h`;
  return `hace ${Math.floor(h / 24)}d`;
}

const LAYER_COLORS: Record<string, string> = {
  "full-stack": "var(--status-info-bg)",
  backend:      "var(--status-warn-bg)",
  frontend:     "var(--status-active-bg)",
};

export function EvolutionPanel({ productId }: Props) {
  const [ctx, setCtx]       = useState<EvolutionContext | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError]   = useState<string | null>(null);

  useEffect(() => {
    api.evolution.get(productId)
      .then(setCtx)
      .catch(() => setError("No se pudo cargar la evolution memory."))
      .finally(() => setLoading(false));
  }, [productId]);

  if (loading) return (
    <div style={{ padding: "2rem", color: "var(--text-muted)", textAlign: "center" }}>
      Cargando evolution memory...
    </div>
  );

  if (error) return (
    <div style={{ padding: "2rem", color: "var(--status-danger-text)" }}>{error}</div>
  );

  if (!ctx || ctx.modules.length === 0) return (
    <div style={{ padding: "2rem", color: "var(--text-muted)", textAlign: "center" }}>
      <Brain size={32} style={{ marginBottom: "0.75rem", opacity: 0.4 }} />
      <p style={{ margin: 0 }}>Sin evolution memory todavía.</p>
      <p style={{ margin: "0.25rem 0 0", fontSize: "0.8rem" }}>
        Se inicializa al generar el scaffold del producto.
      </p>
    </div>
  );

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem", padding: "1rem 0" }}>

      {/* Módulos */}
      <Section icon={<Layers size={14} />} title={`Módulos (${ctx.modules.length})`}>
        <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
          {ctx.modules.map((m) => (
            <div key={m.name} style={{
              display: "flex", alignItems: "center", gap: "0.75rem",
              padding: "0.5rem 0.75rem",
              background: "var(--surface-2)",
              borderRadius: "6px",
            }}>
              <span style={{ fontWeight: 600, flex: 1, fontSize: "0.85rem" }}>{m.name}</span>
              <code style={{ fontSize: "0.75rem", color: "var(--text-muted)" }}>{m.route}</code>
              <Badge color={LAYER_COLORS[m.layer] ?? "var(--surface-3)"}>{m.layer}</Badge>
              <span style={{ fontSize: "0.7rem", color: "var(--text-muted)", whiteSpace: "nowrap" }}>
                {timeAgo(m.addedAt)}
              </span>
            </div>
          ))}
        </div>
      </Section>

      {/* Conexiones */}
      {ctx.relations.length > 0 && (
        <Section icon={<GitBranch size={14} />} title={`Conexiones (${ctx.relations.length})`}>
          <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
            {ctx.relations.map((r, i) => (
              <div key={i} style={{
                padding: "0.5rem 0.75rem",
                background: "var(--surface-2)",
                borderRadius: "6px",
                fontSize: "0.82rem",
              }}>
                <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "0.25rem" }}>
                  <span style={{ fontWeight: 600 }}>{r.from}</span>
                  <ArrowRight size={12} style={{ color: "var(--text-muted)" }} />
                  <span style={{ fontWeight: 600 }}>{r.to}</span>
                  <Badge color="var(--surface-3)">{r.relationType}</Badge>
                </div>
                <p style={{ margin: 0, color: "var(--text-muted)", fontSize: "0.75rem" }}>{r.reason}</p>
              </div>
            ))}
          </div>
        </Section>
      )}

      {/* Decisiones */}
      {ctx.decisions.length > 0 && (
        <Section icon={<Clock size={14} />} title={`Decisiones arquitectónicas (${ctx.decisions.length})`}>
          <div style={{ display: "flex", flexDirection: "column", gap: "0.4rem" }}>
            {[...ctx.decisions].reverse().map((d, i) => (
              <div key={i} style={{
                display: "flex", gap: "0.75rem", alignItems: "flex-start",
                padding: "0.4rem 0.75rem",
                background: "var(--surface-2)",
                borderRadius: "6px",
                fontSize: "0.8rem",
              }}>
                <span style={{ color: "var(--text-muted)", whiteSpace: "nowrap", paddingTop: "1px" }}>
                  {timeAgo(d.madeAt)}
                </span>
                <span>{d.summary}</span>
              </div>
            ))}
          </div>
        </Section>
      )}

      {/* Historial */}
      {ctx.featureHistory.length > 0 && (
        <Section icon={<History size={14} />} title={`Historial de features (${ctx.featureHistory.length})`}>
          <div style={{ display: "flex", flexWrap: "wrap", gap: "0.4rem" }}>
            {ctx.featureHistory.map((f) => (
              <Badge key={f} color="var(--surface-2)">{f}</Badge>
            ))}
          </div>
        </Section>
      )}
    </div>
  );
}

function Section({ icon, title, children }: { icon: React.ReactNode; title: string; children: React.ReactNode }) {
  return (
    <div>
      <div style={{
        display: "flex", alignItems: "center", gap: "0.4rem",
        marginBottom: "0.6rem",
        fontSize: "0.75rem",
        fontWeight: 600,
        color: "var(--text-muted)",
        textTransform: "uppercase",
        letterSpacing: "0.05em",
      }}>
        {icon}
        {title}
      </div>
      {children}
    </div>
  );
}

function Badge({ color, children }: { color: string; children: React.ReactNode }) {
  return (
    <span style={{
      display: "inline-block",
      padding: "0.15rem 0.45rem",
      borderRadius: "4px",
      fontSize: "0.72rem",
      fontWeight: 500,
      background: color,
      color: "var(--text-secondary)",
      whiteSpace: "nowrap",
    }}>
      {children}
    </span>
  );
}
