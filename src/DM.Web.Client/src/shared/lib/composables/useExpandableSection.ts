import { computed, onBeforeUnmount, ref, watch, type Ref } from "vue";
import { useAnimatedHeightToggle } from "./useAnimatedHeightToggle";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "./useExpandableRegistry";

/**
 * An expandable content section with the whole site contract: the
 * useAnimatedHeightToggle reveal in the .expand-zone tempo, registration for
 * "Развернуть все" when it can collapse, and a toggle that clears a pending bulk
 * action. Text truncation is TruncatedContent's; tool reveals and persistent
 * sidebar sections stay out of the registry (UI_STANDARDS.md).
 *
 * Usage:
 *   const zoneRef = ref<HTMLElement | null>(null);
 *   const { isExpanded, toggle, zoneBindings } =
 *     useExpandableSection({ el: zoneRef, label: "MyWidget" });
 *
 *   <div ref="zoneRef" class="expand-zone" v-bind="zoneBindings">...</div>
 *   <button v-if="!isExpanded" @click="toggle(true)">...</button>
 */
export function useExpandableSection(options: {
  /** The animated zone element (carries the global .expand-zone class). */
  el: Ref<HTMLElement | null>;
  /** External expanded state (e.g. a writable computed over props/emit or
   * a parent-owned ref). Omit to let the composable own the state. */
  model?: Ref<boolean>;
  /** Start expanded: nothing to collapse — stays out of the registry.
   * Ignored when `model` is provided (the model's value is the start). */
  startExpanded?: boolean;
  /** Registers while true; the animation works regardless. Default: always on. */
  registryEnabled?: Ref<boolean> | (() => boolean);
  /** Registry handle label (debugging). */
  label?: string;
}) {
  const isExpanded: Ref<boolean> =
    options.model ?? ref(options.startExpanded ?? false);

  const {
    pinnedHeight,
    toggle: animate,
    onTransitionEnd,
  } = useAnimatedHeightToggle(options.el, (next) => {
    isExpanded.value = next;
  });

  // Registered in setup, not onMounted, so a section mounting after a bulk
  // action still syncs.
  const registryGate = options.registryEnabled ?? (() => true);
  const gateValue = computed(() =>
    typeof registryGate === "function" ? registryGate() : registryGate.value,
  );
  const participates = computed(
    () => !options.startExpanded || !!options.model,
  );

  let unregister: (() => void) | null = null;
  function syncRegistration(active: boolean) {
    if (active && !unregister) {
      unregister = registerExpandable({
        id: Symbol(options.label ?? "ExpandableSection"),
        isExpanded: () => isExpanded.value,
        expand: () => {
          if (!isExpanded.value) animate(true);
        },
        collapse: () => {
          if (isExpanded.value) animate(false);
        },
      });
    } else if (!active && unregister) {
      unregister();
      unregister = null;
    }
  }
  watch(
    () => participates.value && gateValue.value,
    (active) => syncRegistration(active),
    { immediate: true },
  );
  onBeforeUnmount(() => unregister?.());

  /** Manual user toggle: clears the registry's pending bulk action. */
  function toggle(next: boolean = !isExpanded.value) {
    notifyExpandableChanged();
    animate(next);
  }

  /** The pin, the in-flight class and both transition listeners. */
  const zoneBindings = computed(() => ({
    class: { animating: pinnedHeight.value !== null },
    style:
      pinnedHeight.value !== null ? { height: pinnedHeight.value } : undefined,
    onTransitionend: onTransitionEnd,
    onTransitioncancel: onTransitionEnd,
  }));

  return { isExpanded, toggle, zoneBindings };
}
