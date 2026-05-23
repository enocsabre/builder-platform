"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { OperationalReport, OperationalBottleneck, WorkflowStatus } from "@/lib/types";

// ── Helpers ───────────────────────────────────────────────────────────────────

function tierColor(tier: string) {
  if (tier === "optimized")  return "text-emerald-400";
  if (tier === "functional") return "text-blue-400";
  if (tier === "limited")    return "text-amber-400";
  return "text-red-400";
}

function tierBg(tier: string) {
  if (tier === "optimized")  return "bg-emerald-900/30 border-emerald-700/40";
  if (tier === "functional") return "bg-blue-900/30 border-blue-700/40";
  if (tier === "limited")    return "bg-amber-900/30 border-amber-700/40";
  return "bg-red-900/30 border-red-700/40";
}

function scoreRing(score: number) {
  if (score >= 75) return "stroke-emerald-400";
  if (score >= 50) return "stroke-blue-400";
  if (score >= 20) return "stroke-amber-400";
  return "stroke-red-400";
}

function sevColor(sev: string) {
  if (sev === "critical") return "text-red-400 bg-red-900/30 border-red-700/40";
  if (sev === "high")     return "text-orange-400 bg-orange-900/30 border-orange-700/40";
  if (sev === "medium")   return "text-amber-400 bg-amber-900/30 border-amber-700/40";
  return "text-slate-400 bg-slate-800/40 border-slate-700/40";
}

function sevLabel(sev: string) {
  if (sev === "critical") return "Crítico";
  if (sev === "high")     return "Alto";
  if (sev === "medium")   return "Medio";
  return "Bajo";
}

function urgencyColor(u: string) {
  if (u === "immediate") return "text-red-400";
  if (u === "near-term") return "text-amber-400";
  return "text-slate-400";
}

function urgencyLabel(u: string) {
  if (u === "immediate") return "Inmediato";
  if (u === "near-term") return "Próximo";
  return "Planificado";
}

function coverageBar(pct: number) {
  if (pct >= 80) return "bg-emerald-500";
  if (pct >= 50) return "bg-blue-500";
  if (pct >= 25) return "bg-amber-500";
  return "bg-red-500";
}

// ── Score ring SVG ────────────────────────────────────────────────────────────

function ScoreRing({ score, tier }: { score: number; tier: string }) {
  const r   = 42;
  const circ = 2 * Math.PI * r;
  const fill = (score / 100) * circ;

  return (
    <div className="flex flex-col items-center gap-1">
      <svg width="110" height="110" viewBox="0 0 110 110" className="-rotate-90">
        <circle cx="55" cy="55" r={r} fill="none" stroke="#1e293b" strokeWidth="10" />
        <circle
          cx="55" cy="55" r={r}
          fill="none"
          strokeWidth="10"
          strokeDasharray={`${fill} ${circ}`}
          strokeLinecap="round"
          className={`transition-all duration-700 ${scoreRing(score)}`}
        />
      </svg>
      <div className="flex flex-col items-center -mt-[78px] mb-12 pointer-events-none">
        <span className={`text-3xl font-bold tabular-nums ${tierColor(tier)}`}>{score}</span>
        <span className="text-xs text-slate-500 mt-0.5">/ 100</span>
      </div>
    </div>
  );
}

// ── Bottleneck card ────────────────────────────────────────────────────────────

function BottleneckCard({ b }: { b: OperationalBottleneck }) {
  return (
    <div className={`rounded-lg border p-3 ${sevColor(b.severity)}`}>
      <div className="flex items-start justify-between gap-2 mb-2">
        <div>
          <p className="font-medium text-sm leading-snug">{b.title}</p>
          <p className="text-xs opacity-70 mt-0.5">{b.impactArea}</p>
        </div>
        <div className="flex flex-col items-end gap-1 shrink-0">
          <span className={`text-[10px] font-semibold px-1.5 py-0.5 rounded border uppercase tracking-wide ${sevColor(b.severity)}`}>
            {sevLabel(b.severity)}
          </span>
          <span className="text-[10px] text-slate-500 font-mono">{b.impactScore} pts</span>
        </div>
      </div>
      <p className="text-xs text-slate-300 mb-2">{b.description}</p>
      <div className="bg-slate-900/60 rounded p-2">
        <p className="text-[10px] text-slate-500 uppercase tracking-wide mb-0.5">Solución</p>
        <p className="text-xs text-slate-200">{b.resolution}</p>
      </div>
      <div className="mt-2 flex items-start gap-1">
        <span className="text-[10px] text-slate-500 shrink-0 mt-0.5">Riesgo:</span>
        <span className="text-[10px] text-slate-400">{b.risk}</span>
      </div>
    </div>
  );
}

// ── Workflow card ──────────────────────────────────────────────────────────────

function WorkflowCard({ w }: { w: WorkflowStatus }) {
  return (
    <div className={`rounded-lg border p-3 ${w.isCritical ? "border-orange-700/40 bg-orange-900/10" : "border-slate-700/40 bg-slate-800/30"}`}>
      <div className="flex items-center justify-between mb-2">
        <div className="flex items-center gap-2">
          {w.isCritical && (
            <span className="text-[10px] bg-orange-900/50 text-orange-400 border border-orange-700/40 px-1.5 py-0.5 rounded uppercase tracking-wide font-semibold">
              Crítico
            </span>
          )}
          <span className="text-sm font-medium text-slate-200">{w.name}</span>
        </div>
        <span className={`text-sm font-bold tabular-nums ${
          w.coverage >= 75 ? "text-emerald-400" :
          w.coverage >= 50 ? "text-blue-400"    :
          w.coverage >= 25 ? "text-amber-400"   : "text-red-400"
        }`}>{w.coverage}%</span>
      </div>

      <div className="w-full bg-slate-700/40 rounded-full h-1.5 mb-2">
        <div
          className={`h-1.5 rounded-full transition-all duration-500 ${coverageBar(w.coverage)}`}
          style={{ width: `${w.coverage}%` }}
        />
      </div>

      {w.presentSteps.length > 0 && (
        <div className="mb-1">
          <p className="text-[10px] text-emerald-500 uppercase tracking-wide mb-0.5">Presentes</p>
          <div className="flex flex-wrap gap-1">
            {w.presentSteps.map(s => (
              <span key={s} className="text-[10px] bg-emerald-900/30 text-emerald-400 border border-emerald-700/30 px-1.5 py-0.5 rounded">
                {s}
              </span>
            ))}
          </div>
        </div>
      )}

      {w.missingSteps.length > 0 && (
        <div className="mb-1">
          <p className="text-[10px] text-red-500 uppercase tracking-wide mb-0.5">Faltantes</p>
          <div className="flex flex-wrap gap-1">
            {w.missingSteps.map(s => (
              <span key={s} className="text-[10px] bg-red-900/30 text-red-400 border border-red-700/30 px-1.5 py-0.5 rounded">
                {s}
              </span>
            ))}
          </div>
        </div>
      )}

      <p className="text-[10px] text-slate-500 mt-1">{w.businessImpact}</p>
    </div>
  );
}

// ── Main panel ────────────────────────────────────────────────────────────────

export function OperationalImpactPanel({ productId }: { productId: string }) {
  const [report, setReport] = useState<OperationalReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError]     = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    api.operationalImpact.get(productId)
      .then(setReport)
      .catch(e => setError(e.message))
      .finally(() => setLoading(false));
  }, [productId]);

  if (loading) return (
    <div className="flex items-center justify-center py-20 text-slate-500 text-sm">
      Analizando impacto operacional...
    </div>
  );

  if (error) return (
    <div className="rounded-lg border border-red-700/40 bg-red-900/20 p-4 text-red-400 text-sm">
      {error}
    </div>
  );

  if (!report) return null;

  const criticalBottlenecks = report.bottlenecks.filter(b => b.severity === "critical");
  const otherBottlenecks    = report.bottlenecks.filter(b => b.severity !== "critical");

  return (
    <div className="space-y-5">

      {/* Header — score + tier + narrative */}
      <div className={`rounded-xl border p-4 ${tierBg(report.operationalTier)}`}>
        <div className="flex items-start gap-5">
          <ScoreRing score={report.operationalScore} tier={report.operationalTier} />
          <div className="flex-1 min-w-0 pt-1">
            <div className="flex items-center gap-2 mb-1">
              <span className={`text-xs font-semibold px-2 py-0.5 rounded border uppercase tracking-wide ${tierBg(report.operationalTier)} ${tierColor(report.operationalTier)}`}>
                {report.operationalTierLabel}
              </span>
              <span className="text-xs text-slate-500">{report.industryLabel}</span>
            </div>
            <p className="text-slate-300 text-sm leading-relaxed">{report.operationalNarrative}</p>

            {report.topBottleneckTitle && (
              <div className="mt-3 rounded-lg bg-slate-900/60 border border-slate-700/40 p-2.5">
                <p className="text-[10px] text-slate-500 uppercase tracking-wide mb-0.5">Cuello principal</p>
                <p className="text-xs text-orange-300 font-medium">{report.topBottleneckTitle}</p>
                <p className="text-[10px] text-slate-400 mt-0.5">{report.topBottleneckResolution}</p>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Critical bottlenecks */}
      {criticalBottlenecks.length > 0 && (
        <section>
          <h3 className="text-xs font-semibold text-red-400 uppercase tracking-widest mb-2">
            Cuellos Críticos ({criticalBottlenecks.length})
          </h3>
          <div className="space-y-2">
            {criticalBottlenecks.map((b, i) => <BottleneckCard key={i} b={b} />)}
          </div>
        </section>
      )}

      {/* Other bottlenecks */}
      {otherBottlenecks.length > 0 && (
        <section>
          <h3 className="text-xs font-semibold text-slate-400 uppercase tracking-widest mb-2">
            Otros Cuellos Operacionales ({otherBottlenecks.length})
          </h3>
          <div className="space-y-2">
            {otherBottlenecks.map((b, i) => <BottleneckCard key={i} b={b} />)}
          </div>
        </section>
      )}

      {/* Workflow coverage */}
      {report.workflows.length > 0 && (
        <section>
          <h3 className="text-xs font-semibold text-slate-400 uppercase tracking-widest mb-2">
            Cobertura de Flujos Operacionales
          </h3>
          <div className="space-y-2">
            {report.workflows.map((w, i) => <WorkflowCard key={i} w={w} />)}
          </div>
        </section>
      )}

      {/* Top impact suggestions */}
      {report.topImpactSuggestions.length > 0 && (
        <section>
          <h3 className="text-xs font-semibold text-slate-400 uppercase tracking-widest mb-2">
            Mejoras de Mayor Impacto
          </h3>
          <div className="space-y-2">
            {report.topImpactSuggestions.map((s, i) => (
              <div key={i} className="rounded-lg border border-slate-700/40 bg-slate-800/30 p-3">
                <div className="flex items-start justify-between gap-2">
                  <p className="text-sm font-medium text-slate-200">{s.title}</p>
                  <span className={`text-[10px] font-semibold shrink-0 ${urgencyColor(s.urgency)}`}>
                    {urgencyLabel(s.urgency)}
                  </span>
                </div>
                <p className="text-xs text-slate-400 mt-1">{s.operationalValue}</p>
              </div>
            ))}
          </div>
        </section>
      )}

      {report.bottlenecks.length === 0 && (
        <div className="rounded-lg border border-emerald-700/30 bg-emerald-900/10 p-4 text-center">
          <p className="text-emerald-400 font-medium text-sm">Sin cuellos operacionales detectados</p>
          <p className="text-slate-500 text-xs mt-1">El sistema cubre los flujos operacionales principales.</p>
        </div>
      )}

      <div className="text-[10px] text-slate-600 text-right">
        Sprint 42 · Operational Impact Intelligence · {new Date(report.analyzedAt).toLocaleString("es-CR")}
      </div>
    </div>
  );
}
