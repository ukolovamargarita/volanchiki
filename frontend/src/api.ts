import type { Company, CompanyDetails, Health, NewsFeed, RunResult } from "./types";
import { mockCompanies, mockDetails, mockHealth, mockNews, mockRun } from "./mock";

// VITE_USE_MOCK=true  -> всё работает на заглушках (npm run dev:mock), бэкенд не нужен
// VITE_API_BASE       -> если API на другом домене (по умолчанию тот же origin, /api)
export const USE_MOCK = import.meta.env.VITE_USE_MOCK === "true";
const BASE: string = import.meta.env.VITE_API_BASE ?? "";

async function http<T>(path: string, init?: RequestInit, timeoutMs = 20000): Promise<T> {
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), timeoutMs);
  try {
    const res = await fetch(BASE + path, { ...init, signal: ctrl.signal });
    if (!res.ok) {
      let msg = `HTTP ${res.status}`;
      try {
        const body = await res.json();
        msg = body?.error ?? body?.title ?? msg;
      } catch {
        /* тело не JSON */
      }
      throw new Error(msg);
    }
    return (await res.json()) as T;
  } catch (e) {
    if (e instanceof DOMException && e.name === "AbortError") {
      throw new Error("Сервер слишком долго отвечает. Попробуйте ещё раз.");
    }
    throw e;
  } finally {
    clearTimeout(timer);
  }
}

export const api = {
  health: (): Promise<Health> => (USE_MOCK ? mockHealth() : http("/api/health")),

  demoCompanies: (take = 8): Promise<Company[]> =>
    USE_MOCK ? mockCompanies() : http(`/api/companies/demo?take=${take}`),

  searchCompanies: (q: string): Promise<Company[]> =>
    USE_MOCK ? mockCompanies(q) : http(`/api/companies?q=${encodeURIComponent(q)}&take=12`),

  company: (id: string): Promise<CompanyDetails> =>
    USE_MOCK ? mockDetails(id) : http(`/api/companies/${encodeURIComponent(id)}`),

  news: (opts?: { q?: string; companyId?: string; take?: number }): Promise<NewsFeed> => {
    if (USE_MOCK) return mockNews(opts);
    const p = new URLSearchParams();
    if (opts?.q) p.set("q", opts.q);
    if (opts?.companyId) p.set("companyId", opts.companyId);
    if (opts?.take) p.set("take", String(opts.take));
    const qs = p.toString();
    return http(`/api/news${qs ? `?${qs}` : ""}`);
  },

  // Агент работает 20–90 сек, поэтому таймаут большой
  runInsight: (companyId: string, newsHint: string): Promise<RunResult> =>
    USE_MOCK
      ? mockRun(companyId, newsHint)
      : http(
          "/api/insights/run",
          {
            method: "POST",
            headers: { "Content-Type": "application/json; charset=utf-8" },
            body: JSON.stringify({ companyId, newsHint }),
          },
          180000,
        ),
};
