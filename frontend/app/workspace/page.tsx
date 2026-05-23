"use client";
import { useState, useEffect, useRef, useCallback } from "react";
import { useRouter } from "next/navigation";
import {
  Plus, Layers, Clock, ExternalLink, Trash2, Cpu, Star,
  Globe, Play, Square, Code, ArrowRight, X,
  UtensilsCrossed, Stethoscope, Users, Gamepad2, Building2,
  Heart, GraduationCap, ShoppingBag, Truck,
  CheckCircle2, AlertTriangle, XCircle, Zap,
  Rocket, Eye, TrendingUp, Dumbbell, Brain
} from "lucide-react";
import { AppShell } from "@/components/AppShell";
import { StatusBadge } from "@/components/StatusBadge";
import { api } from "@/lib/api";
import type { ProductSummary } from "@/lib/types";
import { PROCESSING_STATUSES } from "@/lib/types";

// ─── Industry config ────────────────────────────────────────────────────────

type IndustryEntry = { label: string; colorKey: "active" | "warn" | "danger" | "info" | "indigo"; Icon: React.FC<{size?: number; style?: React.CSSProperties}> };

const INDUSTRY: Record<string, IndustryEntry> = {
  restaurant:  { label: "Restaurantes / F&B",  colorKey: "warn",   Icon: UtensilsCrossed },
  veterinary:  { label: "Veterinaria",          colorKey: "active", Icon: Stethoscope },
  hr_payroll:  { label: "RRHH / Planilla",      colorKey: "info",   Icon: Users },
  gaming:      { label: "Gaming",               colorKey: "indigo", Icon: Gamepad2 },
  real_estate: { label: "Bienes Raíces",        colorKey: "active", Icon: Building2 },
  healthcare:  { label: "Salud",                colorKey: "danger", Icon: Heart },
  education:   { label: "Educación",            colorKey: "info",   Icon: GraduationCap },
  ecommerce:   { label: "E-Commerce",           colorKey: "warn",   Icon: ShoppingBag },
  logistics:   { label: "Logística",            colorKey: "info",   Icon: Truck },
  general:     { label: "General",              colorKey: "indigo", Icon: Layers },
};

function industryOf(key: string | null): IndustryEntry {
  return (key && INDUSTRY[key]) ? INDUSTRY[key] : INDUSTRY.general!;
}

function colorBg(key: "active" | "warn" | "danger" | "info" | "indigo"): string {
  return `var(--status-${key}-bg)`;
}
function colorTxt(key: "active" | "warn" | "danger" | "info" | "indigo"): string {
  return `var(--status-${key}-text)`;
}

// ─── Time util ───────────────────────────────────────────────────────────────

function timeAgo(iso: string | null): string {
  if (!iso) return "—";
  const diff  = Date.now() - new Date(iso).getTime();
  const mins  = Math.floor(diff / 60000);
  const hours = Math.floor(diff / 3600000);
  const days  = Math.floor(diff / 86400000);
  if (mins < 1)   return "ahora";
  if (mins < 60)  return `hace ${mins}m`;
  if (hours < 24) return `hace ${hours}h`;
  return `hace ${days}d`;
}

// ─── Filter types ────────────────────────────────────────────────────────────

type FilterKey = "all" | "building" | "deployed" | "preview" | "degraded" | "favorites";

const FILTER_LABELS: Record<FilterKey, string> = {
  all:       "Todos",
  building:  "En construcción",
  deployed:  "Deployados",
  preview:   "Preview activo",
  degraded:  "Degradados",
  favorites: "Favoritos",
};

function matchesFilter(p: ProductSummary, filter: FilterKey, favs: Set<string>): boolean {
  switch (filter) {
    case "all":       return true;
    case "building":  return p.isProcessing || PROCESSING_STATUSES.includes(p.status);
    case "deployed":  return p.deployStatus === "deployed";
    case "preview":   return p.previewStatus === "running";
    case "degraded":  return p.runtimeHealth === "degraded" || p.runtimeHealth === "broken";
    case "favorites": return favs.has(p.id);
  }
}

// ─── Platform stats ──────────────────────────────────────────────────────────

const phaseShortLabel: Record<string, string> = {
  queued:           "En cola",
  discovery:        "Discovery",
  architecting:     "Arquitectura",
  planning:         "Sprint planning",
  building:         "Construyendo",
  reviewing:        "Revisando",
  waiting_approval: "Esperando aprobación",
};

// ─── Main page ───────────────────────────────────────────────────────────────

export default function WorkspacePage() {
  const router  = useRouter();
  const [products, setProducts] = useState<ProductSummary[]>([]);
  const [loading, setLoading]   = useState(true);
  const [creating, setCreating] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [name, setName]         = useState("");
  const [prompt, setPrompt]     = useState("");
  const [filter, setFilter]     = useState<FilterKey>("all");
  const [industryFilter, setIndustryFilter] = useState<string | null>(null);
  const [favs, setFavs]         = useState<Set<string>>(new Set());
  const promptRef = useRef<HTMLTextAreaElement>(null);
  const [founderMode, setFounderMode] = useState(false);
  useEffect(() => {
    document.body.style.paddingBottom = founderMode ? "60px" : "";
    return () => { document.body.style.paddingBottom = ""; };
  }, [founderMode]);

  useEffect(() => {
    try {
      const stored = localStorage.getItem("bp-favs");
      if (stored) setFavs(new Set(JSON.parse(stored) as string[]));
    } catch {}
  }, []);

  const toggleFav = useCallback((id: string) => {
    setFavs(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      try { localStorage.setItem("bp-favs", JSON.stringify([...next])); } catch {}
      return next;
    });
  }, []);

  useEffect(() => {
    let timer: ReturnType<typeof setTimeout>;
    async function refresh() {
      const list = await api.products.list().catch(() => null);
      if (!list) return;
      setProducts(list);
      setLoading(false);
      const hasLive = list.some(p => p.isProcessing || PROCESSING_STATUSES.includes(p.status) || p.previewStatus === "starting");
      if (hasLive) timer = setTimeout(refresh, 2500);
    }
    refresh();
    return () => clearTimeout(timer);
  }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim() || !prompt.trim()) return;
    setCreating(true);
    try {
      const p = await api.products.create(name.trim(), prompt.trim());
      router.push(`/workspace/${p.id}`);
    } catch { setCreating(false); }
  }

  async function handleDelete(e: React.MouseEvent, id: string) {
    e.stopPropagation();
    if (!confirm("¿Eliminar este producto?")) return;
    await api.products.delete(id);
    setProducts(prev => prev.filter(p => p.id !== id));
  }

  function updateProduct(updated: ProductSummary) {
    setProducts(prev => prev.map(p => p.id === updated.id ? { ...p, ...updated } : p));
  }

  // Derived stats
  const total     = products.length;
  const building  = products.filter(p => p.isProcessing || PROCESSING_STATUSES.includes(p.status)).length;
  const deployed  = products.filter(p => p.deployStatus === "deployed").length;
  const previews  = products.filter(p => p.previewStatus === "running").length;
  const degraded  = products.filter(p => p.runtimeHealth === "degraded" || p.runtimeHealth === "broken").length;
  const healthy   = products.filter(p => p.runtimeHealth === "healthy").length;

  // Industry breakdown (for filter chips)
  const industryKeys = [...new Set(products.map(p => p.industryKey).filter(Boolean))] as string[];

  // Filtered list
  const filtered = products.filter(p => {
    if (!matchesFilter(p, filter, favs)) return false;
    if (industryFilter && p.industryKey !== industryFilter) return false;
    return true;
  });

  const filterCount = (f: FilterKey) => products.filter(p => matchesFilter(p, f, favs)).length;

  return (
    <AppShell>
      <div className="max-w-6xl mx-auto px-6 py-8 space-y-6">

        {/* ── Header ────────────────────────────────────────────────── */}
        <div className="flex items-start justify-between gap-4">
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: "10px", marginBottom: "4px", flexWrap: "wrap" }}>
              <h1 className="text-[22px] font-bold" style={{ color: "var(--foreground)" }}>Builder OS</h1>
              <span style={{
                fontSize: "9px", fontWeight: "700", padding: "2px 8px", borderRadius: "4px",
                background: "rgba(99,102,241,0.15)", color: "#a5b4fc", letterSpacing: "0.06em",
                border: "1px solid rgba(99,102,241,0.2)",
              }}>
                AI PRODUCT OS
              </span>
              {!loading && total > 0 && (
                <span style={{
                  display: "inline-flex", alignItems: "center", gap: "4px",
                  fontSize: "9px", fontWeight: "600", padding: "2px 8px", borderRadius: "4px",
                  background: "rgba(52,211,153,0.1)", color: "#34d399", letterSpacing: "0.05em",
                }}>
                  <span style={{ width: "4px", height: "4px", borderRadius: "50%", background: "currentColor" }} />
                  PLATAFORMA ACTIVA
                </span>
              )}
            </div>
            <p className="text-[13px]" style={{ color: "var(--muted)" }}>
              {loading
                ? "Conectando..."
                : total === 0
                ? "Genera sistemas operacionales para cualquier industria"
                : `${total} producto${total !== 1 ? "s" : ""} generados por IA · multi-vertical${previews > 0 ? ` · ${previews} preview${previews !== 1 ? "s" : ""} en vivo` : ""}${deployed > 0 ? ` · ${deployed} en producción` : ""}`}
            </p>
          </div>
          <div style={{ display: "flex", gap: "8px", alignItems: "center", flexShrink: 0 }}>
            {!loading && total > 0 && (
              <button
                onClick={() => setFounderMode(f => !f)}
                style={{
                  display: "flex", alignItems: "center", gap: "6px",
                  fontSize: "12px", fontWeight: "600", padding: "7px 14px", borderRadius: "8px",
                  background: founderMode ? "rgba(99,102,241,0.2)" : "var(--surface)",
                  color: founderMode ? "#a5b4fc" : "var(--muted)",
                  border: founderMode ? "1px solid rgba(99,102,241,0.4)" : "1px solid var(--border)",
                  cursor: "pointer", transition: "all 0.15s",
                }}
              >
                <Rocket size={13} />
                {founderMode ? "Demo activo" : "Modo Demo"}
              </button>
            )}
            <button
              className="btn-primary flex items-center gap-2 shrink-0"
              onClick={() => { setShowForm(true); setTimeout(() => promptRef.current?.focus(), 50); }}
            >
              <Plus size={15} />
              Nuevo producto
            </button>
          </div>
        </div>

        {/* ── Platform metrics ──────────────────────────────────────── */}
        {!loading && total > 0 && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(150px, 1fr))", gap: "10px" }}>
            <MetricCard value={String(total)}    label="Generados por IA"   colorKey="indigo" Icon={Layers} />
            <MetricCard value={deployed > 0 ? String(deployed) : "—"} label="En producción"     colorKey="active" Icon={Globe}         highlight={deployed > 0} />
            <MetricCard value={previews > 0 ? String(previews) : "—"} label="Previews activos"  colorKey="info"   Icon={Eye}           highlight={previews > 0} />
            {building > 0
              ? <MetricCard value={String(building)} label="En construcción" colorKey="warn" Icon={Cpu} />
              : <MetricCard value={degraded === 0 ? "✓" : String(degraded)} label={degraded === 0 ? "Sistema OK" : "Degradados"} colorKey={degraded === 0 ? "active" : "danger"} Icon={CheckCircle2} highlight={degraded === 0} />
            }
          </div>
        )}

        {/* ── Live Previews Strip ───────────────────────────────────── */}
        {!loading && previews > 0 && (
          <div style={{
            display: "flex", flexWrap: "wrap", gap: "10px", alignItems: "center",
            padding: "12px 16px", borderRadius: "10px",
            background: "rgba(52,211,153,0.06)", border: "1px solid rgba(52,211,153,0.18)",
          }}>
            <span style={{ display: "flex", alignItems: "center", gap: "6px", fontSize: "12px", fontWeight: "600", color: "#34d399" }}>
              <span style={{ width: "6px", height: "6px", borderRadius: "50%", background: "#34d399", flexShrink: 0 }} />
              {previews === 1 ? "1 preview en vivo" : `${previews} previews en vivo`}
            </span>
            <span style={{ color: "rgba(255,255,255,0.15)", fontSize: "12px" }}>·</span>
            {products.filter(p => p.previewStatus === "running").map(p => {
              const url = p.previewUrl || (p.previewPort ? `http://localhost:${p.previewPort}` : null);
              return url ? (
                <a key={p.id} href={url} target="_blank" rel="noopener noreferrer"
                  style={{ display: "inline-flex", alignItems: "center", gap: "4px", fontSize: "12px", color: "#60a5fa", textDecoration: "none" }}>
                  {p.name} :{p.previewPort} <ExternalLink size={10} />
                </a>
              ) : (
                <span key={p.id} style={{ fontSize: "12px", color: "#60a5fa" }}>{p.name}</span>
              );
            })}
          </div>
        )}

        {/* ── Multi-vertical showcase ───────────────────────────────── */}
        {!loading && (
          <MultiVerticalShowcase products={products} />
        )}

        {/* ── Create form ───────────────────────────────────────────── */}
        {showForm && (
          <form
            onSubmit={handleCreate}
            className="card space-y-4 animate-slide-in"
            style={{ border: "1px solid var(--accent)", background: "var(--surface-elevated)" }}
          >
            <div className="flex items-center gap-2">
              <Cpu size={16} style={{ color: "var(--accent)" }} />
              <h2 className="text-[14px] font-semibold" style={{ color: "var(--foreground)" }}>
                Describí tu producto
              </h2>
            </div>
            <div className="space-y-1">
              <label className="text-[12px] font-medium" style={{ color: "var(--muted)" }}>Nombre</label>
              <input
                value={name}
                onChange={e => setName(e.target.value)}
                placeholder="ej. QuincenaCR"
                className="w-full px-3 py-2 rounded-lg text-[14px] outline-none"
                style={{ background: "var(--background)", border: "1px solid var(--border)", color: "var(--foreground)" }}
              />
            </div>
            <div className="space-y-1">
              <label className="text-[12px] font-medium" style={{ color: "var(--muted)" }}>
                Describí qué quiere hacer tu SaaS
              </label>
              <textarea
                ref={promptRef}
                value={prompt}
                onChange={e => setPrompt(e.target.value)}
                placeholder="ej. Un SaaS para gestionar planilla quincenal de restaurantes en Costa Rica."
                rows={4}
                className="w-full px-3 py-2 rounded-lg text-[14px] outline-none resize-none"
                style={{ background: "var(--background)", border: "1px solid var(--border)", color: "var(--foreground)" }}
              />
            </div>
            <div className="flex items-center gap-3 pt-1">
              <button type="submit" className="btn-primary" disabled={creating || !name.trim() || !prompt.trim()}>
                {creating ? "Iniciando runtime..." : "Crear producto"}
              </button>
              <button type="button" className="btn-ghost" onClick={() => { setShowForm(false); setName(""); setPrompt(""); }}>
                Cancelar
              </button>
            </div>
          </form>
        )}

        {/* ── Loading skeleton ──────────────────────────────────────── */}
        {loading && (
          <div className="grid gap-4" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}>
            {[...Array(3)].map((_, i) => (
              <div key={i} className="card shimmer-bg h-48" />
            ))}
          </div>
        )}

        {/* ── Empty state ───────────────────────────────────────────── */}
        {!loading && total === 0 && !showForm && (
          <EmptyState onNew={() => { setShowForm(true); setTimeout(() => promptRef.current?.focus(), 50); }} />
        )}

        {/* ── Filters ───────────────────────────────────────────────── */}
        {!loading && total > 0 && (
          <div className="flex flex-wrap gap-2 items-center">
            {(Object.keys(FILTER_LABELS) as FilterKey[]).map(f => {
              const count = filterCount(f);
              if (f === "favorites" && count === 0) return null;
              return (
                <FilterChip
                  key={f}
                  label={FILTER_LABELS[f]}
                  count={count}
                  active={filter === f}
                  onClick={() => setFilter(filter === f ? "all" : f)}
                />
              );
            })}
            {industryKeys.length > 1 && (
              <>
                <span style={{ width: "1px", height: "20px", background: "var(--border)", margin: "0 4px" }} />
                {industryKeys.map(k => {
                  const ind = industryOf(k);
                  return (
                    <FilterChip
                      key={k}
                      label={ind.label}
                      count={products.filter(p => p.industryKey === k).length}
                      active={industryFilter === k}
                      colorKey={ind.colorKey}
                      onClick={() => setIndustryFilter(industryFilter === k ? null : k)}
                    />
                  );
                })}
              </>
            )}
          </div>
        )}

        {/* ── Founder story section ─────────────────────────────────── */}
        {!loading && founderMode && total > 0 && (
          <FounderStorySection products={products} />
        )}

        {/* ── Product gallery ───────────────────────────────────────── */}
        {!loading && total > 0 && (
          <>
            {filtered.length === 0 ? (
              <div className="py-12 text-center" style={{ color: "var(--muted)" }}>
                <p className="text-[14px]">Sin productos que coincidan con el filtro.</p>
              </div>
            ) : (
              <div className="grid gap-4" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))" }}>
                {filtered.map(p => (
                  <ProductCard
                    key={p.id}
                    product={p}
                    isFav={favs.has(p.id)}
                    onToggleFav={() => toggleFav(p.id)}
                    onOpen={() => router.push(`/workspace/${p.id}`)}
                    onDelete={e => handleDelete(e, p.id)}
                    onUpdate={updateProduct}
                    onAnalysis={() => router.push(`/workspace/${p.id}?tab=intelligence`)}
                  />
                ))}
              </div>
            )}
          </>
        )}
      </div>
      {founderMode && (
        <FounderModeBar products={products} onClose={() => setFounderMode(false)} />
      )}
    </AppShell>
  );
}

// ─── MetricCard ──────────────────────────────────────────────────────────────

function MetricCard({ value, label, colorKey, Icon, highlight }: {
  value: string; label: string;
  colorKey: "active" | "warn" | "danger" | "info" | "indigo";
  Icon: React.FC<{size?: number; style?: React.CSSProperties}>;
  highlight?: boolean;
}) {
  return (
    <div style={{
      padding: "12px 14px", borderRadius: "10px",
      background: highlight ? colorBg(colorKey) : "var(--surface)",
      border: `1px solid ${highlight ? colorTxt(colorKey) + "33" : "var(--border)"}`,
      display: "flex", alignItems: "center", gap: "10px",
    }}>
      <div style={{
        width: "32px", height: "32px", borderRadius: "8px", flexShrink: 0,
        background: highlight ? "rgba(255,255,255,0.1)" : "var(--surface-elevated)",
        display: "flex", alignItems: "center", justifyContent: "center",
      }}>
        <Icon size={14} style={{ color: colorTxt(colorKey) }} />
      </div>
      <div>
        <div style={{ fontSize: "18px", fontWeight: "700", color: highlight ? colorTxt(colorKey) : "var(--foreground)", letterSpacing: "-0.02em", lineHeight: "1" }}>{value}</div>
        <div style={{ fontSize: "10px", color: highlight ? colorTxt(colorKey) : "var(--muted)", marginTop: "3px", fontWeight: "500" }}>{label}</div>
      </div>
    </div>
  );
}

// ─── ProductEvolutionBar ─────────────────────────────────────────────────────

function ProductEvolutionBar({ product: p }: { product: ProductSummary }) {
  const stages: { label: string; done: boolean; color: string }[] = [
    { label: "Diseño",   done: true,                              color: "#818cf8" },
    { label: "Código",   done: p.scaffoldStatus === "complete",   color: "#818cf8" },
    { label: "Preview",  done: p.previewStatus === "running",     color: "#60a5fa" },
    { label: "Deploy",   done: p.deployStatus === "deployed",     color: "#34d399" },
  ];
  return (
    <div style={{ display: "flex", alignItems: "flex-start" }}>
      {stages.map((s, i) => (
        <div key={s.label} style={{ display: "flex", alignItems: "center", flex: i < stages.length - 1 ? 1 : undefined }}>
          <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: "3px" }}>
            <div style={{
              width: "7px", height: "7px", borderRadius: "50%",
              background: s.done ? s.color : "var(--border)",
              transition: "background 0.3s",
            }} />
            <span style={{ fontSize: "9px", color: s.done ? "var(--muted)" : "var(--border)", whiteSpace: "nowrap" }}>{s.label}</span>
          </div>
          {i < stages.length - 1 && (
            <div style={{ flex: 1, height: "1px", margin: "0 3px", marginBottom: "12px", background: s.done ? s.color + "55" : "var(--border)" }} />
          )}
        </div>
      ))}
    </div>
  );
}

// ─── FounderStorySection ─────────────────────────────────────────────────────

function FounderStorySection({ products }: { products: ProductSummary[] }) {
  const generated    = products.filter(p => p.scaffoldStatus === "complete").length;
  const livePreviews = products.filter(p => p.previewStatus === "running");
  const deployedList = products.filter(p => p.deployStatus === "deployed");

  return (
    <div style={{
      borderRadius: "14px", overflow: "hidden",
      border: "1px solid rgba(99,102,241,0.25)",
      background: "linear-gradient(135deg, rgba(99,102,241,0.06) 0%, rgba(0,0,0,0) 70%)",
    }}>
      {/* Story header */}
      <div style={{ padding: "20px 24px", borderBottom: "1px solid rgba(99,102,241,0.12)" }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "8px", flexWrap: "wrap", gap: "8px" }}>
          <span style={{
            fontSize: "9px", fontWeight: "700", letterSpacing: "0.08em", color: "#818cf8",
            padding: "2px 8px", borderRadius: "4px", background: "rgba(99,102,241,0.12)",
          }}>
            AI PRODUCT OPERATING SYSTEM
          </span>
          {livePreviews.length > 0 && (
            <span style={{ display: "flex", alignItems: "center", gap: "5px", fontSize: "11px", color: "#34d399", fontWeight: "600" }}>
              <span style={{ width: "5px", height: "5px", borderRadius: "50%", background: "currentColor", animation: "demo-pulse 1.4s ease-in-out infinite" }} />
              {livePreviews.length} preview{livePreviews.length !== 1 ? "s" : ""} corriendo ahora
            </span>
          )}
        </div>
        <h2 style={{ fontSize: "18px", fontWeight: "700", color: "var(--foreground)", marginBottom: "6px", letterSpacing: "-0.01em" }}>
          No solo genera SaaS. Entiende cómo deberían evolucionar.
        </h2>
        <p style={{ fontSize: "13px", color: "var(--muted)", lineHeight: "1.5", maxWidth: "560px", margin: 0 }}>
          Builder OS genera sistemas operacionales completos y los analiza: detecta gaps, sugiere conexiones,
          razona sobre la evolución de cada vertical. Un arquitecto de producto operacional con IA.
        </p>
      </div>

      {/* Metrics */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", padding: "16px 24px", gap: "16px" }}>
        <StoryMetric value={String(generated)}         label="Productos generados"  sub="arquitectura + código + preview" />
        <StoryMetric value={String(livePreviews.length)} label="Corriendo ahora"  sub="previews activos en múltiples verticales" highlight={livePreviews.length > 0} />
        <StoryMetric value="10+"                         label="Verticales"        sub="restaurant · vet · HR · gaming · logística · más" />
      </div>

      {/* Live links */}
      {livePreviews.length > 0 && (
        <div style={{ padding: "12px 24px", borderTop: "1px solid rgba(99,102,241,0.12)", display: "flex", alignItems: "center", gap: "10px", flexWrap: "wrap" }}>
          <span style={{ fontSize: "11px", color: "var(--muted)", flexShrink: 0 }}>Ver en vivo →</span>
          {livePreviews.map(p => {
            const url = p.previewPort ? `http://localhost:${p.previewPort}` : p.previewUrl;
            return url ? (
              <a key={p.id} href={url} target="_blank" rel="noopener noreferrer"
                style={{
                  display: "inline-flex", alignItems: "center", gap: "4px",
                  fontSize: "12px", fontWeight: "500", padding: "4px 12px", borderRadius: "6px",
                  background: "rgba(52,211,153,0.1)", color: "#34d399",
                  border: "1px solid rgba(52,211,153,0.2)", textDecoration: "none",
                }}>
                {p.name} <ExternalLink size={10} />
              </a>
            ) : null;
          })}
          {deployedList.map(p => p.deployUrl ? (
            <a key={p.id} href={p.deployUrl} target="_blank" rel="noopener noreferrer"
              style={{
                display: "inline-flex", alignItems: "center", gap: "4px",
                fontSize: "12px", fontWeight: "500", padding: "4px 12px", borderRadius: "6px",
                background: "var(--status-active-bg)", color: "var(--status-active-text)",
                border: "1px solid var(--border)", textDecoration: "none",
              }}>
              {p.name} ↗ <ExternalLink size={10} />
            </a>
          ) : null)}
        </div>
      )}
    </div>
  );
}

function StoryMetric({ value, label, sub, highlight }: { value: string; label: string; sub: string; highlight?: boolean }) {
  return (
    <div>
      <div style={{ fontSize: "28px", fontWeight: "700", color: highlight ? "#34d399" : "#a5b4fc", letterSpacing: "-0.02em", marginBottom: "4px" }}>{value}</div>
      <div style={{ fontSize: "12px", fontWeight: "600", color: "var(--foreground-muted)", marginBottom: "2px" }}>{label}</div>
      <div style={{ fontSize: "10px", color: "var(--muted)" }}>{sub}</div>
    </div>
  );
}

// ─── FounderModeBar ──────────────────────────────────────────────────────────

function FounderModeBar({ products, onClose }: { products: ProductSummary[]; onClose: () => void }) {
  const livePreviews = products.filter(p => p.previewStatus === "running");
  const deployedList = products.filter(p => p.deployStatus === "deployed");

  return (
    <div style={{
      position: "fixed", bottom: 0, left: 0, right: 0, zIndex: 9000,
      background: "rgba(8,8,12,0.96)", backdropFilter: "blur(20px)",
      borderTop: "1px solid rgba(99,102,241,0.35)",
      padding: "0 24px", height: "56px",
      display: "flex", alignItems: "center", gap: "16px",
    }}>
      <div style={{ display: "flex", alignItems: "center", gap: "8px", flexShrink: 0 }}>
        <span style={{ width: "6px", height: "6px", borderRadius: "50%", background: "#a5b4fc", animation: "demo-pulse 2s ease-in-out infinite" }} />
        <span style={{ fontSize: "11px", fontWeight: "700", color: "#a5b4fc", letterSpacing: "0.06em" }}>FOUNDER DEMO</span>
      </div>

      <span style={{ width: "1px", height: "20px", background: "rgba(255,255,255,0.08)", flexShrink: 0 }} />

      <span style={{ fontSize: "11px", color: "var(--muted)", flexShrink: 0 }}>En vivo:</span>
      <div style={{ display: "flex", gap: "6px", flexWrap: "wrap" }}>
        <DemoNavChip label="Builder OS" href="/workspace" colorKey="indigo" />
        {livePreviews.map(p => {
          const url = p.previewPort ? `http://localhost:${p.previewPort}` : p.previewUrl;
          return url ? <DemoNavChip key={p.id} label={p.name} href={url} external colorKey="info" /> : null;
        })}
        {deployedList.map(p => p.deployUrl ? (
          <DemoNavChip key={p.id} label={`${p.name} ↗`} href={p.deployUrl} external colorKey="active" />
        ) : null)}
      </div>

      <div style={{ marginLeft: "auto", display: "flex", alignItems: "center", gap: "12px" }}>
        <span style={{ fontSize: "11px", color: "var(--muted)" }}>
          {products.length} prod · {livePreviews.length} en vivo
        </span>
        <button
          onClick={onClose}
          style={{ padding: "4px", borderRadius: "6px", background: "none", border: "none", cursor: "pointer", color: "var(--muted)", display: "flex" }}
        >
          <X size={14} />
        </button>
      </div>
    </div>
  );
}

function DemoNavChip({ label, href, external, colorKey }: {
  label: string; href: string; external?: boolean;
  colorKey: "active" | "warn" | "danger" | "info" | "indigo";
}) {
  return (
    <a
      href={href}
      target={external ? "_blank" : "_self"}
      rel="noopener noreferrer"
      style={{
        display: "inline-flex", alignItems: "center", gap: "4px",
        fontSize: "11px", fontWeight: "500", padding: "4px 10px", borderRadius: "6px",
        background: colorBg(colorKey), color: colorTxt(colorKey),
        border: `1px solid ${colorTxt(colorKey)}22`,
        textDecoration: "none", whiteSpace: "nowrap",
      }}
    >
      {label}
      {external && <ExternalLink size={9} />}
    </a>
  );
}

// ─── StatPill ────────────────────────────────────────────────────────────────

// ─── FilterChip ──────────────────────────────────────────────────────────────

function FilterChip({ label, count, active, colorKey, onClick }: {
  label: string; count: number; active: boolean;
  colorKey?: "active" | "warn" | "danger" | "info" | "indigo";
  onClick: () => void;
}) {
  const ck = colorKey ?? "indigo";
  return (
    <button
      onClick={onClick}
      style={{
        fontSize: "12px", fontWeight: "500", padding: "5px 12px", borderRadius: "8px", cursor: "pointer",
        border: active ? `1px solid ${colorTxt(ck)}` : "1px solid var(--border)",
        background: active ? colorBg(ck) : "var(--surface)",
        color: active ? colorTxt(ck) : "var(--muted)",
        transition: "all 0.15s",
      }}
    >
      {label}
      {count > 0 && (
        <span style={{
          marginLeft: "6px", fontSize: "10px", fontWeight: "700",
          padding: "1px 5px", borderRadius: "99px",
          background: active ? "rgba(255,255,255,0.15)" : "var(--surface-elevated)",
          color: active ? colorTxt(ck) : "var(--muted)",
        }}>
          {count}
        </span>
      )}
    </button>
  );
}

// ─── ProductCard ─────────────────────────────────────────────────────────────

function ProductCard({ product: p, isFav, onToggleFav, onOpen, onDelete, onUpdate, onAnalysis }: {
  product:      ProductSummary;
  isFav:        boolean;
  onToggleFav:  () => void;
  onOpen:       () => void;
  onDelete:     (e: React.MouseEvent) => void;
  onUpdate:     (p: ProductSummary) => void;
  onAnalysis:   () => void;
}) {
  const [previewLoading, setPreviewLoading] = useState(false);
  const [vsCodeLoading,  setVsCodeLoading]  = useState(false);

  const isBuilding    = p.isProcessing || PROCESSING_STATUSES.includes(p.status);
  const isDeployed    = p.deployStatus === "deployed";
  const isPreviewOn   = p.previewStatus === "running";
  const canPreview    = p.scaffoldStatus === "complete";
  const canVsCode     = p.scaffoldStatus === "complete";
  const phaseText     = phaseShortLabel[p.runtimePhase];
  const ind           = industryOf(p.industryKey);
  const ck            = ind.colorKey;

  async function handlePreviewToggle() {
    if (previewLoading) return;
    setPreviewLoading(true);
    try {
      const updated = isPreviewOn
        ? await api.products.stopPreview(p.id)
        : await api.products.startPreview(p.id);
      onUpdate({ ...p, ...updated });
    } catch { /* ignore */ }
    finally { setPreviewLoading(false); }
  }

  async function handleVsCode() {
    if (vsCodeLoading) return;
    setVsCodeLoading(true);
    try { await api.products.openVSCode(p.id); } catch { /* ignore */ }
    finally { setTimeout(() => setVsCodeLoading(false), 1500); }
  }

  return (
    <div
      onClick={onOpen}
      className="card cursor-pointer group animate-slide-in"
      style={{
        border: isBuilding
          ? "1px solid rgba(99,102,241,0.3)"
          : isDeployed
          ? `1px solid ${colorBg("active")}`
          : "1px solid var(--border)",
        transition: "border-color 0.2s, box-shadow 0.2s",
        display: "flex", flexDirection: "column", gap: "14px",
      }}
    >
      {/* ── Top: thumbnail + name + star ──────────────────────────── */}
      <div className="flex items-start gap-3">
        {/* Industry thumbnail */}
        <div style={{
          width: "48px", height: "48px", borderRadius: "12px", flexShrink: 0,
          background: colorBg(ck), display: "flex", alignItems: "center", justifyContent: "center",
        }}>
          {isBuilding
            ? <Cpu size={20} className="animate-spin-slow" style={{ color: colorTxt(ck) }} />
            : <ind.Icon size={20} style={{ color: colorTxt(ck) }} />
          }
        </div>

        {/* Name + industry */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-1.5">
            <span className="text-[15px] font-semibold truncate" style={{ color: "var(--foreground)" }}>
              {p.name}
            </span>
            {isBuilding && phaseText && (
              <span className="text-[10px] font-medium animate-blink shrink-0" style={{ color: "var(--status-indigo-text)" }}>
                {phaseText}
              </span>
            )}
          </div>
          <div className="flex items-center gap-1.5 mt-0.5 flex-wrap">
            <span className="text-[11px]" style={{ color: colorTxt(ck) }}>{ind.label}</span>
            <span style={{ color: "var(--border)", fontSize: "10px" }}>·</span>
            <StatusBadge status={p.status} />
          </div>
          {/* Autonomy narrative — only when scaffold is complete */}
          {p.scaffoldStatus === "complete" && !isBuilding && (
            <div className="flex items-center gap-1.5 mt-0.5 flex-wrap">
              <span style={{ fontSize: "10px", color: "var(--muted)" }}>Generado por IA</span>
              {isPreviewOn && (
                <>
                  <span style={{ color: "var(--border)", fontSize: "10px" }}>·</span>
                  <span style={{ fontSize: "10px", color: "var(--status-active-text)", display: "flex", alignItems: "center", gap: "3px" }}>
                    <span style={{ width: "4px", height: "4px", borderRadius: "50%", background: "currentColor", display: "inline-block" }} />
                    preview en vivo
                  </span>
                </>
              )}
            </div>
          )}
          {isBuilding && (
            <div style={{ fontSize: "10px", color: "var(--muted)", marginTop: "2px" }}>
              Generando código automáticamente...
            </div>
          )}
        </div>

        {/* Star (favorite) */}
        <button
          onClick={e => { e.stopPropagation(); onToggleFav(); }}
          className="shrink-0 p-1 rounded-md transition-colors"
          style={{ color: isFav ? "var(--status-warn-text)" : "var(--muted)", background: "transparent" }}
          title={isFav ? "Quitar de favoritos" : "Agregar a favoritos"}
        >
          <Star size={14} fill={isFav ? "currentColor" : "none"} />
        </button>
      </div>

      {/* ── Status badges row ─────────────────────────────────────── */}
      <div className="flex flex-wrap gap-1.5">
        {isDeployed && (
          <BadgePill label="DEPLOYADO" colorKey="active" Icon={Globe} />
        )}
        {isPreviewOn && (
          <BadgePill label="PREVIEW" colorKey="info" Icon={Zap} />
        )}
        {isBuilding && (
          <BadgePill label="EN CONSTRUCCIÓN" colorKey="indigo" Icon={Cpu} />
        )}
        {p.runtimeHealth === "healthy" && !isBuilding && (
          <BadgePill label="SALUDABLE" colorKey="active" Icon={CheckCircle2} />
        )}
        {p.runtimeHealth === "degraded" && (
          <BadgePill label="DEGRADADO" colorKey="warn" Icon={AlertTriangle} />
        )}
        {p.runtimeHealth === "broken" && (
          <BadgePill label="ROTO" colorKey="danger" Icon={XCircle} />
        )}
      </div>

      {/* ── Evolution timeline ────────────────────────────────────── */}
      {p.scaffoldStatus === "complete" && (
        <ProductEvolutionBar product={p} />
      )}

      {/* ── Deploy URL ────────────────────────────────────────────── */}
      {isDeployed && p.deployUrl && (
        <a
          href={p.deployUrl} target="_blank" rel="noopener noreferrer"
          onClick={e => e.stopPropagation()}
          className="flex items-center gap-1.5 transition-opacity hover:opacity-70"
          style={{ color: "var(--status-active-text)", fontSize: "12px", textDecoration: "none" }}
        >
          <Globe size={11} />
          <span className="truncate">{p.deployUrl.replace(/^https?:\/\//, "")}</span>
          <ExternalLink size={10} style={{ flexShrink: 0 }} />
        </a>
      )}

      {/* ── Timestamps ────────────────────────────────────────────── */}
      <div className="flex items-center gap-3 flex-wrap" style={{ color: "var(--muted)", fontSize: "11px" }}>
        <span className="flex items-center gap-1">
          <Clock size={10} />
          {timeAgo(p.updatedAt)}
        </span>
        {p.deployedAt && (
          <span className="flex items-center gap-1">
            <Globe size={10} />
            deploy {timeAgo(p.deployedAt)}
          </span>
        )}
        {p.previewPort && isPreviewOn && (
          <span style={{ color: "var(--status-info-text)" }}>
            :port {p.previewPort}
          </span>
        )}
      </div>

      {/* ── Quick actions ─────────────────────────────────────────── */}
      <div
        className="flex items-center gap-2 flex-wrap pt-1"
        style={{ borderTop: "1px solid var(--border)" }}
        onClick={e => e.stopPropagation()}
      >
        {/* Open workspace */}
        <ActionBtn label="Abrir" Icon={ArrowRight} onClick={onOpen} primary />

        {/* Intelligence */}
        {canPreview && (
          <ActionBtn
            label="Análisis"
            Icon={Brain}
            onClick={onAnalysis}
            colorKey="indigo"
          />
        )}

        {/* Preview toggle */}
        {canPreview && (
          <ActionBtn
            label={previewLoading ? "..." : isPreviewOn ? "Detener" : "Preview"}
            Icon={isPreviewOn ? Square : Play}
            onClick={handlePreviewToggle}
            colorKey={isPreviewOn ? "danger" : "info"}
            disabled={previewLoading || p.previewStatus === "starting"}
          />
        )}

        {/* Open preview URL */}
        {isPreviewOn && p.previewUrl && (
          <ActionBtn
            label="Abrir ↗"
            Icon={ExternalLink}
            onClick={() => { window.open(p.previewUrl!, "_blank"); }}
            colorKey="info"
          />
        )}

        {/* VS Code */}
        {canVsCode && (
          <ActionBtn
            label={vsCodeLoading ? "Abriendo..." : "VS Code"}
            Icon={Code}
            onClick={handleVsCode}
            disabled={vsCodeLoading}
          />
        )}

        {/* Deploy URL */}
        {isDeployed && p.deployUrl && (
          <ActionBtn
            label="Sitio ↗"
            Icon={Globe}
            onClick={() => { window.open(p.deployUrl!, "_blank"); }}
            colorKey="active"
          />
        )}

        {/* Delete — pushed to end */}
        <button
          onClick={onDelete}
          className="ml-auto p-1.5 rounded-md transition-colors"
          style={{ color: "var(--muted)", background: "transparent" }}
          title="Eliminar"
        >
          <Trash2 size={13} />
        </button>
      </div>
    </div>
  );
}

// ─── BadgePill ───────────────────────────────────────────────────────────────

function BadgePill({ label, colorKey, Icon }: {
  label:    string;
  colorKey: "active" | "warn" | "danger" | "info" | "indigo";
  Icon:     React.FC<{size?: number; style?: React.CSSProperties}>;
}) {
  return (
    <span style={{
      display: "inline-flex", alignItems: "center", gap: "4px",
      fontSize: "10px", fontWeight: "700", padding: "2px 8px", borderRadius: "99px",
      background: colorBg(colorKey), color: colorTxt(colorKey), letterSpacing: "0.04em",
    }}>
      <Icon size={9} />
      {label}
    </span>
  );
}

// ─── ActionBtn ───────────────────────────────────────────────────────────────

function ActionBtn({ label, Icon, onClick, primary, colorKey, disabled }: {
  label:     string;
  Icon:      React.FC<{size?: number; style?: React.CSSProperties}>;
  onClick:   () => void | Promise<void>;
  primary?:  boolean;
  colorKey?: "active" | "warn" | "danger" | "info" | "indigo";
  disabled?: boolean;
}) {
  const ck = colorKey ?? "indigo";
  const bg = primary ? "var(--accent)" : colorBg(ck);
  const fg = primary ? "#fff" : colorTxt(ck);
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      style={{
        display: "inline-flex", alignItems: "center", gap: "5px",
        fontSize: "11px", fontWeight: "600", padding: "5px 10px", borderRadius: "7px",
        background: disabled ? "var(--surface-elevated)" : bg,
        color: disabled ? "var(--muted)" : fg,
        border: "none", cursor: disabled ? "not-allowed" : "pointer",
        transition: "opacity 0.15s",
        opacity: disabled ? 0.5 : 1,
      }}
    >
      <Icon size={11} />
      {label}
    </button>
  );
}

// ─── EmptyState ──────────────────────────────────────────────────────────────

function EmptyState({ onNew }: { onNew: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center py-24 space-y-6">
      <div
        style={{
          width: "72px", height: "72px", borderRadius: "20px",
          display: "flex", alignItems: "center", justifyContent: "center",
          background: "var(--accent)", boxShadow: "0 0 40px rgba(99,102,241,0.3)",
        }}
      >
        <Layers size={32} color="#fff" />
      </div>
      <div className="text-center space-y-2" style={{ maxWidth: "360px" }}>
        <p className="text-[20px] font-bold" style={{ color: "var(--foreground)", letterSpacing: "-0.02em" }}>
          Genera tu primer producto
        </p>
        <p className="text-[14px]" style={{ color: "var(--muted)", lineHeight: "1.6" }}>
          Elige una industria, describe tu idea — Builder OS genera la arquitectura,
          el código y un preview operacional en minutos.
        </p>
      </div>
      <div className="flex flex-col items-center gap-3">
        <button
          className="btn-primary flex items-center gap-2"
          style={{ padding: "12px 24px", fontSize: "15px", borderRadius: "12px" }}
          onClick={onNew}
        >
          <Plus size={16} />Crear primer producto
        </button>
        <p style={{ fontSize: "12px", color: "var(--muted)", opacity: 0.6 }}>
          Restaurant · Gym · Veterinary · HR · y más
        </p>
      </div>
    </div>
  );
}

// ─── MultiVerticalShowcase ───────────────────────────────────────────────────

type ShowcaseVertical = {
  key: string; name: string; subtitle: string; industryKey: string;
  modules: string[];
  kpis: { v: string; l: string }[];
};

const SHOWCASE_VERTICALS: ShowcaseVertical[] = [
  {
    key: "restaurant",
    name: "Restaurant Operations",
    subtitle: "POS · KDS · Inventario · Mesas",
    industryKey: "restaurant",
    modules: ["Dashboard operacional", "Pedidos y comandas", "Cocina KDS", "Gestión de mesas", "Control de inventario"],
    kpis: [{ v: "~180", l: "Órdenes/día" }, { v: "12", l: "Mesas" }, { v: "5", l: "Módulos" }],
  },
  {
    key: "hr_payroll",
    name: "HR & Planilla",
    subtitle: "Empleados · Quincena · Reportes",
    industryKey: "hr_payroll",
    modules: ["Dashboard de personal", "Cálculo de planilla", "Control de asistencia", "Vacaciones y permisos", "Reportes CCSS"],
    kpis: [{ v: "~45", l: "Empleados" }, { v: "Qnal", l: "Planilla" }, { v: "5", l: "Módulos" }],
  },
  {
    key: "gym",
    name: "Gym Operations",
    subtitle: "Membresías · Clases · Asistencia",
    industryKey: "gym",
    modules: ["Dashboard membresías", "Clases programadas", "Control de asistencia", "Pagos y renovaciones", "Reportes de uso"],
    kpis: [{ v: "~200", l: "Miembros" }, { v: "35/sem", l: "Clases" }, { v: "5", l: "Módulos" }],
  },
  {
    key: "veterinary",
    name: "Veterinary Clinic",
    subtitle: "Pacientes · Citas · Historial",
    industryKey: "veterinary",
    modules: ["Dashboard clínico", "Gestión de citas", "Historial médico", "Control de medicamentos", "Facturación"],
    kpis: [{ v: "~80", l: "Pacientes" }, { v: "~15/día", l: "Citas" }, { v: "5", l: "Módulos" }],
  },
];

const HR_LOCAL_URL   = "http://localhost:3102";
const HR_PUBLIC_URL  = "https://hr-planilla-showcase.vercel.app";
const REST_LOCAL_URL = "http://localhost:3100";
const REST_PUBLIC_URL = "https://restaurant-showcase-cr.vercel.app";

function useShowcaseUrl(localUrl: string, publicUrl: string): string | null {
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    const check = async () => {
      try {
        const r = await fetch(localUrl, { method: "HEAD", signal: AbortSignal.timeout(2000) });
        setUrl(r.status < 500 ? localUrl : publicUrl);
      } catch { setUrl(publicUrl); }
    };
    void check();
    const id = setInterval(check, 30000);
    return () => clearInterval(id);
  }, [localUrl, publicUrl]);
  return url;
}

function MultiVerticalShowcase({ products }: { products: ProductSummary[] }) {
  const liveProduct = products.find(p => p.previewStatus === "running");
  const hrUrl   = useShowcaseUrl(HR_LOCAL_URL,   HR_PUBLIC_URL);
  const restUrl = useShowcaseUrl(REST_LOCAL_URL, REST_PUBLIC_URL);

  const hrLive   = hrUrl   !== null;
  const restLive = restUrl !== null;

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "12px", gap: "8px", flexWrap: "wrap" }}>
        <div>
          <h2 style={{ fontSize: "15px", fontWeight: "700", color: "var(--foreground)", marginBottom: "2px" }}>
            Verticales generados
          </h2>
          <p style={{ fontSize: "12px", color: "var(--muted)", margin: 0 }}>
            El mismo engine genera sistemas operacionales completos para cualquier industria
          </p>
        </div>
        <span style={{
          fontSize: "9px", fontWeight: "700", padding: "2px 8px", borderRadius: "4px",
          background: "rgba(99,102,241,0.15)", color: "#a5b4fc", letterSpacing: "0.06em",
          border: "1px solid rgba(99,102,241,0.2)", flexShrink: 0,
        }}>
          MULTI-VERTICAL
        </span>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))", gap: "12px" }}>
        {SHOWCASE_VERTICALS.map((v, idx) => {
          const ind: IndustryEntry = v.industryKey === "gym"
            ? { label: "Gym / Fitness", colorKey: "info", Icon: Dumbbell }
            : industryOf(v.industryKey);
          const ck = ind.colorKey;
          const isLive =
            (idx === 0 && (liveProduct != null || restLive)) ||
            (idx === 1 && hrLive);
          const previewUrl = isLive
            ? (idx === 1
                ? hrUrl
                : (liveProduct?.previewPort
                    ? `http://localhost:${liveProduct.previewPort}`
                    : (liveProduct?.previewUrl ?? restUrl)))
            : null;

          return (
            <div key={v.key} style={{
              borderRadius: "12px", overflow: "hidden",
              border: isLive ? `1px solid ${colorTxt(ck)}33` : "1px solid var(--border)",
              background: "var(--surface)", display: "flex", flexDirection: "column",
            }}>
              {/* Header */}
              <div style={{ padding: "14px 16px", borderBottom: "1px solid var(--border)", display: "flex", alignItems: "center", gap: "10px" }}>
                <div style={{
                  width: "34px", height: "34px", borderRadius: "9px", flexShrink: 0,
                  background: colorBg(ck), display: "flex", alignItems: "center", justifyContent: "center",
                }}>
                  <ind.Icon size={17} style={{ color: colorTxt(ck) }} />
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: "13px", fontWeight: "600", color: "var(--foreground)", marginBottom: "1px" }}>{v.name}</div>
                  <div style={{ fontSize: "10px", color: "var(--muted)" }}>{v.subtitle}</div>
                </div>
                {isLive
                  ? <span style={{ display: "inline-flex", alignItems: "center", gap: "4px", fontSize: "9px", fontWeight: "700", padding: "2px 8px", borderRadius: "99px", background: colorBg("active"), color: colorTxt("active"), letterSpacing: "0.05em", flexShrink: 0 }}>
                      <span style={{ width: "4px", height: "4px", borderRadius: "50%", background: "currentColor", animation: "demo-pulse 1.4s ease-in-out infinite" }} />
                      LIVE
                    </span>
                  : <span style={{ fontSize: "9px", fontWeight: "600", padding: "2px 8px", borderRadius: "99px", background: "var(--surface-elevated)", color: "var(--muted)", letterSpacing: "0.04em", flexShrink: 0 }}>SHOWCASE</span>
                }
              </div>

              {/* KPI row */}
              <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", borderBottom: "1px solid var(--border)" }}>
                {v.kpis.map((kpi, ki) => (
                  <div key={kpi.l} style={{ padding: "9px 12px", borderRight: ki < 2 ? "1px solid var(--border)" : "none" }}>
                    <div style={{ fontSize: "14px", fontWeight: "700", color: colorTxt(ck), letterSpacing: "-0.01em" }}>{kpi.v}</div>
                    <div style={{ fontSize: "9px", color: "var(--muted)", marginTop: "2px" }}>{kpi.l}</div>
                  </div>
                ))}
              </div>

              {/* Modules */}
              <div style={{ padding: "10px 16px", flex: 1, display: "flex", flexDirection: "column", gap: "5px" }}>
                {v.modules.map(m => (
                  <div key={m} style={{ display: "flex", alignItems: "center", gap: "7px", fontSize: "11px", color: "var(--muted)" }}>
                    <span style={{ width: "4px", height: "4px", borderRadius: "50%", background: colorTxt(ck), flexShrink: 0, opacity: 0.6 }} />
                    {m}
                  </div>
                ))}
              </div>

              {/* Footer */}
              <div style={{ padding: "8px 16px", borderTop: "1px solid var(--border)", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                <span style={{ fontSize: "9px", color: "var(--muted)", opacity: 0.6 }}>Generado por IA · Builder OS</span>
                {isLive && previewUrl
                  ? <a href={previewUrl} target="_blank" rel="noopener noreferrer"
                      style={{ display: "inline-flex", alignItems: "center", gap: "3px", fontSize: "10px", fontWeight: "600", color: colorTxt("active"), textDecoration: "none" }}>
                      Ver demo <ExternalLink size={9} />
                    </a>
                  : <span style={{ fontSize: "9px", color: "var(--muted)", opacity: 0.5 }}>Preview próximamente</span>
                }
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
