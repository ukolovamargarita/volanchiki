import type { Company, CompanyDetails, NewsFeed, RunResult } from "../types";

export const company: Company = {
  id: "U_TEST_1",
  clientType: "CLIENT",
  okvedCode: "24.10",
  okvedName: "Производство стали",
  revenue: 18_400_000_000,
  city: "Липецк",
  region: "Липецкая область",
  groupId: "G1",
  products: { rko: true, loan: true, guarantee: false, leasing: false, deposit: false, payroll: false, factoring: false },
};

export const runResult: RunResult = {
  runId: "run-1",
  status: "ok",
  insight: {
    id: "i1",
    companyId: company.id,
    title: "Заголовок инсайта",
    whatHappened: "ТЕКСТ-ФАКТА ЦБ сохранил ставку",
    whyMattersForCompany: "ТЕКСТ-ПОЧЕМУ-ВАЖНО у компании есть кредит",
    bankView: "ТЕКСТ-БАНКА аналитики банка отмечают",
    modelView: "ТЕКСТ-МОДЕЛИ гипотеза",
    risks: ["РИСК-1", "РИСК-2"],
    opportunities: ["ВОЗМОЖНОСТЬ-1"],
    suggestedActions: ["ДЕЙСТВИЕ-1"],
    sources: [
      { kind: "alfa_analytics", title: "Policy_Rate_11Sep", excerpt: "Ставка без изменения на 14,0%" },
      { kind: "news", title: "Новость РБК", url: "https://example.com/n" },
    ],
    relevance: "high",
    createdAt: "2026-10-25T10:00:00Z",
    disclaimer: "Информационный материал",
    cta: "Связаться с менеджером",
  },
  trace: [
    { step: 1, action: "start", at: "2026-10-25T10:00:00Z", detail: "company=U_TEST_1" },
    { step: 2, action: "tool", tool: "get_company_profile", at: "2026-10-25T10:00:01Z", detail: "need profile" },
    { step: 3, action: "done", at: "2026-10-25T10:00:09Z" },
  ],
};

export const newsFeed: NewsFeed = {
  items: [
    {
      id: "n1",
      source: "РБК",
      title: "ЦБ сохранил ключевую ставку",
      summary: "Кратко",
      url: "https://example.com/1",
      category: "Ставка",
      publishedAt: "2026-10-25T09:00:00Z",
      score: 9,
      isSeed: false,
      text: "ТЕКСТ-НОВОСТИ-1 ЦБ сохранил ключевую ставку",
    },
    {
      id: "n2",
      source: "Интерфакс",
      title: "Инфляция замедлилась",
      summary: "Кратко",
      url: null,
      category: "Инфляция",
      publishedAt: "2026-10-25T08:00:00Z",
      score: 5,
      isSeed: false,
      text: "ТЕКСТ-НОВОСТИ-2 Инфляция замедлилась",
    },
  ],
  meta: { total: 5, live: 2 },
};

export const details: CompanyDetails = {
  company,
  group: { id: "G1", size: 3, revenue: 40_000_000_000, members: [company] },
  payments: [],
};
