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

// The probe of the other address is a request of its own; here it never
// answers, so a dead connection is announced without the offer.
vi.mock("@/shared/lib/composables/useAddressPing", () => ({
  addressAnswers: () => Promise.resolve(false),
}));

/** Lets a plate the interceptor holds for its caller run out. */
const endOfTask = () => new Promise((resolve) => setTimeout(resolve, 0));

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

  /**
   * The wait a rate limit carries is in a header, and a caller is handed the
   * problem document alone. A form that shows the limit itself has to count the
   * seconds down, so the number has to survive the trip out of the transport —
   * without it the form can only say "позже", which is the sentence that sent
   * readers back to pressing the button to find out.
   */
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
    // The header is exposed across origins by name, so an edge that forgot to
    // do so is a real way for it to go missing. The field being absent is what
    // tells a form to say the sentence without a number in it.
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
  // The page asks at start whether anybody is signed in, and for a guest the
  // answer is a 401. Read as an expiry it put "Сессия истекла" in front of
  // every guest on every visit.
  it("takes the 401 of a session probe for a guest, not an expiry", async () => {
    const { module, onRejected } = await loadClientWithInterceptor();
    const handler = vi.fn();
    module.setSessionExpiredHandler(handler);
    toast.warning.mockClear();

    await expect(
      onRejected({
        response: { status: 401, headers: {} },
        config: { probesSession: true },
      }),
    ).rejects.toBeDefined();

    expect(handler).not.toHaveBeenCalled();
    expect(toast.warning).not.toHaveBeenCalled();
  });

  /**
   * A page asks for ten things at once, and an ended session answers every one
   * of them with a 401. That is one event: the plate is raised once and the
   * session is ended once, not once per request. The answers come back spread
   * out, some after the plate has faded, so the toast's own dedupe by text
   * does not cover them.
   */
  it("says an expiry once, however many requests it answers", async () => {
    const { module, onRejected } = await loadClientWithInterceptor();
    const handler = vi.fn();
    module.setSessionExpiredHandler(handler);
    toast.warning.mockClear();
    const now = vi.spyOn(Date, "now");
    const expired = () =>
      onRejected({ response: { status: 401, headers: {} } });

    try {
      // Five answers within ten seconds, the last one after the plate faded.
      for (const at of [1000, 1010, 1020, 6000, 11000]) {
        now.mockReturnValue(at);
        await expect(expired()).rejects.toBeDefined();
      }

      expect(toast.warning).toHaveBeenCalledTimes(1);
      expect(handler).toHaveBeenCalledTimes(1);

      // A refusal after a quiet longer than any request may take is a session
      // that ended anew, and it is said again.
      now.mockReturnValue(11000 + 30000);
      await expect(expired()).rejects.toBeDefined();

      expect(toast.warning).toHaveBeenCalledTimes(2);
      expect(handler).toHaveBeenCalledTimes(2);
    } finally {
      now.mockRestore();
    }
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
    await endOfTask();
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

  it("stays quiet on a server error the form prints itself", async () => {
    // The guest forms print a failed press in their own strip, and the strip
    // stays until the reader changes something. A toast over it fades in eight
    // seconds and says the same thing in the same words.
    const { onRejected } = await loadClientWithInterceptor();

    await expect(
      onRejected({
        response: { status: 500, headers: {} },
        config: { ownsRefusal: true },
      }),
    ).rejects.toBeDefined();

    expect(toast.error).not.toHaveBeenCalled();
  });

  it("says the general plate when the refusal named nothing", async () => {
    // A 403 the error middleware never saw — a rejected preflight, a path the
    // framework refuses before routing — comes back with a zero-length body.
    // Nobody named the request, so the plate says it was a request.
    await refuse("");

    expect(toast.error.mock.calls).toEqual([
      ["Запрос не выполнен: недостаточно прав"],
    ]);
  });

  it("reads the general title of the authorization check as naming nothing", async () => {
    // The API titles every refusal of the intention manager with one sentence,
    // and relayed as it stood it said nothing about which press it answered.
    await refuse({
      type: "",
      title: "Недостаточно прав для этого действия",
      status: 403,
      traceId: "00-abc",
    });

    expect(toast.error.mock.calls).toEqual([
      ["Запрос не выполнен: недостаточно прав"],
    ]);
  });
});

/**
 * A refusal the server named only in general - no rights, a server error - is
 * one plate, and its head is the phrase of the call that pressed. The call site
 * holds that phrase for notifyFailure and says nothing on these statuses, the
 * interceptor holds the reason and does not know the press: the plate waits
 * for the caller to hand its phrase over before it goes up.
 */
describe("Api on a refusal named only in general", () => {
  beforeEach(() => {
    vi.resetModules();
    toast.error.mockClear();
  });

  /** A client whose every request fails the way axios fails it. */
  async function failing(status: number, data: unknown = "") {
    const { AxiosError } = await import("axios");
    const axios = (await import("axios")).default;
    let onRejected: (error: unknown) => Promise<unknown> = (error) =>
      Promise.reject(error);
    const failure = () =>
      onRejected(
        Object.assign(new AxiosError("failed"), {
          response: { status, data, headers: {} },
        }),
      );

    vi.spyOn(axios, "create").mockReturnValue({
      interceptors: {
        response: {
          use: (_: unknown, rejected: typeof onRejected) => {
            onRejected = rejected;
          },
        },
      },
      get: vi.fn(failure),
      post: vi.fn(failure),
    } as never);

    const { default: api } = await import("./client");
    const { notifyFailure } = await import("@/shared/lib/errors");
    return { api, notifyFailure };
  }

  it.each([
    [500, "Не удалось сохранить пост: ошибка сервера"],
    [502, "Не удалось сохранить пост: ошибка сервера"],
    [403, "Не удалось сохранить пост: недостаточно прав"],
  ])(
    "puts the phrase of the call at the head of a %i",
    async (status, plate) => {
      const { api, notifyFailure } = await failing(status);

      const { error } = await api.post("posts/1", {});
      notifyFailure(error!, "Не удалось сохранить пост");
      await endOfTask();

      // One plate: the named one, and not the general one beside it.
      expect(toast.error.mock.calls).toEqual([[plate]]);
    },
  );

  it("names a request nobody named as a request", async () => {
    const { api } = await failing(500, {
      type: "",
      title: "Ошибка сервера",
      status: 500,
      traceId: "t",
    });

    await api.get("games");
    await endOfTask();

    expect(toast.error.mock.calls).toEqual([
      ["Запрос не выполнен: ошибка сервера"],
    ]);
  });

  it("relays a 5xx the server titled with a sentence of its own", async () => {
    const { api, notifyFailure } = await failing(503, {
      type: "",
      title: "Сайт на обслуживании до 04:00",
      status: 503,
      traceId: "t",
    });

    const { error } = await api.post("posts/1", {});
    notifyFailure(error!, "Не удалось сохранить пост");
    await endOfTask();

    expect(toast.error.mock.calls).toEqual([["Сайт на обслуживании до 04:00"]]);
  });

  it("leaves a plate that is already up as it is", async () => {
    // A caller that waited on something else first is too late to name it:
    // the general plate stands, and no second one follows.
    const { api, notifyFailure } = await failing(500);

    const { error } = await api.post("posts/1", {});
    await endOfTask();
    notifyFailure(error!, "Не удалось сохранить пост");

    expect(toast.error.mock.calls).toEqual([
      ["Запрос не выполнен: ошибка сервера"],
    ]);
  });
});

describe("Api on a request that got no answer", () => {
  beforeEach(() => {
    vi.resetModules();
    toast.error.mockClear();
  });

  it.each(["ERR_NETWORK", "ECONNABORTED"])(
    "says the site is not answering at this address (%s)",
    async (code) => {
      const { onRejected } = await loadClientWithInterceptor();

      await expect(onRejected({ code })).rejects.toBeDefined();

      expect(toast.error).toHaveBeenCalledWith(
        "Сайт не отвечает по этому адресу",
      );
    },
  );
});

/**
 * A 429 used to be a toast and nothing else.
 *
 * It faded after eight seconds, the form underneath was unchanged — button
 * live, strip empty — and the reader pressed again, bought the same refusal and
 * concluded the form was broken. The way in is where that happens, so those
 * requests take the limit over and answer it in the form itself, with the wait
 * running down on the button.
 *
 * Which is a flag of its own and not a wider reading of `ownsRefusal`: that one
 * is set on requests with no opinion about a rate limit at all, and silencing
 * their toast as a side effect is how a refusal comes to be announced nowhere.
 */
describe("Api on 429", () => {
  beforeEach(() => {
    vi.resetModules();
    toast.warning.mockClear();
  });

  async function limit(config?: unknown) {
    const { onRejected } = await loadClientWithInterceptor();

    await expect(
      onRejected({
        response: { status: 429, headers: { "retry-after": "30" } },
        config,
      }),
    ).rejects.toBeDefined();
    // The plate is held for the page to take over, until the end of the task.
    await endOfTask();
  }

  it("says how long to wait when nobody else will", async () => {
    await limit();

    expect(toast.warning).toHaveBeenCalledWith(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });

  it("stays quiet for a request that shows the limit itself", async () => {
    await limit({ ownsRateLimit: true });

    expect(toast.warning).not.toHaveBeenCalled();
  });

  it("still speaks for a request that only took the refusal over", async () => {
    // ownsRefusal is about a 403 and is set on requests that say nothing about
    // a rate limit. Reading it as both would take this toast away from them.
    await limit({ ownsRefusal: true });

    expect(toast.warning).toHaveBeenCalledWith(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });
});

/**
 * A refusal the page says at the place of the press is not said again in the
 * corner (the owner's rule of 26 September 2026, UI_STANDARDS, "Всплывающие
 * сообщения"). The interceptor holds every plate it raises for a refusal - the
 * server's own sentence, the general one, a rate limit - until the end of the
 * task, and the page that writes the refusal in its own line takes it down.
 *
 * Before this the plate of a 403 worded by the server and of a 429 went up at
 * once, and a page could only keep its own line empty on those statuses: the
 * reader got the refusal in the corner, away from the button that was pressed.
 */
describe("Api on a refusal the page says itself", () => {
  beforeEach(() => {
    vi.resetModules();
    toast.error.mockClear();
    toast.warning.mockClear();
  });

  /** A client whose every request fails with this status and body. */
  async function failing(
    status: number,
    data: unknown = "",
    headers: Record<string, string> = {},
  ) {
    const { AxiosError } = await import("axios");
    const axios = (await import("axios")).default;
    let onRejected: (error: unknown) => Promise<unknown> = (error) =>
      Promise.reject(error);
    const failure = () =>
      onRejected(
        Object.assign(new AxiosError("failed"), {
          response: { status, data, headers },
        }),
      );

    vi.spyOn(axios, "create").mockReturnValue({
      interceptors: {
        response: {
          use: (_: unknown, rejected: typeof onRejected) => {
            onRejected = rejected;
          },
        },
      },
      post: vi.fn(failure),
    } as never);

    const { default: api } = await import("./client");
    const { refusalHere } = await import("@/shared/lib/errors");
    return { api, refusalHere };
  }

  it.each([
    [
      "the server's own sentence",
      403,
      {
        type: "",
        title: "Вы в черном списке этого блога",
        status: 403,
        traceId: "t",
      },
      {},
      "Вы в черном списке этого блога",
    ],
    ["a refusal that named nothing", 403, "", {}, "Не удалось сохранить пост"],
    [
      "a server error",
      500,
      { type: "", title: "Ошибка сервера", status: 500, traceId: "t" },
      {},
      "Ошибка сервера",
    ],
    [
      "a rate limit",
      429,
      "",
      { "retry-after": "30" },
      "Слишком много попыток, попробуйте через 30 секунд",
    ],
  ])(
    "leaves %s to the line at the place",
    async (_case, status, body, headers, line) => {
      const { api, refusalHere } = await failing(status, body, headers);

      const { error } = await api.post("posts/1", {});
      const said = refusalHere(error!, "Не удалось сохранить пост");
      await endOfTask();

      expect(said).toBe(line);
      expect(toast.error).not.toHaveBeenCalled();
      expect(toast.warning).not.toHaveBeenCalled();
    },
  );

  it("still speaks for a refusal nobody on the page said", async () => {
    const { api } = await failing(403, {
      type: "",
      title: "Вы в черном списке этого блога",
      status: 403,
      traceId: "t",
    });

    await api.post("posts/1", {});
    await endOfTask();

    expect(toast.error.mock.calls).toEqual([
      ["Вы в черном списке этого блога"],
    ]);
  });

  it("keeps the plate of an expired session, which is not about the press", async () => {
    const { api, refusalHere } = await failing(401);

    const { error } = await api.post("posts/1", {});
    const said = refusalHere(error!, "Не удалось сохранить пост");
    await endOfTask();

    expect(said).toBeNull();
    expect(toast.warning).toHaveBeenCalledWith("Сессия истекла, войдите снова");
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
