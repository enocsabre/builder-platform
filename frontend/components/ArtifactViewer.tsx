"use client";

import { useState, useEffect } from "react";
import { X, CheckCircle, FileText, GitBranch, Database, Map, Layers } from "lucide-react";
import { api } from "@/lib/api";
import type { Artifact, ArtifactType } from "@/lib/types";
import { MarkdownRenderer } from "./MarkdownRenderer";

interface Props {
  productId: string;
  artifactId: string;
  onClose: () => void;
  onApproved?: (artifactId: string) => void;
}

const TYPE_META: Record<ArtifactType, { label: string; icon: React.ElementType; color: string }> = {
  brief:        { label: "Product Brief",    icon: FileText,  color: "var(--status-info)" },
  architecture: { label: "Architecture",     icon: GitBranch, color: "var(--status-active)" },
  db_schema:    { label: "DB Schema",        icon: Database,  color: "var(--status-warn)" },
  roadmap:      { label: "Roadmap",          icon: Map,       color: "#a78bfa" },
  sprint_plan:  { label: "Sprint Plan",      icon: Layers,    color: "var(--status-active)" },
};

const STATUS_STYLE: Record<string, string> = {
  Draft:      "badge-warn",
  Approved:   "badge-active",
  Superseded: "badge-inactive",
};

export function ArtifactViewer({ productId, artifactId, onClose, onApproved }: Props) {
  const [artifact, setArtifact] = useState<Artifact | null>(null);
  const [loading, setLoading]   = useState(true);
  const [approving, setApproving] = useState(false);

  useEffect(() => {
    api.artifacts.get(productId, artifactId)
      .then(setArtifact)
      .finally(() => setLoading(false));
  }, [productId, artifactId]);

  const handleApprove = async () => {
    if (!artifact) return;
    setApproving(true);
    try {
      const updated = await api.artifacts.approve(productId, artifactId);
      setArtifact(updated);
      onApproved?.(artifactId);
    } finally {
      setApproving(false);
    }
  };

  const meta = artifact ? (TYPE_META[artifact.type] ?? TYPE_META.brief) : null;
  const Icon = meta?.icon ?? FileText;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm"
      onClick={(e) => { if (e.target === e.currentTarget) onClose(); }}
    >
      <div className="relative flex h-[90vh] w-full max-w-4xl flex-col rounded-xl border border-[var(--border)] bg-[var(--background)] shadow-2xl">
        {/* Header */}
        <div className="flex items-start justify-between border-b border-[var(--border)] px-6 py-4">
          <div className="flex items-center gap-3">
            <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-[var(--surface-elevated)]">
              <Icon size={18} style={{ color: meta?.color }} />
            </div>
            <div>
              {loading ? (
                <div className="h-5 w-48 rounded bg-[var(--surface-elevated)] shimmer-bg" />
              ) : (
                <>
                  <h2 className="text-sm font-semibold text-[var(--foreground)]">{artifact?.title}</h2>
                  <div className="mt-0.5 flex items-center gap-2">
                    <span className="text-xs text-[var(--foreground-muted)]">{meta?.label}</span>
                    {artifact && (
                      <>
                        <span className="text-xs text-[var(--border)]">·</span>
                        <span className="text-xs text-[var(--foreground-muted)]">v{artifact.version}</span>
                        <span className="text-xs text-[var(--border)]">·</span>
                        <span className={`badge ${STATUS_STYLE[artifact.status] ?? "badge-inactive"}`}>
                          {artifact.status}
                        </span>
                      </>
                    )}
                  </div>
                </>
              )}
            </div>
          </div>
          <div className="flex items-center gap-2">
            {artifact?.status === "Draft" && (
              <button
                onClick={handleApprove}
                disabled={approving}
                className="btn-primary flex items-center gap-1.5 px-3 py-1.5 text-xs"
              >
                <CheckCircle size={13} />
                {approving ? "Aprobando…" : "Aprobar"}
              </button>
            )}
            <button onClick={onClose} className="btn-ghost p-1.5">
              <X size={16} />
            </button>
          </div>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto px-6 py-5">
          {loading ? (
            <div className="space-y-3">
              {[...Array(8)].map((_, i) => (
                <div key={i} className={`h-3 rounded shimmer-bg ${i % 3 === 2 ? "w-1/2" : "w-full"}`} />
              ))}
            </div>
          ) : artifact ? (
            <MarkdownRenderer content={artifact.content} />
          ) : (
            <p className="text-sm text-[var(--foreground-muted)]">Artifact not found.</p>
          )}
        </div>

        {/* Footer */}
        {artifact && (
          <div className="border-t border-[var(--border)] px-6 py-3 text-xs text-[var(--foreground-muted)]">
            Generado: {new Date(artifact.generatedAt).toLocaleString("es-CR")}
          </div>
        )}
      </div>
    </div>
  );
}
