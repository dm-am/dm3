/**
 * @vitest-environment node
 */

/**
 * The application subscribes to realtime pushes once, and not behind the result
 * of a connect attempt.
 *
 * Both entry points used to read `const connected = await connectSignalR()` and
 * register the handler only when that came back true. Nothing retried the
 * registration afterwards: the automatic reconnect of the SignalR client revives
 * a connection that started, and a first attempt that failed leaves the socket to
 * be started by some later call - by which time the handler is still not
 * registered. The messages badge and the notifications badge have no polling
 * fallback of their own, so for the rest of that session they moved only on a
 * full page load.
 *
 * Registration is idempotent by construction - the composable keeps handlers in a
 * Set keyed by function identity - which is why the fix is one unconditional call
 * and why this checks there is exactly one of it.
 *
 * Asserted on the source, because what it forbids is a branch, and a branch that
 * is not taken renders identically to one that is.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";

/** This spec sits at the root of the client sources. */
const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const APP = readFileSync(join(CLIENT_SRC, "app", "App.vue"), "utf8");

describe("realtime push subscription", () => {
  it("registers the notification handler exactly once", () => {
    // The destructuring of the composable spells the name without a paren, so
    // only calls are counted here.
    expect(APP.match(/onNotification\(/g) ?? []).toHaveLength(1);
    expect(APP).toContain("onNotification(handleNotification);");
  });

  it("registers before the first connect attempt rather than after it", () => {
    const subscribeAt = APP.indexOf("onNotification(handleNotification);");
    const connectAt = APP.indexOf("connectSignalR()");

    expect(subscribeAt).toBeGreaterThan(-1);
    expect(connectAt).toBeGreaterThan(-1);
    expect(subscribeAt).toBeLessThan(connectAt);
  });

  it("keeps nothing conditional on the outcome of a connect attempt", () => {
    expect(APP).not.toMatch(/=\s*await connectSignalR\(\)/);
  });
});
