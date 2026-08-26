import { onMounted, ref, watch, type Ref } from "vue";
import { useRoute } from "vue-router";

/**
 * Minimal typed shape of ExpandableList's `defineExpose({ expandItem })`.
 *
 * `ExpandableList` is generic (`<script setup generic="T">`), so a template
 * ref typed via `InstanceType<typeof ExpandableList>` fails vue-tsc (a
 * generic SFC component type doesn't satisfy the `new (...) => any`
 * constraint `InstanceType` needs). Since callers here only need
 * `expandItem`, a narrow structural type sidesteps that entirely.
 */
export interface ExpandableListExpose {
  expandItem: (id: string) => void;
}

/**
 * Template ref for an ExpandableList that opens the row named by the URL hash.
 *
 * Deep-link support: #<item-id> (e.g. "#games") auto-expands the matching row.
 * Scrolling itself is handled globally by the router (router.ts already does
 * document.getElementById(hash) on every navigation) — this only adds the
 * expand-on-arrival behavior.
 *
 * A hash reaches the page two ways, and only one of them is a mount:
 *
 *   1. the document loads at the address — a link from outside, a reload, a
 *      new tab. The section component mounts with the hash already set;
 *   2. the hash changes inside a document that is already open — an
 *      address-bar paste onto the page one is already standing on, an in-page
 *      anchor, back/forward between two anchors. This is a same-document
 *      navigation: `route.hash` updates and NOTHING remounts.
 *
 * Hence a watcher next to the mount hook. Left on `onMounted` alone, case 2
 * did nothing at all — and that is the case one hits while reading the rules,
 * which made the deep link look broken even though the address was right.
 *
 * `expandItem` is idempotent, so a hash that resolves to an already open row
 * leaves it open instead of toggling it shut.
 *
 * The id is checked against the items rather than passed on blindly: the hash
 * also addresses the `<section>` wrappers on the rules page, and every rules
 * section would otherwise try to expand a row belonging to another one.
 */
export function useExpandOnHash(
  items: readonly { id: string }[],
): Ref<ExpandableListExpose | null> {
  const route = useRoute();
  const listRef = ref<ExpandableListExpose | null>(null);

  function expandForHash(hash: string) {
    const id = hash.slice(1);
    if (id && items.some((item) => item.id === id)) {
      listRef.value?.expandItem(id);
    }
  }

  // Mount, not setup: the template ref is only bound once the list has
  // rendered, and `listRef.value` is still null while setup runs.
  onMounted(() => expandForHash(route.hash));

  watch(() => route.hash, expandForHash);

  return listRef;
}
