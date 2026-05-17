"use client";

import { useState, useEffect } from "react";
import { ProductDetail, ManagedFile, FileRevision } from "@/lib/types";
import { Lock, Pencil, RefreshCw, FileCode, FileJson, FileCog } from "lucide-react";

const API = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5238";

interface FilesPanelProps {
  product: ProductDetail;
}

const FILE_TYPE_ICON: Record<string, React.ReactNode> = {
  json: <FileJson size={13} />,
  tsx:  <FileCode size={13} />,
  css:  <FileCog  size={13} />,
};

const PATCH_TYPE_LABEL: Record<string, string> = {
  nav_reorder:          "Nav reorder",
  dashboard_premium:    "Dashboard premium",
  dashboard_quick_stats:"Quick stats",
};

export default function FilesPanel({ product }: FilesPanelProps) {
  const [managedFiles, setManagedFiles] = useState<ManagedFile[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedRevision, setSelectedRevision] = useState<{
    id: string; relativePath: string; patchType: string; reason: string;
    beforeContent: string | null; afterContent: string; createdAt: string;
  } | null>(null);
  const [diffLoading, setDiffLoading] = useState(false);

  const load = async () => {
    setLoading(true);
    try {
      const res = await fetch(`${API}/api/products/${product.id}/runtime-files`);
      if (res.ok) setManagedFiles(await res.json());
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, [product.id]);

  const loadRevision = async (rev: FileRevision) => {
    setDiffLoading(true);
    try {
      const res = await fetch(`${API}/api/products/${product.id}/revisions/${rev.id}`);
      if (res.ok) setSelectedRevision(await res.json());
    } finally {
      setDiffLoading(false);
    }
  };

  const recentRevisions = product.fileRevisions.slice(0, 10);

  return (
    <div style={{ padding: "16px" }}>

      {/* Section: Managed Files */}
      <div style={{ marginBottom: "24px" }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "12px" }}>
          <p style={{ fontSize: "11px", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.5px", fontWeight: "600" }}>
            Archivos Runtime-Managed
          </p>
          <button
            onClick={load}
            disabled={loading}
            style={{ background: "none", border: "none", color: "var(--muted)", cursor: "pointer", padding: "2px 4px" }}
          >
            <RefreshCw size={12} style={{ transform: loading ? "rotate(360deg)" : "none", transition: "transform 0.5s" }} />
          </button>
        </div>

        {!product.projectPath ? (
          <InfoBox type="warning">El scaffold debe estar completo para ver los archivos managed.</InfoBox>
        ) : managedFiles.length === 0 && !loading ? (
          <InfoBox type="info">No se encontraron archivos managed. El scaffold puede estar incompleto.</InfoBox>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: "4px" }}>
            {managedFiles.map((f) => (
              <div
                key={f.relativePath}
                style={{
                  display:       "flex",
                  alignItems:    "center",
                  gap:           "10px",
                  padding:       "9px 12px",
                  background:    f.exists ? "var(--surface)" : "var(--surface-elevated)",
                  borderRadius:  "8px",
                  border:        "1px solid var(--border)",
                  opacity:       f.exists ? 1 : 0.5,
                }}
              >
                <span style={{ color: "var(--muted)", flexShrink: 0 }}>
                  {FILE_TYPE_ICON[f.fileType] ?? <FileCode size={13} />}
                </span>

                <div style={{ flex: 1, minWidth: 0 }}>
                  <p style={{ fontSize: "12px", color: "var(--foreground)", fontWeight: "600", marginBottom: "1px" }}>
                    {f.displayName}
                  </p>
                  <p style={{ fontSize: "10px", color: "var(--muted)", fontFamily: "monospace", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                    {f.relativePath}
                  </p>
                </div>

                <div style={{ display: "flex", alignItems: "center", gap: "8px", flexShrink: 0 }}>
                  {f.revisionCount > 0 && (
                    <span style={{
                      fontSize: "10px", fontWeight: "700", padding: "1px 6px",
                      borderRadius: "10px", background: "var(--status-info-bg)", color: "var(--status-info-text)",
                    }}>
                      {f.revisionCount} rev
                    </span>
                  )}

                  {f.lastModified && (
                    <span style={{ fontSize: "10px", color: "var(--muted)" }}>
                      {new Date(f.lastModified).toLocaleTimeString("es-CR", { hour: "2-digit", minute: "2-digit" })}
                    </span>
                  )}

                  {f.isEditable
                    ? <Pencil size={11} style={{ color: "var(--status-active-text)" }} />
                    : <Lock   size={11} style={{ color: "var(--muted)" }} />
                  }
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Section: Recent Revisions */}
      <div>
        <p style={{ fontSize: "11px", color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.5px", fontWeight: "600", marginBottom: "12px" }}>
          Revisiones ({recentRevisions.length})
        </p>

        {recentRevisions.length === 0 ? (
          <InfoBox type="info">
            Ninguna revisión aún. Enviá &ldquo;haz el dashboard más premium&rdquo; o
            &ldquo;pon el dashboard primero en el sidebar&rdquo; para ver el runtime en acción.
          </InfoBox>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
            {recentRevisions.map((r) => (
              <div
                key={r.id}
                style={{
                  padding:      "10px 14px",
                  background:   "var(--surface)",
                  borderRadius: "8px",
                  border:       "1px solid var(--border)",
                }}
              >
                <div style={{ display: "flex", alignItems: "center", gap: "8px", marginBottom: "4px" }}>
                  <span style={{
                    fontSize: "10px", fontWeight: "700", padding: "1px 6px",
                    borderRadius: "4px", background: "var(--status-warn-bg)", color: "var(--status-warn-text)",
                    textTransform: "uppercase",
                  }}>
                    {PATCH_TYPE_LABEL[r.patchType] ?? r.patchType}
                  </span>
                  <span style={{ fontSize: "10px", color: "var(--muted)", marginLeft: "auto" }}>
                    {new Date(r.createdAt).toLocaleString("es-CR", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })}
                  </span>
                </div>

                <p style={{ fontSize: "11px", fontFamily: "monospace", color: "var(--muted)", marginBottom: "6px", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                  {r.relativePath}
                </p>

                <p style={{ fontSize: "12px", color: "var(--foreground)" }}>{r.reason}</p>

                {r.hasDiff && (
                  <button
                    onClick={() => loadRevision(r)}
                    style={{
                      marginTop: "8px", fontSize: "11px", color: "var(--accent)",
                      background: "none", border: "none", cursor: "pointer", padding: 0,
                    }}
                  >
                    Ver diff →
                  </button>
                )}
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Diff modal */}
      {selectedRevision && (
        <div
          style={{
            position: "fixed", inset: 0, background: "rgba(0,0,0,0.7)", zIndex: 1000,
            display: "flex", alignItems: "center", justifyContent: "center", padding: "24px",
          }}
          onClick={() => setSelectedRevision(null)}
        >
          <div
            style={{
              background: "var(--surface)", borderRadius: "12px", border: "1px solid var(--border)",
              width: "min(90vw, 900px)", maxHeight: "80vh", overflow: "hidden", display: "flex", flexDirection: "column",
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ padding: "16px 20px", borderBottom: "1px solid var(--border)", display: "flex", alignItems: "center", gap: "12px" }}>
              <div style={{ flex: 1 }}>
                <p style={{ fontSize: "13px", fontWeight: "700", color: "var(--foreground)", marginBottom: "2px" }}>
                  {PATCH_TYPE_LABEL[selectedRevision.patchType] ?? selectedRevision.patchType}
                </p>
                <p style={{ fontSize: "11px", fontFamily: "monospace", color: "var(--muted)" }}>{selectedRevision.relativePath}</p>
              </div>
              <button onClick={() => setSelectedRevision(null)} style={{ background: "none", border: "none", color: "var(--muted)", cursor: "pointer", fontSize: "18px" }}>
                ×
              </button>
            </div>

            {diffLoading ? (
              <div style={{ padding: "40px", textAlign: "center", color: "var(--muted)", fontSize: "13px" }}>Cargando diff...</div>
            ) : (
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", overflow: "hidden", flex: 1 }}>
                <DiffPane label="Antes" content={selectedRevision.beforeContent} color="var(--status-danger-bg)" />
                <DiffPane label="Después" content={selectedRevision.afterContent} color="var(--status-active-bg)" />
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

function DiffPane({ label, content, color }: { label: string; content: string | null; color: string }) {
  return (
    <div style={{ display: "flex", flexDirection: "column", borderRight: "1px solid var(--border)", overflow: "hidden" }}>
      <div style={{ padding: "8px 12px", background: color, borderBottom: "1px solid var(--border)" }}>
        <p style={{ fontSize: "11px", fontWeight: "700", color: "var(--foreground)" }}>{label}</p>
      </div>
      <pre style={{
        flex: 1, overflow: "auto", margin: 0, padding: "12px",
        fontSize: "11px", lineHeight: "1.6", fontFamily: "monospace",
        color: "var(--foreground)", whiteSpace: "pre-wrap", wordBreak: "break-all",
      }}>
        {content ?? "(sin contenido previo)"}
      </pre>
    </div>
  );
}

function InfoBox({ type, children }: { type: "info" | "warning"; children: React.ReactNode }) {
  const colors = type === "warning"
    ? { bg: "var(--status-warn-bg)",  border: "rgba(251,191,36,0.2)",  text: "var(--status-warn-text)"  }
    : { bg: "var(--status-info-bg)",  border: "rgba(96,165,250,0.2)",  text: "var(--status-info-text)"  };
  return (
    <div style={{ padding: "10px 14px", background: colors.bg, border: `1px solid ${colors.border}`, borderRadius: "8px" }}>
      <p style={{ fontSize: "12px", color: colors.text, lineHeight: "1.6" }}>{children}</p>
    </div>
  );
}
