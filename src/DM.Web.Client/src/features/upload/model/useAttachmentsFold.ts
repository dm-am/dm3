import { onBeforeUnmount, ref, watch } from "vue";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "@/shared/lib/composables/useExpandableRegistry";

// One file opens the strip too, so the clip always does the same thing.
export function useAttachmentsFold(count: () => number) {
  const open = ref(false);
  const handleId = Symbol("attachments");
  let unregister: (() => void) | null = null;

  watch(
    () => count() > 0,
    (has) => {
      if (has && !unregister) {
        unregister = registerExpandable({
          id: handleId,
          isExpanded: () => open.value,
          expand: () => {
            open.value = true;
          },
          collapse: () => {
            open.value = false;
          },
        });
      } else if (!has && unregister) {
        unregister();
        unregister = null;
        open.value = false;
      }
    },
    { immediate: true },
  );
  onBeforeUnmount(() => unregister?.());

  // Notifies the registry so pendingAction resets.
  function toggle() {
    open.value = !open.value;
    notifyExpandableChanged();
  }

  return { open, toggle };
}
