import { useEffect, useState } from "react";

// Это оценка этапов по времени, а не реальный стрим от бэкенда.
// Реальные шаги приходят в трассировке после ответа (см. TracePanel).
const STAGES = [
  "Собираем профиль компании",
  "Ищем позицию Альфа-Банка в аналитике",
  "Формулируем инсайт",
  "Проверяем обоснованность вывода",
];

export function RunProgress() {
  const [i, setI] = useState(0);
  useEffect(() => {
    const t = setInterval(() => setI((x) => Math.min(x + 1, STAGES.length - 1)), 9000);
    return () => clearInterval(t);
  }, []);
  return (
    <section className="card progress">
      <div className="spinner" />
      <ol>
        {STAGES.map((s, k) => (
          <li key={s} className={k < i ? "done" : k === i ? "now" : ""}>
            {s}
          </li>
        ))}
      </ol>
    </section>
  );
}
