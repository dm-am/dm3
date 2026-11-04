import { onMounted, ref, watch, type Ref } from "vue";
import { useRoute } from "vue-router";

/**
 * The exposed part of ExpandableList: the component is generic, and
 * InstanceType of a generic SFC fails vue-tsc.
 */
export interface ExpandableListExpose {
  expandItem: (id: string) => void;
}

/**
 * The template ref of an ExpandableList that opens the row named by the hash
 * (the router scrolls to it). The hash arrives on mount and also without one,
 * on a same-document navigation, hence the watcher. Only an id of this list's
 * own items opens a row: the hash also names the page's sections.
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

  // The template ref is bound only after mount.
  onMounted(() => expandForHash(route.hash));

  watch(() => route.hash, expandForHash);

  return listRef;
}
