import { useEffect, useState } from "react";
import { api } from "../api";
import { PRESET_EVENTS } from "../events";
import type { NewsItem } from "../types";

// Заметная точка входа на главном экране (критерий 09)
export function NewsWidget({ onOpen }: { onOpen: (eventText?: string) => void }) {
  const [items, setItems] = useState<{ tag: string; title: string; text: string }[]>(
    PRESET_EVENTS.slice(0, 3).map((e) => ({ tag: e.tag, title: e.title, text: e.text })),
  );

  useEffect(() => {
    let cancelled = false;
    api
      .news({ take: 3 })
      .then((r) => {
        if (cancelled || !r.items.length) return;
        setItems(
          r.items.map((n: NewsItem) => ({
            tag: n.category || n.source,
            title: n.title,
            text: n.text,
          })),
        );
      })
      .catch(() => {
        /* оставляем пресеты */
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <section className="hero">
      <div className="hero-bg" aria-hidden />
      <div className="hero-inner">
        <div className="hero-copy">
          <p className="hero-brand">Альфа-Бизнес</p>
          <h1 className="hero-title">Новостные инсайты</h1>
          <p className="hero-lead">
            Не лента новостей — персональная оценка события для вашей компании: факты, позиция банка и вывод модели.
          </p>
          <div className="hero-actions">
            <button type="button" className="btn btn--light" onClick={() => onOpen()}>
              Открыть раздел
            </button>
          </div>
        </div>

        <ul className="hero-events">
          {items.map((e, i) => (
            <li key={e.title + i} style={{ animationDelay: `${0.12 + i * 0.08}s` }}>
              <button type="button" className="hero-event" onClick={() => onOpen(e.text)}>
                <span className="hero-event-tag">{e.tag}</span>
                <span className="hero-event-title">{e.title}</span>
                <span className="hero-event-go" aria-hidden>
                  →
                </span>
              </button>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
