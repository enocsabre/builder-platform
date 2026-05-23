"use client";

import { useState, useEffect } from "react";
import {
  Brain, AlertTriangle, CheckCircle2, ArrowRight,
  Lightbulb, Link2, TrendingUp, Zap, RefreshCw,
} from "lucide-react";
import { api } from "@/lib/api";
import type { IntelligenceReport, IntelligenceGap, IntelligenceConnection, IntelligenceSuggestion } from "@/lib/types";

interface Props {
  productId: string;
}

function priorityColor(p: string): string {
  if (p === "high")   return "var(--status-danger-text)";
  if (p === "medium") return "var(--status-warn-text)";
  return "var(--status-info-text)";
}

function priorityBg(p: string): string {
  if (p === "high")   return "var(--status-danger-bg)";
  if (p === "medium") return "var(--status-warn-bg)";
  return "var(--status-info-bg)";
}

function stageColor(s: string): string {
  if (s === "mature") return "var(--status-active-text)";
  if (s === "growth") return "var(--status-info-text)";
  return "var(--status-indigo-text)";
}

function stageBg(s: string): string {
  if (s === "mature") return "var(--status-active-bg)";
  if (s === "growth") return "var(--status-info-bg)";
  return "var(--status-indigo-bg)";
}

function categoryColor(c: string): string {
  if (c === "automation") return "var(--status-info-text)";
  if (c === "reporting")  return "var(--status-warn-text)";
  if (c === "financial")  return "var(--status-active-text)";
  return "var(--status-indigo-text)";
}

function categoryLabel(c: string): string {
  if (c === "automation")  return "Automatización";
  if (c === "reporting")   return "Reportes";
  if (c === "financial")   return "Financiero";
  return "Operaciones";
}

export function IntelligencePanel({ productId }: Props) {
  const [report, setReport]   = useState<IntelligenceReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError]     = useState<string | null>(null);

  function load() {
    setLoading(true);
    setError(null);
    api.intelligence.get(productId)
      .then(setReport)
      .catch(() => setError("No se pudo cargar el análisis de inteligencia."))
      .finally(() => setLoading(false));
  }

  useEffect(() => { load(); }, [productId]);

  if (loading) return (
    <div style={{ padding: "32px", display: "flex", alignItems: "center", gap: "10px", color: "var(--muted)" }}>
      <Brain size={16} style={{ animation: "spin 1.5s linear infinite" }} />
      <span style={{ fontSize: "13px" }}>Analizando producto...</span>
    </div>
  );

  if (error || !report) return (
    <div style={{ padding: "24px", fontSize: "13px", color: "var(--status-danger-text)" }}>
      {error ?? "Sin datos de inteligencia."}
    </div>
  );

  const highGaps = report.gaps.filter(g => g.priority === "high");
  const otherGaps = report.gaps.filter(g => g.priority !== "high");
  const undetectedConns = report.connections.filter(c => !c.detected);
  const detectedConns   = report.connections.filter(c => c.detected);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "20px", padding: "4px 0" }}>

      {/* ── Header: stage + narrative ─────────────────────────────────────── */}
      <div style={{
        borderRadius: "12px", padding: "18px 20px",
        background: "linear-gradient(135deg, rgba(99,102,241,0.08) 0%, rgba(0,0,0,0) 70%)",
        border: "1px solid rgba(99,102,241,0.18)",
      }}>
        <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: "12px", marginBottom: "12px" }}>
          <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
            <div style={{
              width: "32px", height: "32px", borderRadius: "8px", flexShrink: 0,
              background: "rgba(99,102,241,0.15)",
              display: "flex", alignItems: "center", justifyContent: "center",
            }}>
              <Brain size={15} style={{ color: "#a5b4fc" }} />
            </div>
            <div>
              <div style={{ fontSize: "13px", fontWeight: "700", color: "var(--foreground)" }}>
                Análisis de Inteligencia Operacional
              </div>
              <div style={{ fontSize: "11px", color: "var(--muted)", marginTop: "1px" }}>
                {report.industryLabel} · {report.moduleCount} módulo{report.moduleCount !== 1 ? "s" : ""} registrado{report.moduleCount !== 1 ? "s" : ""}
              </div>
            </div>
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: "8px", flexShrink: 0 }}>
            <span style={{
              fontSize: "10px", fontWeight: "700", padding: "3px 10px", borderRadius: "99px",
              background: stageBg(report.evolutionStage),
              color: stageColor(report.evolutionStage),
              letterSpacing: "0.04em",
            }}>
              {report.evolutionStageLabel.toUpperCase()}
            </span>
            <button
              onClick={load}
              style={{ padding: "4px", borderRadius: "6px", background: "none", border: "none", cursor: "pointer", color: "var(--muted)", display: "flex" }}
              title="Actualizar análisis"
            >
              <RefreshCw size={12} />
            </button>
          </div>
        </div>

        {/* Narrative */}
        <p style={{ fontSize: "13px", color: "var(--foreground)", lineHeight: "1.6", margin: "0 0 12px 0" }}>
          {report.narrative}
        </p>

        {/* Next milestone */}
        <div style={{
          display: "flex", alignItems: "center", gap: "8px",
          padding: "8px 12px", borderRadius: "8px",
          background: "rgba(99,102,241,0.1)", border: "1px solid rgba(99,102,241,0.2)",
        }}>
          <TrendingUp size={12} style={{ color: "#a5b4fc", flexShrink: 0 }} />
          <span style={{ fontSize: "11px", color: "#a5b4fc", fontWeight: "600" }}>Próximo hito:</span>
          <span style={{ fontSize: "11px", color: "var(--muted)" }}>{report.evolutionNextMilestone}</span>
        </div>
      </div>

      {/* ── Suggestions (top) ────────────────────────────────────────────────── */}
      {report.suggestions.length > 0 && (
        <section>
          <SectionHeader Icon={Lightbulb} label="Sugerencias del Builder" count={report.suggestions.length} color="#fbbf24" />
          <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
            {report.suggestions.map((s, i) => (
              <SuggestionCard key={i} s={s} />
            ))}
          </div>
        </section>
      )}

      {/* ── Gaps ────────────────────────────────────────────────────────────── */}
      {report.gaps.length > 0 && (
        <section>
          <SectionHeader Icon={AlertTriangle} label="Gaps detectados" count={report.gaps.length} color="var(--status-warn-text)" />
          <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
            {highGaps.map((g, i) => <GapCard key={i} gap={g} />)}
            {otherGaps.map((g, i) => <GapCard key={`other-${i}`} gap={g} />)}
          </div>
        </section>
      )}

      {/* ── Connections ──────────────────────────────────────────────────────── */}
      {report.connections.length > 0 && (
        <section>
          <SectionHeader Icon={Link2} label="Conexiones entre módulos" count={report.connections.length} color="var(--status-info-text)" />
          <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
            {undetectedConns.map((c, i) => <ConnectionCard key={i} conn={c} />)}
            {detectedConns.map((c, i) => <ConnectionCard key={`det-${i}`} conn={c} />)}
          </div>
        </section>
      )}

      {/* ── Empty state ──────────────────────────────────────────────────────── */}
      {report.gaps.length === 0 && report.connections.length === 0 && (
        <div style={{
          padding: "24px", borderRadius: "10px", textAlign: "center",
          border: "1px solid var(--border)", background: "var(--surface)",
        }}>
          <CheckCircle2 size={20} style={{ color: "var(--status-active-text)", margin: "0 auto 8px" }} />
          <div style={{ fontSize: "13px", fontWeight: "600", color: "var(--foreground)", marginBottom: "4px" }}>
            Sistema bien configurado
          </div>
          <div style={{ fontSize: "12px", color: "var(--muted)" }}>
            No se detectaron gaps críticos ni conexiones faltantes para esta industria.
          </div>
        </div>
      )}

      {/* ── Footer meta ──────────────────────────────────────────────────────── */}
      <div style={{ fontSize: "10px", color: "var(--muted)", opacity: 0.5, paddingTop: "4px" }}>
        Análisis generado · {new Date(report.analyzedAt).toLocaleTimeString("es-CR", { hour: "2-digit", minute: "2-digit" })} · Builder Intelligence Engine — Sprint 38
      </div>
    </div>
  );
}

// ── Sub-components ─────────────────────────────────────────────────────────────

function SectionHeader({
  Icon, label, count, color,
}: {
  Icon: React.FC<{ size?: number; style?: React.CSSProperties }>;
  label: string; count: number; color: string;
}) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: "6px", marginBottom: "10px" }}>
      <Icon size={13} style={{ color }} />
      <span style={{ fontSize: "12px", fontWeight: "700", color: "var(--foreground)", letterSpacing: "0.02em" }}>
        {label}
      </span>
      <span style={{
        fontSize: "10px", fontWeight: "700", padding: "1px 6px", borderRadius: "99px",
        background: "var(--surface-elevated)", color: "var(--muted)",
      }}>
        {count}
      </span>
    </div>
  );
}

function GapCard({ gap }: { gap: IntelligenceGap }) {
  return (
    <div style={{
      padding: "10px 14px", borderRadius: "8px",
      background: "var(--surface)",
      border: `1px solid ${priorityColor(gap.priority)}22`,
      display: "flex", alignItems: "flex-start", gap: "10px",
    }}>
      <div style={{
        width: "6px", height: "6px", borderRadius: "50%", marginTop: "5px", flexShrink: 0,
        background: priorityColor(gap.priority),
      }} />
      <div style={{ flex: 1 }}>
        <div style={{ display: "flex", alignItems: "center", gap: "8px", marginBottom: "2px" }}>
          <span style={{ fontSize: "12px", fontWeight: "600", color: "var(--foreground)" }}>
            {gap.module}
          </span>
          <span style={{
            fontSize: "9px", fontWeight: "700", padding: "1px 6px", borderRadius: "99px",
            background: priorityBg(gap.priority), color: priorityColor(gap.priority),
            letterSpacing: "0.04em",
          }}>
            {gap.priority.toUpperCase()}
          </span>
        </div>
        <div style={{ fontSize: "11px", color: "var(--muted)", lineHeight: "1.5" }}>
          {gap.reason}
        </div>
      </div>
    </div>
  );
}

function ConnectionCard({ conn }: { conn: IntelligenceConnection }) {
  const detected = conn.detected;
  return (
    <div style={{
      padding: "10px 14px", borderRadius: "8px",
      background: "var(--surface)",
      border: detected
        ? "1px solid var(--status-active-text)22"
        : "1px solid var(--status-warn-text)22",
      display: "flex", alignItems: "flex-start", gap: "10px",
    }}>
      {detected
        ? <CheckCircle2 size={13} style={{ color: "var(--status-active-text)", marginTop: "2px", flexShrink: 0 }} />
        : <Link2      size={13} style={{ color: "var(--status-warn-text)",   marginTop: "2px", flexShrink: 0 }} />
      }
      <div style={{ flex: 1 }}>
        <div style={{ display: "flex", alignItems: "center", gap: "6px", marginBottom: "2px", flexWrap: "wrap" }}>
          <span style={{ fontSize: "12px", fontWeight: "600", color: "var(--foreground)" }}>{conn.from}</span>
          <ArrowRight size={10} style={{ color: "var(--muted)", flexShrink: 0 }} />
          <span style={{ fontSize: "12px", fontWeight: "600", color: "var(--foreground)" }}>{conn.to}</span>
          <span style={{
            fontSize: "9px", fontWeight: "700", padding: "1px 6px", borderRadius: "99px",
            background: detected ? "var(--status-active-bg)" : "var(--status-warn-bg)",
            color:      detected ? "var(--status-active-text)" : "var(--status-warn-text)",
            letterSpacing: "0.04em", flexShrink: 0,
          }}>
            {detected ? "CONECTADO" : "FALTANTE"}
          </span>
        </div>
        <div style={{ fontSize: "11px", color: "var(--muted)", lineHeight: "1.5", marginBottom: "4px" }}>
          {conn.label}
        </div>
        <div style={{ fontSize: "10px", color: "var(--muted)", opacity: 0.7 }}>
          Impacto: {conn.impact}
        </div>
      </div>
    </div>
  );
}

function SuggestionCard({ s }: { s: IntelligenceSuggestion }) {
  return (
    <div style={{
      padding: "12px 14px", borderRadius: "8px",
      background: "var(--surface)",
      border: "1px solid rgba(99,102,241,0.2)",
      display: "flex", alignItems: "flex-start", gap: "10px",
    }}>
      <div style={{
        width: "28px", height: "28px", borderRadius: "7px", flexShrink: 0,
        background: "rgba(99,102,241,0.12)",
        display: "flex", alignItems: "center", justifyContent: "center",
      }}>
        <Zap size={12} style={{ color: "#a5b4fc" }} />
      </div>
      <div style={{ flex: 1 }}>
        <div style={{ display: "flex", alignItems: "center", gap: "8px", marginBottom: "3px", flexWrap: "wrap" }}>
          <span style={{ fontSize: "12px", fontWeight: "700", color: "var(--foreground)" }}>{s.title}</span>
          <span style={{
            fontSize: "9px", fontWeight: "700", padding: "1px 6px", borderRadius: "99px",
            background: "var(--surface-elevated)", color: categoryColor(s.category),
            letterSpacing: "0.04em",
          }}>
            {categoryLabel(s.category).toUpperCase()}
          </span>
        </div>
        <div style={{ fontSize: "11px", color: "var(--muted)", lineHeight: "1.5", marginBottom: "4px" }}>
          {s.context}
        </div>
        <div style={{ fontSize: "10px", color: categoryColor(s.category), opacity: 0.8 }}>
          Impacto esperado: {s.impact}
        </div>
      </div>
    </div>
  );
}
