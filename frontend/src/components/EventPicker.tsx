import { useEffect, useState } from "react";
import { PRESET_EVENTS } from "../events";

export function EventPicker({
  initial,
  disabled,
  onRun,
}: {
  initial?: string;
  disabled: boolean;
  onRun: (text: string) => void;
}) {
  const [text, setText] = useState(initial ?? PRESET_EVENTS[0].text);

  useEffect(() => {
    if (initial) setText(initial);
  }, [initial]);

  return (
    <section className="card">
      <h3>2. Выберите событие или вставьте свою новость</h3>
      <div className="chips wrap">
        {PRESET_EVENTS.map((e) => (
          <button key={e.id} className={"chip btn-chip" + (text === e.text ? " on" : "")} onClick={() => setText(e.text)}>
            {e.title}
          </button>
        ))}
      </div>
      <textarea
        className="input area"
        rows={4}
        value={text}
        onChange={(e) => setText(e.target.value)}
        placeholder="Вставьте текст новости или опишите событие"
      />
      <div className="row">
        <button className="btn red" disabled={disabled || text.trim().length < 5} onClick={() => onRun(text.trim())}>
          {disabled ? "Анализируем…" : "Получить инсайт"}
        </button>
        <span className="muted small">Обычно 20–90 секунд: агент собирает профиль, аналитику банка и проверяет вывод</span>
      </div>
    </section>
  );
}
