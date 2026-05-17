"use client";
import { useState, useEffect, useRef } from "react";
import { useRouter } from "next/navigation";
import { Plus, Layers, Clock, ExternalLink, Trash2, Cpu } from "lucide-react";
import { AppShell } from "@/components/AppShell";
import { StatusBadge } from "@/components/StatusBadge";
import { api } from "@/lib/api";
import type { ProductSummary } from "@/lib/types";
import { PROCESSING_STATUSES } from "@/lib/types";

function timeAgo(iso: string) {
  const diff  = Date.now() - new Date(iso).getTime();
  const mins  = Math.floor(diff / 60000);
  const hours = Math.floor(diff / 3600000);
  const days  = Math.floor(diff / 86400000);
  if (mins < 1)   return "ahora";
  if (mins < 60)  return `hace ${mins}m`;
  if (hours < 24) return `hace ${hours}h`;
  return `hace ${days}d`;
}

const phaseShortLabel: Record<string, string> = {
  queued:           "En cola...",
  discovery:        "Discovery...",
  architecting:     "Arquitectura...",
  planning:         "Sprint planning...",
  building:         "Construyendo",
  reviewing:        "Revisando",
  waiting_approval: "Esperando aprobación",
};

export default function WorkspacePage() {
  const router  = useRouter();
  const [products, setProducts] = useState<ProductSummary[]>([]);
  const [loading, setLoading]   = useState(true);
  const [creating, setCreating] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [name, setName]         = useState("");
  const [prompt, setPrompt]     = useState("");
  const promptRef = useRef<HTMLTextAreaElement>(null);

  // Refresh list — poll if any product is actively processing
  useEffect(() => {
    let timer: ReturnType<typeof setTimeout>;
    async function refresh() {
      const list = await api.products.list().catch(() => null);
      if (!list) return;
      setProducts(list);
      setLoading(false);
      const hasLive = list.some(p => p.isProcessing || PROCESSING_STATUSES.includes(p.status));
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
    } catch {
      setCreating(false);
    }
  }

  async function handleDelete(e: React.MouseEvent, id: string) {
    e.stopPropagation();
    if (!confirm("¿Eliminar este producto?")) return;
    await api.products.delete(id);
    setProducts(prev => prev.filter(p => p.id !== id));
  }

  return (
    <AppShell>
      <div className="max-w-5xl mx-auto px-6 py-8 space-y-6">
        {/* header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-[22px] font-bold" style={{ color: "var(--foreground)" }}>Workspace</h1>
            <p className="text-[13px] mt-0.5" style={{ color: "var(--muted)" }}>
              {products.length} producto{products.length !== 1 ? "s" : ""} en construcción
            </p>
          </div>
          <button
            className="btn-primary flex items-center gap-2"
            onClick={() => { setShowForm(true); setTimeout(() => promptRef.current?.focus(), 50); }}
          >
            <Plus size={15} />
            Nuevo producto
          </button>
        </div>

        {/* create form */}
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
                placeholder="ej. Un SaaS para gestionar planilla quincenal de restaurantes en Costa Rica, con control de horas extra y días libres según el Código de Trabajo."
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

        {/* product list */}
        {loading ? (
          <div className="space-y-3">
            {[...Array(2)].map((_, i) => (
              <div key={i} className="card shimmer-bg h-20" />
            ))}
          </div>
        ) : products.length === 0 && !showForm ? (
          <EmptyState onNew={() => { setShowForm(true); setTimeout(() => promptRef.current?.focus(), 50); }} />
        ) : (
          <div className="grid gap-3">
            {products.map(p => (
              <ProductCard
                key={p.id}
                product={p}
                onClick={() => router.push(`/workspace/${p.id}`)}
                onDelete={e => handleDelete(e, p.id)}
              />
            ))}
          </div>
        )}
      </div>
    </AppShell>
  );
}

function ProductCard({ product: p, onClick, onDelete }: {
  product: ProductSummary;
  onClick: () => void;
  onDelete: (e: React.MouseEvent) => void;
}) {
  const isLive    = p.isProcessing || PROCESSING_STATUSES.includes(p.status);
  const phaseText = phaseShortLabel[p.runtimePhase];

  return (
    <div
      onClick={onClick}
      className="card flex items-center gap-4 cursor-pointer group transition-all hover:border-[var(--border-strong)] animate-slide-in"
      style={{ borderColor: isLive ? "rgba(99,102,241,0.3)" : "var(--border)" }}
    >
      <div
        className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0"
        style={{ background: "var(--status-indigo-bg)" }}
      >
        {isLive ? (
          <Cpu size={18} className="animate-spin-slow" style={{ color: "var(--accent)" }} />
        ) : (
          <Layers size={18} style={{ color: "var(--status-indigo-text)" }} />
        )}
      </div>

      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-[14px] font-semibold truncate" style={{ color: "var(--foreground)" }}>{p.name}</span>
          <StatusBadge status={p.status} />
          {isLive && phaseText && (
            <span className="text-[11px] font-medium animate-blink" style={{ color: "var(--status-indigo-text)" }}>
              {phaseText}
            </span>
          )}
        </div>
        <div className="flex items-center gap-1 mt-0.5" style={{ color: "var(--muted)" }}>
          <Clock size={11} />
          <span className="text-[12px]">Actualizado {timeAgo(p.updatedAt)}</span>
        </div>
      </div>

      <div className="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
        {p.previewUrl && (
          <a
            href={p.previewUrl} target="_blank" rel="noopener noreferrer"
            onClick={e => e.stopPropagation()}
            className="p-2 rounded-lg transition-colors"
            style={{ background: "var(--status-active-bg)", color: "var(--status-active-text)" }}
            title="Abrir preview"
          >
            <ExternalLink size={14} />
          </a>
        )}
        <button
          onClick={onDelete}
          className="p-2 rounded-lg transition-colors"
          style={{ background: "var(--status-danger-bg)", color: "var(--status-danger-text)" }}
          title="Eliminar"
        >
          <Trash2 size={14} />
        </button>
      </div>
    </div>
  );
}

function EmptyState({ onNew }: { onNew: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center py-24 space-y-4">
      <div className="w-16 h-16 rounded-2xl flex items-center justify-center" style={{ background: "var(--status-indigo-bg)" }}>
        <Layers size={28} style={{ color: "var(--status-indigo-text)", opacity: 0.6 }} />
      </div>
      <div className="text-center space-y-1">
        <p className="text-[16px] font-semibold" style={{ color: "var(--foreground)" }}>Sin productos todavía</p>
        <p className="text-[13px]" style={{ color: "var(--muted)" }}>Describí tu idea y el Builder OS empieza a construir.</p>
      </div>
      <button className="btn-primary flex items-center gap-2" onClick={onNew}>
        <Plus size={15} />Crear primer producto
      </button>
    </div>
  );
}
