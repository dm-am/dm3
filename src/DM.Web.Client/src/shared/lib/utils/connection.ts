/**
 * Whether the reader's connection allows traffic nobody asked for (address
 * measurements, prefetch). Read at call time: a phone leaving wifi changes it.
 */

/** The reader asked the browser for as little traffic as possible. */
export function savingData(): boolean {
  return navigator.connection?.saveData === true;
}

/** The two 2g grades: 3g is where prefetch pays best. */
export function onSlowConnection(): boolean {
  const effective = navigator.connection?.effectiveType;
  return effective === "2g" || effective === "slow-2g";
}

/**
 * Calls back when the network changes: online again, or a connection change
 * (wifi to mobile, a VPN); offline is not a reason. Returns the unsubscribe.
 */
export function onConnectionChange(listener: () => void): () => void {
  window.addEventListener("online", listener);
  const info = navigator.connection;
  info?.addEventListener?.("change", listener);
  return () => {
    window.removeEventListener("online", listener);
    info?.removeEventListener?.("change", listener);
  };
}
