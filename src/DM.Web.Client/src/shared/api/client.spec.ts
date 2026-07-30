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

  // Every shape a failed response can carry that is not a problem document.
  // Measured: GET /v1/global-chat/nope answers 404 with Content-Length 0, and a
  // request that misses the API entirely comes back as an HTML error page.
  it.each([
    ["an empty body", ""],
    ["no body", null],
    ["an HTML error page", "<html><body>404</body></html>"],
  ])(
    "reports a failure with %s as an error, not as success",
    async (_, body) => {
      const api = await apiWithResponse(404, body);

      const { data, error } = await api.get("nosuchthing");

      expect(data).toBeNull();
      // Truthiness is the whole point: `if (error)` is what every caller writes,
      // and an empty string passed that check as success with no data.
      expect(Boolean(error)).toBe(true);
      expect(error?.status).toBe(404);
      // Left empty so the call site's own `error.title || "не удалось ..."` wins.
      expect(error?.title).toBe("");
    },
  );

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

  it("reports a request that never got a response", async () => {
    const { AxiosError } = await import("axios");
    const axios = (await import("axios")).default;

    vi.spyOn(axios, "create").mockReturnValue({
      interceptors: { response: { use: vi.fn() } },
      // No `response` at all: the request never reached the API, or the browser
      // cut it. This path always worked; it is here so that the three shapes a
      // failure can arrive in are covered together.
      get: vi.fn().mockRejectedValue(new AxiosError("Network Error")),
    } as never);

    const { default: api } = await import("./client");
    const { data, error } = await api.get("anything");

    expect(data).toBeNull();
    expect(Boolean(error)).toBe(true);
    expect(error?.status).toBe(0);
    expect(error?.title).toBe("");
  });
});
