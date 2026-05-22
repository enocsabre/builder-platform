import type { ProductSummary, ProductDetail, Message, Approval, ArtifactSummary, Artifact, EvolutionContext, RefactorRecommendation, SimulationStatus } from "./types";

const BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5238";

function getToken(): string | null {
  if (typeof window === "undefined") return null;
  const m = document.cookie.match(/(?:^|; )bp-token=([^;]*)/);
  return m ? decodeURIComponent(m[1]) : null;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getToken();
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(init?.headers as Record<string, string>),
  };
  if (token) headers["Authorization"] = `Bearer ${token}`;

  const res = await fetch(`${BASE}${path}`, { ...init, headers });

  if (res.status === 401) {
    if (typeof window !== "undefined") window.location.href = "/login";
    throw new Error("401: Unauthorized");
  }
  if (!res.ok) {
    const body = await res.text().catch(() => "");
    throw new Error(`${res.status}: ${body}`);
  }
  if (res.status === 204) return undefined as T;
  return res.json();
}

export const api = {
  products: {
    list:          ()                                  => request<ProductSummary[]>("/api/products"),
    get:           (id: string)                        => request<ProductDetail>(`/api/products/${id}`),
    create:        (name: string, prompt: string)      => request<ProductSummary>("/api/products", { method: "POST", body: JSON.stringify({ name, prompt }) }),
    delete:        (id: string)                        => request<void>(`/api/products/${id}`, { method: "DELETE" }),
    setStatus:     (id: string, status: string)        => request<ProductSummary>(`/api/products/${id}/status`, { method: "PATCH", body: JSON.stringify({ status }) }),
    setPreviewUrl: (id: string, url: string | null)    => request<ProductSummary>(`/api/products/${id}/preview-url`, { method: "PATCH", body: JSON.stringify({ previewUrl: url }) }),
    openVSCode:    (id: string)                        => request<{ message: string; path: string }>(`/api/products/${id}/open-vscode`, { method: "POST" }),
    startPreview:  (id: string)                        => request<ProductSummary>(`/api/products/${id}/preview/start`, { method: "POST" }),
    stopPreview:   (id: string)                        => request<ProductSummary>(`/api/products/${id}/preview/stop`,  { method: "POST" }),
  },
  messages: {
    send: (productId: string, content: string) =>
      request<Message>(`/api/products/${productId}/messages`, { method: "POST", body: JSON.stringify({ content }) }),
  },
  approvals: {
    resolve: (productId: string, approvalId: string, approved: boolean, note?: string) =>
      request<Approval>(`/api/products/${productId}/approvals/${approvalId}/resolve`, {
        method: "POST",
        body:   JSON.stringify({ approved, note: note ?? null }),
      }),
  },
  artifacts: {
    list:    (productId: string)                       => request<ArtifactSummary[]>(`/api/products/${productId}/artifacts`),
    get:     (productId: string, artifactId: string)   => request<Artifact>(`/api/products/${productId}/artifacts/${artifactId}`),
    approve: (productId: string, artifactId: string, note?: string) =>
      request<Artifact>(`/api/products/${productId}/artifacts/${artifactId}/approve`, {
        method: "POST",
        body:   JSON.stringify({ note: note ?? null }),
      }),
  },

  evolution: {
    get: (id: string) => request<EvolutionContext>(`/api/products/${id}/evolution`),
  },

  refactor: {
    list:    (id: string)                            => request<RefactorRecommendation[]>(`/api/products/${id}/refactor`),
    resolve: (id: string, recId: string, accepted: boolean, note?: string) =>
      request<RefactorRecommendation>(`/api/products/${id}/refactor/${recId}/resolve`, {
        method: "POST",
        body:   JSON.stringify({ accepted, note: note ?? null }),
      }),
    execute: (id: string, recId: string) =>
      request<RefactorRecommendation>(`/api/products/${id}/refactor/${recId}/execute`, { method: "POST" }),
  },

  simulation: {
    start:  (id: string, scenario: string) =>
      request<SimulationStatus>(`/api/products/${id}/simulate/start`, { method: "POST", body: JSON.stringify({ scenario }) }),
    stop:   (id: string) =>
      request<SimulationStatus>(`/api/products/${id}/simulate/stop`, { method: "POST" }),
    status: (id: string) =>
      request<SimulationStatus>(`/api/products/${id}/simulate/status`),
  },
};
