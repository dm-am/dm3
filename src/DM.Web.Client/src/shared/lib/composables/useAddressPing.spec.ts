/**
 * The reading the address block draws is only worth what it measures.
 *
 * Two things here are easy to break silently and impossible to see in a
 * rendered page: which of the two round trips becomes the figure, and what
 * makes the composable measure again. The first trip to a host pays for the
 * DNS lookup and the TLS handshake and reads about twice the working figure;
 * a refactor that reports it instead of the second one changes every number on
 * the board and no test of the block notices. A network change is the one
 * event the three-minute focus rule exists to approximate; drop the listener
 * and the board keeps describing a network the device has left.
 *
 * Time is faked as a whole: `fetch` answers after a scripted delay on the
 * fake clock, and `performance.now` runs on the same clock, so a trip measures
 * exactly its scripted duration however the trips of the two hosts interleave.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { defineComponent } from "vue";
import { mount, type VueWrapper } from "@vue/test-utils";
import { SITE_ADDRESSES } from "@/shared/config/site";
import { useAddressPing, type AddressPing } from "./useAddressPing";

const hosts = SITE_ADDRESSES.map((address) => address.host);

/** A component that owns one composable instance and exposes its record. */
const Probe = defineComponent({
  setup() {
    const pings = useAddressPing();
    return { pings };
  },
  template: "<div />",
});

let fetchMock: ReturnType<typeof vi.fn>;
/** Every wrapper a test mounts, so its listeners leave with the test. */
let mounted: VueWrapper[] = [];

/**
 * Scripts `fetch`: the n-th call answers after `durations[n]` ms of fake
 * time, or rejects when the entry is `null`. Calls past the script answer in
 * one millisecond.
 */
function scriptFetch(durations: (number | null)[]) {
  let call = 0;
  fetchMock.mockImplementation(
    () =>
      new Promise<Response>((resolve, reject) => {
        const duration = durations[call++];
        if (duration === null) {
          reject(new TypeError("Failed to fetch"));
          return;
        }
        setTimeout(() => resolve(new Response(null)), duration ?? 1);
      }),
  );
}

function render(): VueWrapper {
  const wrapper = mount(Probe);
  mounted.push(wrapper);
  return wrapper;
}

function pingsOf(wrapper: VueWrapper): Record<string, AddressPing> {
  return (wrapper.vm as unknown as { pings: Record<string, AddressPing> })
    .pings;
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date("2026-09-07T12:00:00Z"));
  fetchMock = vi.fn();
  scriptFetch([]);
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => {
  for (const wrapper of mounted) wrapper.unmount();
  mounted = [];
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe("useAddressPing", () => {
  it("asks every address twice and reports the second, warm, round trip", async () => {
    // Both first trips start together: host A cold 210, host B cold 400. Host
    // A's warm trip is the third call (90), host B's the fourth (150).
    scriptFetch([210, 400, 90, 150]);
    const wrapper = render();
    await vi.advanceTimersByTimeAsync(1000);

    expect(fetchMock).toHaveBeenCalledTimes(hosts.length * 2);
    const pings = pingsOf(wrapper);
    expect(pings[hosts[0]]).toEqual({ status: "up", latencyMs: 90 });
    expect(pings[hosts[1]]).toEqual({ status: "up", latencyMs: 150 });
  });

  it("calls an address down when its first trip fails, and asks it nothing more", async () => {
    // Host A answers twice; host B refuses at once.
    scriptFetch([100, null, 100]);
    const wrapper = render();
    await vi.advanceTimersByTimeAsync(1000);

    const pings = pingsOf(wrapper);
    expect(pings[hosts[1]]).toEqual({ status: "down", latencyMs: null });
    expect(pings[hosts[0]]).toEqual({ status: "up", latencyMs: 100 });
    // One refused trip to the dead host, two to the live one.
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it("measures again when the device comes back online", async () => {
    render();
    await vi.advanceTimersByTimeAsync(100);
    const afterMount = fetchMock.mock.calls.length;

    // Past the guard against a browser reporting one change as several.
    await vi.advanceTimersByTimeAsync(6_000);
    window.dispatchEvent(new Event("online"));
    await vi.advanceTimersByTimeAsync(100);

    expect(fetchMock.mock.calls.length).toBe(afterMount + hosts.length * 2);
  });

  it("does not turn several change events in a row into several rounds", async () => {
    render();
    await vi.advanceTimersByTimeAsync(100);
    const afterMount = fetchMock.mock.calls.length;

    await vi.advanceTimersByTimeAsync(6_000);
    window.dispatchEvent(new Event("online"));
    window.dispatchEvent(new Event("online"));
    window.dispatchEvent(new Event("online"));
    await vi.advanceTimersByTimeAsync(100);

    expect(fetchMock.mock.calls.length).toBe(afterMount + hosts.length * 2);
  });

  it("stops listening when the block leaves the page", async () => {
    const wrapper = render();
    await vi.advanceTimersByTimeAsync(100);
    wrapper.unmount();
    mounted = [];
    const afterUnmount = fetchMock.mock.calls.length;

    await vi.advanceTimersByTimeAsync(6_000);
    window.dispatchEvent(new Event("online"));
    await vi.advanceTimersByTimeAsync(100);

    expect(fetchMock.mock.calls.length).toBe(afterUnmount);
  });
});
