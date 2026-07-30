/**
 * The session store itself lives in shared/stores: it is read at every layer,
 * including one shared composable, so it cannot sit in a slice. It is
 * re-exported here because this slice owns the calls that move it (lib/session)
 * and consumers want both from one place.
 */
export { useAuthStore } from "@/shared/stores";
