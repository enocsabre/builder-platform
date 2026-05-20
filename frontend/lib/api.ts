import type { ProductSummary, ProductDetail, Message, Approval, ArtifactSummary, Artifact } from "./types";

const BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5238";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    headers: { "Content-Type": "application/json", ...init?.headers },
    ...init,
  });
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
};
