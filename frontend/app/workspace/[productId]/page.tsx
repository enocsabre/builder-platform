"use client";
import { useState, useEffect, useRef, use, useCallback } from "react";
import { useRouter } from "next/navigation";
import {
  ArrowLeft, ExternalLink, Send, Check, X,
  Clock, Activity, CheckSquare, MessageSquare, Database,
  FileText, ExternalLink as ArtifactLink, FolderOpen, Zap, Map, Monitor, HardDrive, Shield,
} from "lucide-react";
import { AppShell } from "@/components/AppShell";
import { StatusBadge } from "@/components/StatusBadge";
import { RuntimeBar } from "@/components/RuntimeBar";
import { ArtifactViewer } from "@/components/ArtifactViewer";
import ScaffoldTree from "@/components/ScaffoldTree";
import ChangesPanel from "@/components/ChangesPanel";
import StructurePanel from "@/components/StructurePanel";
import PreviewPanel from "@/components/PreviewPanel";
import FilesPanel from "@/components/FilesPanel";
import QualityPanel from "@/components/QualityPanel";
import { api } from "@/lib/api";
import type {
  ProductDetail, Message, Activity as ActivityEvent,
  Approval, MemoryEntry, ArtifactSummary,
} from "@/lib/types";
import { PROCESSING_STATUSES, ACTIVE_STATUSES } from "@/lib/types";

// ── helpers ──────────────────────────────────────────────────────────────────

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

function formatTime(iso: string) {
  return new Date(iso).toLocaleTimeString("es-CR", { hour: "2-digit", minute: "2-digit" });
}

const activityIcon: Record<string, string> = {
  ProductCreated:        "🚀",
  DiscoveryStarted:      "🔍",
  BriefGenerated:        "📋",
  ApprovalPending:       "⏳",
  ApprovalResolved:      "✅",
  ArchitectureGenerated: "🏗️",
  SprintStarted:         "⚡",
  SprintCompleted:       "🎯",
  BuildComplete:         "🏁",
  ArtifactGenerated:     "📄",
  ScaffoldStarted:         "🗂️",
  ScaffoldCompleted:       "📁",
  FeatureDetected:         "⚡",
  ScaffoldDeltaStarted:    "🔧",
  BackendModuleGenerated:  "⬡",
  FrontendModuleGenerated: "◈",
  NavigationUpdated:       "🧭",
  RuntimeReviewCompleted:  "✔",
  ProjectScanned:          "🗺️",
  RegistryUpdated:         "📋",
  DashboardWidgetAdded:    "📊",
  NavigationItemAdded:     "🧭",
  PreviewStarting:         "▶",
  PreviewRunning:          "🟢",
  PreviewStopped:          "⏹",
  PreviewError:            "🔴",
  RuntimePatchStarted:     "✏️",
  RuntimeFileUpdated:      "📝",
  PreviewRestarted:        "🔄",
  RuntimePatchSkipped:     "⏭",
  RuntimePatchFailed:      "⚠️",
  ErrorOccurred:           "❌",
  StatusChanged:         "🔄",
  MessageSent:           "💬",
};

const artifactTypeLabel: Record<string, string> = {
  brief:        "Product Brief",
  architecture: "Arquitectura",
  db_schema:    "DB Schema",
  roadmap:      "Roadmap",
  sprint_plan:  "Sprint Plan",
};

const intentLabel: Record<string, string> = {
  create_product:     "crear",
  feature_request:    "feature",
  ui_refinement:      "UI",
  bug_fix:            "bug",
  stabilization:      "estabilizar",
  refactor:           "refactor",
  deployment_request: "deploy",
  dashboard_request:  "dashboard",
  ui_evolution:       "UI evolución",
  unknown:            "consulta",
};

// ── Page ─────────────────────────────────────────────────────────────────────

export default function ProductWorkspacePage({ params }: { params: Promise<{ productId: string }> }) {
  const { productId } = use(params);
  const router = useRouter();

  const [product, setProduct]     = useState<ProductDetail | null>(null);
  const [loading, setLoading]     = useState(true);
  const [input, setInput]         = useState("");
  const [sending, setSending]     = useState(false);
  const [activeTab, setActiveTab] = useState<"artifacts" | "activity" | "approvals" | "memory" | "scaffold" | "changes" | "structure" | "preview" | "archivos" | "calidad">("artifacts");
  const [resolvingId, setResolvingId]     = useState<string | null>(null);
  const [previewActioning, setPreviewActioning] = useState(false);
  const [prevActivityCount, setPrevActivityCount] = useState(0);
  const [viewingArtifact, setViewingArtifact] = useState<string | null>(null);

  const messagesEndRef = useRef<HTMLDivElement>(null);

  const load = useCallback(async () => {
    const p = await api.products.get(productId).catch(() => null);
    if (!p) { setLoading(false); return; }
    setProduct(prev => {
      if (prev && p.activity.length > prev.activity.length) setPrevActivityCount(prev.activity.length);
      return p;
    });
    setLoading(false);
  }, [productId]);

  // Auto-poll when product is processing
  useEffect(() => {
    let timer: ReturnType<typeof setTimeout>;
    async function tick() {
      await load();
      setProduct(current => {
        if (!current) return current;
        const isLive        = current.isProcessing || PROCESSING_STATUSES.includes(current.status) || current.scaffoldStatus === "generating" || current.runtimePhase === "executing";
        const isActive      = ACTIVE_STATUSES.includes(current.status);
        const previewLoading = current.previewStatus === "starting";
        if (isLive || previewLoading) timer = setTimeout(tick, 2000);
        else if (isActive)            timer = setTimeout(tick, 5000);
        return current;
      });
    }
    tick();
    return () => clearTimeout(timer);
  }, [load]);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [product?.messages.length]);

  async function sendMessage(e: React.FormEvent) {
    e.preventDefault();
    if (!input.trim() || sending || !product) return;
    const content = input.trim();
    setInput("");
    setSending(true);

    // Optimistic user bubble
    const optUser: Message = {
      id: crypto.randomUUID(), role: "User", content,
      detectedIntent: null, confidence: null,
      createdAt: new Date().toISOString(),
    };
    const optRuntime: Message = {
      id: crypto.randomUUID(), role: "Runtime", content: "Procesando...",
      detectedIntent: null, confidence: null,
      createdAt: new Date(Date.now() + 500).toISOString(),
    };
    setProduct(p => p ? { ...p, messages: [...p.messages, optUser, optRuntime] } : p);

    try {
      await api.messages.send(productId, content);
      // Wait briefly for orchestrator to process the intent
      setTimeout(load, 1800);
      setTimeout(load, 4000);
    } catch {
      setProduct(p => p ? { ...p, messages: p.messages.filter(m => m.id !== optUser.id && m.id !== optRuntime.id) } : p);
    } finally {
      setSending(false);
    }
  }

  async function resolveApproval(approvalId: string, approved: boolean) {
    if (!product) return;
    setResolvingId(approvalId);
    try {
      await api.approvals.resolve(productId, approvalId, approved);
      await load();
      // Poll a bit to catch the orchestrator continuing the pipeline
      setTimeout(load, 2500);
      setTimeout(load, 6000);
      setTimeout(load, 10000);
    } finally {
      setResolvingId(null);
    }
  }

  async function startPreview() {
    if (!product || previewActioning) return;
    setPreviewActioning(true);
    try {
      await fetch(`http://localhost:5238/api/products/${productId}/preview/start`, { method: "POST" });
      // Poll every 2s while starting — driven by previewLoading polling above
      await load();
    } finally {
      setPreviewActioning(false);
    }
  }

  async function stopPreview() {
    if (!product || previewActioning) return;
    setPreviewActioning(true);
    try {
      await fetch(`http://localhost:5238/api/products/${productId}/preview/stop`, { method: "POST" });
      await load();
    } finally {
      setPreviewActioning(false);
    }
  }

  if (loading) return (
    <AppShell>
      <div className="flex items-center justify-center h-full py-32">
        <p className="text-[14px]" style={{ color: "var(--muted)" }}>Cargando workspace...</p>
      </div>
    </AppShell>
  );

  if (!product) return (
    <AppShell>
      <div className="flex items-center justify-center h-full py-32">
        <p className="text-[14px]" style={{ color: "var(--status-danger-text)" }}>Producto no encontrado.</p>
      </div>
    </AppShell>
  );

  const pendingApprovals = product.approvals.filter(a => a.status === "Pending");
  const isLive = product.isProcessing || PROCESSING_STATUSES.includes(product.status);

  return (
    <AppShell>
      <div className="h-[calc(100vh-49px)] flex flex-col">
        {/* product header */}
        <div
          className="flex items-center gap-3 px-5 py-3 border-b shrink-0"
          style={{ borderColor: "var(--border)", background: "var(--surface)" }}
        >
          <button onClick={() => router.push("/workspace")} className="p-1.5 rounded-lg" style={{ color: "var(--muted)" }}>
            <ArrowLeft size={16} />
          </button>

          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <h2 className="text-[15px] font-semibold truncate" style={{ color: "var(--foreground)" }}>{product.name}</h2>
              <StatusBadge status={product.status} />
              {pendingApprovals.length > 0 && (
                <span className="badge badge-warn">{pendingApprovals.length} pendiente{pendingApprovals.length > 1 ? "s" : ""}</span>
              )}
            </div>
          </div>

          {product.previewStatus === "running" && product.previewUrl && (
            <a
              href={product.previewUrl} target="_blank" rel="noopener noreferrer"
              className="btn-ghost flex items-center gap-1.5 text-[12px] px-3 py-1.5"
              style={{ color: "var(--status-active-text)", borderColor: "var(--status-active-bg)" }}
            >
              <ExternalLink size={13} />Abrir preview
            </a>
          )}
        </div>

        {/* runtime status bar */}
        <RuntimeBar status={product.status} isProcessing={product.isProcessing} runtimePhase={product.runtimePhase} />

        {/* split layout */}
        <div className="flex-1 flex overflow-hidden">
          {/* LEFT — Chat */}
          <div className="flex flex-col border-r" style={{ width: "55%", borderColor: "var(--border)" }}>
            <div className="px-4 py-2 border-b flex items-center gap-1.5" style={{ borderColor: "var(--border)" }}>
              <MessageSquare size={13} style={{ color: "var(--muted)" }} />
              <span className="text-[12px] font-medium" style={{ color: "var(--muted)" }}>Chat del producto</span>
            </div>

            <div className="flex-1 overflow-y-auto p-4 space-y-4">
              {product.messages.length === 0 && (
                <div className="text-center py-12">
                  <p className="text-[13px]" style={{ color: "var(--muted)" }}>Enviá un mensaje para continuar.</p>
                </div>
              )}
              {product.messages.map((msg, i) => (
                <ChatBubble key={msg.id} message={msg} isNew={i >= prevActivityCount} />
              ))}
              {isLive && (
                <div className="flex gap-3">
                  <div className="w-7 h-7 rounded-full flex items-center justify-center shrink-0 mt-0.5 text-[11px] font-bold"
                    style={{ background: "var(--surface-elevated)", border: "1px solid var(--border)", color: "var(--status-indigo-text)" }}>
                    AI
                  </div>
                  <div className="flex items-center gap-1.5 px-4 py-2.5 rounded-2xl" style={{ background: "var(--surface-elevated)", borderRadius: "4px 16px 16px 16px" }}>
                    {[0, 1, 2].map(i => (
                      <span key={i} className="w-1.5 h-1.5 rounded-full animate-pulse-dot"
                        style={{ background: "var(--status-indigo-text)", animationDelay: `${i * 0.2}s` }} />
                    ))}
                  </div>
                </div>
              )}
              <div ref={messagesEndRef} />
            </div>

            <form onSubmit={sendMessage} className="p-4 border-t" style={{ borderColor: "var(--border)" }}>
              <div className="flex items-end gap-2 rounded-xl p-2"
                style={{ background: "var(--surface-elevated)", border: "1px solid var(--border)" }}>
                <textarea
                  value={input}
                  onChange={e => setInput(e.target.value)}
                  onKeyDown={e => { if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); sendMessage(e); } }}
                  placeholder="Escribí una instrucción, feature, bug o pregunta..."
                  rows={1}
                  className="flex-1 resize-none bg-transparent outline-none text-[14px] py-1 px-2"
                  style={{ color: "var(--foreground)", maxHeight: "120px" }}
                />
                <button
                  type="submit"
                  disabled={!input.trim() || sending}
                  className="p-2 rounded-lg shrink-0 transition-colors"
                  style={{
                    background: input.trim() && !sending ? "var(--accent)" : "var(--surface)",
                    color:      input.trim() && !sending ? "#fff"          : "var(--muted)",
                  }}
                >
                  <Send size={15} />
                </button>
              </div>
              <p className="text-[11px] mt-1.5 px-2" style={{ color: "var(--muted-foreground)" }}>
                Enter envía · Shift+Enter nueva línea
              </p>
            </form>
          </div>

          {/* RIGHT — Artifacts, Activity, Approvals, Memory */}
          <div className="flex flex-col overflow-hidden" style={{ flex: 1 }}>
            <div className="flex border-b shrink-0 overflow-x-auto" style={{ borderColor: "var(--border)" }}>
              <TabBtn active={activeTab === "artifacts"} onClick={() => setActiveTab("artifacts")}
                icon={<FileText size={13} />} label="Artefactos" count={product.artifacts.length} />
              <TabBtn active={activeTab === "activity"} onClick={() => setActiveTab("activity")}
                icon={<Activity size={13} />} label="Actividad" count={product.activity.length} />
              <TabBtn active={activeTab === "approvals"} onClick={() => setActiveTab("approvals")}
                icon={<CheckSquare size={13} />} label="Aprobaciones"
                count={pendingApprovals.length} countClass="badge-warn" />
              <TabBtn active={activeTab === "scaffold"} onClick={() => setActiveTab("scaffold")}
                icon={<FolderOpen size={13} />} label="Scaffold"
                count={product.scaffoldEntries?.length ?? 0}
                countClass={product.scaffoldStatus === "complete" ? "badge-active" : "badge-muted"} />
              <TabBtn active={activeTab === "changes"} onClick={() => setActiveTab("changes")}
                icon={<Zap size={13} />} label="Cambios"
                count={product.scaffoldChanges?.filter(c => c.changeType === "created").length ?? 0}
                countClass="badge-active" />
              <TabBtn active={activeTab === "structure"} onClick={() => setActiveTab("structure")}
                icon={<Map size={13} />} label="Estructura"
                count={product.modules?.length ?? 0}
                countClass={(product.modules?.length ?? 0) > 0 ? "badge-active" : "badge-muted"} />
              <TabBtn active={activeTab === "preview"} onClick={() => setActiveTab("preview")}
                icon={<Monitor size={13} />} label="Preview"
                count={0}
                countClass={product.previewStatus === "running" ? "badge-active" : "badge-muted"} />
              <TabBtn active={activeTab === "archivos"} onClick={() => setActiveTab("archivos")}
                icon={<HardDrive size={13} />} label="Archivos"
                count={product.fileRevisions?.length ?? 0}
                countClass={(product.fileRevisions?.length ?? 0) > 0 ? "badge-warn" : "badge-muted"} />
              <TabBtn active={activeTab === "calidad"} onClick={() => setActiveTab("calidad")}
                icon={<Shield size={13} />} label="Calidad"
                count={product.validationRuns?.length ?? 0}
                countClass={
                  product.runtimeHealth === "healthy"   ? "badge-active" :
                  product.runtimeHealth === "degraded"  ? "badge-warn"   :
                  product.runtimeHealth === "broken"    ? "badge-danger"  : "badge-muted"
                } />
              <TabBtn active={activeTab === "memory"} onClick={() => setActiveTab("memory")}
                icon={<Database size={13} />} label="Memoria" count={0} />
            </div>

            <div className="flex-1 overflow-y-auto">
              {activeTab === "artifacts"  && <ArtifactsPanel artifacts={product.artifacts} onView={setViewingArtifact} />}
              {activeTab === "activity"   && <ActivityPanel events={product.activity} prevCount={prevActivityCount} onViewArtifact={setViewingArtifact} />}
              {activeTab === "approvals"  && <ApprovalsPanel approvals={product.approvals} resolvingId={resolvingId} onResolve={resolveApproval} onViewArtifact={setViewingArtifact} />}
              {activeTab === "scaffold"   && (
                <ScaffoldTree
                  entries={product.scaffoldEntries ?? []}
                  scaffoldStatus={product.scaffoldStatus}
                  projectPath={product.projectPath}
                />
              )}
              {activeTab === "changes"    && (
                <ChangesPanel changes={product.scaffoldChanges ?? []} />
              )}
              {activeTab === "structure"  && (
                <StructurePanel modules={product.modules ?? []} />
              )}
              {activeTab === "preview"    && (
                <PreviewPanel
                  product={product}
                  onStartPreview={startPreview}
                  onStopPreview={stopPreview}
                  isActioning={previewActioning}
                />
              )}
              {activeTab === "archivos"   && <FilesPanel product={product} />}
              {activeTab === "calidad"    && <QualityPanel product={product} />}
              {activeTab === "memory"     && <MemoryPanel entries={product.memory} />}
            </div>
          </div>
        </div>
      </div>
      {viewingArtifact && (
        <ArtifactViewer
          productId={productId}
          artifactId={viewingArtifact}
          onClose={() => setViewingArtifact(null)}
          onApproved={() => { setViewingArtifact(null); load(); }}
        />
      )}
    </AppShell>
  );
}

// ── Chat bubble ───────────────────────────────────────────────────────────────

function ChatBubble({ message, isNew }: { message: Message; isNew: boolean }) {
  const isUser    = message.role === "User";
  const isThinking = message.content === "Procesando...";

  return (
    <div className={`flex gap-3 ${isUser ? "flex-row-reverse" : "flex-row"} ${isNew ? "animate-slide-in" : ""}`}>
      <div className="w-7 h-7 rounded-full flex items-center justify-center shrink-0 mt-0.5 text-[11px] font-bold"
        style={{
          background: isUser ? "var(--accent)" : "var(--surface-elevated)",
          color:      isUser ? "#fff"          : "var(--status-indigo-text)",
          border:     isUser ? "none"          : "1px solid var(--border)",
        }}>
        {isUser ? "U" : "AI"}
      </div>

      <div className={`max-w-[75%] space-y-1 ${isUser ? "items-end" : "items-start"} flex flex-col`}>
        <div
          className={`px-4 py-2.5 text-[13px] leading-relaxed ${isThinking ? "animate-blink" : ""}`}
          style={{
            background:   isUser ? "var(--accent)" : "var(--surface-elevated)",
            color:        isUser ? "#fff"          : "var(--foreground)",
            borderRadius: isUser ? "16px 4px 16px 16px" : "4px 16px 16px 16px",
          }}
        >
          {message.content}
        </div>

        <div className={`flex items-center gap-1.5 px-1 ${isUser ? "flex-row-reverse" : ""}`}>
          <span className="text-[11px]" style={{ color: "var(--muted-foreground)" }}>
            {formatTime(message.createdAt)}
          </span>
          {message.detectedIntent && message.role === "User" && (
            <span className="badge badge-indigo" style={{ fontSize: "9px", letterSpacing: "0.05em" }}>
              {intentLabel[message.detectedIntent] ?? message.detectedIntent}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

// ── Artifacts panel ───────────────────────────────────────────────────────────

function ArtifactsPanel({ artifacts, onView }: { artifacts: ArtifactSummary[]; onView: (id: string) => void }) {
  if (artifacts.length === 0) return (
    <div className="flex flex-col items-center justify-center py-16 space-y-2 px-4">
      <FileText size={28} style={{ color: "var(--muted)", opacity: 0.3 }} />
      <p className="text-[13px]" style={{ color: "var(--muted)" }}>Los artefactos se generan durante el pipeline.</p>
    </div>
  );

  const statusStyle: Record<string, string> = {
    Draft:      "badge-warn",
    Approved:   "badge-active",
    Superseded: "badge-inactive",
  };

  return (
    <div className="p-4 space-y-2">
      {artifacts.map(a => (
        <button
          key={a.id}
          onClick={() => onView(a.id)}
          className="w-full text-left rounded-xl p-4 transition-colors hover:border-[var(--accent)]"
          style={{ background: "var(--surface-elevated)", border: "1px solid var(--border)" }}
        >
          <div className="flex items-start justify-between gap-3">
            <div className="flex-1 min-w-0">
              <p className="text-[13px] font-medium truncate" style={{ color: "var(--foreground)" }}>{a.title}</p>
              <p className="text-[11px] mt-0.5" style={{ color: "var(--muted)" }}>
                {artifactTypeLabel[a.type] ?? a.type} · v{a.version}
              </p>
            </div>
            <div className="flex items-center gap-2 shrink-0">
              <span className={`badge ${statusStyle[a.status] ?? "badge-inactive"}`}>{a.status}</span>
              <ArtifactLink size={13} style={{ color: "var(--muted)" }} />
            </div>
          </div>
          <p className="text-[11px] mt-2" style={{ color: "var(--muted-foreground)" }}>
            {new Date(a.generatedAt).toLocaleString("es-CR")}
          </p>
        </button>
      ))}
    </div>
  );
}

// ── Activity panel ────────────────────────────────────────────────────────────

function ActivityPanel({ events, prevCount, onViewArtifact }: {
  events: ActivityEvent[];
  prevCount: number;
  onViewArtifact: (id: string) => void;
}) {
  if (events.length === 0) return (
    <div className="flex flex-col items-center justify-center py-16 space-y-2 px-4">
      <Activity size={28} style={{ color: "var(--muted)", opacity: 0.3 }} />
      <p className="text-[13px]" style={{ color: "var(--muted)" }}>Sin actividad todavía.</p>
    </div>
  );

  return (
    <div className="p-4 space-y-1">
      {events.map((ev, i) => (
        <div key={ev.id} className={`flex gap-3 py-2 ${i < prevCount ? "" : "animate-slide-in"}`}>
          <div className="flex flex-col items-center shrink-0">
            <span className="text-[16px] leading-none">{activityIcon[ev.eventType] ?? "•"}</span>
            {i < events.length - 1 && (
              <div className="w-px flex-1 mt-1.5" style={{ background: "var(--border)", minHeight: "12px" }} />
            )}
          </div>
          <div className="flex-1 min-w-0 pb-1">
            <p className="text-[13px] font-medium" style={{ color: "var(--foreground)" }}>{ev.title}</p>
            {ev.details && (
              <p className="text-[12px] mt-0.5 leading-relaxed" style={{ color: "var(--muted)" }}>{ev.details}</p>
            )}
            <div className="flex items-center gap-2 mt-0.5">
              <div className="flex items-center gap-1" style={{ color: "var(--muted-foreground)" }}>
                <Clock size={10} />
                <span className="text-[11px]">{timeAgo(ev.createdAt)}</span>
              </div>
              {ev.artifactId && (
                <button
                  onClick={() => onViewArtifact(ev.artifactId!)}
                  className="text-[11px] flex items-center gap-1 transition-opacity hover:opacity-80"
                  style={{ color: "var(--status-indigo-text)" }}
                >
                  <FileText size={10} />Ver artefacto
                </button>
              )}
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

// ── Approvals panel ───────────────────────────────────────────────────────────

function ApprovalsPanel({ approvals, resolvingId, onResolve, onViewArtifact }: {
  approvals: Approval[];
  resolvingId: string | null;
  onResolve: (id: string, approved: boolean) => void;
  onViewArtifact: (id: string) => void;
}) {
  if (approvals.length === 0) return (
    <div className="flex flex-col items-center justify-center py-16 space-y-2 px-4">
      <CheckSquare size={28} style={{ color: "var(--muted)", opacity: 0.3 }} />
      <p className="text-[13px]" style={{ color: "var(--muted)" }}>Sin aprobaciones pendientes.</p>
    </div>
  );

  return (
    <div className="p-4 space-y-3">
      {approvals.map(ap => (
        <div key={ap.id} className="rounded-xl p-4 space-y-3 animate-slide-in"
          style={{
            background: ap.status === "Pending" ? "var(--surface-elevated)" : "var(--surface)",
            border:     `1px solid ${ap.status === "Pending" ? "rgba(251,191,36,0.25)" : "var(--border)"}`,
          }}>
          <div className="flex items-start gap-2">
            <div className="flex-1 min-w-0">
              <p className="text-[13px] font-semibold" style={{ color: "var(--foreground)" }}>{ap.title}</p>
              <p className="text-[12px] mt-1 leading-relaxed" style={{ color: "var(--muted)" }}>{ap.description}</p>
            </div>
            <ApprovalBadge status={ap.status} />
          </div>

          {ap.status === "Pending" ? (
            <div className="flex flex-wrap gap-2">
              {ap.artifactId && (
                <button
                  onClick={() => onViewArtifact(ap.artifactId!)}
                  className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[12px] font-medium transition-colors"
                  style={{ background: "var(--surface)", border: "1px solid var(--border)", color: "var(--status-indigo-text)" }}
                >
                  <FileText size={13} />Ver artefacto
                </button>
              )}
              <button onClick={() => onResolve(ap.id, true)} disabled={resolvingId === ap.id}
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[12px] font-medium transition-colors"
                style={{ background: "var(--status-active-bg)", color: "var(--status-active-text)" }}>
                <Check size={13} />{resolvingId === ap.id ? "..." : "Aprobar"}
              </button>
              <button onClick={() => onResolve(ap.id, false)} disabled={resolvingId === ap.id}
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[12px] font-medium transition-colors"
                style={{ background: "var(--status-danger-bg)", color: "var(--status-danger-text)" }}>
                <X size={13} />Rechazar
              </button>
            </div>
          ) : ap.resolutionNote ? (
            <p className="text-[12px] italic" style={{ color: "var(--muted)" }}>Nota: {ap.resolutionNote}</p>
          ) : null}
        </div>
      ))}
    </div>
  );
}

// ── Memory panel ──────────────────────────────────────────────────────────────

const memoryKeyLabel: Record<string, string> = {
  industry:         "Industria detectada",
  saas_type:        "Tipo de SaaS",
  brief:            "Brief generado",
  architecture:     "Arquitectura",
  roadmap:          "Roadmap",
  features:         "Features detectadas",
  db_entities:      "Entidades DB",
  last_intent:      "Último intent",
  current_sprint:   "Sprint actual",
  sprint_count:     "Total sprints",
  build_started_at: "Inicio construcción",
};

function MemoryPanel({ entries }: { entries: MemoryEntry[] }) {
  if (entries.length === 0) return (
    <div className="flex flex-col items-center justify-center py-16 space-y-2 px-4">
      <Database size={28} style={{ color: "var(--muted)", opacity: 0.3 }} />
      <p className="text-[13px]" style={{ color: "var(--muted)" }}>La memoria del runtime se llenará durante el pipeline.</p>
    </div>
  );

  const isMarkdown = (key: string) => ["brief", "architecture", "roadmap"].includes(key);

  return (
    <div className="p-4 space-y-3">
      {entries.map(e => (
        <div key={e.key} className="rounded-xl p-3 space-y-1.5"
          style={{ background: "var(--surface-elevated)", border: "1px solid var(--border)" }}>
          <div className="flex items-center justify-between">
            <span className="text-[11px] font-semibold uppercase tracking-wider" style={{ color: "var(--muted)" }}>
              {memoryKeyLabel[e.key] ?? e.key}
            </span>
            <span className="text-[10px]" style={{ color: "var(--muted-foreground)" }}>{timeAgo(e.createdAt)}</span>
          </div>
          {isMarkdown(e.key) ? (
            <details>
              <summary className="text-[12px] cursor-pointer" style={{ color: "var(--status-indigo-text)" }}>
                Ver contenido generado
              </summary>
              <pre className="text-[11px] mt-2 whitespace-pre-wrap leading-relaxed"
                style={{ color: "var(--muted)", fontFamily: "ui-monospace, monospace" }}>
                {e.value}
              </pre>
            </details>
          ) : (
            <p className="text-[13px]" style={{ color: "var(--foreground)" }}>
              {e.key === "features" || e.key === "db_entities" ? e.value.replace(/\|/g, " · ") : e.value}
            </p>
          )}
        </div>
      ))}
    </div>
  );
}

// ── Shared UI ─────────────────────────────────────────────────────────────────

function ApprovalBadge({ status }: { status: Approval["status"] }) {
  if (status === "Pending")  return <span className="badge badge-warn shrink-0">Pendiente</span>;
  if (status === "Approved") return <span className="badge badge-active shrink-0">Aprobado</span>;
  return <span className="badge badge-danger shrink-0">Rechazado</span>;
}

function TabBtn({ active, onClick, icon, label, count, countClass = "badge-muted" }: {
  active: boolean; onClick: () => void; icon: React.ReactNode;
  label: string; count: number; countClass?: string;
}) {
  return (
    <button onClick={onClick}
      className="flex items-center gap-1.5 px-4 py-2.5 text-[12px] font-medium border-b-2 transition-colors"
      style={{ borderBottomColor: active ? "var(--accent)" : "transparent", color: active ? "var(--foreground)" : "var(--muted)" }}>
      {icon}{label}
      {count > 0 && <span className={`badge ${countClass} ml-0.5`}>{count}</span>}
    </button>
  );
}
