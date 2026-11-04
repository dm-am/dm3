import { describe, it, expect } from "vitest";
import { buildStatusLines } from "./tooltipBuilders";

describe("buildStatusLines", () => {
  it("names the draft group by the status a draft shows, Оформляется", () => {
    expect(
      buildStatusLines({ draft: 2, active: 1 }, ["игра", "игры", "игр"]),
    ).toEqual(["  Оформляется: 2 игры", "  Активные: 1 игра"]);
  });
});
