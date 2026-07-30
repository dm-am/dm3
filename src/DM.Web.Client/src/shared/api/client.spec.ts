import { describe, it, expect, vi, beforeEach } from "vitest";

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => ({ error: vi.fn(), warning: vi.fn(), success: vi.fn() }),
}));

/**
 * A request that failed must come back as an error, and the check every caller
 * writes is `if (error)`.
 *
 * That held only while the server sent a body. An unrouted path and a wrong
 * method are answered by the framework, not by the error middleware, with a
 * zero-length body — axios then reports `response.data` as the empty string,
 * which is falsy. Returning it verbatim handed callers a 404 that read as
 * success with no data, and a store that checks it went down its "there is
 * nothing more to load" branch and told the reader the history had ended.
 */
describe("Api.send", () => {
  beforeEach(() => {
    vi.resetModules();
  });

  async function apiWithResponse(status: number, data: unknown) {
    const { AxiosError } = await import("axios");
    const axios = (await import("axios")).default;

    vi.spyOn(axios, "create").mockReturnValue({
      interceptors: { response: { use: vi.fn() } },
      get: vi.fn().mockRejectedValue(
        Object.assign(new AxiosError("failed"), {
          response: { status, data, headers: {} },
        }),
      ),
    } as never);

    const module = await import("./client");
    return module.default;
  }

  it("reports an empty-bodied failure as an error, not as success", async () => {
    const api = await apiWithResponse(404, "");

    const { data, error } = await api.get("nosuchthing");

    expect(data).toBeNull();
    expect(error).not.toBeNull();
    // Truthiness is the whole point: `if (error)` is what every caller writes.
    expect(Boolean(error)).toBe(true);
    expect(error?.status).toBe(404);
  });

  it("keeps the problem document the server sent", async () => {
    const problem = {
      type: "https://tools.ietf.org/html/rfc9110#section-15.5.5",
      title: "Game not found",
      status: 410,
      traceId: "00-abc",
    };
    const api = await apiWithResponse(410, problem);

    const { error } = await api.get("games/whatever");

    // The server's own message is more specific than anything synthesised
    // here, so it must survive untouched.
    expect(error).toEqual(problem);
  });
});
