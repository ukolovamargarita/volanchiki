import { useEffect, useState } from "react";
import { api, USE_MOCK } from "./api";
import type { Health } from "./types";
import { NewsWidget } from "./components/NewsWidget";
import { Workspace } from "./components/Workspace";

type View = "home" | "insights";

const NAV = ["Главная", "Счета и платежи", "Кредиты", "Валюта", "Документы", "Аналитика"];

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
      <aside className="sidebar">
        <div className="logo" onClick={() => setView("home")} role="button" tabIndex={0}>
          <span className="logo-mark">А</span>
          <span>Альфа-Бизнес</span>
        </div>
        <nav>
          {NAV.map((n, i) => (
            <button key={n} className={"nav-item" + (i === 0 && view === "home" ? " active" : "")} onClick={() => i === 0 && setView("home")}>
              {n}
            </button>
          ))}
          <button className={"nav-item accent" + (view === "insights" ? " active" : "")} onClick={() => open()}>
            <span className="dot" /> Новостные инсайты
          </button>
        </nav>
        <div className="sidebar-foot">
          <StatusPill health={health} err={healthErr} />
        </div>
      </aside>

      <main className="main">
        <header className="topbar">
          <div>
            <div className="eyebrow">{view === "home" ? "Главная" : "Новостные инсайты"}</div>
            <h1>{view === "home" ? "Добрый день" : "Что новость значит для вашего бизнеса"}</h1>
          </div>
          {USE_MOCK && <span className="mock-flag">демо-режим без сервера</span>}
        </header>

        {view === "home" ? (
          <div className="home-grid">
            <NewsWidget onOpen={open} />
            <FakeCard title="Остатки на счетах" value="1 284 560 000 ₽" note="по всем счетам" />
            <FakeCard title="Платежи сегодня" value="37" note="12 ожидают подписи" />
          </div>
        ) : (
          <Workspace initialEvent={presetEvent} />
        )}
      </main>
    </div>
  );
}

function FakeCard({ title, value, note }: { title: string; value: string; note: string }) {
  return (
    <section className="card fake">
      <div className="muted">{title}</div>
      <div className="big">{value}</div>
      <div className="muted small">{note}</div>
    </section>
  );
}

function StatusPill({ health, err }: { health: Health | null; err: boolean }) {
  if (err) return <div className="status bad">API недоступен</div>;
  if (!health) return <div className="status">Проверка API…</div>;
  if (!health.dataLoaded) return <div className="status warn">Данные загружаются…</div>;
  if (!health.llmConfigured) return <div className="status warn">LLM-ключ не задан</div>;
  return <div className="status ok">API онлайн · {health.companies.toLocaleString("ru-RU")} компаний</div>;
}
