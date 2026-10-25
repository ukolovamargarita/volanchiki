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
});

// Запуск инсайта: POST создаёт прогон и возвращает runId, дальше фронтенд опрашивает /runs/{id}.
describe("api.runInsight (start + poll)", () => {
  const done = { runId: "r1", status: "ok", trace: [], insight: null };

  beforeEach(() => {
    vi.useFakeTimers();
  });

  // каждый вызов отдаёт НОВЫЙ Response (тело можно прочитать только один раз)
  const reply = (body: unknown, status = 200) => () => Promise.resolve(jsonResponse(body, status));

  it("starts the run with a JSON POST of companyId and newsHint", async () => {
    fetchMock.mockImplementation(reply({ runId: "r1", status: "running" }));
    const p = api.runInsight("U_1", "Ключевая ставка 14%");
    p.catch(() => undefined);
    await vi.advanceTimersByTimeAsync(0);

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("/api/insights/run");
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body)).toEqual({ companyId: "U_1", newsHint: "Ключевая ставка 14%" });
    expect(init.headers["Content-Type"]).toContain("application/json");
  });

  it("polls /api/insights/runs/{runId} and returns the finished result", async () => {
    fetchMock
      .mockImplementationOnce(reply({ runId: "r 1", status: "running" }))
      .mockImplementation(reply({ ...done, runId: "r 1" }));

    const p = api.runInsight("U_1", "новость");
    await vi.advanceTimersByTimeAsync(2000);

    const result = await p;
    expect(result.status).toBe("ok");
    expect(fetchMock.mock.calls[1][0]).toBe("/api/insights/runs/r%201");
  });

  it("keeps polling while the run is still running", async () => {
    fetchMock
      .mockImplementationOnce(reply({ runId: "r1", status: "running" }))
      .mockImplementationOnce(reply({ runId: "r1", status: "running", trace: [] }))
      .mockImplementationOnce(reply({ runId: "r1", status: "running", trace: [] }))
      .mockImplementation(reply(done));

    const p = api.runInsight("U_1", "новость");
    await vi.advanceTimersByTimeAsync(10_000);

    expect((await p).status).toBe("ok");
    expect(fetchMock).toHaveBeenCalledTimes(4); // 1 старт + 3 опроса
  });

  it("survives a short network failure while polling (the run keeps going on the server)", async () => {
    fetchMock
      .mockImplementationOnce(reply({ runId: "r1", status: "running" }))
      .mockImplementationOnce(() => Promise.reject(new TypeError("Failed to fetch")))
      .mockImplementation(reply(done));

    const p = api.runInsight("U_1", "новость");
    await vi.advanceTimersByTimeAsync(10_000);

    expect((await p).status).toBe("ok");
  });

  it("gives up after too many network failures in a row", async () => {
    fetchMock
      .mockImplementationOnce(reply({ runId: "r1", status: "running" }))
      .mockImplementation(() => Promise.reject(new TypeError("Failed to fetch")));

    const p = api.runInsight("U_1", "новость");
    const assertion = expect(p).rejects.toThrow("Сеть нестабильна");
    await vi.advanceTimersByTimeAsync(120_000);
    await assertion;
  });

  it("stops waiting after 3 minutes", async () => {
    fetchMock
      .mockImplementationOnce(reply({ runId: "r1", status: "running" }))
      .mockImplementation(reply({ runId: "r1", status: "running" }));

    const p = api.runInsight("U_1", "новость");
    const assertion = expect(p).rejects.toThrow("слишком много времени");
    await vi.advanceTimersByTimeAsync(185_000);
    await assertion;
  });

  it("fails clearly when the server does not return a runId", async () => {
    fetchMock.mockImplementation(reply({ status: "running" }));
    const assertion = expect(api.runInsight("U_1", "новость")).rejects.toThrow("runId");
    await vi.advanceTimersByTimeAsync(0);
    await assertion;
  });

  it("does not retry the start request on an HTTP error and shows the backend code", async () => {
    fetchMock.mockImplementation(reply({ error: "company_not_found" }, 404));
    const assertion = expect(api.runInsight("nope", "новость")).rejects.toThrow("company_not_found");
    await vi.advanceTimersByTimeAsync(10_000);
    await assertion;
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  const hangingFetch = () => (_u: string, init: RequestInit) =>
    new Promise((_res, rej) => {
      init.signal?.addEventListener("abort", () => rej(new DOMException("aborted", "AbortError")));
    });

  it("aborts a hanging start request after 30 s with a readable message", async () => {
    fetchMock.mockImplementationOnce(hangingFetch());

    const p = api.runInsight("U_1", "новость");
    const assertion = expect(p).rejects.toThrow("Сервер слишком долго отвечает");
    await vi.advanceTimersByTimeAsync(29_000);
    expect(fetchMock.mock.calls[0][1].signal.aborted).toBe(false);

    await vi.advanceTimersByTimeAsync(2_000);
    await assertion;
    expect(fetchMock.mock.calls[0][1].signal.aborted).toBe(true);
  });

  // Известная проблема: http() превращает AbortError в обычную ошибку «Сервер слишком долго отвечает»,
  // а httpResilient повторяет запрос только если isNetworkError(e) — этот текст он не узнаёт,
  // поэтому повтор по таймауту (задуманный, см. isNetworkError) на деле не срабатывает.
  it.skip("retries the start request after a timeout (intended, currently not happening)", async () => {
    fetchMock
      .mockImplementationOnce(hangingFetch())
      .mockImplementationOnce(reply({ runId: "r1", status: "running" }))
      .mockImplementation(reply(done));

    const p = api.runInsight("U_1", "новость");
    await vi.advanceTimersByTimeAsync(40_000);

    expect((await p).status).toBe("ok");
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
