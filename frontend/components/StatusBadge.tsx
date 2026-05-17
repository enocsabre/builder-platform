"use client";
import type { ProductStatus } from "@/lib/types";

const map: Record<ProductStatus, { label: string; cls: string }> = {
  Draft:        { label: "Borrador",     cls: "badge-muted" },
  Discovering:  { label: "Discovery",   cls: "badge-info" },
  Architecting: { label: "Arquitectura", cls: "badge-indigo" },
  Planning:     { label: "Planning",    cls: "badge-indigo" },
  Building:     { label: "Construyendo", cls: "badge-warn" },
  Reviewing:    { label: "Revisión",    cls: "badge-info" },
  Stable:       { label: "Estable",     cls: "badge-active" },
  Error:        { label: "Error",       cls: "badge-danger" },
};

export function StatusBadge({ status }: { status: ProductStatus }) {
  const { label, cls } = map[status] ?? { label: status, cls: "badge-muted" };
  return <span className={`badge ${cls}`}>{label}</span>;
}
