import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { NewsWidget } from "./NewsWidget";
import { PRESET_EVENTS } from "../events";
import { newsFeed } from "../test/fixtures";

vi.mock("../api", () => ({ api: { news: vi.fn() } }));
import { api } from "../api";
const newsMock = vi.mocked(api.news);

beforeEach(() => {
  newsMock.mockReset();
});

describe("NewsWidget (entry point on the home screen)", () => {
  it("shows live news from the backend", async () => {
    newsMock.mockResolvedValue(newsFeed);
    render(<NewsWidget onOpen={() => {}} />);

    expect(await screen.findByText("ЦБ сохранил ключевую ставку")).toBeInTheDocument();
    expect(newsMock).toHaveBeenCalledWith({ take: 3 });
  });

  it("falls back to bank topics when the feed fails", async () => {
    newsMock.mockRejectedValue(new Error("boom"));
    render(<NewsWidget onOpen={() => {}} />);

    // дождаться, пока компонент обработает отказ, и убедиться, что темы банка остались
    await waitFor(() => expect(newsMock).toHaveBeenCalled());
    await act(async () => {
      await new Promise((r) => setTimeout(r, 0));
    });
    expect(screen.getByText(PRESET_EVENTS[0].title)).toBeInTheDocument();
    expect(screen.queryByText("Инфляция замедлилась")).not.toBeInTheDocument();
  });

  it("falls back to bank topics when the feed is empty", async () => {
    newsMock.mockResolvedValue({ items: [], meta: { total: 0, live: 0 } });
    render(<NewsWidget onOpen={() => {}} />);

    expect(await screen.findByText(PRESET_EVENTS[0].title)).toBeInTheDocument();
  });

  it("opens the section with the clicked event's text", async () => {
    newsMock.mockResolvedValue(newsFeed);
    const onOpen = vi.fn();
    render(<NewsWidget onOpen={onOpen} />);

    await userEvent.click(await screen.findByText("Инфляция замедлилась"));

    expect(onOpen).toHaveBeenCalledWith(newsFeed.items[1].text);
  });

  it("opens the section without a preset text from the main button", async () => {
    newsMock.mockResolvedValue(newsFeed);
    const onOpen = vi.fn();
    render(<NewsWidget onOpen={onOpen} />);

    await userEvent.click(screen.getByRole("button", { name: "Открыть раздел" }));

    expect(onOpen).toHaveBeenCalledWith();
  });
});
