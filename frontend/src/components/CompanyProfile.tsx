import { useEffect, useState } from "react";
import { api } from "../api";
import type { CompanyDetails } from "../types";
import { PRODUCT_LABELS, money } from "../format";

export function CompanyProfile({ companyId }: { companyId: string }) {
  const [d, setD] = useState<CompanyDetails | null>(null);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    setD(null);
    setErr(null);
    api.company(companyId).then(setD).catch((e: Error) => setErr(e.message));
  }, [companyId]);

  if (err)
    return (
      <section className="panel">
        <div className="error">Профиль не загрузился: {err}</div>
      </section>
    );
  if (!d)
    return (
      <section className="panel">
        <div className="muted">Загрузка профиля…</div>
      </section>
    );

  const c = d.company;
  const active = Object.entries(c.products)
    .filter(([, v]) => v)
    .map(([k]) => k);

  return (
    <section className="panel">
      <h3>Профиль клиента</h3>
      <dl className="facts">
        <dt>Деятельность</dt>
        <dd>
          {c.okvedName ?? "—"} {c.okvedCode && <span className="muted">({c.okvedCode})</span>}
        </dd>
        <dt>Выручка</dt>
        <dd>{money(c.revenue)}</dd>
        <dt>Регион</dt>
        <dd>{[c.city, c.region].filter(Boolean).join(", ") || "—"}</dd>
        <dt>Группа</dt>
        <dd>{d.group.size > 1 ? `${d.group.size} компаний, выручка ${money(d.group.revenue)}` : "Не входит в группу"}</dd>
      </dl>
      <div className="chips">
        {active.length ? (
          active.map((k) => (
            <span key={k} className="chip">
              {PRODUCT_LABELS[k] ?? k}
            </span>
          ))
        ) : (
          <span className="muted small">Продуктов банка нет</span>
        )}
      </div>
    </section>
  );
}
