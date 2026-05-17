"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Layers, Cpu } from "lucide-react";

export function AppShell({ children }: { children: React.ReactNode }) {
  const path = usePathname();

  return (
    <div className="min-h-screen flex flex-col" style={{ background: "var(--background)" }}>
      {/* topbar */}
      <header
        className="flex items-center gap-3 px-6 py-3 border-b shrink-0"
        style={{ borderColor: "var(--border)", background: "var(--surface)" }}
      >
        <div className="flex items-center gap-2">
          <Cpu size={18} className="text-[var(--accent)]" />
          <span className="text-[15px] font-semibold tracking-tight" style={{ color: "var(--foreground)" }}>
            Builder Platform
          </span>
          <span
            className="badge badge-indigo ml-1"
            style={{ fontSize: "9px", letterSpacing: "0.08em" }}
          >
            SPRINT 0
          </span>
        </div>

        <nav className="flex items-center gap-1 ml-6">
          <NavItem href="/workspace" label="Workspace" icon={<Layers size={14} />} active={path.startsWith("/workspace")} />
        </nav>
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
