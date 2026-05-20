"use client";
import { useState } from "react";
import {
  Rocket, ExternalLink, Clock, GitBranch, GitCommit,
  CheckCircle, XCircle, Loader, ChevronDown, ChevronRight, AlertTriangle,
} from "lucide-react";
import type { ProductDetail, DeployRunSummary } from "@/lib/types";

const API = "http://localhost:5238";

function timeAgo(iso: string) {
  const diff  = Date.now() - new Date(iso).getTime();
  const mins  = Math.floor(diff / 60000);
  const hours = Math.floor(diff / 3600000);
  const days  = Math.floor(diff / 86400000);
  if (mins < 1)   return "ahora";
  if (mins < 60)  return `${mins}m`;
  if (hours < 24) return `${hours}h`;
  return `${days}d`;
}

function duration(start: string, end: string | null) {
  if (!end) return "en progreso";
  const secs = Math.floor((new Date(end).getTime() - new Date(start).getTime()) / 1000);
  if (secs < 60)  return `${secs}s`;
  return `${Math.floor(secs / 60)}m ${secs % 60}s`;
}

// ── Status banner ─────────────────────────────────────────────────────────────

function DeployStatusBanner({ status, deployUrl }: { status: string; deployUrl: string | null }) {
  const configs: Record<string, { label: string; bg: string; color: string; icon: React.ReactNode }> = {
    not_deployed: {
      label: "Sin deployar",
      bg: "var(--surface-elevated)",
      color: "var(--muted)",
      icon: <Rocket size={16} />,
    },
    preparing: {
      label: "Preparando…",
      bg: "rgba(99,102,241,0.1)",
      color: "var(--status-indigo-text)",
      icon: <Loader size={16} className="animate-spin" />,
    },
    building: {
      label: "Ejecutando quality gates…",
      bg: "rgba(251,191,36,0.1)",
      color: "var(--status-warn-text)",
      icon: <Loader size={16} className="animate-spin" />,
    },
    deploying: {
      label: "Deployando a Vercel…",
      bg: "rgba(99,102,241,0.1)",
      color: "var(--status-indigo-text)",
      icon: <Loader size={16} className="animate-spin" />,
    },
    deployed: {
      label: "Deployado",
      bg: "rgba(52,211,153,0.1)",
      color: "var(--status-active-text)",
      icon: <CheckCircle size={16} />,
    },
    failed: {
      label: "Deploy fallido",
      bg: "rgba(239,68,68,0.1)",
      color: "var(--status-danger-text)",
      icon: <XCircle size={16} />,
    },
    recovering: {
      label: "Recuperando…",
      bg: "rgba(251,191,36,0.1)",
      color: "var(--status-warn-text)",
      icon: <Loader size={16} className="animate-spin" />,
    },
  };

  const cfg = configs[status] ?? configs.not_deployed;

  return (
    <div
      className="rounded-xl p-4 flex items-center justify-between gap-3"
      style={{ background: cfg.bg, border: `1px solid ${cfg.color}30` }}
    >
      <div className="flex items-center gap-2.5" style={{ color: cfg.color }}>
        {cfg.icon}
        <span className="text-[13px] font-semibold">{cfg.label}</span>
      </div>
      {status === "deployed" && deployUrl && (
        <a
          href={deployUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[12px] font-medium"
          style={{ background: "var(--status-active-bg)", color: "var(--status-active-text)" }}
        >
          <ExternalLink size={12} />Abrir producción
        </a>
      )}
    </div>
  );
}

// ── Deploy run row ────────────────────────────────────────────────────────────

function DeployRunRow({ run, productId }: { run: DeployRunSummary; productId: string }) {
  const [expanded, setExpanded] = useState(false);
  const [logs, setLogs]         = useState<string | null>(null);
  const [loadingLogs, setLoadingLogs] = useState(false);

  const isRunning = run.status === "running";
  const passed    = run.status === "passed";

  async function loadLogs() {
    if (logs !== null) { setExpanded(!expanded); return; }
    setExpanded(true);
    setLoadingLogs(true);
    try {
      const res  = await fetch(`${API}/api/products/${productId}/deployments/${run.id}`);
      const data = await res.json();
      setLogs(data.logs ?? "(sin logs)");
    } catch {
      setLogs("Error cargando logs.");
    } finally {
      setLoadingLogs(false);
    }
  }

  return (
    <div
      className="rounded-xl overflow-hidden"
      style={{ background: "var(--surface-elevated)", border: "1px solid var(--border)" }}
    >
      <button
        className="w-full text-left p-4 flex items-start gap-3"
        onClick={loadLogs}
      >
        <div className="shrink-0 mt-0.5">
          {isRunning ? <Loader size={15} className="animate-spin" style={{ color: "var(--status-indigo-text)" }} />
          : passed   ? <CheckCircle size={15} style={{ color: "var(--status-active-text)" }} />
                     : <XCircle    size={15} style={{ color: "var(--status-danger-text)" }} />}
        </div>

        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-[12px] font-semibold" style={{ color: "var(--foreground)" }}>
              {passed ? "Deploy exitoso" : isRunning ? "Deploy en progreso" : "Deploy fallido"}
            </span>
            {run.deployUrl && (
              <a
                href={run.deployUrl}
                target="_blank"
                rel="noopener noreferrer"
                onClick={e => e.stopPropagation()}
                className="flex items-center gap-1 text-[11px]"
                style={{ color: "var(--status-active-text)" }}
              >
                <ExternalLink size={10} />{run.deployUrl.replace("https://", "")}
              </a>
            )}
          </div>
          <div className="flex items-center gap-3 mt-1 flex-wrap">
            {run.branch && (
              <span className="flex items-center gap-1 text-[11px]" style={{ color: "var(--muted)" }}>
                <GitBranch size={10} />{run.branch}
              </span>
            )}
            {run.commitHash && (
              <span className="flex items-center gap-1 text-[11px]" style={{ color: "var(--muted)" }}>
                <GitCommit size={10} />{run.commitHash}
              </span>
            )}
            <span className="flex items-center gap-1 text-[11px]" style={{ color: "var(--muted-foreground)" }}>
              <Clock size={10} />{timeAgo(run.startedAt)} · {duration(run.startedAt, run.finishedAt)}
            </span>
          </div>
        </div>

        <div className="shrink-0 ml-1" style={{ color: "var(--muted)" }}>
          {expanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
        </div>
      </button>

      {expanded && (
        <div className="border-t px-4 pb-4 pt-3" style={{ borderColor: "var(--border)" }}>
          {loadingLogs ? (
            <p className="text-[12px]" style={{ color: "var(--muted)" }}>Cargando logs…</p>
          ) : (
            <pre
              className="text-[11px] whitespace-pre-wrap leading-relaxed overflow-auto max-h-64 rounded-lg p-3"
              style={{ background: "var(--surface)", color: "var(--muted)", fontFamily: "ui-monospace, monospace" }}
            >
              {logs}
            </pre>
          )}
        </div>
      )}
    </div>
  );
}

// ── Deploy Panel ──────────────────────────────────────────────────────────────

export default function DeployPanel({
  product,
  onRefresh,
}: {
  product: ProductDetail;
  onRefresh: () => void;
}) {
  const [deploying, setDeploying] = useState(false);
  const [error, setError]         = useState<string | null>(null);

  const isDeploying = ["preparing", "building", "deploying"].includes(product.deployStatus);
  const canDeploy   =
    product.scaffoldStatus === "complete" &&
    product.runtimeHealth  !== "broken"   &&
    !isDeploying;

  async function triggerDeploy() {
    if (!canDeploy || deploying) return;
    setError(null);
    setDeploying(true);
    try {
      const res = await fetch(`${API}/api/products/${product.id}/deploy`, { method: "POST" });
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        setError(body.error ?? `Error ${res.status}`);
      } else {
        // Poll until deploy state changes
        await onRefresh();
        setTimeout(onRefresh, 3000);
        setTimeout(onRefresh, 8000);
        setTimeout(onRefresh, 20000);
      }
    } catch {
      setError("Error de conexión con el backend.");
    } finally {
      setDeploying(false);
    }
  }

  return (
    <div className="p-4 space-y-4">
      {/* Status banner */}
      <DeployStatusBanner status={product.deployStatus} deployUrl={product.deployUrl} />

      {/* Git info */}
      {(product.deployBranch || product.deployCommitHash) && (
        <div
          className="rounded-xl p-3 flex items-center gap-4"
          style={{ background: "var(--surface-elevated)", border: "1px solid var(--border)" }}
        >
          {product.deployBranch && (
            <span className="flex items-center gap-1.5 text-[12px]" style={{ color: "var(--muted)" }}>
              <GitBranch size={13} />{product.deployBranch}
            </span>
          )}
          {product.deployCommitHash && (
            <span className="flex items-center gap-1.5 text-[12px]" style={{ color: "var(--muted)" }}>
              <GitCommit size={13} />{product.deployCommitHash}
            </span>
          )}
          {product.deployedAt && (
            <span className="flex items-center gap-1.5 text-[12px]" style={{ color: "var(--muted-foreground)" }}>
              <Clock size={13} />Deployado {timeAgo(product.deployedAt)}
            </span>
          )}
        </div>
      )}

      {/* Deploy button */}
      <div className="space-y-2">
        {!canDeploy && !isDeploying && (
          <div
            className="rounded-lg px-3 py-2 flex items-start gap-2 text-[12px]"
            style={{ background: "rgba(251,191,36,0.08)", color: "var(--status-warn-text)" }}
          >
            <AlertTriangle size={14} className="shrink-0 mt-0.5" />
            <span>
              {product.scaffoldStatus !== "complete"
                ? "El scaffold debe estar completo antes de deployar."
                : product.runtimeHealth === "broken"
                ? "Runtime health es broken. Ejecutá validación primero."
                : "Deploy no disponible en este momento."}
            </span>
          </div>
        )}

        <button
          onClick={triggerDeploy}
          disabled={!canDeploy || deploying || isDeploying}
          className="w-full flex items-center justify-center gap-2 py-3 rounded-xl text-[13px] font-semibold transition-all"
          style={{
            background: canDeploy && !deploying && !isDeploying
              ? "var(--accent)"
              : "var(--surface-elevated)",
            color: canDeploy && !deploying && !isDeploying
              ? "#fff"
              : "var(--muted)",
            border: "1px solid transparent",
            cursor: canDeploy && !deploying && !isDeploying ? "pointer" : "not-allowed",
          }}
        >
          {deploying || isDeploying
            ? <><Loader size={15} className="animate-spin" />Iniciando deploy…</>
            : <><Rocket size={15} />Deploy a producción</>}
        </button>

        {error && (
          <p className="text-[12px] text-center" style={{ color: "var(--status-danger-text)" }}>{error}</p>
        )}

        <p className="text-[11px] text-center" style={{ color: "var(--muted-foreground)" }}>
          El deploy ejecuta quality gates + next build antes de publicar. Requiere confirmación explícita.
        </p>
      </div>

      {/* Run history */}
      {(product.deployRuns?.length ?? 0) > 0 && (
        <div className="space-y-2">
          <p className="text-[11px] font-semibold uppercase tracking-wider px-1" style={{ color: "var(--muted)" }}>
            Historial ({product.deployRuns.length})
          </p>
          {product.deployRuns.map(run => (
            <DeployRunRow key={run.id} run={run} productId={product.id} />
          ))}
        </div>
      )}
    </div>
  );
}
