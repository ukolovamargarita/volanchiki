// Три слоя происхождения информации — требование кейса (критерий 03):
// пользователь должен видеть, что факт, что позиция банка, а что вывод AI.
export type Origin = "fact" | "bank" | "ai";

const META: Record<Origin, { label: string; hint: string }> = {
  fact: { label: "Факт из источника", hint: "Информация из первоисточника (новость, данные)" },
  bank: { label: "Позиция Альфа-Банка", hint: "Из аналитических материалов банка" },
  ai: { label: "Вывод AI", hint: "Гипотеза модели на основе данных о компании" },
};

export function Badge({ origin }: { origin: Origin }) {
  return (
    <span className={"badge origin-" + origin} title={META[origin].hint}>
      {META[origin].label}
    </span>
  );
}
