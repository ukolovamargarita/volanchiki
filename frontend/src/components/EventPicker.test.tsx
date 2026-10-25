import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { EventPicker } from "./EventPicker";
import { PRESET_EVENTS } from "../events";
import { newsFeed } from "../test/fixtures";

// Сеть заменена заглушкой: проверяем поведение компонента, а не бэкенда.
vi.mock("../api", () => ({ api: { news: vi.fn() } }));
import { api } from "../api";
const newsMock = vi.mocked(api.news);

const textarea = () => screen.getByRole("textbox") as HTMLTextAreaElement;

beforeEach(() => {
  newsMock.mockReset();
  newsMock.mockResolvedValue(newsFeed);
});

describe("EventPicker", () => {
  it("loads the feed for the selected company and shows its titles", async () => {
    render(<EventPicker companyId="U_1" disabled={false} onRun={() => {}} />);

    expect(await screen.findByText("ЦБ сохранил ключевую ставку")).toBeInTheDocument();
    expect(screen.getByText("Инфляция замедлилась")).toBeInTheDocument();
    expect(newsMock).toHaveBeenCalledWith({ companyId: "U_1", take: 12 });
  });

  it("reports how many news are live", async () => {
    render(<EventPicker disabled={false} onRun={() => {}} />);
    expect(await screen.findByText(/RSS · 2 живых из 5/)).toBeInTheDocument();
  });

  it("says the feed is offline demo when nothing live came in", async () => {
    newsMock.mockResolvedValue({ ...newsFeed, meta: { total: 4, live: 0 } });
    render(<EventPicker disabled={false} onRun={() => {}} />);
    expect(await screen.findByText(/Офлайн-демо · 4 тем/)).toBeInTheDocument();
  });

  it("says the feed is empty when there is nothing at all", async () => {
    newsMock.mockResolvedValue({ items: [], meta: { total: 0, live: 0 } });
    render(<EventPicker disabled={false} onRun={() => {}} />);
    expect(await screen.findByText(/Лента пуста/)).toBeInTheDocument();
  });

  it("pre-fills the text with the top news item when nothing was chosen", async () => {
    render(<EventPicker disabled={false} onRun={() => {}} />);
    await waitFor(() => expect(textarea().value).toContain("ТЕКСТ-НОВОСТИ-1"));
  });

  it("uses the initial text instead of the top news item", async () => {
    render(<EventPicker initial="Моё событие из виджета" disabled={false} onRun={() => {}} />);
    await screen.findByText("ЦБ сохранил ключевую ставку");
    expect(textarea().value).toBe("Моё событие из виджета");
  });

  it("puts the text of a clicked news item into the field", async () => {
    render(<EventPicker disabled={false} onRun={() => {}} />);
    await userEvent.click(await screen.findByText("Инфляция замедлилась"));
    expect(textarea().value).toContain("ТЕКСТ-НОВОСТИ-2");
  });

  it("puts the text of a bank topic into the field", async () => {
    render(<EventPicker disabled={false} onRun={() => {}} />);
    await userEvent.click(await screen.findByRole("button", { name: PRESET_EVENTS[2].title }));
    expect(textarea().value).toBe(PRESET_EVENTS[2].text);
  });

  it("runs with the trimmed text", async () => {
    const onRun = vi.fn();
    render(<EventPicker initial="  Произвольная новость  " disabled={false} onRun={onRun} />);
    await userEvent.click(screen.getByRole("button", { name: "Получить инсайт" }));
    expect(onRun).toHaveBeenCalledWith("Произвольная новость");
  });

  it("disables the run button for text shorter than 5 characters", async () => {
    render(<EventPicker initial="abcd" disabled={false} onRun={() => {}} />);
    expect(screen.getByRole("button", { name: "Получить инсайт" })).toBeDisabled();

    await userEvent.type(textarea(), "e");
    expect(screen.getByRole("button", { name: "Получить инсайт" })).toBeEnabled();
  });

  it("asks to choose a company when disabled", () => {
    render(<EventPicker initial="Достаточно длинный текст" disabled={true} onRun={() => {}} />);
    expect(screen.getByRole("button", { name: "Выберите компанию" })).toBeDisabled();
  });

  it("shows a busy label while the agent works", () => {
    render(<EventPicker initial="Достаточно длинный текст" disabled={true} busy={true} onRun={() => {}} />);
    expect(screen.getByRole("button", { name: "Анализируем…" })).toBeDisabled();
  });

  it("keeps bank topics usable and tells the user when the feed fails", async () => {
    newsMock.mockRejectedValue(new Error("boom"));
    const onRun = vi.fn();
    render(<EventPicker disabled={false} onRun={onRun} />);

    expect(await screen.findByText(/не удалось загрузить/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: PRESET_EVENTS[0].title }));
    await userEvent.click(screen.getByRole("button", { name: "Получить инсайт" }));
    expect(onRun).toHaveBeenCalledWith(PRESET_EVENTS[0].text);
  });
});
