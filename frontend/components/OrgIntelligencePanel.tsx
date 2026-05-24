"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { OrgReport, OwnershipGap, HumanBottleneck, RoleSuggestion, DelegationOpportunity } from "@/lib/types";

// ── Helpers ───────────────────────────────────────────────────────────────────

function tierColor(tier: string) {
  if (tier === "scalable")            return "text-teal-400";
  if (tier === "structured")          return "text-cyan-400";
  if (tier === "partially-delegated") return "text-amber-400";
  return "text-orange-400";
}

function tierBg(tier: string) {
  if (tier === "scalable")            return "bg-teal-900/30 border-teal-700/40";
  if (tier === "structured")          return "bg-cyan-900/30 border-cyan-700/40";
  if (tier === "partially-delegated") return "bg-amber-900/30 border-amber-700/40";
  return "bg-orange-900/30 border-orange-700/40";
}

function scoreStroke(tier: string) {
  if (tier === "scalable")            return "stroke-teal-400";
  if (tier === "structured")          return "stroke-cyan-400";
  if (tier === "partially-delegated") return "stroke-amber-400";
  return "stroke-orange-400";
}

function sevBadge(sev: string) {
  if (sev === "critical") return "text-red-400 bg-red-900/30 border-red-700/40";
  if (sev === "high")     return "text-orange-400 bg-orange-900/30 border-orange-700/40";
  return "text-amber-400 bg-amber-900/30 border-amber-700/40";
}

function sevLabel(sev: string) {
  return sev === "critical" ? "Crítico" : sev === "high" ? "Alto" : "Medio";
}

function priorityColor(p: string) {
  if (p === "immediate") return "text-red-400";
  if (p === "soon")      return "text-amber-400";
  return "text-slate-400";
}

function priorityLabel(p: string) {
  if (p === "immediate") return "Inmediato";
  if (p === "soon")      return "Próximo";
  return "Futuro";
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

// ── Ownership gap card ────────────────────────────────────────────────────────

function OwnershipGapCard({ g }: { g: OwnershipGap }) {
  return (
    <div className={`rounded-lg border p-3 ${sevBadge(g.severity)}`}>
      <div className="flex items-start justify-between gap-2 mb-2">
        <div>
          <p className="font-medium text-sm leading-snug">{g.area}</p>
        </div>
        <span className={`text-[10px] font-semibold shrink-0 px-1.5 py-0.5 rounded border uppercase tracking-wide ${sevBadge(g.severity)}`}>
          {sevLabel(g.severity)}
        </span>
      </div>
      <p className="text-xs text-slate-300 mb-2">{g.description}</p>

      <div className="bg-red-950/30 border border-red-900/30 rounded p-2 mb-2">
        <p className="text-[10px] text-red-400 uppercase tracking-wide mb-0.5">Riesgo sin Ownership</p>
        <p className="text-xs text-slate-300">{g.risk}</p>
      </div>

      <div className="bg-teal-950/30 border border-teal-800/30 rounded p-2">
        <p className="text-[10px] text-teal-400 uppercase tracking-wide mb-0.5">Rol Sugerido</p>
        <p className="text-xs text-slate-200 font-medium">{g.suggestedOwner}</p>
      </div>
    </div>
  );
}

// ── Human bottleneck card ─────────────────────────────────────────────────────

function BottleneckCard({ b }: { b: HumanBottleneck }) {
  return (
    <div className="rounded-lg border border-red-700/40 bg-red-900/10 p-3">
      <div className="flex items-start justify-between gap-2 mb-1">
        <p className="text-sm font-medium text-red-300">{b.title}</p>
        <span className={`text-[10px] font-semibold shrink-0 px-1.5 py-0.5 rounded border ${sevBadge(b.severity)}`}>
          {sevLabel(b.severity)}
        </span>
      </div>
      <p className="text-xs text-slate-400 mb-2">{b.description}</p>

      <div className="flex items-center gap-2 mb-2">
        <span className="text-[10px] text-slate-500 shrink-0">Concentración:</span>
        <span className="text-[10px] font-mono text-orange-300 bg-orange-900/20 px-1.5 py-0.5 rounded">
          {b.concentration}
        </span>
      </div>

      <div className="bg-slate-900/60 rounded p-2">
        <p className="text-[10px] text-slate-500 uppercase tracking-wide mb-0.5">Riesgo de Colapso</p>
        <p className="text-xs text-orange-200/80">{b.collapseRisk}</p>
      </div>
    </div>
  );
}

// ── Role suggestion card ──────────────────────────────────────────────────────

function RoleCard({ r, rank }: { r: RoleSuggestion; rank: number }) {
  return (
    <div className="rounded-lg border border-teal-700/30 bg-teal-900/10 p-3">
      <div className="flex items-start justify-between gap-2 mb-1">
        <div className="flex items-center gap-2">
          <span className="text-[10px] font-bold text-teal-500 bg-teal-900/50 border border-teal-700/40 w-5 h-5 rounded-full flex items-center justify-center shrink-0">
            {rank}
          </span>
          <p className="text-sm font-medium text-teal-200">{r.roleTitle}</p>
        </div>
        <span className={`text-[10px] font-semibold shrink-0 ${priorityColor(r.priority)}`}>
          {priorityLabel(r.priority)}
        </span>
      </div>
      <p className="text-xs text-slate-400 mb-2 ml-7">{r.responsibilities}</p>
      <div className="ml-7 bg-slate-900/60 rounded p-2">
        <p className="text-[10px] text-slate-500 uppercase tracking-wide mb-0.5">Por qué ahora</p>
        <p className="text-xs text-slate-300">{r.businessCase}</p>
      </div>
    </div>
  );
}

// ── Delegation card ───────────────────────────────────────────────────────────

function DelegationCard({ d }: { d: DelegationOpportunity }) {
  return (
    <div className="rounded-lg border border-cyan-700/30 bg-cyan-900/10 p-3">
      <p className="text-sm font-medium text-cyan-200 mb-1">{d.title}</p>
      <div className="mb-2">
        <p className="text-[10px] text-slate-500 uppercase tracking-wide mb-0.5">Estado actual</p>
        <p className="text-xs text-amber-300/80">{d.currentState}</p>
      </div>
      <div className="bg-slate-900/60 rounded p-2 mb-2">
        <p className="text-[10px] text-cyan-400 uppercase tracking-wide mb-0.5">Cómo delegar</p>
        <p className="text-xs text-slate-200">{d.delegationPath}</p>
      </div>
      <div className="flex items-start gap-1">
        <span className="text-[10px] text-teal-500 font-semibold shrink-0 mt-0.5">↑</span>
        <p className="text-[10px] text-teal-400">{d.impact}</p>
      </div>
    </div>
  );
}

// ── Main panel ────────────────────────────────────────────────────────────────

export function OrgIntelligencePanel({ productId }: { productId: string }) {
  const [report, setReport] = useState<OrgReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError]     = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    api.org.get(productId)
      .then(setReport)
      .catch(e => setError(e.message))
      .finally(() => setLoading(false));
  }, [productId]);

  if (loading) return (
    <div className="flex items-center justify-center py-20 text-slate-500 text-sm">
      Analizando estructura organizacional...
    </div>
  );

  if (error) return (
    <div className="rounded-lg border border-red-700/40 bg-red-900/20 p-4 text-red-400 text-sm">
      {error}
    </div>
  );

  if (!report) return null;

  const criticalGaps = report.ownershipGaps.filter(g => g.severity === "critical");
  const otherGaps    = report.ownershipGaps.filter(g => g.severity !== "critical");

  return (
    <div className="space-y-5">

      {/* Header — score + tier + narrative */}
      <div className={`rounded-xl border p-4 ${tierBg(report.orgMaturityTier)}`}>
        <div className="flex items-start gap-5">
          <ScoreRing score={report.orgMaturityScore} tier={report.orgMaturityTier} />
          <div className="flex-1 min-w-0 pt-1">
            <div className="flex items-center gap-2 mb-1">
              <span className={`text-xs font-semibold px-2 py-0.5 rounded border uppercase tracking-wide ${tierBg(report.orgMaturityTier)} ${tierColor(report.orgMaturityTier)}`}>
                {report.orgMaturityLabel}
              </span>
              <span className="text-xs text-slate-500">{report.industryLabel}</span>
            </div>
            <p className="text-slate-300 text-sm leading-relaxed">{report.orgNarrative}</p>

            {report.topConcernTitle && (
              <div className="mt-3 rounded-lg bg-slate-900/60 border border-slate-700/40 p-2.5">
                <p className="text-[10px] text-slate-500 uppercase tracking-wide mb-0.5">Principal Gap de Ownership</p>
                <p className="text-xs text-orange-300 font-medium">{report.topConcernTitle}</p>
                <p className="text-[10px] text-slate-400 mt-0.5">{report.topConcernDescription}</p>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Human bottlenecks — most dangerous, shown first */}
      {report.humanBottlenecks.length > 0 && (
        <section>
          <h3 className="text-xs font-semibold text-red-400 uppercase tracking-widest mb-2">
            Cuellos Humanos Críticos ({report.humanBottlenecks.length})
          </h3>
          <div className="space-y-2">
            {report.humanBottlenecks.map((b, i) => <BottleneckCard key={i} b={b} />)}
          </div>
        </section>
      )}

      {/* Critical ownership gaps */}
      {criticalGaps.length > 0 && (
        <section>
          <h3 className="text-xs font-semibold text-orange-400 uppercase tracking-widest mb-2">
            Gaps de Ownership Críticos ({criticalGaps.length})
          </h3>
          <div className="space-y-2">
            {criticalGaps.map((g, i) => <OwnershipGapCard key={i} g={g} />)}
          </div>
        </section>
      )}

      {/* Other ownership gaps */}
      {otherGaps.length > 0 && (
        <section>
          <h3 className="text-xs font-semibold text-slate-400 uppercase tracking-widest mb-2">
            Otros Gaps de Ownership ({otherGaps.length})
          </h3>
          <div className="space-y-2">
            {otherGaps.map((g, i) => <OwnershipGapCard key={i} g={g} />)}
          </div>
        </section>
      )}

      {/* Role suggestions */}
      {report.roleSuggestions.length > 0 && (
        <section>
          <h3 className="text-xs font-semibold text-teal-400 uppercase tracking-widest mb-2">
            Roles Necesarios para Esta Operación
          </h3>
          <div className="space-y-2">
            {report.roleSuggestions.map((r, i) => <RoleCard key={i} r={r} rank={i + 1} />)}
          </div>
        </section>
      )}

      {/* Delegation opportunities */}
      {report.topDelegationOpportunities.length > 0 && (
        <section>
          <h3 className="text-xs font-semibold text-cyan-400 uppercase tracking-widest mb-2">
            Oportunidades de Delegación
          </h3>
          <div className="space-y-2">
            {report.topDelegationOpportunities.map((d, i) => <DelegationCard key={i} d={d} />)}
          </div>
        </section>
      )}

      {report.ownershipGaps.length === 0 && report.humanBottlenecks.length === 0 && (
        <div className="rounded-lg border border-teal-700/30 bg-teal-900/10 p-4 text-center">
          <p className="text-teal-400 font-medium text-sm">Estructura organizacional bien definida</p>
          <p className="text-slate-500 text-xs mt-1">Los procesos críticos tienen ownership identificado.</p>
        </div>
      )}

      <div className="text-[10px] text-slate-600 text-right">
        Sprint 44 · Organizational Intelligence · {new Date(report.analyzedAt).toLocaleString("es-CR")}
      </div>
    </div>
  );
}
