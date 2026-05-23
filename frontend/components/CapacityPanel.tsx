"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { CapacityReport, SaturationPoint, ManualOperation, AutomationOpportunity } from "@/lib/types";

// ── Helpers ───────────────────────────────────────────────────────────────────

function tierColor(tier: string) {
  if (tier === "mature")   return "text-violet-400";
  if (tier === "scalable") return "text-indigo-400";
  if (tier === "partial")  return "text-amber-400";
  return "text-red-400";
}

function tierBg(tier: string) {
  if (tier === "mature")   return "bg-violet-900/30 border-violet-700/40";
  if (tier === "scalable") return "bg-indigo-900/30 border-indigo-700/40";
  if (tier === "partial")  return "bg-amber-900/30 border-amber-700/40";
  return "bg-red-900/30 border-red-700/40";
}

function scoreStroke(tier: string) {
  if (tier === "mature")   return "stroke-violet-400";
  if (tier === "scalable") return "stroke-indigo-400";
  if (tier === "partial")  return "stroke-amber-400";
  return "stroke-red-400";
}

function sevBadge(sev: string) {
  if (sev === "critical") return "text-red-400 bg-red-900/30 border-red-700/40";
  if (sev === "high")     return "text-orange-400 bg-orange-900/30 border-orange-700/40";
  return "text-amber-400 bg-amber-900/30 border-amber-700/40";
}

function sevLabel(sev: string) {
  return sev === "critical" ? "Crítico" : sev === "high" ? "Alto" : "Medio";
}

function impactColor(imp: string) {
  if (imp === "transformational") return "text-violet-400";
  if (imp === "high")             return "text-indigo-400";
  return "text-slate-400";
}

function impactLabel(imp: string) {
  if (imp === "transformational") return "Transformacional";
  if (imp === "high")             return "Alto";
  return "Medio";
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

// ── Score ring ────────────────────────────────────────────────────────────────

function ScoreRing({ score, tier }: { score: number; tier: string }) {
  const r    = 42;
  const circ = 2 * Math.PI * r;
  const fill = (score / 100) * circ;

  return (
    <div className="flex flex-col items-center">
      <svg width="110" height="110" viewBox="0 0 110 110" className="-rotate-90">
        <circle cx="55" cy="55" r={r} fill="none" stroke="#1e293b" strokeWidth="10" />
        <circle
          cx="55" cy="55" r={r}
          fill="none" strokeWidth="10"
          strokeDasharray={`${fill} ${circ}`}
          strokeLinecap="round"
          className={`transition-all duration-700 ${scoreStroke(tier)}`}
        />
      </svg>
      <div className="flex flex-col items-center -mt-[78px] mb-12 pointer-events-none">
        <span className={`text-3xl font-bold tabular-nums ${tierColor(tier)}`}>{score}</span>
        <span className="text-xs text-slate-500 mt-0.5">/ 100</span>
      </div>
    </div>
  );
}

// ── Saturation point card ──────────────────────────────────────────────────────

function SaturationCard({ s }: { s: SaturationPoint }) {
  return (
    <div className={`rounded-lg border p-3 ${sevBadge(s.severity)}`}>
      <div className="flex items-start justify-between gap-2 mb-2">
        <div>
          <p className="font-medium text-sm leading-snug">{s.title}</p>
        </div>
        <div className="flex flex-col items-end gap-1 shrink-0">
          <span className={`text-[10px] font-semibold px-1.5 py-0.5 rounded border uppercase tracking-wide ${sevBadge(s.severity)}`}>
            {sevLabel(s.severity)}
          </span>
          <span className="text-[10px] text-slate-500 font-mono">{s.scalingRisk} pts</span>
        </div>
      </div>
      <p className="text-xs text-slate-300 mb-2">{s.description}</p>

      <div className="bg-red-950/40 border border-red-900/30 rounded p-2 mb-2">
        <p className="text-[10px] text-red-400 uppercase tracking-wide mb-0.5">¿Qué colapsa al crecer?</p>
        <p className="text-xs text-slate-300">{s.collapseScenario}</p>
      </div>

      <div className="bg-slate-900/60 rounded p-2">
        <p className="text-[10px] text-slate-500 uppercase tracking-wide mb-0.5">Solución de Escala</p>
        <p className="text-xs text-slate-200">{s.automationFix}</p>
      </div>
    </div>
  );
}

// ── Manual operation card ─────────────────────────────────────────────────────

function ManualOpCard({ m }: { m: ManualOperation }) {
  return (
    <div className="rounded-lg border border-slate-700/40 bg-slate-800/30 p-3">
      <div className="flex items-start justify-between gap-2 mb-1">
        <p className="text-sm font-medium text-slate-200">{m.title}</p>
        <span className={`text-[10px] font-semibold shrink-0 px-1.5 py-0.5 rounded border ${sevBadge(m.humanCost)}`}>
          {sevLabel(m.humanCost)}
        </span>
      </div>
      <p className="text-xs text-slate-400 mb-2">{m.description}</p>
      <div className="flex items-start gap-2">
        <div className="flex-1">
          <p className="text-[10px] text-slate-500 uppercase tracking-wide mb-0.5">Impacto en Crecimiento</p>
          <p className="text-xs text-amber-300/80">{m.impactOnGrowth}</p>
        </div>
      </div>
      <div className="mt-2 bg-indigo-950/30 border border-indigo-800/30 rounded p-2">
        <p className="text-[10px] text-indigo-400 uppercase tracking-wide mb-0.5">Camino de Automatización</p>
        <p className="text-xs text-slate-300">{m.automationPath}</p>
      </div>
    </div>
  );
}

// ── Automation opportunity card ───────────────────────────────────────────────

function AutomationCard({ a, rank }: { a: AutomationOpportunity; rank: number }) {
  return (
    <div className="rounded-lg border border-violet-700/30 bg-violet-900/10 p-3">
      <div className="flex items-start justify-between gap-2 mb-1">
        <div className="flex items-center gap-2">
          <span className="text-[10px] font-bold text-violet-500 bg-violet-900/50 border border-violet-700/40 w-5 h-5 rounded-full flex items-center justify-center shrink-0">
            {rank}
          </span>
          <p className="text-sm font-medium text-slate-200">{a.title}</p>
        </div>
        <span className={`text-[10px] font-semibold shrink-0 ${urgencyColor(a.urgency)}`}>
          {urgencyLabel(a.urgency)}
        </span>
      </div>
      <p className="text-xs text-slate-400 mb-2 ml-7">{a.operationalValue}</p>
      <div className="ml-7 flex items-center gap-2">
        <span className={`text-[10px] font-semibold ${impactColor(a.impact)}`}>
          ↑ {impactLabel(a.impact)}
        </span>
        <span className="text-[10px] text-slate-500">·</span>
        <span className="text-[10px] text-emerald-400">{a.unlocks}</span>
      </div>
    </div>
  );
}

// ── Main panel ────────────────────────────────────────────────────────────────

export function CapacityPanel({ productId }: { productId: string }) {
  const [report, setReport] = useState<CapacityReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError]     = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    api.capacity.get(productId)
      .then(setReport)
      .catch(e => setError(e.message))
      .finally(() => setLoading(false));
  }, [productId]);

  if (loading) return (
    <div className="flex items-center justify-center py-20 text-slate-500 text-sm">
      Analizando capacidad operacional...
    </div>
  );

  if (error) return (
    <div className="rounded-lg border border-red-700/40 bg-red-900/20 p-4 text-red-400 text-sm">
      {error}
    </div>
  );

  if (!report) return null;

  const critSat = report.saturationPoints.filter(s => s.severity === "critical");
  const otherSat = report.saturationPoints.filter(s => s.severity !== "critical");

  return (
    <div className="space-y-5">

      {/* Header — score + tier + narrative */}
      <div className={`rounded-xl border p-4 ${tierBg(report.capacityTier)}`}>
        <div className="flex items-start gap-5">
          <ScoreRing score={report.capacityScore} tier={report.capacityTier} />
          <div className="flex-1 min-w-0 pt-1">
            <div className="flex items-center gap-2 mb-1">
              <span className={`text-xs font-semibold px-2 py-0.5 rounded border uppercase tracking-wide ${tierBg(report.capacityTier)} ${tierColor(report.capacityTier)}`}>
                {report.capacityTierLabel}
              </span>
              <span className="text-xs text-slate-500">{report.industryLabel}</span>
            </div>
            <p className="text-slate-300 text-sm leading-relaxed">{report.scalingNarrative}</p>

            {report.topRiskTitle && (
              <div className="mt-3 rounded-lg bg-slate-900/60 border border-slate-700/40 p-2.5">
                <p className="text-[10px] text-slate-500 uppercase tracking-wide mb-0.5">Mayor Riesgo de Escala</p>
                <p className="text-xs text-orange-300 font-medium">{report.topRiskTitle}</p>
                <p className="text-[10px] text-slate-400 mt-0.5">{report.topRiskDescription}</p>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Critical saturation points */}
      {critSat.length > 0 && (
        <section>
          <h3 className="text-xs font-semibold text-red-400 uppercase tracking-widest mb-2">
            Puntos de Saturación Críticos ({critSat.length})
          </h3>
          <div className="space-y-2">
            {critSat.map((s, i) => <SaturationCard key={i} s={s} />)}
          </div>
        </section>
      )}

      {/* Other saturation points */}
      {otherSat.length > 0 && (
        <section>
          <h3 className="text-xs font-semibold text-slate-400 uppercase tracking-widest mb-2">
            Otros Puntos de Saturación ({otherSat.length})
          </h3>
          <div className="space-y-2">
            {otherSat.map((s, i) => <SaturationCard key={i} s={s} />)}
          </div>
        </section>
      )}

      {/* Manual operations */}
      {report.manualOperations.length > 0 && (
        <section>
          <h3 className="text-xs font-semibold text-amber-400 uppercase tracking-widest mb-2">
            Operaciones Manuales con Riesgo de Escala ({report.manualOperations.length})
          </h3>
          <div className="space-y-2">
            {report.manualOperations.map((m, i) => <ManualOpCard key={i} m={m} />)}
          </div>
        </section>
      )}

      {/* Automation opportunities */}
      {report.topAutomationOpportunities.length > 0 && (
        <section>
          <h3 className="text-xs font-semibold text-violet-400 uppercase tracking-widest mb-2">
            Automatizaciones de Mayor Impacto
          </h3>
          <div className="space-y-2">
            {report.topAutomationOpportunities.map((a, i) => (
              <AutomationCard key={i} a={a} rank={i + 1} />
            ))}
          </div>
        </section>
      )}

      {report.saturationPoints.length === 0 && report.manualOperations.length === 0 && (
        <div className="rounded-lg border border-violet-700/30 bg-violet-900/10 p-4 text-center">
          <p className="text-violet-400 font-medium text-sm">Operación con buena capacidad de escala</p>
          <p className="text-slate-500 text-xs mt-1">No se detectaron puntos de saturación críticos en los flujos actuales.</p>
        </div>
      )}

      <div className="text-[10px] text-slate-600 text-right">
        Sprint 43 · Operational Capacity Intelligence · {new Date(report.analyzedAt).toLocaleString("es-CR")}
      </div>
    </div>
  );
}
