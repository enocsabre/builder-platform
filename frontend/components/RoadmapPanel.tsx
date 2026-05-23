"use client";

import { useState, useEffect } from "react";
import {
  Map, RefreshCw, Target, Zap, TrendingUp, ArrowRight,
  Link2, CheckCircle2, Clock, Layers,
} from "lucide-react";
import { api } from "@/lib/api";
import type { StrategicRoadmap, RoadmapMilestone, RoadmapDependency } from "@/lib/types";

interface Props {
  productId: string;
}

// ── Color helpers ────────────────────────────────────────────────────────────

function phaseColor(phase: string): string {
  if (phase === "now")  return "var(--status-danger-text)";
  if (phase === "next") return "var(--status-warn-text)";
  return "var(--muted)";
}

function phaseBg(phase: string): string {
  if (phase === "now")  return "var(--status-danger-bg)";
  if (phase === "next") return "var(--status-warn-bg)";
  return "var(--surface-elevated)";
}

function phaseLabel(phase: string): string {
  if (phase === "now")  return "AHORA";
  if (phase === "next") return "SIGUIENTE";
  return "DESPUÉS";
}

function categoryIcon(cat: string) {
  if (cat === "core")        return Layers;
  if (cat === "integration") return Link2;
  if (cat === "analytics")   return TrendingUp;
  return Zap;
}

function categoryColor(cat: string): string {
  if (cat === "core")        return "#a5b4fc";
  if (cat === "integration") return "var(--status-info-text)";
  if (cat === "analytics")   return "var(--status-active-text)";
  return "#fbbf24";
}

function categoryLabel(cat: string): string {
  if (cat === "core")        return "Core";
  if (cat === "integration") return "Integración";
  if (cat === "analytics")   return "Analytics";
  return "Crecimiento";
}

function priorityColor(p: string): string {
  if (p === "critical") return "var(--status-danger-text)";
  if (p === "high")     return "var(--status-warn-text)";
  return "var(--status-info-text)";
}

function completionColor(score: number): string {
  if (score >= 75) return "#22c55e";
  if (score >= 50) return "#38bdf8";
  if (score >= 25) return "#f59e0b";
  return "#ef4444";
}

// ── Main panel ───────────────────────────────────────────────────────────────

export function RoadmapPanel({ productId }: Props) {
  const [roadmap, setRoadmap] = useState<StrategicRoadmap | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError]     = useState<string | null>(null);

  function load() {
    setLoading(true);
    setError(null);
    api.roadmap.get(productId)
      .then(setRoadmap)
      .catch(() => setError("No se pudo cargar el roadmap estratégico."))
      .finally(() => setLoading(false));
  }

  useEffect(() => { load(); }, [productId]);

  if (loading) return (
    <div style={{ padding: "32px", display: "flex", alignItems: "center", gap: "10px", color: "var(--muted)" }}>
      <Map size={16} style={{ animation: "spin 1.5s linear infinite" }} />
      <span style={{ fontSize: "13px" }}>Generando roadmap estratégico...</span>
    </div>
  );

  if (error || !roadmap) return (
    <div style={{ padding: "24px", fontSize: "13px", color: "var(--status-danger-text)" }}>
      {error ?? "Sin datos de roadmap."}
    </div>
  );

  const nowItems  = roadmap.milestones.filter(m => m.phase === "now");
  const nextItems = roadmap.milestones.filter(m => m.phase === "next");
  const laterItems = roadmap.milestones.filter(m => m.phase === "later");

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "20px", padding: "4px 0" }}>

      {/* ── Completion Header ─────────────────────────────────────────────── */}
      <div style={{
        borderRadius: "12px", padding: "16px 20px",
        background: "var(--surface)", border: "1px solid var(--border)",
      }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "12px", marginBottom: "12px" }}>
          <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
            <div style={{
              width: "32px", height: "32px", borderRadius: "8px",
              background: "rgba(99,102,241,0.12)",
              display: "flex", alignItems: "center", justifyContent: "center",
            }}>
              <Map size={15} style={{ color: "#a5b4fc" }} />
            </div>
            <div>
              <div style={{ fontSize: "13px", fontWeight: "700", color: "var(--foreground)" }}>
                Roadmap Estratégico — {roadmap.industryLabel}
              </div>
              <div style={{ fontSize: "11px", color: "var(--muted)", marginTop: "1px" }}>
                {roadmap.completedCheckpoints} de {roadmap.totalCheckpoints} módulos del perfil ideal
              </div>
            </div>
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
            <div style={{
              fontSize: "18px", fontWeight: "800",
              color: completionColor(roadmap.completionScore),
            }}>
              {roadmap.completionScore}%
            </div>
            <button
              onClick={load}
              style={{ padding: "4px", borderRadius: "6px", background: "none", border: "none", cursor: "pointer", color: "var(--muted)", display: "flex" }}
              title="Actualizar roadmap"
            >
              <RefreshCw size={12} />
            </button>
          </div>
        </div>

        {/* Progress bar */}
        <div style={{ height: "6px", borderRadius: "99px", background: "var(--border)", overflow: "hidden", marginBottom: "8px" }}>
          <div style={{
            height: "100%", borderRadius: "99px",
            width: `${roadmap.completionScore}%`,
            background: completionColor(roadmap.completionScore),
            transition: "width 0.6s ease",
          }} />
        </div>

        {/* Narrative */}
        <p style={{ fontSize: "12px", color: "var(--muted)", lineHeight: "1.6", margin: 0 }}>
          {roadmap.growthNarrative}
        </p>
      </div>

      {/* ── Next Focus ──────────────────────────────────────────────────────── */}
      {roadmap.nextFocusTitle && (
        <div style={{
          padding: "14px 16px", borderRadius: "10px",
          background: "linear-gradient(135deg, rgba(239,68,68,0.08) 0%, rgba(0,0,0,0) 70%)",
          border: "1px solid rgba(239,68,68,0.2)",
          display: "flex", alignItems: "flex-start", gap: "12px",
        }}>
          <Target size={14} style={{ color: "var(--status-danger-text)", flexShrink: 0, marginTop: "2px" }} />
          <div>
            <div style={{ fontSize: "11px", fontWeight: "700", color: "var(--status-danger-text)", letterSpacing: "0.04em", marginBottom: "3px" }}>
              PRÓXIMO FOCO
            </div>
            <div style={{ fontSize: "13px", fontWeight: "700", color: "var(--foreground)", marginBottom: "3px" }}>
              {roadmap.nextFocusTitle}
            </div>
            <div style={{ fontSize: "11px", color: "var(--muted)", lineHeight: "1.5" }}>
              {roadmap.nextFocusWhy}
            </div>
          </div>
        </div>
      )}

      {/* ── Milestones by phase ──────────────────────────────────────────────── */}
      {nowItems.length > 0 && (
        <PhaseSection phase="now" items={nowItems} />
      )}
      {nextItems.length > 0 && (
        <PhaseSection phase="next" items={nextItems} />
      )}
      {laterItems.length > 0 && (
        <PhaseSection phase="later" items={laterItems} />
      )}

      {/* ── Dependencies ──────────────────────────────────────────────────────── */}
      {roadmap.dependencies.length > 0 && (
        <section>
          <SectionHeader Icon={Link2} label="Dependencias operacionales" count={roadmap.dependencies.length} color="var(--status-info-text)" />
          <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
            {roadmap.dependencies.map((dep, i) => (
              <DependencyCard key={i} dep={dep} />
            ))}
          </div>
        </section>
      )}

      {/* ── Empty state ──────────────────────────────────────────────────────── */}
      {roadmap.milestones.length === 0 && (
        <div style={{
          padding: "28px", borderRadius: "10px", textAlign: "center",
          border: "1px solid var(--border)", background: "var(--surface)",
        }}>
          <CheckCircle2 size={20} style={{ color: "var(--status-active-text)", margin: "0 auto 8px" }} />
          <div style={{ fontSize: "13px", fontWeight: "600", color: "var(--foreground)", marginBottom: "4px" }}>
            Roadmap completo
          </div>
          <div style={{ fontSize: "12px", color: "var(--muted)" }}>
            El producto cubre todos los módulos del perfil ideal para su industria.
          </div>
        </div>
      )}

      {/* ── Footer ──────────────────────────────────────────────────────────── */}
      <div style={{ fontSize: "10px", color: "var(--muted)", opacity: 0.5, paddingTop: "4px" }}>
        Roadmap generado · {new Date(roadmap.generatedAt).toLocaleTimeString("es-CR", { hour: "2-digit", minute: "2-digit" })} · Builder Strategic Engine — Sprint 41
      </div>
    </div>
  );
}

// ── Sub-components ────────────────────────────────────────────────────────────

function PhaseSection({ phase, items }: { phase: string; items: RoadmapMilestone[] }) {
  return (
    <section>
      <div style={{ display: "flex", alignItems: "center", gap: "8px", marginBottom: "10px" }}>
        <span style={{
          fontSize: "10px", fontWeight: "800", padding: "2px 8px", borderRadius: "99px",
          background: phaseBg(phase), color: phaseColor(phase), letterSpacing: "0.06em",
        }}>
          {phaseLabel(phase)}
        </span>
        <div style={{ flex: 1, height: "1px", background: "var(--border)" }} />
        <span style={{ fontSize: "10px", color: "var(--muted)" }}>{items.length} módulo{items.length !== 1 ? "s" : ""}</span>
      </div>
      <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
        {items.map((m, i) => <MilestoneCard key={i} milestone={m} />)}
      </div>
    </section>
  );
}

function MilestoneCard({ milestone: m }: { milestone: RoadmapMilestone }) {
  const CatIcon = categoryIcon(m.category);
  return (
    <div style={{
      padding: "12px 14px", borderRadius: "10px",
      background: "var(--surface)",
      border: `1px solid ${phaseColor(m.phase)}22`,
    }}>
      <div style={{ display: "flex", alignItems: "flex-start", gap: "10px" }}>
        <div style={{
          width: "28px", height: "28px", borderRadius: "7px", flexShrink: 0,
          background: `${categoryColor(m.category)}18`,
          display: "flex", alignItems: "center", justifyContent: "center",
        }}>
          <CatIcon size={12} style={{ color: categoryColor(m.category) }} />
        </div>
        <div style={{ flex: 1 }}>
          <div style={{ display: "flex", alignItems: "center", gap: "6px", marginBottom: "4px", flexWrap: "wrap" }}>
            <span style={{ fontSize: "12px", fontWeight: "700", color: "var(--foreground)" }}>
              {m.title}
            </span>
            <span style={{
              fontSize: "9px", fontWeight: "700", padding: "1px 6px", borderRadius: "99px",
              background: `${categoryColor(m.category)}18`, color: categoryColor(m.category),
              letterSpacing: "0.04em",
            }}>
              {categoryLabel(m.category).toUpperCase()}
            </span>
            {m.priority === "critical" && (
              <span style={{
                fontSize: "9px", fontWeight: "700", padding: "1px 6px", borderRadius: "99px",
                background: "var(--status-danger-bg)", color: "var(--status-danger-text)",
                letterSpacing: "0.04em",
              }}>
                CRÍTICO
              </span>
            )}
          </div>

          {/* Why */}
          <div style={{ fontSize: "11px", color: "var(--muted)", lineHeight: "1.5", marginBottom: "6px" }}>
            {m.why}
          </div>

          {/* Unlocks */}
          <div style={{
            display: "flex", alignItems: "flex-start", gap: "5px",
            padding: "5px 8px", borderRadius: "6px",
            background: "rgba(99,102,241,0.07)", border: "1px solid rgba(99,102,241,0.15)",
          }}>
            <ArrowRight size={10} style={{ color: "#a5b4fc", flexShrink: 0, marginTop: "2px" }} />
            <span style={{ fontSize: "10px", color: "#a5b4fc", lineHeight: "1.5" }}>
              <strong>Desbloquea:</strong> {m.unlocks}
            </span>
          </div>

          {/* Required modules */}
          {m.requiredModules.length > 0 && (
            <div style={{ display: "flex", alignItems: "center", gap: "5px", marginTop: "6px", flexWrap: "wrap" }}>
              <Clock size={9} style={{ color: "var(--muted)", flexShrink: 0 }} />
              <span style={{ fontSize: "9px", color: "var(--muted)" }}>Requiere:</span>
              {m.requiredModules.map((req, i) => (
                <span key={i} style={{
                  fontSize: "9px", padding: "1px 6px", borderRadius: "99px",
                  background: "var(--surface-elevated)", color: "var(--muted)",
                  border: "1px solid var(--border)",
                }}>
                  {req}
                </span>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function DependencyCard({ dep }: { dep: RoadmapDependency }) {
  return (
    <div style={{
      padding: "10px 14px", borderRadius: "8px",
      background: "var(--surface)", border: "1px solid var(--status-info-text)18",
      display: "flex", alignItems: "flex-start", gap: "10px",
    }}>
      <Link2 size={12} style={{ color: "var(--status-info-text)", marginTop: "2px", flexShrink: 0 }} />
      <div style={{ flex: 1 }}>
        <div style={{ display: "flex", alignItems: "center", gap: "6px", marginBottom: "3px", flexWrap: "wrap" }}>
          <span style={{ fontSize: "11px", fontWeight: "600", color: "var(--foreground)" }}>{dep.from}</span>
          <ArrowRight size={9} style={{ color: "var(--status-info-text)", flexShrink: 0 }} />
          <span style={{ fontSize: "11px", fontWeight: "600", color: "var(--foreground)" }}>{dep.to}</span>
        </div>
        <div style={{ fontSize: "10px", color: "var(--muted)", lineHeight: "1.5" }}>
          {dep.reason}
        </div>
      </div>
    </div>
  );
}

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
