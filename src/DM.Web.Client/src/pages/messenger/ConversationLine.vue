<script setup lang="ts">
/**
 * ConversationLine: the line at the top of a correspondence's frame, where
 * the global chat has its events, and in their dress (ChatStrip): who the
 * correspondence is with and whether he is on the site, then the two things
 * done to it, "поиск" and "…".
 *
 * A conversation of two names the other one, a link to his profile, with
 * "[online]" or "[offline]" after the name, the site's way of saying it. A
 * group names itself and the number of its participants.
 *
 * "поиск" opens the search of the correspondence under the line (the page
 * draws it); it stands only over a correspondence with something in it.
 *
 * "…" opens the menu of the correspondence, the site's one menu (_Menu.sass):
 * the profile of the other one, the clearing of the history for the reader
 * alone, the leaving of a group. Each line stands only where the server says
 * the reader may do it, and "…" only where one line does. The clearing and
 * the leaving ask first (the page's windows).
 *
 * The menu hangs under "…" at its left edge, or at its right edge where the
 * window has no room for it to the right. It is drawn over the page rather
 * than inside the frame of the feed, which clips what stands outside it: in a
 * narrow column the menu was cut at the frame's edge. A scroll or a resize
 * takes it down, as it would leave it hanging away from "…". So does the focus
 * leaving "…" and the menu both: drawn at the end of the page, the menu is
 * not where Tab goes from "…", and it stood open over the feed the focus
 * went to.
 *
 * The line is one inline run with real spaces, as the events are, so it
 * copies as it reads: "SolohinLex [online] | поиск | …".
 */
import { computed, nextTick, onUnmounted, ref, watch } from "vue";
import type { Chat } from "@/entities/message";
import type { User } from "@/shared/api/models/community";
import { isUserOnline } from "@/shared/lib/utils/chat";
import { useMenuKeyboard } from "@/shared/lib/composables/useMenuKeyboard";
import { ChatStrip } from "@/widgets/chat-feed";

const props = withDefaults(
  defineProps<{
    /** The open correspondence; null while it is on its way. */
    chat: Chat | null;
    /** The other one of a conversation of two. */
    interlocutor?: User | null;
    /** The search under the line is open. */
    searchOpen?: boolean;
    /** There is something to search in. */
    canSearch?: boolean;
  }>(),
  { interlocutor: null, searchOpen: false, canSearch: false },
);

const emit = defineEmits<{
  "toggle-search": [];
  "clear-history": [];
  leave: [];
}>();

const isGroup = computed(() => props.chat?.type === "Group");

const name = computed(() =>
  isGroup.value
    ? props.chat?.title || "Групповая переписка"
    : (props.interlocutor?.username ?? ""),
);

const online = computed(() =>
  isUserOnline(props.interlocutor?.lastActivityUtc),
);

const participants = computed(() => props.chat?.participants?.length ?? 0);

/** The other one's profile, in a conversation of two. */
const profile = computed(() =>
  !isGroup.value && props.interlocutor
    ? {
        name: "profile",
        params: { username: props.interlocutor.username },
      }
    : null,
);

const canClear = computed(() => props.chat?.viewer?.canClearHistory === true);
const canLeave = computed(() => props.chat?.viewer?.canLeave === true);
const hasMenu = computed(
  () => Boolean(profile.value) || canClear.value || canLeave.value,
);

// ─────────────────────────────────────────────────────────────
// The menu under "…"
// ─────────────────────────────────────────────────────────────
const menuOpen = ref(false);
const anchor = ref<HTMLElement | null>(null);
const trigger = ref<HTMLButtonElement | null>(null);
const menu = ref<HTMLElement | null>(null);

/** Where the menu stands on the screen; null until it is measured. */
const menuAt = ref<{ top: number; left: number } | null>(null);

/** The least the menu keeps from the edge of the window. */
const WINDOW_GUTTER = 8;
/** The gap between "…" and the menu under it, the site's $tiny. */
const MENU_GAP = 4;

function closeMenu() {
  menuOpen.value = false;
}

/** Under "…": at its left edge where the window has room, else at its right. */
function placeMenu() {
  const button = trigger.value?.getBoundingClientRect();
  const panel = menu.value;
  if (!button || !panel) return;
  const width = panel.offsetWidth;
  const room = document.documentElement.clientWidth - WINDOW_GUTTER;
  const left =
    button.left + width <= room
      ? button.left
      : Math.max(WINDOW_GUTTER, button.right - width);
  menuAt.value = { top: button.bottom + MENU_GAP, left };
}

function toggleMenu() {
  menuOpen.value = !menuOpen.value;
}

const menuStyle = computed(() =>
  menuAt.value
    ? { top: `${menuAt.value.top}px`, left: `${menuAt.value.left}px` }
    : { visibility: "hidden" as const },
);

const { handleMenuKeydown } = useMenuKeyboard({
  isOpen: () => menuOpen.value,
  close: closeMenu,
  trigger,
  menu,
});

/** A line that asks before it acts: the window takes the focus from here. */
function pick(action: "clear-history" | "leave") {
  closeMenu();
  trigger.value?.focus();
  if (action === "clear-history") emit("clear-history");
  else emit("leave");
}

/** The keys at "…"; the menu answers its own (it is drawn elsewhere). */
function onAnchorKeydown(event: KeyboardEvent) {
  if (menu.value?.contains(event.target as Node)) return;
  handleMenuKeydown(event);
}

/**
 * The focus gone from "…" and the menu to elsewhere on the page. Nowhere in
 * particular (a press on what takes no focus) is the click's to answer.
 */
function onFocusOut(event: FocusEvent) {
  const next = event.relatedTarget as Node | null;
  if (!next) return;
  if (anchor.value?.contains(next) || menu.value?.contains(next)) return;
  closeMenu();
}

function onDocumentClick(event: MouseEvent) {
  const target = event.target as Node;
  if (anchor.value?.contains(target) || menu.value?.contains(target)) return;
  closeMenu();
}

// While the menu is open. The scroll is caught on its way down: the feed and
// the page scroll in containers of their own.
function listen(on: boolean) {
  if (on) {
    document.addEventListener("click", onDocumentClick);
    window.addEventListener("scroll", closeMenu, true);
    window.addEventListener("resize", closeMenu);
  } else {
    document.removeEventListener("click", onDocumentClick);
    window.removeEventListener("scroll", closeMenu, true);
    window.removeEventListener("resize", closeMenu);
  }
}

watch(menuOpen, async (open) => {
  listen(open);
  if (!open) {
    menuAt.value = null;
    return;
  }
  await nextTick();
  placeMenu();
});

onUnmounted(() => listen(false));
</script>

<template>
  <ChatStrip>
    <span class="conversation-line">
      <template v-if="chat"
        ><span class="line-who"
          ><router-link v-if="profile" :to="profile" class="line-name">{{
            name
          }}</router-link
          ><span v-else class="line-name">{{ name }}</span
          ><template v-if="profile"
            >{{ " ["
            }}<span :class="online ? 'online' : 'offline'">{{
              online ? "online" : "offline"
            }}</span
            >{{ "]" }}</template
          ><span v-else-if="isGroup" class="line-meta">{{
            `, участников: ${participants}`
          }}</span></span
        ><template v-if="canSearch"
          ><span class="line-sep" aria-hidden="true">{{ " | " }}</span
          ><button
            type="button"
            class="strip-item"
            :class="{ act: searchOpen }"
            :aria-expanded="searchOpen"
            aria-controls="conversation-search"
            @click="emit('toggle-search')"
          >
            {{ "поиск" }}
          </button></template
        ><template v-if="hasMenu"
          ><span class="line-sep" aria-hidden="true">{{ " | " }}</span
          ><span
            ref="anchor"
            class="menu-anchor"
            @keydown="onAnchorKeydown"
            @focusout="onFocusOut"
            ><button
              ref="trigger"
              type="button"
              class="strip-item"
              :class="{ act: menuOpen }"
              aria-haspopup="menu"
              :aria-expanded="menuOpen"
              aria-label="Действия с перепиской"
              @click="toggleMenu"
            >
              {{ "…" }}
            </button>
            <Teleport to="body">
              <div
                v-if="menuOpen"
                ref="menu"
                class="conversation-menu"
                role="menu"
                :style="menuStyle"
                @keydown="handleMenuKeydown"
                @focusout="onFocusOut"
              >
                <router-link
                  v-if="profile"
                  :to="profile"
                  class="menu-item"
                  role="menuitem"
                  @click="closeMenu"
                  >Перейти в профиль</router-link
                >
                <button
                  v-if="canClear"
                  type="button"
                  class="menu-item"
                  role="menuitem"
                  @click="pick('clear-history')"
                >
                  Очистить историю
                </button>
                <button
                  v-if="canLeave"
                  type="button"
                  class="menu-item"
                  role="menuitem"
                  @click="pick('leave')"
                >
                  Покинуть группу
                </button>
              </div>
            </Teleport></span
          ></template
        ></template
      >
      <span v-else class="line-placeholder" aria-hidden="true" />
    </span>
  </ChatStrip>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/Menu" as *
@use "@/assets/styles/Skeleton" as *
@use "@/assets/styles/ZIndex" as *

// Who the correspondence is with: bold in the text's colour, as the title of
// an event; the profile a link of it.
.line-name
  font-weight: 700
  color: $text

a.line-name
  +link-tone($text)

.line-meta,
.line-sep
  color: $text-muted

// "[online]" green and "[offline]" grey, the brackets in the line's grey.
.online
  color: $accent-green

.offline
  color: $text-muted

// "поиск" and "…": the grey link-buttons of the events line, $link and bold
// while what they opened is up.
.strip-item
  +muted-link-button
  &
    font-size: $secondary-font-size
    white-space: nowrap
  &.act
    color: $link
    font-weight: 700
  &:focus:not(:focus-visible)
    outline: none

.menu-anchor
  display: inline-block

// Over the page, where placeMenu puts it.
.conversation-menu
  position: fixed
  z-index: $z-dropdown
  // As wide as its longest line: the anchor it hangs from is one sign wide.
  width: max-content
  +menu-panel

.menu-item
  +menu-item
  &
    text-decoration: none

// While the correspondence is on its way: a bar where the name will be.
.line-placeholder
  display: inline-block
  width: 180px
  max-width: 100%
  height: 1em
  vertical-align: middle
  +skeleton-shimmer
</style>
