export function money(v?: number | null): string {
  if (v == null || Number.isNaN(v)) return "—";
  return (
    new Intl.NumberFormat("ru-RU", { notation: "compact", maximumFractionDigits: 1 }).format(v) + " ₽"
  );
}

export const PRODUCT_LABELS: Record<string, string> = {
  rko: "РКО",
  loan: "Кредит",
  guarantee: "Гарантии",
  leasing: "Лизинг",
  deposit: "Депозит",
  payroll: "Зарплатный проект",
  factoring: "Факторинг",
};

export const SOURCE_LABELS: Record<string, string> = {
  alfa_analytics: "Аналитика Альфа-Банка",
  company_feature: "Данные о компании",
  news: "Новость",
};

export const TOOL_LABELS: Record<string, string> = {
  get_company_profile: "Профиль компании",
  search_news: "Поиск новостей",
  search_alfa_analytics: "Поиск по аналитике банка",
  draft_insight: "Сбор инсайта",
};

export const ACTION_LABELS: Record<string, string> = {
  start: "Старт",
  tool: "Вызов инструмента",
  tool_result: "Результат инструмента",
  self_check: "Самопроверка",
  retry_draft: "Доработка после проверки",
  ensure_news: "Догрузка новостей",
  ensure_alfa: "Догрузка аналитики банка",
  ensure_draft: "Догрузка черновика",
  skip_draft: "Черновик пропущен",
  draft_parse_fail: "Ошибка разбора черновика",
  finish: "Завершение плана",
  done: "Готово",
  error: "Ошибка",
};
