import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { InsightView } from "./InsightView";
import { runResult } from "../test/fixtures";

function renderView(result = runResult) {
  return render(<InsightView result={result} companyName="Производство стали" />);
}

// Главное требование кейса (критерий 03): пользователь видит, что факт, что позиция банка, что вывод AI.
describe("InsightView: origin of information", () => {
  it("puts the news fact into the 'fact' block with the fact badge", () => {
    const { container } = renderView();
    const block = container.querySelector(".block.origin-fact") as HTMLElement;

    expect(block).not.toBeNull();
    expect(within(block).getByText(/ТЕКСТ-ФАКТА/)).toBeInTheDocument();
    expect(within(block).getByText("Факт из источника")).toBeInTheDocument();
  });

  it("puts the bank's view into the 'bank' block with the bank badge", () => {
    const { container } = renderView();
    const block = container.querySelector(".block.origin-bank") as HTMLElement;

    expect(block).not.toBeNull();
    expect(within(block).getByText(/ТЕКСТ-БАНКА/)).toBeInTheDocument();
    expect(within(block).getByText("Позиция Альфа-Банка")).toBeInTheDocument();
  });

  it("puts model conclusions into 'ai' blocks and never mixes them with bank or fact blocks", () => {
    const { container } = renderView();
    const ai = Array.from(container.querySelectorAll(".origin-ai")) as HTMLElement[];
    const aiText = ai.map((b) => b.textContent).join(" ");

    expect(aiText).toContain("ТЕКСТ-ПОЧЕМУ-ВАЖНО");
    expect(aiText).toContain("ТЕКСТ-МОДЕЛИ");
    expect(aiText).toContain("РИСК-1");
    expect(aiText).toContain("ВОЗМОЖНОСТЬ-1");
    expect(aiText).toContain("ДЕЙСТВИЕ-1");

    expect(aiText).not.toContain("ТЕКСТ-БАНКА");
    expect(aiText).not.toContain("ТЕКСТ-ФАКТА");

    const nonAi = Array.from(container.querySelectorAll(".origin-fact, .origin-bank"))
      .map((b) => b.textContent)
      .join(" ");
    expect(nonAi).not.toContain("ТЕКСТ-МОДЕЛИ");
    expect(nonAi).not.toContain("ТЕКСТ-ПОЧЕМУ-ВАЖНО");
  });

  it("shows a legend with all three origin badges", () => {
    const { container } = renderView();
    const legend = container.querySelector(".legend") as HTMLElement;

    expect(within(legend).getByText("Факт из источника")).toBeInTheDocument();
    expect(within(legend).getByText("Позиция Альфа-Банка")).toBeInTheDocument();
    expect(within(legend).getByText("Вывод AI")).toBeInTheDocument();
  });
});

describe("InsightView: content", () => {
  it("shows title, company name and relevance label", () => {
    renderView();
    expect(screen.getByText("Заголовок инсайта")).toBeInTheDocument();
    expect(screen.getByText("Производство стали")).toBeInTheDocument();
    expect(screen.getByText("Высокая значимость")).toBeInTheDocument();
  });

  it("falls back to the raw relevance value when it is unknown", () => {
    renderView({ ...runResult, insight: { ...runResult.insight!, relevance: "critical" } });
    expect(screen.getByText("critical")).toBeInTheDocument();
  });

  it("hides the model block when modelView is empty", () => {
    renderView({ ...runResult, insight: { ...runResult.insight!, modelView: "" } });
    expect(screen.queryByText("Вывод модели")).not.toBeInTheDocument();
  });

  it("hides risk and opportunity cards when the lists are empty", () => {
    renderView({ ...runResult, insight: { ...runResult.insight!, risks: [], opportunities: [] } });
    expect(screen.queryByText("Риски")).not.toBeInTheDocument();
    expect(screen.queryByText("Возможности")).not.toBeInTheDocument();
  });

  it("renders nothing when the run has no insight", () => {
    const { container } = renderView({ ...runResult, insight: null });
    expect(container).toBeEmptyDOMElement();
  });

  it("renders the call-to-action and disclaimer from the backend", () => {
    renderView();
    expect(screen.getByRole("button", { name: "Связаться с менеджером" })).toBeInTheDocument();
    expect(screen.getByText("Информационный материал")).toBeInTheDocument();
  });
});

describe("InsightView: sources", () => {
  it("links a source only when it has a url and labels the source kind", () => {
    renderView();

    const link = screen.getByRole("link", { name: "Новость РБК" });
    expect(link).toHaveAttribute("href", "https://example.com/n");
    expect(link).toHaveAttribute("rel", "noreferrer");
    expect(screen.getByText("Policy_Rate_11Sep").closest("a")).toBeNull();

    expect(screen.getByText("Аналитика Альфа-Банка")).toBeInTheDocument();
    expect(screen.getByText("Ставка без изменения на 14,0%")).toBeInTheDocument();
  });

  it("says so honestly when the model gave no sources", () => {
    renderView({ ...runResult, insight: { ...runResult.insight!, sources: [] } });
    expect(screen.getByText("Источники не указаны моделью")).toBeInTheDocument();
  });
});

describe("InsightView: trace", () => {
  it("keeps the trace collapsed until asked, then lists every step with readable labels", async () => {
    renderView();
    expect(screen.queryByText("company=U_TEST_1")).not.toBeInTheDocument();

    await userEvent.click(screen.getByText("Показать шаги (3)"));

    expect(screen.getByText("company=U_TEST_1")).toBeInTheDocument();
    expect(screen.getByText("Вызов инструмента")).toBeInTheDocument();
    expect(screen.getByText("Профиль компании")).toBeInTheDocument();
    expect(screen.getByText("Запуск run-1")).toBeInTheDocument();

    await userEvent.click(screen.getByText("Скрыть"));
    expect(screen.queryByText("company=U_TEST_1")).not.toBeInTheDocument();
  });

  it("shows unknown actions as-is instead of hiding them", async () => {
    renderView({
      ...runResult,
      trace: [{ step: 1, action: "brand_new_action", at: "2026-10-25T10:00:00Z" }],
    });
    await userEvent.click(screen.getByText("Показать шаги (1)"));
    expect(screen.getByText("brand_new_action")).toBeInTheDocument();
  });
});

describe("InsightView: feedback buttons", () => {
  it("marks only the chosen vote as active", async () => {
    renderView();
    const up = screen.getByRole("button", { name: "Полезно" });
    const down = screen.getByRole("button", { name: "Не полезно" });

    await userEvent.click(up);
    expect(up).toHaveClass("is-on");
    expect(down).not.toHaveClass("is-on");

    await userEvent.click(down);
    expect(down).toHaveClass("is-on");
    expect(up).not.toHaveClass("is-on");
  });
});
