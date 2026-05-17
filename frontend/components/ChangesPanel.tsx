"use client";

import { ScaffoldChange } from "@/lib/types";
import { Clock } from "lucide-react";

interface ChangesPanelProps {
  changes: ScaffoldChange[];
}

function timeAgo(iso: string) {
  const diff  = Date.now() - new Date(iso).getTime();
  const mins  = Math.floor(diff / 60000);
  const hours = Math.floor(diff / 3600000);
  if (mins < 1)   return "ahora";
  if (mins < 60)  return `${mins}m`;
  if (hours < 24) return `${hours}h`;
  return `${Math.floor(diff / 86400000)}d`;
}

const LAYER_COLOR: Record<string, string> = {
  backend:    "var(--accent)",
  frontend:   "#06b6d4",
  navigation: "#f59e0b",
  dashboard:  "#a855f7",
  registry:   "#64748b",
};

const LAYER_LABEL: Record<string, string> = {
  backend:    "Backend",
  frontend:   "Frontend",
  navigation: "Navegación",
  dashboard:  "Dashboard",
  registry:   "Registry",
};

const LAYER_ORDER = ["backend", "frontend", "navigation", "dashboard", "registry"];

const CHANGE_STYLE: Record<string, { bg: string; text: string; label: string }> = {
  created: { bg: "rgba(34,197,94,0.1)",    text: "#22c55e",       label: "creado"  },
  skipped: { bg: "rgba(139,146,165,0.1)",  text: "var(--muted)",  label: "saltado" },
};

function fileName(path: string): string {
  return path.replace(/\\/g, "/").split("/").pop() ?? path;
}

function groupByModule(changes: ScaffoldChange[]): Map<string, ScaffoldChange[]> {
  const map = new Map<string, ScaffoldChange[]>();
  for (const c of changes) {
    const key = c.moduleLabel || "Sin módulo";
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(c);
  }
  return map;
}

function groupByLayer(changes: ScaffoldChange[]): Map<string, ScaffoldChange[]> {
  const raw = new Map<string, ScaffoldChange[]>();
  for (const c of changes) {
    const key = c.layer || "backend";
    if (!raw.has(key)) raw.set(key, []);
    raw.get(key)!.push(c);
  }
  const ordered = new Map<string, ScaffoldChange[]>();
  for (const layer of LAYER_ORDER) {
    if (raw.has(layer)) ordered.set(layer, raw.get(layer)!);
  }
  for (const [key, val] of raw.entries()) {
    if (!ordered.has(key)) ordered.set(key, val);
  }
  return ordered;
}

export default function ChangesPanel({ changes }: ChangesPanelProps) {
  if (changes.length === 0) {
    return (
      <div style={{ padding: "48px 16px", textAlign: "center", color: "var(--muted)", fontSize: "13px" }}>
        <div style={{ fontSize: "24px", marginBottom: "8px" }}>⚡</div>
        Los cambios generados aparecerán aquí cuando envíes una feature request.
        <br />
        <span style={{ fontSize: "12px", marginTop: "6px", display: "block" }}>
          Ejemplo: &ldquo;Agregá membresías&rdquo; · &ldquo;Quiero alertas&rdquo; · &ldquo;Necesito reportes&rdquo;
        </span>
      </div>
    );
  }

  const grouped  = groupByModule(changes);
  const totalCreated = changes.filter(c => c.changeType === "created").length;
  const totalSkipped = changes.filter(c => c.changeType === "skipped").length;

  return (
    <div>
      {/* Summary bar */}
      <div
        style={{
          display:      "flex",
          gap:          "24px",
          padding:      "10px 16px",
          borderBottom: "1px solid var(--border)",
          fontSize:     "12px",
          color:        "var(--muted)",
        }}
      >
        <span><strong style={{ color: "#22c55e" }}>{totalCreated}</strong> creado(s)</span>
        <span><strong style={{ color: "var(--muted)" }}>{totalSkipped}</strong> saltado(s)</span>
        <span><strong style={{ color: "var(--foreground)" }}>{grouped.size}</strong> módulo(s)</span>
      </div>

      {/* Module groups */}
      <div style={{ padding: "8px 0" }}>
        {Array.from(grouped.entries()).map(([module, items]) => {
          const byLayer     = groupByLayer(items);
          const createdCount = items.filter(c => c.changeType === "created").length;
          const isBundle    = byLayer.size > 1;

          return (
            <div key={module} style={{ marginBottom: "4px" }}>
              {/* Module header */}
              <div
                style={{
                  padding:      "8px 16px",
                  fontSize:     "11px",
                  fontWeight:   "600",
                  color:        "var(--foreground)",
                  background:   "var(--surface)",
                  borderBottom: "1px solid var(--border)",
                  display:      "flex",
                  alignItems:   "center",
                  gap:          "8px",
                }}
              >
                <span style={{ color: "var(--accent)", fontSize: "14px" }}>◈</span>
                {module}
                {isBundle && (
                  <span
                    style={{
                      fontSize:     "9px",
                      padding:      "1px 6px",
                      borderRadius: "4px",
                      background:   "rgba(99,102,241,0.12)",
                      color:        "var(--accent)",
                      fontWeight:   600,
                    }}
                  >
                    bundle
                  </span>
                )}
                <span style={{ marginLeft: "auto", color: "var(--muted)", fontWeight: 400 }}>
                  {createdCount} creado(s)
                </span>
              </div>

              {/* Layer sections */}
              {Array.from(byLayer.entries()).map(([layer, layerItems]) => {
                const color = LAYER_COLOR[layer] ?? "var(--muted)";
                const label = LAYER_LABEL[layer] ?? layer;

                return (
                  <div key={layer}>
                    {/* Layer sub-header */}
                    <div
                      style={{
                        padding:       "4px 16px 4px 32px",
                        fontSize:      "10px",
                        fontWeight:    "600",
                        color:         color,
                        background:    `${color}0d`,
                        borderBottom:  "1px solid var(--border)",
                        display:       "flex",
                        alignItems:    "center",
                        gap:           "6px",
                        letterSpacing: "0.5px",
                        textTransform: "uppercase",
                      }}
                    >
                      <span
                        style={{
                          width:        "6px",
                          height:       "6px",
                          borderRadius: "50%",
                          background:   color,
                          display:      "inline-block",
                          flexShrink:   0,
                        }}
                      />
                      {label}
                    </div>

                    {/* File rows */}
                    {layerItems.map((change) => {
                      const style = CHANGE_STYLE[change.changeType] ?? CHANGE_STYLE.skipped;

                      return (
                        <div
                          key={change.id}
                          style={{
                            display:      "flex",
                            alignItems:   "center",
                            gap:          "10px",
                            padding:      "6px 16px 6px 48px",
                            fontSize:     "12px",
                            borderBottom: "1px solid var(--border)",
                          }}
                        >
                          <span
                            style={{
                              fontSize:      "9px",
                              padding:       "2px 6px",
                              borderRadius:  "4px",
                              background:    style.bg,
                              color:         style.text,
                              fontWeight:    600,
                              minWidth:      "52px",
                              textAlign:     "center",
                              letterSpacing: "0.3px",
                            }}
                          >
                            {style.label}
                          </span>

                          <span
                            style={{
                              flex:       1,
                              fontFamily: "monospace",
                              color:      change.changeType === "created" ? "var(--foreground)" : "var(--muted)",
                            }}
                          >
                            {fileName(change.targetPath)}
                          </span>

                          <div
                            style={{
                              display:    "flex",
                              alignItems: "center",
                              gap:        "3px",
                              color:      "var(--muted-foreground)",
                              minWidth:   "32px",
                            }}
                          >
                            <Clock size={9} />
                            <span style={{ fontSize: "10px" }}>{timeAgo(change.createdAt)}</span>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                );
              })}
            </div>
          );
        })}
      </div>
    </div>
  );
}
