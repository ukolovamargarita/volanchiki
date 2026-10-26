import { useState } from "react";
import { api } from "../api";
import type { Company, RunResult } from "../types";
import { CompanyPicker } from "./CompanyPicker";
import { CompanyProfile } from "./CompanyProfile";
import { EventPicker } from "./EventPicker";
import { InsightView } from "./InsightView";
import { RunProgress } from "./RunProgress";

export function Workspace({ initialEvent }: { initialEvent?: string }) {
  const [company, setCompany] = useState<Company | null>(null);
  const [running, setRunning] = useState(false);
  const [result, setResult] = useState<RunResult | null>(null);
  const [err, setErr] = useState<string | null>(null);

  const run = async (text: string) => {
    if (!company) return;
    setRunning(true);
    setErr(null);
    setResult(null);
    try {
      const r = await api.runInsight(company.id, text);
      if (r.status !== "ok" || !r.insight) throw new Error(r.error ?? "Агент не вернул результат");
      setResult(r);
    } catch (e) {
      setErr((e as Error).message);
    } finally {
      setRunning(false);
    }
  };

  const clarify = async (question: string) => {
    if (!company || !result?.runId) return;
    setRunning(true);
    setErr(null);
    try {
      const r = await api.clarifyInsight(result.runId, question);
      if (r.status !== "ok" || !r.insight) throw new Error(r.error ?? "Агент не вернул результат");
      setResult(r);
    } catch (e) {
      setErr((e as Error).message);
    } finally {
      setRunning(false);
    }
  };

  return (
    <div className="workspace">
      <div className="col left">
        <CompanyPicker
          selected={company?.id}
          onSelect={(c) => {
            setCompany(c);
            setResult(null);
          }}
        />
        {company && <CompanyProfile companyId={company.id} />}
      </div>
      <div className="col right">
        <EventPicker
          initial={initialEvent}
          companyId={company?.id}
          disabled={running || !company}
          busy={running}
          onRun={run}
        />
        {running && <RunProgress />}
        {err && (
          <section className="panel panel--error">
            <h3>Не получилось</h3>
            <p>{err}</p>
            <p className="muted small">
              При смене Wi‑Fi ↔ мобильный расчёт на сервере продолжается — подождите или нажмите ещё раз. Также
              проверьте LLM_API_KEY и что API доступен.
            </p>
          </section>
        )}
        {result && company && (
          <InsightView
            result={result}
            companyName={company.okvedName ?? company.id}
            busy={running}
            onClarify={clarify}
          />
        )}
      </div>
    </div>
  );
}
