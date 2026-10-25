import { describe, expect, it } from "vitest";
import { ACTION_LABELS, PRODUCT_LABELS, SOURCE_LABELS, TOOL_LABELS, money } from "./format";

describe("money", () => {
  it("returns a dash for missing values", () => {
    expect(money(null)).toBe("—");
    expect(money(undefined)).toBe("—");
    expect(money(Number.NaN)).toBe("—");
  });

  it("formats billions compactly in rubles", () => {
    const s = money(18_400_000_000);
    expect(s).toContain("₽");
    expect(s).toMatch(/18,4/);
    expect(s.toLowerCase()).toMatch(/млрд/);
  });

  it("formats zero without a dash", () => {
    expect(money(0)).toContain("0");
  });
});

describe("label dictionaries", () => {
  it("cover every product returned by the backend", () => {
    for (const key of ["rko", "loan", "guarantee", "leasing", "deposit", "payroll", "factoring"]) {
      expect(PRODUCT_LABELS[key]).toBeTruthy();
    }
  });

  it("cover every tool the agent can call", () => {
    for (const key of ["get_company_profile", "search_alfa_analytics", "draft_insight"]) {
      expect(TOOL_LABELS[key]).toBeTruthy();
    }
  });

  it("cover the source kinds used in insights", () => {
    for (const key of ["alfa_analytics", "company_feature", "news"]) {
      expect(SOURCE_LABELS[key]).toBeTruthy();
    }
  });

  it("cover the trace actions emitted by the orchestrator", () => {
    for (const key of ["start", "tool", "tool_result", "self_check", "retry_draft", "done", "error"]) {
      expect(ACTION_LABELS[key]).toBeTruthy();
    }
  });
});
