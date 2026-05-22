import { NextResponse } from "next/server";

export async function POST() {
  const response = NextResponse.json({ ok: true });
  response.cookies.set("bp-token", "", { path: "/", maxAge: 0 });
  response.cookies.set("bp-email", "", { path: "/", maxAge: 0 });
  return response;
}
