import { getCurrentInstance, onBeforeUnmount, ref, type Ref } from "vue";
import { MEDIUM, SMALL } from "@/shared/lib/constants/geometry";

/**
 * The hover toolbar beside a chat message, shared by both chat views so the
 * keyboard reaches it in both: when it appears, where, and when it goes. The
 * actions it triggers differ per view and stay there.
 */
export interface MessageToolbarOptions {
  /**
   * The global chat positions within its scroll container, the messenger in
   * the viewport (omit).
   */
  container?: Ref<HTMLElement | null>;

  /** A sibling of the row, so focus-within cannot tell focus moving into it. */
  toolbar: Ref<HTMLElement | null>;

  /** Hover is ignored while scrolling, or the toolbar chases the cursor. */
  isScrolling: () => boolean;
}

/** Milliseconds the toolbar survives after the pointer leaves the message. */
const MESSAGE_LEAVE_DELAY = 150;

/** Shorter: it only covers travel between the toolbar's own buttons. */
const TOOLBAR_LEAVE_DELAY = 100;

export function useMessageToolbar(options: MessageToolbarOptions) {
  const hoveredMessageId = ref<string | null>(null);
  const toolbarPosition = ref({ top: 0, right: 0 });
  const isToolbarHovered = ref(false);

  /** Which message is showing "точно удалить?" in place of its delete button. */
  const confirmingDeleteId = ref<string | null>(null);

  let hideTimeout: ReturnType<typeof setTimeout> | null = null;

  function cancelHide(): void {
    if (hideTimeout) {
      clearTimeout(hideTimeout);
      hideTimeout = null;
    }
  }

  /** Drops the toolbar and any half-finished confirmation with it. */
  function hide(): void {
    hoveredMessageId.value = null;
    confirmingDeleteId.value = null;
  }

  function showFor(target: HTMLElement, messageId: string): void {
    cancelHide();

    const rect = target.getBoundingClientRect();
    const origin = options.container?.value;

    if (options.container && !origin) return;

    const bounds = origin?.getBoundingClientRect();
    toolbarPosition.value = bounds
      ? {
          top: rect.top - bounds.top - MEDIUM,
          right: bounds.right - rect.right + SMALL,
        }
      : {
          top: rect.top - MEDIUM,
          right: window.innerWidth - rect.right + SMALL,
        };

    hoveredMessageId.value = messageId;
  }

  function handleMessageMouseEnter(event: MouseEvent, messageId: string): void {
    if (options.isScrolling()) return;
    showFor(event.currentTarget as HTMLElement, messageId);
  }

  /** Not gated on isScrolling: focus does not arrive by accident. */
  function handleMessageFocusIn(event: FocusEvent, messageId: string): void {
    showFor(event.currentTarget as HTMLElement, messageId);
  }

  function handleMessageMouseLeave(): void {
    cancelHide();
    hideTimeout = setTimeout(() => {
      if (!isToolbarHovered.value) hide();
      hideTimeout = null;
    }, MESSAGE_LEAVE_DELAY);
  }

  function handleMessageFocusOut(event: FocusEvent): void {
    const next = event.relatedTarget as Node | null;
    if (next && options.toolbar.value?.contains(next)) return;
    handleMessageMouseLeave();
  }

  /** Also the answer to tabbing into a button: focus cancels the hide as hovering does. */
  function handleToolbarMouseEnter(): void {
    cancelHide();
    isToolbarHovered.value = true;
  }

  function handleToolbarMouseLeave(): void {
    isToolbarHovered.value = false;
    cancelHide();
    hideTimeout = setTimeout(() => {
      hide();
      hideTimeout = null;
    }, TOOLBAR_LEAVE_DELAY);
  }

  /** Leaving the toolbar; a null relatedTarget is focus leaving the document. */
  function handleToolbarFocusOut(event: FocusEvent): void {
    const next = event.relatedTarget as Node | null;
    if (next && options.toolbar.value?.contains(next)) return;
    handleToolbarMouseLeave();
  }

  /** Scrolling takes the toolbar with it; there is nothing for it to sit beside. */
  function handleScrollStart(): void {
    if (!hoveredMessageId.value) return;
    hide();
    isToolbarHovered.value = false;
  }

  // Guarded so the composable runs on its own in a test.
  if (getCurrentInstance()) onBeforeUnmount(cancelHide);

  return {
    hoveredMessageId,
    toolbarPosition,
    isToolbarHovered,
    confirmingDeleteId,
    showFor,
    hide,
    handleMessageMouseEnter,
    handleMessageFocusIn,
    handleMessageMouseLeave,
    handleMessageFocusOut,
    handleToolbarMouseEnter,
    handleToolbarMouseLeave,
    handleToolbarFocusOut,
    handleScrollStart,
  };
}
