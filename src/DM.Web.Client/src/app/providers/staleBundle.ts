// A new deploy renames the chunks, so an open tab reloads once; the flag stops
// a reload loop and is cleared by the router once a later navigation succeeds.
const RELOAD_FLAG = "dm_chunk_reload";

/** Each browser words a failed import its own way, and Vite words a failed stylesheet. */
const STALE_BUNDLE_TEXTS = [
  // Firefox, and the names of the old webpack build.
  /loading (chunk|css chunk|dynamically imported module)/i,
  /import\(\)/i,
  // Chromium.
  /failed to fetch dynamically imported module/i,
  // Safari.
  /importing a module script failed/i,
  // Vite's preload of a page's stylesheet.
  /unable to preload css/i,
];

export function isStaleBundleError(error: unknown): boolean {
  const { name, message } = (error ?? {}) as {
    name?: unknown;
    message?: unknown;
  };
  if (name === "ChunkLoadError") return true;
  const text = typeof message === "string" ? message : "";
  return STALE_BUNDLE_TEXTS.some((pattern) => pattern.test(text));
}

/**
 * Loads the page once per failure streak: at the target of a navigation that
 * failed, so the reader lands where he clicked, or where he is.
 */
export function reloadOnce(target?: string): void {
  cancelPendingReload();
  if (sessionStorage.getItem(RELOAD_FLAG)) return;

  sessionStorage.setItem(RELOAD_FLAG, "1");
  if (target === undefined) window.location.reload();
  else window.location.assign(target);
}

export function clearReloadFlag(): void {
  sessionStorage.removeItem(RELOAD_FLAG);
}

let pendingReload: ReturnType<typeof setTimeout> | null = null;

/** For a caller that handles a failed import itself, as a prefetch does. */
export function cancelPendingReload(): void {
  if (pendingReload !== null) clearTimeout(pendingReload);
  pendingReload = null;
}

/**
 * Covers the imports outside the router: async components, the realtime
 * transport. Vite fires the event before the import rejects, so the reload
 * waits a task: the router's onError then reloads to the navigation's target,
 * and a prefetch cancels it. No preventDefault: the error still reaches its
 * caller. Returns the teardown, which only the tests call.
 */
export function installPreloadErrorReload(): () => void {
  const onPreloadError = (): void => {
    cancelPendingReload();
    pendingReload = setTimeout(() => {
      pendingReload = null;
      reloadOnce();
    }, 0);
  };

  window.addEventListener("vite:preloadError", onPreloadError);
  return () => {
    cancelPendingReload();
    window.removeEventListener("vite:preloadError", onPreloadError);
  };
}
