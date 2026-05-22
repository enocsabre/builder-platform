"use client";

import { useState, useEffect, useCallback } from "react";
import { Play, Square, RefreshCw, Activity, Zap, RotateCcw } from "lucide-react";
import { api } from "@/lib/api";
import type { SimulationStatus } from "@/lib/types";
import { SIMULATION_SCENARIO_LABELS } from "@/lib/types";

interface Props {
  productId:  string;
  onActivity: () => void;
}

const SCENARIOS = [
  { key: "hora_pico",            desc: "Alta demanda de pedidos y mesas llenas" },
  { key: "cocina_congestionada", desc: "Tickets acumulados, cocina bajo presión" },
  { key: "bajo_inventario",      desc: "Ingredientes bajando de stock mínimo" },
  { key: "operacion_normal",     desc: "Flujo habitual de restaurante" },
] as const;

export function SimulationPanel({ productId, onActivity }: Props) {
  const [status, setStatus]         = useState<SimulationStatus | null>(null);
  const [loading, setLoading]       = useState(true);
  const [working, setWorking]       = useState(false);
  const [resetting, setResetting]   = useState(false);
  const [resetMsg, setResetMsg]     = useState<string | null>(null);
  const [selected, setSelected]     = useState<string>("hora_pico");

  const loadStatus = useCallback(() => {
    api.simulation.status(productId)
      .then(setStatus)
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [productId]);

  useEffect(() => { loadStatus(); }, [loadStatus]);

  // Poll ops count while running
  useEffect(() => {
    if (!status?.isRunning) return;
    const id = setInterval(() => {
      api.simulation.status(productId).then(setStatus).catch(() => {});
    }, 3000);
    return () => clearInterval(id);
  }, [status?.isRunning, productId]);

  const start = async () => {
    setWorking(true);
    try {
      const s = await api.simulation.start(productId, selected);
      setStatus(s);
      onActivity();
    } finally {
      setWorking(false);
    }
  };

  const stop = async () => {
    setWorking(true);
    try {
      const s = await api.simulation.stop(productId);
      setStatus(s);
      onActivity();
    } finally {
      setWorking(false);
    }
  };

  const resetDemo = async () => {
    setResetting(true);
    setResetMsg(null);
    try {
      await api.demo.reset(productId);
      setResetMsg("Datos restaurados al estado inicial.");
      onActivity();
      loadStatus();
    } catch {
      setResetMsg("Error al resetear datos.");
    } finally {
      setResetting(false);
    }
  };

  const demoSetup = async () => {
    setResetting(true);
    setResetMsg(null);
    try {
      await api.demo.reset(productId);
      const s = await api.simulation.start(productId, "hora_pico");
      setStatus(s);
      setResetMsg("Demo listo — hora pico activa.");
      onActivity();
    } catch {
      setResetMsg("Error al preparar demo.");
    } finally {
      setResetting(false);
    }
  };

  if (loading) return (
    <div style={{ padding: "2rem", textAlign: "center", color: "var(--text-muted)" }}>
      Cargando simulación...
    </div>
  );

  const isRunning     = status?.isRunning ?? false;
  const activeScenario = status?.scenario ?? null;
  const opsGenerated   = status?.opsGenerated ?? 0;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1rem", padding: "1rem 0" }}>

      {/* ── Demo Mode section ─────────────────────────────────────────────── */}
      <div style={{
        display: "flex", flexDirection: "column", gap: "0.5rem",
        padding: "0.75rem 0.9rem",
        background: "var(--surface-2)",
        borderRadius: "8px",
        border: "1px solid var(--border)",
      }}>
        <div style={{ display: "flex", alignItems: "center", gap: "0.4rem", marginBottom: "0.1rem" }}>
          <Zap size={13} style={{ color: "var(--accent)" }} />
          <span style={{ fontSize: "0.78rem", fontWeight: 700, color: "var(--accent)", letterSpacing: "0.04em" }}>
            DEMO MODE
          </span>
        </div>
        <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
          <button
            onClick={demoSetup}
            disabled={resetting || working}
            style={{
              display: "flex", alignItems: "center", gap: "0.4rem",
              padding: "0.45rem 0.85rem",
              background: "var(--accent)", color: "#fff",
              border: "none", borderRadius: "6px",
              fontWeight: 700, fontSize: "0.8rem",
              cursor: resetting || working ? "not-allowed" : "pointer",
              opacity: resetting || working ? 0.5 : 1,
            }}
          >
            <Zap size={13} />
            {resetting ? "Preparando..." : "Demo Setup"}
          </button>
          <button
            onClick={resetDemo}
            disabled={resetting || working}
            style={{
              display: "flex", alignItems: "center", gap: "0.4rem",
              padding: "0.45rem 0.75rem",
              background: "var(--surface-1)", color: "var(--text-secondary)",
              border: "1px solid var(--border)", borderRadius: "6px",
              fontWeight: 600, fontSize: "0.8rem",
              cursor: resetting || working ? "not-allowed" : "pointer",
              opacity: resetting || working ? 0.5 : 1,
            }}
          >
            <RotateCcw size={13} />
            {resetting ? "Reseteando..." : "Reset datos"}
          </button>
        </div>
        {resetMsg && (
          <div style={{ fontSize: "0.72rem", color: "var(--status-active-text)", marginTop: "0.1rem" }}>
            {resetMsg}
          </div>
        )}
        <div style={{ fontSize: "0.7rem", color: "var(--text-muted)", lineHeight: 1.4 }}>
          <strong>Demo Setup</strong> — resetea datos e inicia hora pico de un clic.<br />
          <strong>Reset datos</strong> — restaura el estado inicial sin iniciar simulación.
        </div>
      </div>

      {/* ── Status banner ─────────────────────────────────────────────────── */}
      {isRunning && (
        <div style={{
          display: "flex", alignItems: "center", gap: "0.6rem",
          padding: "0.65rem 0.9rem",
          background: "var(--status-active-bg)",
          borderRadius: "8px",
          border: "1px solid var(--status-active-text)33",
        }}>
          <Activity size={15} style={{ color: "var(--status-active-text)", flexShrink: 0 }} />
          <div style={{ flex: 1 }}>
            <span style={{ fontWeight: 700, fontSize: "0.83rem", color: "var(--status-active-text)" }}>
              DEMO ACTIVO
            </span>
            <span style={{ fontSize: "0.75rem", color: "var(--text-muted)", marginLeft: "0.5rem" }}>
              {SIMULATION_SCENARIO_LABELS[activeScenario ?? ""] ?? activeScenario}
            </span>
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: "0.3rem" }}>
            <span style={{ fontSize: "0.72rem", color: "var(--text-muted)" }}>Ops:</span>
            <span style={{ fontSize: "0.83rem", fontWeight: 700, color: "var(--status-active-text)" }}>
              {opsGenerated}
            </span>
          </div>
        </div>
      )}

      {/* ── Scenario selector ─────────────────────────────────────────────── */}
      {!isRunning && (
        <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
          <div style={{ fontSize: "0.78rem", fontWeight: 600, color: "var(--text-muted)", marginBottom: "0.25rem" }}>
            Escenario
          </div>
          {SCENARIOS.map(s => (
            <button
              key={s.key}
              onClick={() => setSelected(s.key)}
              style={{
                display: "flex", flexDirection: "column", alignItems: "flex-start",
                padding: "0.55rem 0.75rem",
                borderRadius: "6px",
                border: `1px solid ${selected === s.key ? "var(--accent)" : "var(--border)"}`,
                background: selected === s.key ? "var(--accent)15" : "var(--surface-1)",
                cursor: "pointer",
                textAlign: "left",
              }}
            >
              <span style={{ fontSize: "0.82rem", fontWeight: 600, color: selected === s.key ? "var(--accent)" : "var(--text-primary)" }}>
                {SIMULATION_SCENARIO_LABELS[s.key]}
              </span>
              <span style={{ fontSize: "0.72rem", color: "var(--text-muted)" }}>
                {s.desc}
              </span>
            </button>
          ))}
        </div>
      )}

      {/* ── Controls ─────────────────────────────────────────────────────── */}
      <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
        {!isRunning ? (
          <button
            onClick={start}
            disabled={working || resetting}
            style={{
              display: "flex", alignItems: "center", gap: "0.4rem",
              padding: "0.45rem 0.9rem",
              background: "var(--accent)", color: "#fff",
              border: "none", borderRadius: "6px",
              fontWeight: 600, fontSize: "0.82rem",
              cursor: working || resetting ? "not-allowed" : "pointer",
              opacity: working || resetting ? 0.5 : 1,
            }}
          >
            <Play size={13} />
            {working ? "Iniciando..." : "Iniciar simulación"}
          </button>
        ) : (
          <>
            <button
              onClick={stop}
              disabled={working}
              style={{
                display: "flex", alignItems: "center", gap: "0.4rem",
                padding: "0.45rem 0.9rem",
                background: "var(--status-danger-bg)", color: "var(--status-danger-text)",
                border: "1px solid var(--status-danger-text)33", borderRadius: "6px",
                fontWeight: 600, fontSize: "0.82rem",
                cursor: working ? "not-allowed" : "pointer",
                opacity: working ? 0.5 : 1,
              }}
            >
              <Square size={13} />
              {working ? "Deteniendo..." : "Detener simulación"}
            </button>
            <button
              onClick={loadStatus}
              style={{
                display: "flex", alignItems: "center", gap: "0.3rem",
                padding: "0.4rem 0.6rem",
                background: "var(--surface-2)", color: "var(--text-muted)",
                border: "none", borderRadius: "5px",
                fontSize: "0.75rem", cursor: "pointer",
              }}
            >
              <RefreshCw size={12} />
              Actualizar
            </button>
          </>
        )}
      </div>

      {/* ── Info note ─────────────────────────────────────────────────────── */}
      <div style={{
        fontSize: "0.72rem", color: "var(--text-muted)",
        padding: "0.5rem 0.65rem",
        background: "var(--surface-2)",
        borderRadius: "5px",
        lineHeight: 1.5,
      }}>
        La simulación genera operaciones reales en los archivos <code style={{ fontSize: "0.7rem" }}>.data/</code> del proyecto.
        El dashboard del producto se actualiza automáticamente. Solo modifica datos de simulación — nunca lógica de negocio.
      </div>

      {/* ── Last run info ─────────────────────────────────────────────────── */}
      {!isRunning && status?.startedAt && (
        <div style={{ fontSize: "0.72rem", color: "var(--text-muted)" }}>
          Última simulación: {new Date(status.startedAt).toLocaleString("es-CR")} · {status.opsGenerated} ops
        </div>
      )}
    </div>
  );
}
