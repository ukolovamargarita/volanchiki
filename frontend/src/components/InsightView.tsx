import { useState } from "react";
import type { RunResult } from "../types";
import { SOURCE_LABELS } from "../format";
import { Badge } from "./Badge";
import { TracePanel } from "./TracePanel";

const REL: Record<string, string> = {
  high: "Высокая значимость",
  medium: "Средняя значимость",
  low: "Низкая значимость",
};

export function InsightView({ result, companyName }: { result: RunResult; companyName: string }) {
  const [vote, setVote] = useState<"up" | "down" | null>(null);
  const ins = result.insight;
  if (!ins) return null;

  return (
    <div className="insight">
      <section className="insight-hero">
        <div className="insight-hero-meta">
          <span className={"rel rel--" + ins.relevance}>{REL[ins.relevance] ?? ins.relevance}</span>
          <span className="muted small">{companyName}</span>
        </div>
        <h2 className="insight-title">{ins.title}</h2>
      </section>

      <div className="legend">
        <Badge origin="fact" />
        <Badge origin="bank" />
        <Badge origin="ai" />
        <span className="legend-note">Метка показывает источник блока</span>
      </div>

      <Block n="01" title="Что произошло" origin="fact">
        <p>{ins.whatHappened}</p>
      </Block>

      <Block n="02" title="Почему это важно для вашей компании" origin="ai">
        <p>{ins.whyMattersForCompany}</p>
      </Block>

      <Block n="03" title="Как это оценивает Альфа-Банк" origin="bank">
        <p>{ins.bankView}</p>
      </Block>

      {ins.modelView && (
        <Block n="04" title="Вывод модели" origin="ai">
          <p>{ins.modelView}</p>
        </Block>
      )}

      <div className="two">
        <ListBlock title="Риски" items={ins.risks} />
        <ListBlock title="Возможности" items={ins.opportunities} />
      </div>

      <ListBlock title="Что можно сделать" items={ins.suggestedActions} />

      <section className="panel">
        <h3>На чём основан вывод</h3>
        <ul className="sources">
          {ins.sources.map((s, i) => (
            <li key={i}>
              <div>
                <span className="chip">{SOURCE_LABELS[s.kind] ?? s.kind}</span>{" "}
                {s.url ? (
                  <a href={s.url} target="_blank" rel="noreferrer">
                    {s.title}
                  </a>
                ) : (
                  <strong>{s.title}</strong>
                )}
              </div>
              {s.excerpt && <div className="muted small quote">{s.excerpt}</div>}
            </li>
          ))}
          {!ins.sources.length && <li className="muted">Источники не указаны моделью</li>}
        </ul>
      </section>

      <TracePanel trace={result.trace} runId={result.runId} />

      <section className="panel panel--footer">
        <div className="row">
          <button type="button" className="btn btn--red">
            {ins.cta}
          </button>
          <div className="vote">
            <button
              type="button"
              className={"vote-btn" + (vote === "up" ? " is-on" : "")}
              onClick={() => setVote("up")}
              aria-label="Полезно"
            >
              Полезно
            </button>
            <button
              type="button"
              className={"vote-btn" + (vote === "down" ? " is-on" : "")}
              onClick={() => setVote("down")}
              aria-label="Не полезно"
            >
              Нет
            </button>
          </div>
        </div>
        <p className="muted small disclaimer">{ins.disclaimer}</p>
      </section>
    </div>
  );
}

function Block({
  n,
  title,
  origin,
  children,
}: {
  n: string;
  title: string;
  origin: "fact" | "bank" | "ai";
  children: React.ReactNode;
}) {
  return (
    <section className={"panel block origin-" + origin}>
      <div className="block-head">
        <span className="num">{n}</span>
        <h3>{title}</h3>
        <Badge origin={origin} />
      </div>
      {children}
    </section>
  );
}

function ListBlock({ title, items }: { title: string; items: string[] }) {
  if (!items.length) return null;
  return (
    <section className="panel list origin-ai">
      <h3>{title}</h3>
      <ul>
        {items.map((x, i) => (
          <li key={i}>{x}</li>
        ))}
      </ul>
    </section>
  );
}
