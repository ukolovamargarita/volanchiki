// Заглушки для разработки без бэкенда: npm run dev:mock
// Тексты вымышленные, только чтобы показать, как выглядит интерфейс.
import type { Company, CompanyDetails, Health, RunResult } from "./types";

const wait = (ms: number) => new Promise((r) => setTimeout(r, ms));

const none = { rko: true, loan: false, guarantee: false, leasing: false, deposit: false, payroll: true, factoring: false };

const COMPANIES: Company[] = [
  {
    id: "U_DEMO_1",
    clientType: "CLIENT",
    okvedCode: "46.51",
    okvedName: "Торговля оптовая компьютерами и периферийными устройствами",
    revenue: 18_400_000_000,
    city: "Москва",
    region: "Москва",
    groupId: "G_101",
    products: { ...none, loan: true, leasing: true, factoring: true },
  },
  {
    id: "U_DEMO_2",
    clientType: "CLIENT",
    okvedCode: "24.10",
    okvedName: "Производство чугуна, стали и ферросплавов",
    revenue: 63_900_000_000,
    city: "Липецк",
    region: "Липецкая область",
    groupId: "G_202",
    products: { ...none, guarantee: true, deposit: true },
  },
  {
    id: "U_DEMO_3",
    clientType: "CLIENT",
    okvedCode: "47.11",
    okvedName: "Торговля розничная в неспециализированных магазинах",
    revenue: 27_100_000_000,
    city: "Казань",
    region: "Татарстан",
    groupId: "G_303",
    products: { ...none, loan: true, deposit: true },
  },
];

export async function mockHealth(): Promise<Health> {
  await wait(200);
  return { status: "ok", dataLoaded: true, companies: 12345, alfaDocs: 9, llmConfigured: true };
}

export async function mockCompanies(q?: string): Promise<Company[]> {
  await wait(300);
  if (!q) return COMPANIES;
  return COMPANIES.filter((c) => (c.id + (c.okvedName ?? "")).toLowerCase().includes(q.toLowerCase()));
}

export async function mockDetails(id: string): Promise<CompanyDetails> {
  await wait(300);
  const c = COMPANIES.find((x) => x.id === id) ?? COMPANIES[0];
  return {
    company: c,
    group: { id: c.groupId, size: 4, revenue: (c.revenue ?? 0) * 1.6, members: COMPANIES.slice(0, 3) },
    payments: [
      { payerId: c.id, beneficiaryId: "U_5521", amount: 420_000_000, count: 38 },
      { payerId: "U_7710", beneficiaryId: c.id, amount: 910_000_000, count: 64 },
    ],
  };
}

export async function mockRun(companyId: string, newsHint: string): Promise<RunResult> {
  await wait(3500);
  const c = COMPANIES.find((x) => x.id === companyId) ?? COMPANIES[0];
  return {
    runId: "mock-run",
    status: "ok",
    insight: {
      id: "mock-insight",
      companyId: c.id,
      title: "Дорогие деньги задерживаются: кредитная нагрузка не снизится",
      whatHappened: newsHint,
      whyMattersForCompany: c.products.loan
        ? "У компании есть кредитные продукты, а значит, стоимость обслуживания долга останется высокой дольше, чем ожидалось."
        : "Кредитных продуктов у компании нет, но высокая ставка делает привлекательнее размещение свободных средств.",
      bankView: "Аналитики Альфа-Банка отмечают, что риторика регулятора стала жёстче, а пространство для снижения ставки сократилось.",
      modelView: "Гипотеза модели: пересмотр планов по заёмному финансированию инвестиций на ближайшие два квартала.",
      risks: ["Рост процентных расходов при плавающей ставке", "Давление на маржу у закупщиков в долг"],
      opportunities: ["Размещение временно свободных средств на депозит с повышенной доходностью"],
      suggestedActions: ["Пересчитать график платежей по кредитам", "Обсудить с банком фиксацию ставки"],
      sources: [
        { kind: "alfa_analytics", title: "ALFA-Macro_Insights-Policy_Rate_11Sep", excerpt: "Ставка без изменения на 14,0%, риторика стала жёстче" },
        { kind: "company_feature", title: "Профиль компании", excerpt: "Продукты: " + (c.products.loan ? "кредит" : "РКО") },
      ],
      relevance: "high",
      createdAt: new Date().toISOString(),
      disclaimer: "Материал носит информационный характер и не является индивидуальной инвестиционной рекомендацией.",
      cta: "Связаться с менеджером",
    },
    trace: [
      { step: 1, action: "start", at: new Date().toISOString(), detail: "company=" + c.id },
      { step: 2, action: "tool", tool: "get_company_profile", detail: "need profile", at: new Date().toISOString() },
      { step: 3, action: "tool", tool: "search_alfa_analytics", detail: "need alfa", at: new Date().toISOString() },
      { step: 4, action: "tool", tool: "draft_insight", detail: "need draft", at: new Date().toISOString() },
      { step: 5, action: "self_check", detail: "ok", at: new Date().toISOString() },
      { step: 6, action: "done", at: new Date().toISOString() },
    ],
  };
}
