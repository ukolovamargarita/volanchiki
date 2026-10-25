import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { api, USE_MOCK } from "./api";

const fetchMock = vi.fn();

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe("api (real mode)", () => {
  it("runs in real mode by default in tests", () => {
    expect(USE_MOCK).toBe(false);
  });

  it("requests demo companies with the take parameter", async () => {
    fetchMock.mockResolvedValue(jsonResponse([]));
    await api.demoCompanies(5);
    expect(fetchMock.mock.calls[0][0]).toBe("/api/companies/demo?take=5");
  });

  it("url-encodes the search query", async () => {
    fetchMock.mockResolvedValue(jsonResponse([]));
    await api.searchCompanies("сталь & сплавы");
    expect(fetchMock.mock.calls[0][0]).toBe(
      `/api/companies?q=${encodeURIComponent("сталь & сплавы")}&take=12`,
    );
  });

  it("url-encodes the company id", async () => {
    fetchMock.mockResolvedValue(jsonResponse({}));
    await api.company("U 1/2");
    expect(fetchMock.mock.calls[0][0]).toBe("/api/companies/U%201%2F2");
  });

  it("posts companyId and newsHint as JSON to /api/insights/run", async () => {
    fetchMock.mockResolvedValue(jsonResponse({ runId: "r", status: "ok", trace: [] }));
    await api.runInsight("U_1", "Ключевая ставка 14%");

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/insights/run");
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body)).toEqual({ companyId: "U_1", newsHint: "Ключевая ставка 14%" });
    expect(init.headers["Content-Type"]).toContain("application/json");
  });

  it("returns the parsed body on success", async () => {
    fetchMock.mockResolvedValue(jsonResponse({ status: "ok", dataLoaded: true, companies: 3 }));
    const h = await api.health();
    expect(h.companies).toBe(3);
  });

  it("throws the backend error code when the response is not ok", async () => {
    fetchMock.mockResolvedValue(jsonResponse({ error: "company_not_found" }, 404));
    await expect(api.runInsight("nope", "текст новости")).rejects.toThrow("company_not_found");
  });

  it("falls back to the HTTP status when the error body is not JSON", async () => {
    fetchMock.mockResolvedValue(new Response("<html>boom</html>", { status: 502 }));
    await expect(api.health()).rejects.toThrow("HTTP 502");
  });

  it("turns an aborted request into a readable timeout message", async () => {
    vi.useFakeTimers();
    fetchMock.mockImplementation(
      (_url: string, init: RequestInit) =>
        new Promise((_res, rej) => {
          init.signal?.addEventListener("abort", () => rej(new DOMException("aborted", "AbortError")));
        }),
    );

    const p = api.health();
    const assertion = expect(p).rejects.toThrow("Сервер слишком долго отвечает");
    await vi.advanceTimersByTimeAsync(20_001);
    await assertion;
  });

  it("gives the insight run much more time than ordinary calls (agent takes up to 90 s)", async () => {
    vi.useFakeTimers();
    let aborted = false;
    fetchMock.mockImplementation(
      (_url: string, init: RequestInit) =>
        new Promise((_res, rej) => {
          init.signal?.addEventListener("abort", () => {
            aborted = true;
            rej(new DOMException("aborted", "AbortError"));
          });
        }),
    );

    const p = api.runInsight("U_1", "новость для проверки");
    p.catch(() => undefined);
    await vi.advanceTimersByTimeAsync(100_000);
    expect(aborted).toBe(false);

    await vi.advanceTimersByTimeAsync(81_000);
    expect(aborted).toBe(true);
  });
});

describe("api.news", () => {
  it("requests the plain feed without a query string when no options are given", async () => {
    fetchMock.mockResolvedValue(jsonResponse({ items: [], meta: { total: 0, live: 0 } }));
    await api.news();
    expect(fetchMock.mock.calls[0][0]).toBe("/api/news");
  });

  it("passes q, companyId and take as query parameters", async () => {
    fetchMock.mockResolvedValue(jsonResponse({ items: [], meta: { total: 0, live: 0 } }));
    await api.news({ q: "ставка", companyId: "U_1", take: 5 });

    const url = new URL(fetchMock.mock.calls[0][0], "http://x");
    expect(url.pathname).toBe("/api/news");
    expect(url.searchParams.get("q")).toBe("ставка");
    expect(url.searchParams.get("companyId")).toBe("U_1");
    expect(url.searchParams.get("take")).toBe("5");
  });

  it("omits empty options", async () => {
    fetchMock.mockResolvedValue(jsonResponse({ items: [], meta: { total: 0, live: 0 } }));
    await api.news({ q: "", take: 3 });

    expect(fetchMock.mock.calls[0][0]).toBe("/api/news?take=3");
  });
});
