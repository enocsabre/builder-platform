"use client";
import { useState, useEffect, useRef, useCallback } from "react";
import { useRouter } from "next/navigation";
import {
  Plus, Layers, Clock, ExternalLink, Trash2, Cpu, Star,
  Globe, Play, Square, Code, ArrowRight,
  UtensilsCrossed, Stethoscope, Users, Gamepad2, Building2,
  Heart, GraduationCap, ShoppingBag, Truck,
  CheckCircle2, AlertTriangle, XCircle, Zap
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
            <h1 className="text-[22px] font-bold" style={{ color: "var(--foreground)" }}>
              Builder OS
            </h1>
            <p className="text-[13px] mt-0.5" style={{ color: "var(--muted)" }}>
              Sistema operativo multi-producto
            </p>
          </div>
          <button
            className="btn-primary flex items-center gap-2 shrink-0"
            onClick={() => { setShowForm(true); setTimeout(() => promptRef.current?.focus(), 50); }}
          >
            <Plus size={15} />
            Nuevo producto
          </button>
        </div>

        {/* ── Platform stats bar ────────────────────────────────────── */}
        {!loading && total > 0 && (
          <div className="flex flex-wrap gap-3">
            <StatPill label={`${total} producto${total !== 1 ? "s" : ""}`} colorKey="indigo" />
            {deployed > 0  && <StatPill label={`${deployed} deployado${deployed !== 1 ? "s" : ""}`}    colorKey="active" />}
            {building > 0  && <StatPill label={`${building} en construcción`}                           colorKey="warn" />}
            {previews > 0  && <StatPill label={`${previews} preview activo${previews !== 1 ? "s" : ""}`} colorKey="info" />}
            {degraded > 0  && <StatPill label={`${degraded} degradado${degraded !== 1 ? "s" : ""}`}     colorKey="danger" />}
            {degraded === 0 && healthy > 0 && <StatPill label="Sistema saludable" colorKey="active" />}
          </div>
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
                  />
                ))}
              </div>
            )}
          </>
        )}
      </div>
    </AppShell>
  );
}

// ─── StatPill ────────────────────────────────────────────────────────────────

function StatPill({ label, colorKey }: { label: string; colorKey: "active" | "warn" | "danger" | "info" | "indigo" }) {
  return (
    <span style={{
      fontSize: "11px", fontWeight: "600", padding: "4px 12px", borderRadius: "99px",
      background: colorBg(colorKey), color: colorTxt(colorKey),
      border: `1px solid ${colorBg(colorKey)}`,
    }}>
      {label}
    </span>
  );
}

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

function ProductCard({ product: p, isFav, onToggleFav, onOpen, onDelete, onUpdate }: {
  product:      ProductSummary;
  isFav:        boolean;
  onToggleFav:  () => void;
  onOpen:       () => void;
  onDelete:     (e: React.MouseEvent) => void;
  onUpdate:     (p: ProductSummary) => void;
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
          Tu workspace está vacío
        </p>
        <p className="text-[14px]" style={{ color: "var(--muted)", lineHeight: "1.6" }}>
          Describí tu idea en una línea y el Builder OS diseñará la arquitectura,
          generará el código y empezará a construirlo.
        </p>
      </div>
      <div className="flex flex-col items-center gap-3">
        <button
          className="btn-primary flex items-center gap-2"
          style={{ padding: "12px 24px", fontSize: "15px", borderRadius: "12px" }}
          onClick={onNew}
        >
          <Plus size={16} />Crear mi primer producto
        </button>
        <p style={{ fontSize: "12px", color: "var(--muted)", opacity: 0.6 }}>
          Gratis · Sin tarjeta · Listo en minutos
        </p>
      </div>
    </div>
  );
}
