import { describe, it, expect, vi } from "vitest";
import { useApiResource } from "./useApiResource";
import type { ApiResult } from "@/shared/api/models/common";

/** A fetcher whose response the test releases by hand. */
function deferred<T>() {
  let release!: (value: ApiResult<T>) => void;
  const promise = new Promise<ApiResult<T>>((resolve) => {
    release = resolve;
  });
  return { promise, release };
}

const ok = <T>(data: T): ApiResult<T> => ({ data, error: null });

describe("useApiResource", () => {
  it("makes a joined caller wait for the data, not for nothing", async () => {
    const first = deferred<string[]>();
    const fetcher = vi.fn(() => first.promise);
    const resource = useApiResource(fetcher);

    // The sidebar blocks decide "the request failed" by reading the data right
    // after awaiting fetch(). A second caller used to get an already-resolved
    // promise and read null on a perfectly healthy request.
    const initial = resource.fetch();
    const joined = resource.fetch();

    let joinedSettled = false;
    void joined.then(() => {
      joinedSettled = true;
    });
    await Promise.resolve();
    expect(joinedSettled).toBe(false);

    first.release(ok(["a"]));
    await initial;
    await joined;

    expect(resource.data.value).toEqual(["a"]);
    expect(fetcher).toHaveBeenCalledTimes(1);
  });

  it("keeps the answer of the newest request when an older one lands later", async () => {
    const slow = deferred<string>();
    const fast = deferred<string>();
    const fetcher = vi
      .fn<() => Promise<ApiResult<string>>>()
      .mockReturnValueOnce(slow.promise)
      .mockReturnValueOnce(fast.promise);
    const resource = useApiResource(fetcher);

    const stale = resource.fetch();
    const fresh = resource.fetch(true);

    fast.release(ok("new"));
    await fresh;
    expect(resource.data.value).toBe("new");

    slow.release(ok("old"));
    await stale;
    expect(resource.data.value).toBe("new");
  });

  it("does not repopulate state that reset cleared", async () => {
    const inFlight = deferred<string>();
    const resource = useApiResource(() => inFlight.promise);

    const pending = resource.fetch();
    resource.reset();

    inFlight.release(ok("late"));
    await pending;

    // reset() is what logout calls: an answer still on the wire must not put the
    // previous user's data back.
    expect(resource.data.value).toBeNull();
    expect(resource.loading.value).toBe(false);
  });

  it("keeps the old data on screen while invalidate refetches", async () => {
    const first = deferred<string[]>();
    const second = deferred<string[]>();
    const fetcher = vi
      .fn<() => Promise<ApiResult<string[]>>>()
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise);
    const resource = useApiResource(fetcher);

    first.release(ok(["one game"]));
    await resource.fetch();

    // The sidebar blocks fetch on mount and the shell mounts once per session.
    // reset() was being used after a mutation, and it nulls the data, so
    // creating a game emptied "Мои игры" until a hard reload.
    const refreshing = resource.invalidate();
    expect(resource.data.value).toEqual(["one game"]);

    second.release(ok(["one game", "the new one"]));
    await refreshing;
    expect(resource.data.value).toEqual(["one game", "the new one"]);
  });

  it("does not put a request on the wire for a list nobody loaded", async () => {
    const fetcher = vi.fn(() => Promise.resolve(ok(["x"])));
    const resource = useApiResource(fetcher);

    // A mutation invalidates every list of its kind, most of which no mounted
    // component is showing. Nothing loaded means nothing on screen to correct.
    await resource.invalidate();

    expect(fetcher).not.toHaveBeenCalled();
    expect(resource.data.value).toBeNull();
  });

  it("raises loading synchronously, before the first await", () => {
    const never = deferred<string>();
    const resource = useApiResource(() => never.promise);

    void resource.fetch();

    expect(resource.loading.value).toBe(true);
  });
});
