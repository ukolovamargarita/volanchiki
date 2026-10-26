import type { Company, CompanyDetails, Health, NewsFeed, RunResult } from "./types";
import { mockCompanies, mockDetails, mockHealth, mockNews, mockRun } from "./mock";

// VITE_USE_MOCK=true  -> всё работает на заглушках (npm run dev:mock), бэкенд не нужен
// VITE_API_BASE       -> если API на другом домене (по умолчанию тот же origin, /api)
export const USE_MOCK = import.meta.env.VITE_USE_MOCK === "true";
const BASE: string = import.meta.env.VITE_API_BASE ?? "";

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

function isNetworkError(e: unknown): boolean {
  if (e instanceof TypeError) return true; // Failed to fetch / NetworkError
  if (e instanceof DOMException && e.name === "AbortError") return true;
  const msg = e instanceof Error ? e.message.toLowerCase() : String(e).toLowerCase();
  return (
    msg.includes("failed to fetch") ||
    msg.includes("network") ||
    msg.includes("load failed") ||
    msg.includes("сетев")
  );
}

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

/** Короткий запрос с повторами при обрыве сети (смена Wi‑Fi ↔ мобильный). */
async function httpResilient<T>(path: string, init?: RequestInit, timeoutMs = 20000, attempts = 5): Promise<T> {
  let last: unknown;
  for (let i = 0; i < attempts; i++) {
    try {
      return await http<T>(path, init, timeoutMs);
    } catch (e) {
      last = e;
      if (!isNetworkError(e) || i === attempts - 1) throw e;
      await sleep(800 * (i + 1));
    }
  }
  throw last instanceof Error ? last : new Error(String(last));
}

type StartRunResponse = { runId: string; status: string };

async function pollRun(runId: string): Promise<RunResult> {
  const deadline = Date.now() + 180_000;
  let netFails = 0;

  while (Date.now() < deadline) {
    await sleep(1500);
    try {
      const r = await http<RunResult>(`/api/insights/runs/${encodeURIComponent(runId)}`, undefined, 20000);
      netFails = 0;
      if (r.status === "running") continue;
      return r;
    } catch (e) {
      if (isNetworkError(e)) {
        netFails++;
        if (netFails >= 25) {
          throw new Error(
            "Сеть нестабильна слишком долго. Обновите страницу — если расчёт успел завершиться, попробуйте ещё раз.",
          );
        }
        continue;
      }
      throw e;
    }
  }

  throw new Error("Расчёт занял слишком много времени. Попробуйте ещё раз.");
}

async function runInsightLive(companyId: string, newsHint: string): Promise<RunResult> {
  const start = await httpResilient<StartRunResponse>(
    "/api/insights/run",
    {
      method: "POST",
      headers: { "Content-Type": "application/json; charset=utf-8" },
      body: JSON.stringify({ companyId, newsHint }),
    },
    30000,
    5,
  );

  if (!start?.runId) throw new Error("Сервер не вернул runId");
  return pollRun(start.runId);
}

async function clarifyLive(runId: string, question: string): Promise<RunResult> {
  const start = await httpResilient<StartRunResponse>(
    `/api/insights/runs/${encodeURIComponent(runId)}/clarify`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json; charset=utf-8" },
      body: JSON.stringify({ question }),
    },
    30000,
    5,
  );
  if (!start?.runId) throw new Error("Сервер не вернул runId");
  return pollRun(start.runId);
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

  // Старт + poll: смена сети не роняет уже идущий прогон на сервере
  runInsight: (companyId: string, newsHint: string): Promise<RunResult> =>
    USE_MOCK ? mockRun(companyId, newsHint) : runInsightLive(companyId, newsHint),

  clarifyInsight: (runId: string, question: string): Promise<RunResult> =>
    USE_MOCK ? mockRun("clarify", question) : clarifyLive(runId, question),
};
