import { computed, onBeforeUnmount, ref, watch, type Ref } from "vue";
import { useAnimatedHeightToggle } from "./useAnimatedHeightToggle";
import {
  registerExpandable,
  notifyExpandableChanged,
} from "./useExpandableRegistry";

/**
 * THE building block for an expandable CONTENT section — one call wires the
 * complete site contract, so no new expandable can ever ship half-done:
 *
 *   1. Smooth reveal in the unified site tempo: the pin-and-animate
 *      height pattern (useAnimatedHeightToggle — a real height pin, so
 *      BOTH directions animate) against the global `.expand-zone` class
 *      from Reset.sass.
 *   2. The page-wide "Развернуть все / Свернуть все" ScrollNav toggle:
 *      collapsed-capable instances auto-register in the expandable registry
 *      (bulk actions animate too; late-mounting sections sync with a bulk
 *      action already in flight); `startExpanded` instances have no
 *      collapsed state to offer and stay unregistered.
 *   3. Correct manual-toggle semantics: `toggle` clears the registry's
 *      pending bulk action, exactly like every other manual reveal.
 *
 * Usage:
 *   const zoneRef = ref<HTMLElement | null>(null);
 *   const { isExpanded, toggle, zoneBindings } =
 *     useExpandableSection({ el: zoneRef, label: "MyWidget" });
 *
 *   <div ref="zoneRef" class="expand-zone" v-bind="zoneBindings">...</div>
 *   <button v-if="!isExpanded" @click="toggle(true)">...</button>
 *
 * Scope boundaries (see UI_STANDARDS.md, Animation Standards):
 *   - Text truncation ("показать полностью" over long text) stays with
 *     TruncatedContent — its reveal is coupled to line-snapped clamps.
 *   - TOOL reveals (inline create/edit forms, settings accordions) animate
 *     with useAnimatedHeightToggle directly but stay OUT of the registry:
 *     "Развернуть все" reads content, it must never open editing tools.
 *   - Persistent sidebar sections (localStorage state) also stay out of
 *     the registry and only share the animation tempo tokens.
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
  /** Reactive gate for registry participation (e.g. "only when the viewer
   * is a moderator"). The section registers while true and unregisters
   * while false; animation works regardless. Default: always on. */
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

  // Registry participation. Sync-registration (not onMounted) so a section
  // that mounts AFTER a bulk action still syncs via the registry's
  // pending-action logic; a reactive gate re-syncs on permission changes.
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

  /** Bindings for the `.expand-zone` element: the animation pin + the
   * in-flight class + transition listeners (cancel included, so an
   * interrupted transition never leaves a stuck pin). */
  const zoneBindings = computed(() => ({
    class: { animating: pinnedHeight.value !== null },
    style:
      pinnedHeight.value !== null ? { height: pinnedHeight.value } : undefined,
    onTransitionend: onTransitionEnd,
    onTransitioncancel: onTransitionEnd,
  }));

  return { isExpanded, toggle, zoneBindings };
}
