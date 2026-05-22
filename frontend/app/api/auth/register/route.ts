import { NextRequest, NextResponse } from "next/server";

const BACKEND = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5238";

export async function POST(request: NextRequest) {
  const body = await request.json();

  const backendRes = await fetch(`${BACKEND}/api/auth/register`, {
    method:  "POST",
    headers: { "Content-Type": "application/json" },
    body:    JSON.stringify(body),
  });

  const data = await backendRes.json();

  if (!backendRes.ok) {
    return NextResponse.json(data, { status: backendRes.status });
  }

  const { token, email } = data as { token: string; email: string; expiresAt: string };

  const response = NextResponse.json({ ok: true, email });
  response.cookies.set("bp-token", token, { path: "/", sameSite: "lax", maxAge: 30 * 24 * 60 * 60 });
  response.cookies.set("bp-email", email,  { path: "/", sameSite: "lax", maxAge: 30 * 24 * 60 * 60 });
  return response;
}
