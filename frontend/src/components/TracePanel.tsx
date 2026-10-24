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
                {s.detail && <pre className="trace-detail">{s.detail}</pre>}
              </li>
            ))}
          </ol>
          <div className="muted small">Запуск {runId}</div>
        </>
      )}
    </section>
  );
}
