import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { Workspace } from "./Workspace";
import { company, details, newsFeed, runResult } from "../test/fixtures";

// Сеть заменена заглушкой: проверяем, как интерфейс ведёт себя на разные ответы бэкенда.
vi.mock("../api", () => ({
  api: {
    demoCompanies: vi.fn(),
    searchCompanies: vi.fn(),
    company: vi.fn(),
    news: vi.fn(),
    runInsight: vi.fn(),
  },
}));
import { api } from "../api";

const a = vi.mocked(api);

beforeEach(() => {
  Object.values(a).forEach((f) => f.mockReset());
  a.demoCompanies.mockResolvedValue([company]);
  a.searchCompanies.mockResolvedValue([company]);
  a.company.mockResolvedValue(details);
  a.news.mockResolvedValue(newsFeed);
});

const run = () => userEvent.click(screen.getByRole("button", { name: "Получить инсайт" }));

async function ready() {
  render(<Workspace />);
  await screen.findByText("Производство стали", { selector: ".company-title" });
  await waitFor(() => expect(screen.getByRole("button", { name: "Получить инсайт" })).toBeEnabled());
}

describe("Workspace", () => {
  it("picks the first demo company automatically and shows its profile", async () => {
    await ready();
    expect(await screen.findByText("Профиль клиента")).toBeInTheDocument();
    expect(a.company).toHaveBeenCalledWith("U_TEST_1");
  });

  it("runs the agent for the selected company with the chosen news text", async () => {
    a.runInsight.mockResolvedValue(runResult);
    await ready();

    await run();

    expect(a.runInsight).toHaveBeenCalledTimes(1);
    const [id, text] = a.runInsight.mock.calls[0];
    expect(id).toBe("U_TEST_1");
    expect(text).toContain("ТЕКСТ-НОВОСТИ-1");
    expect(await screen.findByText("Заголовок инсайта")).toBeInTheDocument();
  });

  it("shows progress while waiting and removes it when the result arrives", async () => {
    let resolve!: (v: typeof runResult) => void;
    a.runInsight.mockReturnValue(new Promise((r) => (resolve = r)));
    await ready();

    await run();
    expect(await screen.findByText("Собираем профиль компании")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Анализируем…" })).toBeDisabled();

    resolve(runResult);
    expect(await screen.findByText("Заголовок инсайта")).toBeInTheDocument();
    expect(screen.queryByText("Собираем профиль компании")).not.toBeInTheDocument();
  });

  it("shows the error text when the request fails", async () => {
    a.runInsight.mockRejectedValue(new Error("company_not_found"));
    await ready();

    await run();

    expect(await screen.findByText("Не получилось")).toBeInTheDocument();
    expect(screen.getByText("company_not_found")).toBeInTheDocument();
    expect(screen.queryByText("Заголовок инсайта")).not.toBeInTheDocument();
  });

  it("treats status != ok as a failure and shows the backend's error", async () => {
    a.runInsight.mockResolvedValue({ ...runResult, status: "error", insight: null, error: "LLM недоступен" });
    await ready();

    await run();

    expect(await screen.findByText("LLM недоступен")).toBeInTheDocument();
  });

  it("treats an 'ok' response without insight as a failure instead of showing an empty page", async () => {
    a.runInsight.mockResolvedValue({ ...runResult, insight: null });
    await ready();

    await run();

    expect(await screen.findByText("Агент не вернул результат")).toBeInTheDocument();
  });

  it("clears the previous insight when another company is selected", async () => {
    const other = { ...company, id: "U_TEST_2", okvedName: "Розничная торговля" };
    a.demoCompanies.mockResolvedValue([company, other]);
    a.runInsight.mockResolvedValue(runResult);
    await ready();
    await run();
    expect(await screen.findByText("Заголовок инсайта")).toBeInTheDocument();

    await userEvent.click(screen.getByText("Розничная торговля", { selector: ".company-title" }));

    await waitFor(() => expect(screen.queryByText("Заголовок инсайта")).not.toBeInTheDocument());
  });

  it("tells the user when the company list fails to load", async () => {
    a.demoCompanies.mockRejectedValue(new Error("HTTP 503"));
    render(<Workspace />);
    expect(await screen.findByText(/Не удалось загрузить компании: HTTP 503/)).toBeInTheDocument();
  });
});
