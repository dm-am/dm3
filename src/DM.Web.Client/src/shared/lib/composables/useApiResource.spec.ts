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

  it("makes a caller wait for the refetch invalidate started", async () => {
    const first = deferred<string[]>();
    const second = deferred<string[]>();
    const fetcher = vi
      .fn<() => Promise<ApiResult<string[]>>>()
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise);
    // A minute of cache: without invalidate() the data below is fresh, and a
    // plain fetch() would return at the freshness check.
    const resource = useApiResource(fetcher, { cacheMs: 60_000 });

    first.release(ok(["one game"]));
    await resource.fetch();

    const refreshing = resource.invalidate();

    // invalidate() means "what is on screen is known to be wrong", so the
    // entry has to be aged out rather than dropped: dropped, it reads as
    // absent, and a joining caller is told the data is fresh while the
    // correcting request is still on the wire.
    let joinedSettled = false;
    const joined = resource.fetch();
    void joined.then(() => {
      joinedSettled = true;
    });
    await Promise.resolve();
    expect(joinedSettled).toBe(false);

    second.release(ok(["one game", "the new one"]));
    await refreshing;
    await joined;

    expect(resource.data.value).toEqual(["one game", "the new one"]);
    expect(fetcher).toHaveBeenCalledTimes(2);
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

  it("keeps refreshing in the background after a forced fetch overtook one", async () => {
    const first = deferred<string>();
    const background = deferred<string>();
    const forced = deferred<string>();
    const later = deferred<string>();
    const queue = [
      first.promise,
      background.promise,
      forced.promise,
      later.promise,
    ];
    const fetcher = vi.fn(() => queue.shift() as Promise<ApiResult<string>>);
    // Negative TTL: everything is stale the moment it lands, so the test needs
    // no clock to reach the stale-while-revalidate branch.
    const resource = useApiResource(fetcher, { cacheMs: -1 });

    const initial = resource.fetch();
    first.release(ok("first"));
    await initial;

    // Stale: this one takes the background branch and raises the flag.
    await resource.fetch();

    // A mutation invalidates the same resource while that refresh is still on
    // the wire, so the guard moves on and the background answer is dropped.
    const invalidated = resource.invalidate();
    forced.release(ok("forced"));
    background.release(ok("background"));
    await invalidated;

    // Stale again. With the flag left raised this call returned without a
    // request, and the resource never refreshed again.
    await resource.fetch();

    expect(fetcher).toHaveBeenCalledTimes(4);
  });

  it("raises loading synchronously, before the first await", () => {
    const never = deferred<string>();
    const resource = useApiResource(() => never.promise);

    void resource.fetch();

    expect(resource.loading.value).toBe(true);
  });
});
