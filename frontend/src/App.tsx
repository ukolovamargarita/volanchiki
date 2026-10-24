import { useEffect, useState } from "react";
import { api, USE_MOCK } from "./api";
import type { Health } from "./types";
import { NewsWidget } from "./components/NewsWidget";
import { Workspace } from "./components/Workspace";

type View = "home" | "insights";

const NAV = [
  { id: "home", label: "Главная" },
  { id: "accounts", label: "Счета" },
  { id: "payments", label: "Платежи" },
  { id: "credits", label: "Кредиты" },
  { id: "docs", label: "Документы" },
] as const;

export default function App() {
  const [view, setView] = useState<View>("home");
  const [presetEvent, setPresetEvent] = useState<string | undefined>();
  const [health, setHealth] = useState<Health | null>(null);
  const [healthErr, setHealthErr] = useState(false);

  useEffect(() => {
    api.health().then(setHealth).catch(() => setHealthErr(true));
  }, []);

  const open = (eventText?: string) => {
    setPresetEvent(eventText);
    setView("insights");
    window.scrollTo({ top: 0 });
  };

  return (
    <div className="shell">
      <header className="topnav">
        <div className="topnav-inner">
          <button className="brand" onClick={() => setView("home")} type="button">
            <span className="brand-mark" aria-hidden>
              А
            </span>
            <span className="brand-text">
              <span className="brand-name">Альфа-Бизнес</span>
              <span className="brand-sub">Интернет-банк</span>
            </span>
          </button>

          <nav className="topnav-links" aria-label="Разделы">
            {NAV.map((n) => (
              <button
                key={n.id}
                type="button"
                className={"topnav-link" + (n.id === "home" && view === "home" ? " is-active" : "")}
                onClick={() => n.id === "home" && setView("home")}
              >
                {n.label}
              </button>
            ))}
            <button
              type="button"
              className={"topnav-link topnav-link--feature" + (view === "insights" ? " is-active" : "")}
              onClick={() => open()}
            >
              Инсайты
            </button>
          </nav>

          <div className="topnav-meta">
            {USE_MOCK && <span className="pill pill--demo">демо</span>}
            <StatusPill health={health} err={healthErr} />
          </div>
        </div>
      </header>

      <main className="main">
        {view === "home" ? (
          <>
            <NewsWidget onOpen={open} />
            <section className="dash-strip" aria-label="Сводка">
              <div className="dash-item">
                <div className="dash-label">Остатки на счетах</div>
                <div className="dash-value">1 284 560 000 ₽</div>
                <div className="dash-note">по всем счетам</div>
              </div>
              <div className="dash-item">
                <div className="dash-label">Платежи сегодня</div>
                <div className="dash-value">37</div>
                <div className="dash-note">12 ожидают подписи</div>
              </div>
              <div className="dash-item dash-item--action">
                <div className="dash-label">Новостные инсайты</div>
                <p className="dash-copy">Персональная оценка событий рынка для вашей компании</p>
                <button type="button" className="btn btn--ghost" onClick={() => open()}>
                  Перейти
                </button>
              </div>
            </section>
          </>
        ) : (
          <>
            <div className="page-head">
              <button type="button" className="back-link" onClick={() => setView("home")}>
                ← Главная
              </button>
              <h1 className="page-title">Новостные инсайты</h1>
              <p className="page-lead">Что событие означает для вашего бизнеса — с опорой на аналитику Альфа-Банка</p>
            </div>
            <Workspace initialEvent={presetEvent} />
          </>
        )}
      </main>
    </div>
  );
}

function StatusPill({ health, err }: { health: Health | null; err: boolean }) {
  if (err) return <span className="pill pill--bad">API недоступен</span>;
  if (!health) return <span className="pill">Проверка…</span>;
  if (!health.dataLoaded) return <span className="pill pill--warn">Данные…</span>;
  if (!health.llmConfigured) return <span className="pill pill--warn">Нет LLM-ключа</span>;
  return <span className="pill pill--ok">{health.companies.toLocaleString("ru-RU")} компаний</span>;
}
