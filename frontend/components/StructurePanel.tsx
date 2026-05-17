"use client";
import type { ProductModule } from "@/lib/types";

interface Props {
  modules: ProductModule[];
}

export default function StructurePanel({ modules }: Props) {
  if (modules.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-16 px-4 space-y-2">
        <p className="text-[13px]" style={{ color: "var(--muted)" }}>
          No hay módulos registrados aún.
        </p>
        <p className="text-[12px] text-center" style={{ color: "var(--muted-foreground)" }}>
          El registry se construye automáticamente después de completar el scaffold del proyecto.
        </p>
      </div>
    );
  }

  const scaffold = modules.filter(m => m.source === "scaffold");
  const delta    = modules.filter(m => m.source === "delta");

  return (
    <div className="p-4 space-y-5">
      <ModuleGroup label="Módulos del scaffold inicial" modules={scaffold} accent="var(--status-active-text)" />
      {delta.length > 0 && (
        <ModuleGroup label="Features generadas (delta)" modules={delta} accent="var(--status-warn-text)" />
      )}

      <div className="rounded-lg px-3 py-2 text-[11px] flex items-center gap-2"
        style={{ background: "var(--surface-elevated)", color: "var(--muted-foreground)", border: "1px solid var(--border)" }}>
        <span>📂</span>
        <span>Registry en <code className="font-mono" style={{ color: "var(--foreground)" }}>frontend/registry/modules.json</code></span>
      </div>
    </div>
  );
}

function ModuleGroup({ label, modules, accent }: { label: string; modules: ProductModule[]; accent: string }) {
  if (modules.length === 0) return null;
  return (
    <div className="space-y-2">
      <div className="flex items-center gap-2">
        <span className="text-[11px] font-semibold uppercase tracking-wider" style={{ color: "var(--muted)" }}>
          {label}
        </span>
        <span className="badge" style={{ background: "var(--surface-elevated)", color: accent, border: "1px solid var(--border)", fontSize: "10px" }}>
          {modules.length}
        </span>
      </div>
      <div className="space-y-1.5">
        {modules.map(m => <ModuleCard key={m.id} module={m} accent={accent} />)}
      </div>
    </div>
  );
}

function ModuleCard({ module: m, accent }: { module: ProductModule; accent: string }) {
  return (
    <div className="rounded-lg p-3 space-y-2"
      style={{ background: "var(--surface-elevated)", border: "1px solid var(--border)" }}>
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-2 min-w-0">
          <span className="text-[14px] font-semibold truncate" style={{ color: "var(--foreground)" }}>
            {m.moduleName}
          </span>
          <LayerBadge layer={m.layer} />
        </div>
        <code className="text-[11px] shrink-0" style={{ color: accent }}>
          {m.routePath}
        </code>
      </div>

      <div className="flex flex-wrap gap-2 text-[11px]" style={{ color: "var(--muted-foreground)" }}>
        <MetaItem label="Entity"     value={m.entityName} />
        <MetaItem label="Controller" value={m.controllerName} />
      </div>
    </div>
  );
}

function LayerBadge({ layer }: { layer: string }) {
  const styles: Record<string, string> = {
    "full-stack": "var(--status-indigo-text)",
    "backend":    "var(--status-active-text)",
    "frontend":   "var(--status-warn-text)",
  };
  return (
    <span className="text-[10px] font-medium px-1.5 py-0.5 rounded"
      style={{ background: "var(--surface)", color: styles[layer] ?? "var(--muted)", border: "1px solid var(--border)" }}>
      {layer}
    </span>
  );
}

function MetaItem({ label, value }: { label: string; value: string }) {
  return (
    <span className="flex items-center gap-1">
      <span style={{ color: "var(--muted)" }}>{label}:</span>
      <code className="font-mono" style={{ color: "var(--foreground)" }}>{value}</code>
    </span>
  );
}
