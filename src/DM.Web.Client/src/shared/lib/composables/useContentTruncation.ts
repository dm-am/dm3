/**
 * Composable for content truncation with expand/collapse.
 *
 * Scope (what this composable does):
 *   - Track whether the referenced content element overflows a given
 *     max-height budget (reactive to content changes via watchContent).
 *   - Expose `needsTruncation` + `isExpanded` state and `contentStyle`
 *     for the content container.
 *   - NO DOM mutation. Callers are responsible for pre-trimming leading/
 *     trailing whitespace from their HTML via `trimHtmlWhitespace` in a
 *     computed before passing to v-html. Pushing that responsibility up
 *     keeps this composable side-effect-free and keeps Vue reactivity
 *     the single source of truth over the slot DOM.
 *
 * Typical usage is indirect — prefer the <TruncatedContent> shared
 * component in @/shared/ui/TruncatedContent, which wires this up for
 * the common "show more" pattern with accessibility, media shrinkage,
 * and hard-cut visual contract baked in.
 */

import { ref, computed, watch, nextTick, type Ref, type ComputedRef } from "vue";

export interface ContentTruncationOptions {
  /** Maximum height in px before truncation (default: 150) */
  maxHeight?: number | Ref<number>;
  /** Enable truncation (default: true) */
  enabled?: boolean | Ref<boolean> | ComputedRef<boolean>;
  /** Content to watch for changes (triggers re-measurement) */
  watchContent?: Ref<unknown> | ComputedRef<unknown>;
  /** Callback after content ref is set (for additional initialization) */
  onContentMounted?: (el: HTMLElement) => void;
}

export interface ContentTruncationReturn {
  /** Whether content is currently expanded */
  isExpanded: Ref<boolean>;
  /** Whether content overflows maxHeight */
  isOverflowing: Ref<boolean>;
  /** Whether truncation should be applied (enabled && overflowing) */
  needsTruncation: ComputedRef<boolean>;
  /** Style object for the content container */
  contentStyle: ComputedRef<{ maxHeight?: string }>;
  /** Ref setter function for the content element */
  setContentRef: (el: unknown) => void;
  /** Toggle expand/collapse state */
  toggleExpand: () => void;
  /** Force re-check overflow (call after dynamic content changes) */
  checkOverflow: () => void;
  /** Reference to the content element */
  contentRef: Ref<HTMLElement | null>;
}

export function useContentTruncation(
  options: ContentTruncationOptions = {},
): ContentTruncationReturn {
  const {
    maxHeight: maxHeightOption = 150,
    enabled: enabledOption = true,
    watchContent,
    onContentMounted,
  } = options;

  // Normalize options to refs
  const maxHeight = computed(() =>
    typeof maxHeightOption === "number" ? maxHeightOption : maxHeightOption.value,
  );
  const enabled = computed(() =>
    typeof enabledOption === "boolean" ? enabledOption : enabledOption.value,
  );

  // State
  const isExpanded = ref(false);
  const isOverflowing = ref(false);
  const contentRef = ref<HTMLElement | null>(null);

  // Computed
  const needsTruncation = computed(() => enabled.value && isOverflowing.value);

  const contentStyle = computed(() => {
    if (!needsTruncation.value) return {};
    if (isExpanded.value) return { maxHeight: "none" };
    return { maxHeight: `${maxHeight.value}px` };
  });

  // Methods
  function checkOverflow(): void {
    if (contentRef.value && enabled.value) {
      isOverflowing.value = contentRef.value.scrollHeight > maxHeight.value;
    }
  }

  function toggleExpand(): void {
    isExpanded.value = !isExpanded.value;
  }

  function setContentRef(el: unknown): void {
    const htmlEl = el as HTMLElement | null;
    if (htmlEl && contentRef.value !== htmlEl) {
      contentRef.value = htmlEl;
      nextTick(() => {
        checkOverflow();
        onContentMounted?.(htmlEl);
      });
    }
  }

  // Watch for content changes — re-measure only, no DOM mutation.
  if (watchContent) {
    watch(watchContent, () => {
      nextTick(() => {
        if (contentRef.value) {
          checkOverflow();
        }
      });
    });
  }

  return {
    isExpanded,
    isOverflowing,
    needsTruncation,
    contentStyle,
    setContentRef,
    toggleExpand,
    checkOverflow,
    contentRef,
  };
}
