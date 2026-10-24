// Типы повторяют JSON, который отдаёт бэкенд (ASP.NET, camelCase).
// Источник правды: src/Volanchiki.Api/Domain/*.cs и Controllers/*.cs

export interface Products {
  rko: boolean;
  loan: boolean;
  guarantee: boolean;
  leasing: boolean;
  deposit: boolean;
  payroll: boolean;
  factoring: boolean;
}

export interface Company {
  id: string;
  clientType: string;
  okvedCode?: string | null;
  okvedName?: string | null;
  revenue?: number | null;
  city?: string | null;
  region?: string | null;
  groupId?: string | null;
  products: Products;
}

export interface Payment {
  payerId: string;
  beneficiaryId: string;
  amount: number;
  count: number;
}

export interface CompanyDetails {
  company: Company;
  group: { id?: string | null; size: number; revenue?: number | null; members: Company[] };
  payments: Payment[];
}

export interface InsightSource {
  kind: string; // company_feature | alfa_analytics | news | ...
  title: string;
  url?: string | null;
  excerpt?: string | null;
}

export interface InsightCard {
  id: string;
  companyId: string;
  title: string;
  whatHappened: string;
  whyMattersForCompany: string;
  bankView: string;
  modelView: string;
  risks: string[];
  opportunities: string[];
  suggestedActions: string[];
  sources: InsightSource[];
  relevance: "low" | "medium" | "high" | string;
  createdAt: string;
  disclaimer: string;
  cta: string;
}

export interface TraceStep {
  step: number;
  action: string;
  tool?: string | null;
  detail?: string | null;
  at: string;
}

export interface RunResult {
  runId: string;
  insight?: InsightCard | null;
  trace: TraceStep[];
  status: string;
  error?: string | null;
}

export interface Health {
  status: string;
  dataLoaded: boolean;
  companies: number;
  alfaDocs: number;
  newsCount?: number;
  newsLive?: number;
  llmConfigured: boolean;
}

export interface NewsItem {
  id: string;
  source: string;
  title: string;
  summary: string;
  url?: string | null;
  category: string;
  publishedAt: string;
  score: number;
  isSeed: boolean;
  text: string;
}

export interface NewsFeed {
  items: NewsItem[];
  meta: {
    total: number;
    live: number;
    lastSuccessUtc?: string | null;
    lastAttemptUtc?: string | null;
    lastError?: string | null;
  };
}
