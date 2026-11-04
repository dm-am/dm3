import { describe, it, expect, vi, beforeEach } from "vitest";
import { endOfTask } from "@/shared/lib/testing/tasks";

// The interceptor is driven through the client that installs it, so every
// describe here also holds that the client does install it.

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

/** A client whose every request fails the way axios fails it. */
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
    get: vi.fn(failure),
    post: vi.fn(failure),
  } as never);

  const { default: api } = await import("./client");
  const { notifyFailure, refusalHere } = await import("@/shared/lib/errors");
  return { api, notifyFailure, refusalHere };
}

/** Loads the client with a captured interceptor, for the two describes below. */
async function loadClientWithInterceptor() {
  const axios = (await import("axios")).default;
  const use = vi.fn();

  vi.spyOn(axios, "create").mockReturnValue({
    interceptors: { response: { use } },
  } as never);

  await import("./client");
  const interceptor = await import("./interceptor");
  const rejected = use.mock.calls[0][1] as (error: unknown) => Promise<unknown>;
  // What axios rejects with: its own error, carrying the parts a case names.
  const { AxiosError } = await import("axios");
  const onRejected = (parts: object) =>
    rejected(Object.assign(new AxiosError("failed"), parts));

  return { interceptor, onRejected };
}

// One module owns "who the viewer is": the auth store, not the persisted copy.
describe("Api on 401", () => {
  beforeEach(() => {
    vi.resetModules();
    localStorage.clear();
  });

  it("hands the expired session to the app instead of clearing storage", async () => {
    const { interceptor, onRejected } = await loadClientWithInterceptor();
    const handler = vi.fn();
    interceptor.setSessionExpiredHandler(handler);
    localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));

    await expect(
      onRejected({ response: { status: 401, headers: {} } }),
    ).rejects.toBeDefined();

    expect(handler).toHaveBeenCalledTimes(1);
    // The persisted copy has exactly one writer, the auth store. A second one
    // here is how the store and localStorage drifted apart in the first place.
    expect(localStorage.getItem("user")).not.toBeNull();
  });
  // For a guest the start of the page is answered with a 401.
  it("takes the 401 of a session probe for a guest, not an expiry", async () => {
    const { interceptor, onRejected } = await loadClientWithInterceptor();
    const handler = vi.fn();
    interceptor.setSessionExpiredHandler(handler);
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

  // Ten requests answered with 401 by one ended session are one event, and the
  // answers come spread past the toast's own dedupe.
  it("says an expiry once, however many requests it answers", async () => {
    const { interceptor, onRejected } = await loadClientWithInterceptor();
    const handler = vi.fn();
    interceptor.setSessionExpiredHandler(handler);
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

// The server names its refusal; this is the only place that reads it, since
// notifyFailure stays quiet on 403.
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
    // Signing in names the state of the account under the field.
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
    // The guest forms print a failed press in their own strip.
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
    // A 403 the error middleware never saw comes back with an empty body.
    await refuse("");

    expect(toast.error.mock.calls).toEqual([
      ["Запрос не выполнен: недостаточно прав"],
    ]);
  });

  it("reads a refusal the server marks general as naming nothing", async () => {
    // The API titles every refusal of the intention manager with one sentence,
    // and relayed as it stood it said nothing about which press it answered.
    await refuse({
      type: "",
      title: "Недостаточно прав для этого действия",
      status: 403,
      traceId: "00-abc",
      general: true,
    });

    expect(toast.error.mock.calls).toEqual([
      ["Запрос не выполнен: недостаточно прав"],
    ]);
  });

  it("knows a general refusal by the mark, not by its words", async () => {
    // No copy of the server's title on the client: unmarked, the title is the server's reason.
    await refuse({
      type: "",
      title: "Недостаточно прав для этого действия",
      status: 403,
      traceId: "00-abc",
    });

    expect(toast.error.mock.calls).toEqual([
      ["Недостаточно прав для этого действия"],
    ]);
  });
});

/**
 * A refusal named only in general is one plate, headed by the phrase the call
 * site hands over: the plate waits for it.
 */
describe("Api on a refusal named only in general", () => {
  beforeEach(() => {
    vi.resetModules();
    toast.error.mockClear();
  });

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
      general: true,
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

// A form pressed again answers the 429 itself (ownsRateLimit), apart from
// ownsRefusal, whose callers keep their toast.
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
 * The interceptor holds every plate it raises for a refusal until the end of
 * the task; a page that writes the refusal at the press takes it down.
 */
describe("Api on a refusal the page says itself", () => {
  beforeEach(() => {
    vi.resetModules();
    toast.error.mockClear();
    toast.warning.mockClear();
  });

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
