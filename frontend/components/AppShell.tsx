"use client";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { Layers, Cpu, LogOut, User } from "lucide-react";
import { useState, useEffect } from "react";

export function AppShell({ children }: { children: React.ReactNode }) {
  const path   = usePathname();
  const router = useRouter();
  const [email,          setEmail]          = useState<string | null>(null);
  const [logoutLoading,  setLogoutLoading]  = useState(false);

  useEffect(() => {
    const m = document.cookie.match(/(?:^|; )bp-email=([^;]*)/);
    if (m) setEmail(decodeURIComponent(m[1]));
  }, []);

  async function handleLogout() {
    setLogoutLoading(true);
    try {
      await fetch("/api/auth/logout", { method: "POST" });
    } finally {
      router.replace("/login");
    }
  }

  return (
    <div className="min-h-screen flex flex-col" style={{ background: "var(--background)" }}>
      {/* topbar */}
      <header
        className="flex items-center gap-3 px-6 py-3 border-b shrink-0"
        style={{ borderColor: "var(--border)", background: "var(--surface)" }}
      >
        {/* Brand */}
        <div className="flex items-center gap-2">
          <Cpu size={18} className="text-[var(--accent)]" />
          <span className="text-[15px] font-semibold tracking-tight" style={{ color: "var(--foreground)" }}>
            Builder OS
          </span>
          <span
            className="badge badge-indigo ml-1"
            style={{ fontSize: "9px", letterSpacing: "0.08em" }}
          >
            S23
          </span>
        </div>

        {/* Nav */}
        <nav className="flex items-center gap-1 ml-6">
          <NavItem href="/workspace" label="Workspace" icon={<Layers size={14} />} active={path.startsWith("/workspace")} />
        </nav>

        {/* User + logout — pushed to right */}
        <div className="flex items-center gap-3 ml-auto">
          {email && (
            <div className="flex items-center gap-2" style={{ color: "var(--muted)", fontSize: "12px" }}>
              <User size={13} />
              <span>{email}</span>
            </div>
          )}
          <button
            onClick={handleLogout}
            disabled={logoutLoading}
            title="Cerrar sesión"
            style={{
              display: "flex", alignItems: "center", gap: "5px",
              padding: "5px 10px", borderRadius: "7px", border: "none",
              background: "var(--surface-elevated)", color: "var(--muted)",
              fontSize: "12px", cursor: logoutLoading ? "default" : "pointer",
              opacity: logoutLoading ? 0.6 : 1, transition: "opacity 0.15s",
            }}
            onMouseEnter={e => { if (!logoutLoading) (e.currentTarget as HTMLButtonElement).style.color = "var(--status-danger-text)"; }}
            onMouseLeave={e => { (e.currentTarget as HTMLButtonElement).style.color = "var(--muted)"; }}
          >
            <LogOut size={13} />
            {logoutLoading ? "Saliendo..." : "Salir"}
          </button>
        </div>
      </header>

      <main className="flex-1 overflow-hidden">{children}</main>
    </div>
  );
}

function NavItem({
  href, label, icon, active,
}: { href: string; label: string; icon: React.ReactNode; active: boolean }) {
  return (
    <Link
      href={href}
      className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[13px] font-medium transition-colors"
      style={{
        background: active ? "rgba(99,102,241,0.12)" : "transparent",
        color:      active ? "var(--accent)" : "var(--muted)",
      }}
    >
      {icon}
      {label}
    </Link>
  );
}
