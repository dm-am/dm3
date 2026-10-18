import { describe, it, expect, vi } from "vitest";
import { shownOnPage, useGuardedRequest } from "./useGuardedRequest";
import type { ApiResult, GeneralError } from "@/shared/api/models/common";
import { announceWorded } from "@/shared/lib/errors/generalPlate";

/** A fetcher whose response the test releases by hand. */
function deferred<T>() {
  let release!: (value: ApiResult<T>) => void;
  const promise = new Promise<ApiResult<T>>((resolve) => {
    release = resolve;
  });
  return { promise, release };
}

const ok = <T>(data: T): ApiResult<T> => ({ data, error: null });
const failed = <T>(title: string): ApiResult<T> => ({
  data: null,
  error: { title } as GeneralError,
});

describe("useGuardedRequest", () => {
  it("keeps the answer of the newest request when an older one lands later", async () => {
    const slow = deferred<string>();
    const fast = deferred<string>();
    const request = useGuardedRequest({ message: "не вышло" });
    const applied: (string | null)[] = [];

    const stale = request.run(
      () => slow.promise,
      (data) => applied.push(data),
    );
    const fresh = request.run(
      () => fast.promise,
      (data) => applied.push(data),
    );

    fast.release(ok("новый"));
    await fresh;
    slow.release(ok("старый"));
    await stale;

    // The reader typed a second search while the first was on the wire. The
    // first answer must not repaint the table under the second query.
    expect(applied).toEqual(["новый"]);
  });

  it("leaves loading raised when a superseded answer lands first", async () => {
    const slow = deferred<string>();
    const fast = deferred<string>();
    const request = useGuardedRequest({ message: "не вышло" });

    const stale = request.run(
      () => slow.promise,
      () => {},
    );
    const fresh = request.run(
      () => fast.promise,
      () => {},
    );

    slow.release(ok("старый"));
    await stale;

    // The newest request is still out. Lowering the flag on the old answer
    // would take the skeleton away and show the previous page as if it were
    // the answer.
    expect(request.loading.value).toBe(true);

    fast.release(ok("новый"));
    await fresh;
    expect(request.loading.value).toBe(false);
  });

  it("raises loading synchronously, before the first await", () => {
    const never = deferred<string>();
    const request = useGuardedRequest({ message: "не вышло" });

    void request.run(
      () => never.promise,
      () => {},
    );

    expect(request.loading.value).toBe(true);
  });

  it("keeps what is on screen when the call fails", async () => {
    const request = useGuardedRequest({ message: "Не удалось загрузить" });
    const apply = vi.fn();

    await request.run(() => Promise.resolve(failed<string>("500")), apply);

    expect(request.error.value).toBe("Не удалось загрузить");
    expect(request.loading.value).toBe(false);
    // The error box renders beside the list, not instead of it.
    expect(apply).not.toHaveBeenCalled();
  });

  it("names the failure when the message is a function of it", async () => {
    const request = useGuardedRequest({
      message: (error) => `отказ: ${error.title}`,
    });

    await request.run(
      () => Promise.resolve(failed<string>("нет прав")),
      () => {},
    );

    expect(request.error.value).toBe("отказ: нет прав");
  });

  it("drops the failure once an answer arrives", async () => {
    const request = useGuardedRequest({ message: "не вышло" });

    await request.run(
      () => Promise.resolve(failed<string>("500")),
      () => {},
    );
    expect(request.error.value).toBe("не вышло");

    await request.run(
      () => Promise.resolve(ok("данные")),
      () => {},
    );
    expect(request.error.value).toBeNull();
  });

  it("holds the failure through the next attempt unless told otherwise", async () => {
    const pending = deferred<string>();
    const request = useGuardedRequest({ message: "не вышло" });

    await request.run(
      () => Promise.resolve(failed<string>("500")),
      () => {},
    );

    void request.run(
      () => pending.promise,
      () => {},
    );

    // Default: the banner stays up until the new answer decides, so a filter
    // change does not make it blink.
    expect(request.error.value).toBe("не вышло");
  });

  it("hides the failure for the length of the attempt when asked", async () => {
    const pending = deferred<string>();
    const request = useGuardedRequest({
      message: "не вышло",
      clearErrorOnStart: true,
    });

    await request.run(
      () => Promise.resolve(failed<string>("500")),
      () => {},
    );

    void request.run(
      () => pending.promise,
      () => {},
    );

    expect(request.error.value).toBeNull();
  });

  it("lowers loading when the fetcher throws, and lets the throw out", async () => {
    const request = useGuardedRequest({ message: "не вышло" });

    await expect(
      request.run(
        () => Promise.reject(new Error("сеть отвалилась")),
        () => {},
      ),
    ).rejects.toThrow("сеть отвалилась");

    // A detached API method loses `this` and throws a TypeError. Swallowing it
    // would leave the screen spinning with nothing on the wire.
    expect(request.loading.value).toBe(false);
  });

  it("clears the failure without starting anything", async () => {
    const request = useGuardedRequest({ message: "не вышло" });

    await request.run(
      () => Promise.resolve(failed<string>("500")),
      () => {},
    );
    request.clearError();

    // A scope change (another profile, another game) drops the previous
    // answer and the previous complaint about it.
    expect(request.error.value).toBeNull();
  });
});

/**
 * Every screen that loads through here shows `error` in its own box beside the
 * content. The plate the interceptor held for the same failure said it a
 * second time in the corner, "Запрос не выполнен: ошибка сервера" over "Не
 * удалось загрузить топики [Повторить]".
 */
describe("the plate of a failed load", () => {
  const problem = (status: number): GeneralError => ({
    type: "",
    title: "",
    status,
    traceId: "t",
  });
  const endOfTask = () => new Promise((resolve) => setTimeout(resolve, 0));

  it("is taken down: the screen says the failure in its own box", async () => {
    const failure = problem(500);
    const raise = vi.fn();
    const request = useGuardedRequest({ message: "Не удалось загрузить" });

    await request.run(
      async () => {
        announceWorded(failure, raise);
        return { data: null, error: failure };
      },
      () => {},
    );
    await endOfTask();

    expect(request.error.value).toBe("Не удалось загрузить");
    expect(raise).not.toHaveBeenCalled();
  });

  it.each([401, 0])("stays up for status %i", async (status) => {
    const failure = problem(status);
    const raise = vi.fn();
    const request = useGuardedRequest({ message: "Не удалось загрузить" });

    await request.run(
      async () => {
        announceWorded(failure, raise);
        return { data: null, error: failure };
      },
      () => {},
    );
    await endOfTask();

    expect(raise).toHaveBeenCalledTimes(1);
  });

  // Two requests of one load land in two tasks. The plate of the first is up
  // by the time the second answers, so each is taken over as it lands.
  it("is taken down for each half of a load that waits on two", async () => {
    const first = problem(403);
    const raiseFirst = vi.fn();
    const request = useGuardedRequest({ message: "Не удалось загрузить" });

    await request.run(
      async () => {
        const [a] = await Promise.all([
          Promise.resolve({ data: null, error: first })
            .then((answer) => {
              announceWorded(first, raiseFirst);
              return answer;
            })
            .then(shownOnPage),
          endOfTask()
            .then(() => ({ data: 1, error: null }))
            .then(shownOnPage),
        ]);
        return { data: null, error: a.error };
      },
      () => {},
    );
    await endOfTask();

    expect(raiseFirst).not.toHaveBeenCalled();
  });
});
