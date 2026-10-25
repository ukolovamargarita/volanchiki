import { describe, expect, it } from "vitest";
import { PRESET_EVENTS } from "./events";

describe("PRESET_EVENTS", () => {
  it("have unique ids", () => {
    const ids = PRESET_EVENTS.map((e) => e.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("have text long enough to enable the run button (>= 5 chars)", () => {
    for (const e of PRESET_EVENTS) {
      expect(e.text.trim().length).toBeGreaterThanOrEqual(5);
      expect(e.title.trim().length).toBeGreaterThan(0);
    }
  });
});
