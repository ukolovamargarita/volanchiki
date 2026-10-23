import { PRESET_EVENTS } from "../events";

// Заметная и постоянно доступная точка входа на главном экране (критерий 09)
export function NewsWidget({ onOpen }: { onOpen: (eventText?: string) => void }) {
  return (
    <section className="card widget">
      <div className="widget-head">
        <div>
          <div className="eyebrow light">Новостные инсайты</div>
          <h2>Что происходит на рынке — и что это значит именно для вас</h2>
        </div>
        <button className="btn lime" onClick={() => onOpen()}>
          Открыть
        </button>
      </div>
      <ul className="widget-list">
        {PRESET_EVENTS.slice(0, 3).map((e) => (
          <li key={e.id}>
            <button onClick={() => onOpen(e.text)}>
              <span className="chip dark">{e.tag}</span>
              <span>{e.title}</span>
              <span className="arrow">→</span>
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}
