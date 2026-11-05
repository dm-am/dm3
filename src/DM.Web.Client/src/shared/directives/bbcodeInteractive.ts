import type { Directive } from "vue";
import { initBbcodeInteractive } from "@/shared/lib/bbcode/bbcodeInteractive";
import { cleanupBbcodeInteractive } from "@/shared/lib/bbcode/bbcodeCleanup";

/**
 * Brings the server's markup of an element to life (spoilers, NSFW, images,
 * hidden blocks) for exactly as long as the element shows it: bound again when
 * its markup changes, let go when it leaves the page. "Развернуть все" counts
 * the spoilers bound here, so a stale one would keep the button up and a new
 * one would not answer it.
 *
 * The value is the markup the v-html beside it draws:
 * `<div v-bbcode-interactive="html" v-html="html" />`.
 */
export const vBbcodeInteractive: Directive<HTMLElement, string> = {
  mounted(el) {
    initBbcodeInteractive(el);
  },
  // After the patch: v-html has already put the new markup in.
  updated(el, { value, oldValue }) {
    if (value === oldValue) return;
    cleanupBbcodeInteractive(el);
    initBbcodeInteractive(el);
  },
  beforeUnmount(el) {
    cleanupBbcodeInteractive(el);
  },
};
