import { describe, it, expect, vi, beforeEach } from "vitest";

// One instance, so what the interceptor said can be read back. A factory that
// returns a fresh object per call hands every assertion a spy nobody called.
const toast = vi.hoisted(() => ({
  error: vi.fn(),
  warning: vi.fn(),
  success: vi.fn(),
}));

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => toast,
}));

// The signalr client is loaded by a dynamic import inside the method under
// test, so the builder is replaced module-wide and whatever it is handed is
// read back from here.
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

    // (the 401 interceptor is covered in its own describe below)

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

/** Loads the client with a captured interceptor, for the two describes below. */
async function loadClientWithInterceptor() {
  const axios = (await import("axios")).default;
  const use = vi.fn();

  vi.spyOn(axios, "create").mockReturnValue({
    interceptors: { response: { use } },
  } as never);

  const module = await import("./client");
  const onRejected = use.mock.calls[0][1] as (
    error: unknown,
  ) => Promise<unknown>;

  return { module, onRejected };
}

/**
 * A 401 means the session is gone, and exactly one module owns "who the viewer
 * is": the auth store. The interceptor used to clear the persisted copy behind
 * the store's back, so the store went on reporting an authenticated user — the
 * header, the sidebar blocks and every action button rendered as signed in
 * while the route guard bounced the same viewer to the login modal.
 */
describe("Api on 401", () => {
  beforeEach(() => {
    vi.resetModules();
    localStorage.clear();
  });

  it("hands the expired session to the app instead of clearing storage", async () => {
    const { module, onRejected } = await loadClientWithInterceptor();
    const handler = vi.fn();
    module.setSessionExpiredHandler(handler);
    localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));

    await expect(
      onRejected({ response: { status: 401, headers: {} } }),
    ).rejects.toBeDefined();

    expect(handler).toHaveBeenCalledTimes(1);
    // The persisted copy has exactly one writer, the auth store. A second one
    // here is how the store and localStorage drifted apart in the first place.
    expect(localStorage.getItem("user")).not.toBeNull();
  });
});

/**
 * A 403 is not one event, and the server says which one it is: 68 throw sites
 * answer a refusal with a sentence of their own, and an authorization refusal
 * is answered with one constant title — so the title is the answer, whole.
 *
 * Saying "Недостаточно прав для этого действия" over all of them told a
 * blacklisted reader nothing: the comment box is open, the text comes back, and
 * the one sentence that would explain it was on the wire and thrown away. This
 * is the only place that reads it, because notifyFailure stays quiet on 403 to
 * keep a failure to one toast.
 */
describe("Api on 403", () => {
  beforeEach(() => {
    vi.resetModules();
    toast.error.mockClear();
  });

  async function refuse(body: unknown) {
    const { onRejected } = await loadClientWithInterceptor();

    await expect(
      onRejected({ response: { status: 403, data: body, headers: {} } }),
    ).rejects.toBeDefined();
  }

  it("says the reason the server named", async () => {
    await refuse({
      type: "",
      title: "Вы в черном списке этого блога",
      status: 403,
      traceId: "00-abc",
    });

    expect(toast.error).toHaveBeenCalledWith("Вы в черном списке этого блога");
  });

  it("stays quiet for a request that shows the refusal itself", async () => {
    // Signing in owns its refusal: the server names the state of the account and
    // the form puts that under the field. A second, weaker sentence in a toast
    // would contradict it.
    const { onRejected } = await loadClientWithInterceptor();

    await expect(
      onRejected({
        response: { status: 403, headers: {} },
        config: { ownsRefusal: true },
      }),
    ).rejects.toBeDefined();

    expect(toast.error).not.toHaveBeenCalled();
  });

  it("says the general sentence when the refusal named nothing", async () => {
    // A 403 the error middleware never saw — a rejected preflight, a path the
    // framework refuses before routing — comes back with a zero-length body.
    await refuse("");

    expect(toast.error).toHaveBeenCalledWith(
      "Недостаточно прав для этого действия",
    );
  });
});

/**
 * The realtime socket has to come back on its own.
 *
 * withAutomaticReconnect() with no argument is four attempts — 0, 2, 10 and 30
 * seconds — and then the connection is closed for good, with nothing on the
 * client side trying again. An API restart longer than that, which an ordinary
 * deploy with a warm-up is, left every open tab without realtime until the
 * reader happened to reload the page: the global chat survived on its own
 * polling fallback, while the messenger badge and the notification bell, which
 * have none, froze on the number the page had loaded with.
 */
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
    await api.establishHubConnection("whatsup");

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
