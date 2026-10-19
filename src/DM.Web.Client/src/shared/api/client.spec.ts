import { describe, it, expect, vi, beforeEach } from "vitest";

// The signalr client is imported inside the method, so the builder is replaced
// module-wide.
const hub = vi.hoisted(() => ({ policy: undefined as unknown }));

vi.mock("@microsoft/signalr", () => ({
  HubConnectionBuilder: class {
    withAutomaticReconnect(policy: unknown) {
      hub.policy = policy;
      return this;
    }
    withUrl() {
      return this;
    }
    build() {
      return {};
    }
  },
}));

/**
 * A failed request comes back as an error that `if (error)` sees, even with an
 * empty body: axios reports it as "", which read as success.
 */
describe("Api.send", () => {
  beforeEach(() => {
    vi.resetModules();
  });

  async function apiWithResponse(
    status: number,
    data: unknown,
    headers: Record<string, string> = {},
  ) {
    const { AxiosError } = await import("axios");
    const axios = (await import("axios")).default;

    vi.spyOn(axios, "create").mockReturnValue({
      interceptors: { response: { use: vi.fn() } },
      get: vi.fn().mockRejectedValue(
        Object.assign(new AxiosError("failed"), {
          response: { status, data, headers },
        }),
      ),
    } as never);

    const module = await import("./client");
    return module.default;
  }

  // Measured: an unrouted path answers 404 with an empty body, a request that
  // misses the API comes back as an HTML page.
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

  // A form that shows the rate limit itself counts the seconds of the header down.
  it("carries the wait off Retry-After out with the failure", async () => {
    const api = await apiWithResponse(
      429,
      { type: "", title: "Слишком много запросов", status: 429, traceId: "t" },
      { "retry-after": "45" },
    );

    const { error } = await api.get("account/login");

    expect(error?.retryAfterSeconds).toBe(45);
  });

  it("leaves the wait off when the server named none", async () => {
    // An absent field tells the form to say the sentence without a number.
    const api = await apiWithResponse(429, {
      type: "",
      title: "Слишком много запросов",
      status: 429,
      traceId: "t",
    });

    const { error } = await api.get("account/login");

    expect(error?.retryAfterSeconds).toBeUndefined();
  });

  it("reports a request that never got a response", async () => {
    const { AxiosError } = await import("axios");
    const axios = (await import("axios")).default;

    // (the 401 interceptor is covered in interceptor.spec.ts)

    vi.spyOn(axios, "create").mockReturnValue({
      interceptors: { response: { use: vi.fn() } },
      // No response at all: the request never reached the API.
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

// The default reconnect gives up after four attempts, sooner than a deploy ends.
describe("Api.establishHubConnection", () => {
  type RetryPolicy = {
    nextRetryDelayInMilliseconds: (context: {
      previousRetryCount: number;
    }) => number | null;
  };

  beforeEach(() => {
    vi.resetModules();
    hub.policy = undefined;
  });

  async function connect(): Promise<RetryPolicy | undefined> {
    const axios = (await import("axios")).default;

    vi.spyOn(axios, "create").mockReturnValue({
      interceptors: { response: { use: vi.fn() } },
    } as never);

    const { default: api } = await import("./client");
    await api.establishHubConnection("hubs/notifications");

    return hub.policy as RetryPolicy | undefined;
  }

  it("keeps retrying, and never waits longer than half a minute", async () => {
    const policy = await connect();

    expect(typeof policy?.nextRetryDelayInMilliseconds).toBe("function");

    for (const previousRetryCount of [0, 1, 4, 10, 1000]) {
      // null is how the client says "give up", and the default policy says it
      // on the fifth attempt — about 42 seconds after the connection dropped.
      const delay = policy!.nextRetryDelayInMilliseconds({
        previousRetryCount,
      });

      expect(delay).not.toBeNull();
      expect(delay!).toBeGreaterThan(0);
      expect(delay!).toBeLessThanOrEqual(30000);
    }
  });
});
