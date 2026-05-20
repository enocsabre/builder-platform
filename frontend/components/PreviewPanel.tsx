"use client";

import { ProductDetail, PreviewStatus } from "@/lib/types";
import { ExternalLink, Play, Square, AlertCircle, Loader2, CheckCircle2, FolderCode } from "lucide-react";

interface PreviewPanelProps {
  product: ProductDetail;
  onStartPreview:  () => void;
  onStopPreview:   () => void;
  isActioning:     boolean;
  onOpenVSCode?:   () => void;
  vsCodeLoading?:  boolean;
  vsCodeError?:    string | null;
}

const STATUS_CONFIG: Record<PreviewStatus, {
  label:  string;
  color:  string;
  bg:     string;
  icon:   React.ReactNode;
}> = {
  stopped:  { label: "Detenido",   color: "var(--muted)",              bg: "rgba(139,146,165,0.1)", icon: <Square    size={12} /> },
  starting: { label: "Iniciando…", color: "var(--status-warn-text)",   bg: "var(--status-warn-bg)", icon: <Loader2   size={12} className="animate-spin" /> },
  running:  { label: "Activo",     color: "var(--status-active-text)", bg: "var(--status-active-bg)", icon: <CheckCircle2 size={12} /> },
  error:    { label: "Error",      color: "var(--status-danger-text)", bg: "var(--status-danger-bg)", icon: <AlertCircle  size={12} /> },
};

export default function PreviewPanel({ product, onStartPreview, onStopPreview, isActioning, onOpenVSCode, vsCodeLoading, vsCodeError }: PreviewPanelProps) {
  const status = (product.previewStatus ?? "stopped") as PreviewStatus;
  const cfg    = STATUS_CONFIG[status] ?? STATUS_CONFIG.stopped;
  const canStart  = status === "stopped" || status === "error";
  const canStop   = status === "running" || status === "starting";
  const scaffoldReady = product.scaffoldStatus === "complete";

  return (
    <div style={{ padding: "0" }}>
      {/* Header row */}
      <div
        style={{
          display:      "flex",
          alignItems:   "center",
          gap:          "12px",
          padding:      "12px 16px",
          borderBottom: "1px solid var(--border)",
        }}
      >
        {/* Status badge */}
        <span
          style={{
            display:       "flex",
            alignItems:    "center",
            gap:           "5px",
            fontSize:      "11px",
            fontWeight:    "600",
            padding:       "3px 8px",
            borderRadius:  "6px",
            background:    cfg.bg,
            color:         cfg.color,
          }}
        >
          {cfg.icon}
          {cfg.label}
        </span>

        {/* Port badge */}
        {status === "running" && product.previewPort && (
          <span style={{ fontSize: "11px", color: "var(--muted)", fontFamily: "monospace" }}>
            puerto {product.previewPort}
          </span>
        )}

        {/* Action buttons */}
        <div style={{ marginLeft: "auto", display: "flex", gap: "8px" }}>
          {canStart && (
            <button
              onClick={onStartPreview}
              disabled={!scaffoldReady || isActioning}
              style={{
                display:       "flex",
                alignItems:    "center",
                gap:           "6px",
                padding:       "6px 14px",
                borderRadius:  "8px",
                border:        "none",
                background:    scaffoldReady && !isActioning ? "var(--accent)" : "var(--surface-elevated)",
                color:         scaffoldReady && !isActioning ? "#fff" : "var(--muted)",
                fontSize:      "12px",
                fontWeight:    "600",
                cursor:        scaffoldReady && !isActioning ? "pointer" : "not-allowed",
              }}
            >
              <Play size={12} />
              Iniciar preview
            </button>
          )}

          {canStop && (
            <button
              onClick={onStopPreview}
              disabled={isActioning}
              style={{
                display:      "flex",
                alignItems:   "center",
                gap:          "6px",
                padding:      "6px 14px",
                borderRadius: "8px",
                border:       "1px solid var(--border)",
                background:   "var(--surface-elevated)",
                color:        "var(--muted)",
                fontSize:     "12px",
                fontWeight:   "600",
                cursor:       isActioning ? "not-allowed" : "pointer",
              }}
            >
              <Square size={12} />
              Detener
            </button>
          )}

          {status === "running" && product.previewUrl && (
            <a
              href={product.previewUrl}
              target="_blank"
              rel="noopener noreferrer"
              style={{
                display:      "flex",
                alignItems:   "center",
                gap:          "6px",
                padding:      "6px 14px",
                borderRadius: "8px",
                border:       "none",
                background:   "var(--status-active-bg)",
                color:        "var(--status-active-text)",
                fontSize:     "12px",
                fontWeight:   "600",
                cursor:       "pointer",
                textDecoration: "none",
              }}
            >
              <ExternalLink size={12} />
              Abrir SaaS
            </a>
          )}

          {scaffoldReady && onOpenVSCode && (
            <button
              onClick={onOpenVSCode}
              disabled={vsCodeLoading}
              title={product.projectPath ?? ""}
              style={{
                display:      "flex",
                alignItems:   "center",
                gap:          "6px",
                padding:      "6px 14px",
                borderRadius: "8px",
                border:       "1px solid var(--border)",
                background:   vsCodeLoading ? "var(--surface-elevated)" : "var(--surface)",
                color:        vsCodeLoading ? "var(--muted)" : "var(--foreground)",
                fontSize:     "12px",
                fontWeight:   "600",
                cursor:       vsCodeLoading ? "not-allowed" : "pointer",
              }}
            >
              {vsCodeLoading ? <Loader2 size={12} className="animate-spin" /> : <FolderCode size={12} />}
              {vsCodeLoading ? "Abriendo…" : "VS Code"}
            </button>
          )}
        </div>
      </div>

      {/* Info section */}
      <div style={{ padding: "16px" }}>
        {/* Not ready */}
        {!scaffoldReady && (
          <InfoBox type="warning">
            El scaffold debe estar completo antes de iniciar el preview.
            Aprobá el pipeline hasta que el ScaffoldStatus sea &ldquo;complete&rdquo;.
          </InfoBox>
        )}

        {/* Running — URL card */}
        {status === "running" && product.previewUrl && (
          <div
            style={{
              padding:      "14px 16px",
              background:   "var(--surface)",
              borderRadius: "10px",
              border:       "1px solid var(--border)",
              marginBottom: "12px",
            }}
          >
            <p style={{ fontSize: "11px", color: "var(--muted)", marginBottom: "4px", textTransform: "uppercase", letterSpacing: "0.5px" }}>
              URL del preview
            </p>
            <a
              href={product.previewUrl}
              target="_blank"
              rel="noopener noreferrer"
              style={{ fontSize: "14px", fontFamily: "monospace", color: "var(--accent)", textDecoration: "none" }}
            >
              {product.previewUrl}
            </a>
          </div>
        )}

        {/* Starting */}
        {status === "starting" && (
          <InfoBox type="info">
            Iniciando servidor Next.js del proyecto generado.
            Si es la primera vez, el proceso instala dependencias (npm install) — puede tardar 60-120 segundos.
          </InfoBox>
        )}

        {/* Error */}
        {status === "error" && product.previewError && (
          <div
            style={{
              padding:      "12px 14px",
              background:   "var(--status-danger-bg)",
              border:       "1px solid rgba(248,113,113,0.2)",
              borderRadius: "8px",
              marginBottom: "12px",
            }}
          >
            <p style={{ fontSize: "11px", fontWeight: "600", color: "var(--status-danger-text)", marginBottom: "6px" }}>
              Error al iniciar preview
            </p>
            <pre
              style={{
                fontSize:    "11px",
                color:       "var(--status-danger-text)",
                whiteSpace:  "pre-wrap",
                wordBreak:   "break-all",
                margin:      0,
                fontFamily:  "monospace",
                opacity:     0.85,
              }}
            >
              {product.previewError}
            </pre>
          </div>
        )}

        {/* VS Code error */}
        {vsCodeError && (
          <div
            style={{
              padding:      "12px 14px",
              background:   "var(--status-warn-bg)",
              border:       "1px solid rgba(251,191,36,0.2)",
              borderRadius: "8px",
              marginBottom: "12px",
            }}
          >
            <p style={{ fontSize: "11px", fontWeight: "600", color: "var(--status-warn-text)", marginBottom: "4px" }}>
              VS Code no disponible
            </p>
            <p style={{ fontSize: "11px", color: "var(--status-warn-text)", lineHeight: "1.6" }}>
              {vsCodeError}
            </p>
          </div>
        )}

        {/* Details grid */}
        {scaffoldReady && (
          <div
            style={{
              display:       "grid",
              gridTemplateColumns: "1fr 1fr",
              gap:           "8px",
              marginTop:     status === "stopped" ? 0 : "12px",
            }}
          >
            <InfoRow label="Proyecto" value={product.name} />
            <InfoRow label="Scaffold" value={product.scaffoldStatus} />
            <InfoRow label="Puerto"   value={product.previewPort ? String(product.previewPort) : "—"} />
            <InfoRow label="Último inicio"
              value={product.previewLastStartedAt
                ? new Date(product.previewLastStartedAt).toLocaleTimeString("es-CR")
                : "—"} />
          </div>
        )}

        {/* Hint */}
        {status === "stopped" && scaffoldReady && (
          <p style={{ fontSize: "12px", color: "var(--muted)", marginTop: "16px", lineHeight: "1.6" }}>
            El preview corre el frontend generado con <code style={{ fontFamily: "monospace", background: "var(--surface-elevated)", padding: "1px 4px", borderRadius: "3px" }}>npm run dev</code> en un puerto local.
            Podés navegar el dashboard y los módulos generados, y los cambios de código se reflejan automáticamente.
          </p>
        )}
      </div>
    </div>
  );
}

function InfoBox({ type, children }: { type: "info" | "warning"; children: React.ReactNode }) {
  const colors = type === "warning"
    ? { bg: "var(--status-warn-bg)",  border: "rgba(251,191,36,0.2)",  text: "var(--status-warn-text)"  }
    : { bg: "var(--status-info-bg)",  border: "rgba(96,165,250,0.2)",  text: "var(--status-info-text)"  };
  return (
    <div style={{ padding: "10px 14px", background: colors.bg, border: `1px solid ${colors.border}`, borderRadius: "8px", marginBottom: "12px" }}>
      <p style={{ fontSize: "12px", color: colors.text, lineHeight: "1.6" }}>{children}</p>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ padding: "10px 12px", background: "var(--surface)", borderRadius: "8px", border: "1px solid var(--border)" }}>
      <p style={{ fontSize: "10px", color: "var(--muted)", marginBottom: "3px", textTransform: "uppercase", letterSpacing: "0.4px" }}>{label}</p>
      <p style={{ fontSize: "12px", fontFamily: "monospace", color: "var(--foreground)" }}>{value}</p>
    </div>
  );
}
