import { useState } from "react";
import type { TraceStep } from "../types";
import { ACTION_LABELS, TOOL_LABELS } from "../format";

export function TracePanel({ trace, runId }: { trace: TraceStep[]; runId: string }) {
  const [open, setOpen] = useState(false);
  return (
    <section className="panel">
      <button type="button" className="link-row" onClick={() => setOpen(!open)}>
        <h3>Как мы это посчитали</h3>
        <span>{open ? "Скрыть" : `Показать шаги (${trace.length})`}</span>
      </button>
      {open && (
        <>
          <ol className="trace">
            {trace.map((s) => (
              <li key={s.step}>
                <div className="trace-title">
                  {ACTION_LABELS[s.action] ?? s.action}
                  {s.tool && <span className="chip">{TOOL_LABELS[s.tool] ?? s.tool}</span>}
                </div>
                {s.detail && <pre className="trace-detail">{formatDetail(s)}</pre>}
              </li>
            ))}
          </ol>
          <div className="muted small">Запуск {runId}</div>
        </>
      )}
    </section>
  );
}

function formatDetail(s: TraceStep): string {
  const raw = s.detail ?? "";
  // уже человекочитаемый текст с бэка (после TraceSummary)
  if (!raw.trimStart().startsWith("{") && !raw.trimStart().startsWith("[")) return raw;

  try {
    const o = JSON.parse(raw);
    if (s.tool === "search_news" && Array.isArray(o.hits)) {
      const lines = [`Новостей: ${o.hits.length}` + (o.query ? ` · запрос: «${o.query}»` : "")];
      for (const h of o.hits.slice(0, 4)) {
        lines.push(`• [${h.category ?? "—"}] ${h.title} — ${h.source ?? ""}`);
      }
      return lines.join("\n");
    }
    if (s.tool === "search_alfa_analytics" && Array.isArray(o.hits)) {
      const lines = [`Аналитика банка: ${o.hits.length} фрагм.`];
      for (const h of o.hits.slice(0, 3)) {
        lines.push(`• ${h.file ?? h.title}: ${(h.excerpt ?? "").slice(0, 120)}`);
      }
      return lines.join("\n");
    }
    if (s.tool === "get_company_profile" && o.company) {
      const c = o.company;
      return `Компания ${c.id}: ${c.okvedName ?? "—"}. ${c.city ?? ""}. Выручка ${c.revenue ?? "—"}.`;
    }
    if (s.tool === "draft_insight" && o.title) {
      return `«${o.title}»\n${String(o.what_happened ?? "").replace(/\\n/g, " ").slice(0, 240)}`;
    }
    return JSON.stringify(o, null, 2);
  } catch {
    return raw;
  }
}
