import { useEffect, useRef, useState } from "react";
import { api } from "../api";
import type { Company } from "../types";
import { money } from "../format";

export function CompanyPicker({ selected, onSelect }: { selected?: string; onSelect: (c: Company) => void }) {
  const [items, setItems] = useState<Company[]>([]);
  const [q, setQ] = useState("");
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState<string | null>(null);
  const first = useRef(true);

  useEffect(() => {
    const t = setTimeout(
      () => {
        setLoading(true);
        setErr(null);
        const req = q.trim() ? api.searchCompanies(q.trim()) : api.demoCompanies(8);
        req
          .then((list) => {
            setItems(list);
            if (first.current && list.length && !selected) onSelect(list[0]);
            first.current = false;
          })
          .catch((e: Error) => setErr(e.message))
          .finally(() => setLoading(false));
      },
      first.current ? 0 : 350,
    );
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [q]);

  return (
    <section className="panel">
      <h3>1. Компания</h3>
      <input
        className="input"
        placeholder="Поиск по ID или виду деятельности"
        value={q}
        onChange={(e) => setQ(e.target.value)}
      />
      {err && <div className="error">Не удалось загрузить компании: {err}</div>}
      {loading && !items.length && <div className="muted">Загрузка…</div>}
      <ul className="company-list">
        {items.map((c) => (
          <li key={c.id}>
            <button type="button" className={"company-item" + (c.id === selected ? " is-active" : "")} onClick={() => onSelect(c)}>
              <div className="company-title">{c.okvedName ?? "Вид деятельности не указан"}</div>
              <div className="muted small">
                {c.id} · {c.city ?? c.region ?? "—"} · {money(c.revenue)}
              </div>
            </button>
          </li>
        ))}
        {!loading && !items.length && !err && <li className="muted">Ничего не найдено</li>}
      </ul>
    </section>
  );
}
