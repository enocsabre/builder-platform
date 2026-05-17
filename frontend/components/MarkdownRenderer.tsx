"use client";

interface Props {
  content: string;
  className?: string;
}

export function MarkdownRenderer({ content, className = "" }: Props) {
  const lines = content.split("\n");
  const elements: React.ReactNode[] = [];
  let tableRows: string[][] = [];
  let tableHeader: string[] | null = null;
  let inTable = false;
  let listItems: string[] = [];
  let inList = false;
  let key = 0;

  const flushList = () => {
    if (listItems.length > 0) {
      elements.push(
        <ul key={key++} className="my-2 space-y-1 pl-4">
          {listItems.map((item, i) => (
            <li key={i} className="flex gap-2 text-sm text-[var(--foreground-muted)]">
              <span className="mt-1.5 h-1 w-1 flex-shrink-0 rounded-full bg-[var(--border)]" />
              <span dangerouslySetInnerHTML={{ __html: inlineFormat(item) }} />
            </li>
          ))}
        </ul>
      );
      listItems = [];
      inList = false;
    }
  };

  const flushTable = () => {
    if (tableRows.length > 0 || tableHeader) {
      elements.push(
        <div key={key++} className="my-3 overflow-x-auto rounded border border-[var(--border)]">
          <table className="w-full text-sm">
            {tableHeader && (
              <thead>
                <tr className="border-b border-[var(--border)] bg-[var(--surface-elevated)]">
                  {tableHeader.map((h, i) => (
                    <th key={i} className="px-3 py-2 text-left text-xs font-semibold uppercase tracking-wide text-[var(--foreground-muted)]">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
            )}
            <tbody>
              {tableRows.map((row, ri) => (
                <tr key={ri} className={ri % 2 === 0 ? "" : "bg-[var(--surface)]"}>
                  {row.map((cell, ci) => (
                    <td key={ci} className="px-3 py-2 text-[var(--foreground)]" dangerouslySetInnerHTML={{ __html: inlineFormat(cell) }} />
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      );
      tableHeader = null;
      tableRows = [];
      inTable = false;
    }
  };

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];

    // Table detection
    if (line.trim().startsWith("|")) {
      flushList();
      const cells = line.split("|").filter((_, idx, arr) => idx > 0 && idx < arr.length - 1).map(c => c.trim());
      if (cells.every(c => /^[-:]+$/.test(c))) {
        // separator row — skip
        inTable = true;
        continue;
      }
      if (!inTable && tableHeader === null) {
        tableHeader = cells;
      } else {
        tableRows.push(cells);
      }
      inTable = true;
      continue;
    } else if (inTable) {
      flushTable();
    }

    // List item
    if (/^[-*]\s/.test(line)) {
      flushTable();
      listItems.push(line.replace(/^[-*]\s/, ""));
      inList = true;
      continue;
    } else if (inList && line.trim() === "") {
      flushList();
      continue;
    } else if (inList) {
      flushList();
    }

    // Headings
    if (line.startsWith("### ")) {
      flushList(); flushTable();
      elements.push(<h3 key={key++} className="mt-4 mb-1 text-xs font-semibold uppercase tracking-widest text-[var(--foreground-muted)]">{line.slice(4)}</h3>);
    } else if (line.startsWith("## ")) {
      flushList(); flushTable();
      elements.push(<h2 key={key++} className="mt-5 mb-2 text-sm font-bold text-[var(--foreground)] border-b border-[var(--border)] pb-1">{line.slice(3)}</h2>);
    } else if (line.startsWith("# ")) {
      flushList(); flushTable();
      elements.push(<h1 key={key++} className="mt-2 mb-3 text-base font-bold text-[var(--foreground)]">{line.slice(2)}</h1>);
    } else if (line.startsWith("```")) {
      // Skip code fences
    } else if (line.trim() === "") {
      elements.push(<div key={key++} className="h-2" />);
    } else {
      elements.push(
        <p key={key++} className="text-sm leading-relaxed text-[var(--foreground)]"
          dangerouslySetInnerHTML={{ __html: inlineFormat(line) }} />
      );
    }
  }

  flushList();
  flushTable();

  return <div className={`space-y-0.5 ${className}`}>{elements}</div>;
}

function inlineFormat(text: string): string {
  return text
    .replace(/\*\*(.+?)\*\*/g, '<strong class="font-semibold text-[var(--foreground)]">$1</strong>')
    .replace(/`(.+?)`/g, '<code class="rounded bg-[var(--surface-elevated)] px-1 py-0.5 font-mono text-xs text-[var(--status-info)]">$1</code>');
}
