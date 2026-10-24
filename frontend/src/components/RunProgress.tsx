import { useEffect, useState } from "react";

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
    <section className="panel progress">
      <div className="spinner" aria-hidden />
      <ol>
        {STAGES.map((s, k) => (
          <li key={s} className={k < i ? "is-done" : k === i ? "is-now" : ""}>
            {s}
          </li>
        ))}
      </ol>
    </section>
  );
}
