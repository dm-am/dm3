/**
 * @vitest-environment node
 */

import { describe, it, expect, vi } from "vitest";

const remove = vi.hoisted(() => vi.fn());

vi.mock("@/shared/api", () => ({
  Api: { delete: remove },
}));

const { default: banApi } = await import("./banApi");

describe("the lift of a ban", () => {
  it("sends the reason in the body, with the window's flags", () => {
    banApi.liftBan("b-1", "Разобрались", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });

    expect(remove).toHaveBeenCalledWith(
      "bans/b-1",
      { reason: "Разобрались" },
      { ownsRefusal: true, ownsRateLimit: true },
    );
  });
});
