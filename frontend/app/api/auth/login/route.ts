import { NextRequest, NextResponse } from "next/server";

const BACKEND = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5238";

export async function POST(request: NextRequest) {
  const body = await request.json();

  let backendRes: Response;
  try {
    backendRes = await fetch(`${BACKEND}/api/auth/login`, {
      method:  "POST",
      headers: { "Content-Type": "application/json" },
      body:    JSON.stringify(body),
    });
  } catch {
    return NextResponse.json({ error: "Backend no disponible" }, { status: 503 });
  }

  if (!backendRes.ok) {
    return NextResponse.json({ error: "Credenciales incorrectas" }, { status: 401 });
  }

  const data: { token: string; email: string; expiresAt: string } = await backendRes.json();

  const response = NextResponse.json({ ok: true, email: data.email });
  response.cookies.set("bp-token", data.token, {
    path:     "/",
    sameSite: "lax",
    maxAge:   30 * 24 * 60 * 60,
    // Not httpOnly: client JS reads it for Authorization header in api.ts
  });
  response.cookies.set("bp-email", data.email, {
    path:     "/",
    sameSite: "lax",
    maxAge:   30 * 24 * 60 * 60,
  });

  return response;
}
