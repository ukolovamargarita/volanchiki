import { useEffect, useState } from "react";
import { api } from "../api";
import { PRESET_EVENTS } from "../events";
import type { NewsItem } from "../types";

export function EventPicker({
  initial,
  companyId,
  disabled,
  busy,
  onRun,
}: {
  initial?: string;
  companyId?: string;
  disabled: boolean;
  busy?: boolean;
  onRun: (text: string) => void;
}) {
  const [text, setText] = useState(initial ?? "");
  const [news, setNews] = useState<NewsItem[]>([]);
  const [newsMeta, setNewsMeta] = useState<string>("");
  const [newsErr, setNewsErr] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  useEffect(() => {
    if (initial) {
      setText(initial);
      setSelectedId(null);
    }
  }, [initial]);

  useEffect(() => {
    let cancelled = false;
    const load = () => {
      api
        .news({ companyId, take: 12 })
        .then((r) => {
          if (cancelled) return;
          setNews(r.items);
          setNewsErr(false);
          const live = r.meta.live;
          const total = r.meta.total;
          const biz = (r.meta as { business?: number }).business;
          setNewsMeta(
            live > 0
              ? `экономика · ${r.items.length} в ленте` +
                  (typeof biz === "number" ? ` · ${biz} бизнес из ${total}` : ` · ${total} всего`)
              : total > 0
                ? `Офлайн-демо · ${total} тем`
                : "Лента пуста",
          );
          if (!initial && !text && r.items[0]) {
            setText(r.items[0].text);
            setSelectedId(r.items[0].id);
          }
        })
        .catch(() => {
          if (!cancelled) setNewsErr(true);
        });
    };
    load();
    const timer = window.setInterval(load, 60_000);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
    // text специально не в deps — иначе цикл при автоподстановке
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [companyId, initial]);

  const label = busy ? "Анализируем…" : disabled ? "Выберите компанию" : "Получить инсайт";

  return (
    <section className="panel">
      <h3>2. Событие</h3>
      <p className="muted small" style={{ marginTop: -4, marginBottom: 10 }}>
        Лента из открытых RSS (обновление на сервере ~3 мин)
        {newsMeta ? ` · ${newsMeta}` : ""}
        {newsErr ? " · не удалось загрузить, ниже темы банка" : ""}
      </p>

      {news.length > 0 && (
        <ul className="news-list">
          {news.map((n) => (
            <li key={n.id}>
              <button
                type="button"
                className={"news-item" + (selectedId === n.id ? " is-on" : "")}
                onClick={() => {
                  setSelectedId(n.id);
                  setText(n.text);
                }}
              >
                <span className="news-item-meta">
                  <span className="news-item-tag">{n.category}</span>
                  <span className="news-item-src">{n.source}</span>
                </span>
                <span className="news-item-title">{n.title}</span>
              </button>
            </li>
          ))}
        </ul>
      )}

      <div className="chips wrap" style={{ marginTop: 12 }}>
        <span className="muted small" style={{ width: "100%", marginBottom: 4 }}>
          Темы из аналитики банка
        </span>
        {PRESET_EVENTS.map((e) => (
          <button
            key={e.id}
            type="button"
            className={"chip-btn" + (text === e.text ? " is-on" : "")}
            onClick={() => {
              setSelectedId(null);
              setText(e.text);
            }}
          >
            {e.title}
          </button>
        ))}
      </div>

      <textarea
        className="input area"
        rows={4}
        value={text}
        onChange={(e) => {
          setSelectedId(null);
          setText(e.target.value);
        }}
        placeholder="Или вставьте свой текст новости / события"
      />
      <div className="row">
        <button
          type="button"
          className="btn btn--red"
          disabled={disabled || text.trim().length < 5}
          onClick={() => onRun(text.trim())}
        >
          {label}
        </button>
        <span className="muted small">Обычно 20–90 секунд</span>
      </div>
    </section>
  );
}
