/**
 * What the reader's connection says before we spend it on their behalf.
 *
 * Both readings are about traffic nobody asked for: the address block measures
 * two hosts to draw a status nobody requested, and route prefetch downloads a
 * page nobody has opened yet. Requests a reader DID ask for are never subject
 * to this — a slow connection is a reason to send less speculatively, not a
 * reason to refuse the thing on screen.
 *
 * One module rather than a private copy per caller: the second copy is where
 * the two answers to "may we spend this reader's traffic" start to differ, and
 * the interface below has to be hand-written either way.
 *
 * Read at call time and not cached, for the same reason as
 * `prefersReducedMotion`: a phone leaving wifi changes both flags mid-session,
 * and a value read once at module load would answer for the whole of it.
 */

/**
 * `navigator.connection` is not in lib.dom, so the two fields read here are
 * declared by hand. Deliberately not the whole Network Information API: what
 * is not read does not need a type, and `downlink`/`rtt` are numbers whose
 * meaning changes with the browser.
 */
interface NetworkInformationLike {
  saveData?: boolean;
  effectiveType?: string;
  addEventListener?(type: "change", listener: () => void): void;
  removeEventListener?(type: "change", listener: () => void): void;
}

function connection(): NetworkInformationLike | undefined {
  return (navigator as Navigator & { connection?: NetworkInformationLike })
    .connection;
}

/**
 * The reader has asked the browser for as little traffic as possible. An
 * explicit request, and the one flag that admits no interpretation.
 */
export function savingData(): boolean {
  return connection()?.saveData === true;
}

/**
 * The connection is slow enough that anything speculative is worse than
 * useless: on 2g a round trip is measured in seconds, and a chunk fetched for
 * a page the reader may never open competes for that link with the page they
 * are actually reading.
 *
 * Only the two 2g grades. `3g` is where most of the mobile audience sits and
 * is exactly where the head start pays best, so treating it as slow would
 * switch the feature off for the readers it was written for.
 */
export function onSlowConnection(): boolean {
  const effective = connection()?.effectiveType;
  return effective === "2g" || effective === "slow-2g";
}

/**
 * Calls back when the reader's network changes: the device comes back online,
 * or the connection itself reports a change (wifi to mobile data, a VPN
 * switched on or off). Returns the function that stops listening.
 *
 * `online` alone misses the cases that matter most — a VPN toggling leaves
 * the device online throughout — and `navigator.connection` alone is absent
 * in some browsers, so both are listened to where they exist. `offline` is
 * not a reason to measure: there is nothing to reach, and the block keeps its
 * last honest reading until the network is back.
 */
export function onConnectionChange(listener: () => void): () => void {
  window.addEventListener("online", listener);
  const info = connection();
  info?.addEventListener?.("change", listener);
  return () => {
    window.removeEventListener("online", listener);
    info?.removeEventListener?.("change", listener);
  };
}
