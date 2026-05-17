"use client";
import type { ProductStatus } from "@/lib/types";

const phaseLabel: Record<string, string> = {
  queued:           "En cola — iniciando pipeline...",
  discovery:        "Analizando dominio y features...",
  architecting:     "Generando arquitectura del sistema...",
  planning:         "Generando sprint planning...",
  building:         "Construyendo — Sprint en ejecución",
  reviewing:        "Revisando calidad del código...",
  waiting_approval: "Esperando tu aprobación para continuar",
  idle:             "",
};

interface Props {
  status:       ProductStatus;
  isProcessing: boolean;
  runtimePhase: string;
}

export function RuntimeBar({ status, isProcessing, runtimePhase }: Props) {
  const label = phaseLabel[runtimePhase] ?? "";
  const isWaiting = runtimePhase === "waiting_approval";
  const isActive  = isProcessing || ["Discovering", "Architecting", "Planning"].includes(status);

  if (!isActive && !isWaiting) return null;

  return (
    <div
      className="flex items-center gap-3 px-5 py-2 text-[12px] font-medium shrink-0"
      style={{
        background:   isWaiting ? "var(--status-warn-bg)"   : "var(--status-indigo-bg)",
        borderBottom: `1px solid ${isWaiting ? "var(--status-warn-text)" : "var(--accent)"}20`,
        color:        isWaiting ? "var(--status-warn-text)" : "var(--status-indigo-text)",
      }}
    >
      {isWaiting ? (
        <>
          <span className="text-[14px]">⏳</span>
          <span>{label}</span>
        </>
      ) : (
        <>
          <span
            className="w-2 h-2 rounded-full shrink-0 animate-pulse-dot"
            style={{ background: "var(--status-indigo-text)" }}
          />
          <span className="animate-blink">Builder OS</span>
          <span className="opacity-60">·</span>
          <span>{label}</span>
        </>
      )}
    </div>
  );
}
