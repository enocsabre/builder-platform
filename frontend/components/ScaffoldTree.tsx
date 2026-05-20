"use client";

import { ScaffoldEntry, ScaffoldStatus } from "@/lib/types";
import { FolderCode, Loader2 } from "lucide-react";

interface ScaffoldTreeProps {
  entries: ScaffoldEntry[];
  scaffoldStatus: ScaffoldStatus;
  projectPath: string | null;
  onOpenVSCode?:  () => void;
  vsCodeLoading?: boolean;
}

const LANG_COLOR: Record<string, string> = {
  csharp:     "var(--accent)",
  typescript: "#3b82f6",
  tsx:        "#06b6d4",
  json:       "#f59e0b",
  css:        "#a855f7",
  markdown:   "var(--muted)",
  xml:        "#10b981",
};

const LANG_LABEL: Record<string, string> = {
  csharp:     "C#",
  typescript: "TS",
  tsx:        "TSX",
  json:       "JSON",
  css:        "CSS",
  markdown:   "MD",
  xml:        "XML",
};

function fileIcon(lang: string | null): string {
  switch (lang) {
    case "csharp":     return "⬡";
    case "typescript":
    case "tsx":        return "◈";
    case "json":       return "◎";
    case "css":        return "◇";
    case "markdown":   return "◻";
    default:           return "◦";
  }
}

interface TreeNode {
  name:     string;
  fullPath: string;
  entry:    ScaffoldEntry | null;
  children: Map<string, TreeNode>;
}

function buildTree(entries: ScaffoldEntry[]): TreeNode {
  const root: TreeNode = { name: "", fullPath: "", entry: null, children: new Map() };

  for (const entry of entries) {
    const parts = entry.relativePath.replace(/\\/g, "/").split("/");
    let node = root;
    for (let i = 0; i < parts.length; i++) {
      const part = parts[i];
      if (!node.children.has(part)) {
        node.children.set(part, {
          name:     part,
          fullPath: parts.slice(0, i + 1).join("/"),
          entry:    null,
          children: new Map(),
        });
      }
      node = node.children.get(part)!;
    }
    node.entry = entry;
  }

  return root;
}

function TreeNodeRow({ node, depth }: { node: TreeNode; depth: number }) {
  const isDir = node.children.size > 0;
  const lang  = node.entry?.language ?? null;
  const color = lang ? (LANG_COLOR[lang] ?? "var(--muted)") : "var(--muted)";
  const badge = lang ? (LANG_LABEL[lang] ?? lang.toUpperCase()) : null;

  return (
    <>
      <div
        style={{
          display:     "flex",
          alignItems:  "center",
          gap:         "6px",
          padding:     "3px 12px",
          paddingLeft: `${12 + depth * 16}px`,
          fontSize:    "12px",
          color:       isDir ? "var(--foreground)" : "var(--muted)",
          borderRadius: "4px",
        }}
      >
        <span style={{ color: isDir ? "var(--accent)" : color, fontSize: "10px", minWidth: "12px" }}>
          {isDir ? "▸" : fileIcon(lang)}
        </span>
        <span style={{ flex: 1, fontFamily: "monospace" }}>{node.name}</span>
        {badge && (
          <span
            style={{
              fontSize:        "9px",
              padding:         "1px 5px",
              borderRadius:    "3px",
              background:      `${color}22`,
              color:           color,
              fontWeight:      600,
              letterSpacing:   "0.5px",
              textTransform:   "uppercase",
            }}
          >
            {badge}
          </span>
        )}
      </div>
      {Array.from(node.children.values()).map((child) => (
        <TreeNodeRow key={child.fullPath} node={child} depth={depth + 1} />
      ))}
    </>
  );
}

export default function ScaffoldTree({ entries, scaffoldStatus, projectPath, onOpenVSCode, vsCodeLoading }: ScaffoldTreeProps) {
  if (scaffoldStatus === "none") {
    return (
      <div style={{ padding: "32px", textAlign: "center", color: "var(--muted)", fontSize: "13px" }}>
        El scaffold se generará automáticamente cuando apruebes los artefactos de planificación.
      </div>
    );
  }

  if (scaffoldStatus === "generating") {
    return (
      <div style={{ padding: "32px", textAlign: "center", color: "var(--muted)", fontSize: "13px" }}>
        <div style={{ marginBottom: "8px", fontSize: "20px" }}>⚙</div>
        Generando estructura del proyecto…
      </div>
    );
  }

  if (scaffoldStatus === "error") {
    return (
      <div style={{ padding: "32px", textAlign: "center", color: "var(--error, #ef4444)", fontSize: "13px" }}>
        Error al generar el scaffold. Revisá los logs de actividad.
      </div>
    );
  }

  const tree = buildTree(entries);
  const fileCount   = entries.filter((e) => e.entryType === "file").length;
  const langs       = [...new Set(entries.map((e) => e.language).filter(Boolean))];

  return (
    <div>
      {/* Stats bar */}
      <div
        style={{
          display:        "flex",
          gap:            "24px",
          padding:        "12px 16px",
          borderBottom:   "1px solid var(--border)",
          fontSize:       "12px",
          color:          "var(--muted)",
        }}
      >
        <span>
          <strong style={{ color: "var(--foreground)" }}>{fileCount}</strong> archivos
        </span>
        <span>
          <strong style={{ color: "var(--foreground)" }}>{langs.length}</strong> lenguajes
        </span>
        {projectPath && (
          <span style={{ marginLeft: "auto", fontFamily: "monospace", fontSize: "11px", color: "var(--muted)" }}>
            {projectPath}
          </span>
        )}
        {onOpenVSCode && (
          <button
            onClick={onOpenVSCode}
            disabled={vsCodeLoading}
            title="Abrir proyecto en VS Code"
            style={{
              display:      "flex",
              alignItems:   "center",
              gap:          "5px",
              padding:      "4px 10px",
              borderRadius: "6px",
              border:       "1px solid var(--border)",
              background:   "var(--surface)",
              color:        vsCodeLoading ? "var(--muted)" : "var(--foreground)",
              fontSize:     "11px",
              fontWeight:   "600",
              cursor:       vsCodeLoading ? "not-allowed" : "pointer",
              marginLeft:   projectPath ? "8px" : "auto",
            }}
          >
            {vsCodeLoading
              ? <Loader2 size={11} style={{ animation: "spin 1s linear infinite" }} />
              : <FolderCode size={11} />
            }
            {vsCodeLoading ? "Abriendo…" : "Abrir en VS Code"}
          </button>
        )}
      </div>

      {/* File tree */}
      <div style={{ padding: "8px 0" }}>
        {Array.from(tree.children.values()).map((child) => (
          <TreeNodeRow key={child.fullPath} node={child} depth={0} />
        ))}
      </div>
    </div>
  );
}
