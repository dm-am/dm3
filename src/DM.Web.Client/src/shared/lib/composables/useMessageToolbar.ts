import { getCurrentInstance, onBeforeUnmount, ref, type Ref } from "vue";

/**
 * The hover toolbar that appears beside a chat message.
 *
 * Both chat views grew their own copy, and the copies drifted: the global chat
 * gained keyboard reachability — messages are `tabindex="0"`, focusin reveals
 * the toolbar and focusout starts the same hide countdown mouseleave does —
 * and the messenger never did, so a keyboard user could reach every message
 * action in one view and none in the other. Whatever the toolbar learns next
 * should reach both, which is what this file is for.
 *
 * The domain actions the toolbar triggers are deliberately not here: liking,
 * editing and deleting are one API call each and differ per view. What is
 * shared is when the toolbar appears, where, and when it goes away.
 */
export interface MessageToolbarOptions {
  /**
   * Element the toolbar is positioned inside. The global chat's toolbar is
   * absolutely positioned within its scroll container, so its coordinates are
   * relative to that box; the messenger's is fixed, so it wants the viewport.
   * Omit for the viewport.
   */
  container?: Ref<HTMLElement | null>;

  /**
   * The toolbar element. Needed to tell "focus moved into the toolbar" from
   * "focus left": the toolbar is a sibling of the message row, not a
   * descendant, so focus-within cannot answer that on its own.
   */
  toolbar: Ref<HTMLElement | null>;

  /**
   * True while the list is scrolling. Hover is ignored then — otherwise the
   * toolbar chases the cursor down the list as messages slide past it.
   */
  isScrolling: () => boolean;
}

/** Milliseconds the toolbar survives after the pointer leaves the message. */
const MESSAGE_LEAVE_DELAY = 150;

/**
 * Milliseconds after leaving the toolbar itself. Shorter than the message
 * delay: the pointer is already on its way out, and the grace period only has
 * to cover travel between the toolbar's own buttons.
 */
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
          top: rect.top - bounds.top - 16,
          right: bounds.right - rect.right + 8,
        }
      : { top: rect.top - 16, right: window.innerWidth - rect.right + 8 };

    hoveredMessageId.value = messageId;
  }

  function handleMessageMouseEnter(event: MouseEvent, messageId: string): void {
    if (options.isScrolling()) return;
    showFor(event.currentTarget as HTMLElement, messageId);
  }

  /**
   * Keyboard counterpart to hover. Not gated on isScrolling: focus does not
   * arrive by accident, and a keyboard user never scrolls past the message
   * they just tabbed to.
   */
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

  /**
   * Tabbing into a toolbar button must cancel the message's pending hide the
   * way hovering it does, or the timer fires mid-Tab and takes the toolbar
   * away before the button can be pressed.
   */
  function handleToolbarFocusIn(): void {
    handleToolbarMouseEnter();
  }

  /**
   * Leaving the toolbar entirely, as opposed to moving between its own
   * buttons. A null relatedTarget means focus left the document — the address
   * bar, say — which counts as leaving.
   */
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

  // Guarded so the composable can be exercised on its own: a pending timer
  // fires into a torn-down component otherwise, and registering the hook
  // outside setup() is a warning rather than a mechanism.
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
    handleToolbarFocusIn,
    handleToolbarFocusOut,
    handleScrollStart,
  };
}
