<script setup lang="ts">
import { computed, ref } from "vue";
import { useAuthStore } from "@/entities/user";
import { useSessionExit } from "@/features/auth";
import { useMessagingStore } from "@/entities/message";
import { useNotificationStore } from "@/entities/notification";
import { useUiStore } from "@/shared/stores/ui";
import { useMenuKeyboard } from "@/shared/lib/composables/useMenuKeyboard";
import { useShellMode } from "@/shared/lib/composables/useShellMode";
import { storeToRefs } from "pinia";
import { Tooltip } from "@/shared/ui/Tooltip";
import { SvgIcon } from "@/shared/ui/Icon";
import GuestActions from "./GuestActions.vue";
import MainMenu from "./MainMenu.vue";
import SiteStatistics from "./SiteStatistics.vue";

const uiStore = useUiStore();
const userStore = useAuthStore();
const { user } = storeToRefs(userStore);

const messagingStore = useMessagingStore();
const { totalUnreadCount } = storeToRefs(messagingStore);

const notificationStore = useNotificationStore();
const { unreadCount: unreadNotificationsCount } =
  storeToRefs(notificationStore);

const hasUnread = computed(() => totalUnreadCount.value > 0);
const hasUnreadNotifications = computed(
  () => unreadNotificationsCount.value > 0,
);

// Where the header stands (useShellMode). Up to the shell breakpoint the menu
// and the statistics are not here: they lead the two views of the sliding
// column (App.vue), and its one button stands at the left edge. The page has
// each of the two once, at every width.
const { narrow } = useShellMode();
const menuOpen = computed(() => uiStore.openedPanel !== null);

// "Выйти" opens a small menu offering both a local sign-out and a
// sign-out-everywhere action (doc 4.2.1.1).
const logoutMenuOpen = ref(false);
const logoutTrigger = ref<HTMLElement | null>(null);
const logoutMenu = ref<HTMLElement | null>(null);
function toggleLogoutMenu() {
  logoutMenuOpen.value = !logoutMenuOpen.value;
}
function closeLogoutMenu() {
  logoutMenuOpen.value = false;
}

// The dropdown is announced as a menu, so it answers Escape and the arrow keys
// like one. It answered neither before: opening it leaves focus on the trigger,
// so pressing the trigger a second time was the only key that closed it, a
// click on the backdrop the only other way out, and nothing walked the items.
const { handleMenuKeydown } = useMenuKeyboard({
  isOpen: () => logoutMenuOpen.value,
  close: closeLogoutMenu,
  trigger: logoutTrigger,
  menu: logoutMenu,
});

// Signing out is not a navigation, so the route guard never runs: on /account
// the viewer stayed on a page whose whole body is `v-if="user"` — a heading
// over nothing. Leaving the page is part of the action.
const { signOut, signOutAll } = useSessionExit();

async function handleSignOut() {
  closeLogoutMenu();
  await signOut();
}
async function handleSignOutAll() {
  closeLogoutMenu();
  await signOutAll();
}
</script>

<template>
  <header class="header" :class="{ 'header--narrow': narrow }">
    <!-- The button of the sliding column, at the edge it comes in from; the
         column opens on its left view and closes from its own bar
         (MobileDrawer). -->
    <div v-if="narrow" class="menu-button">
      <Tooltip text="Меню" placement="bottom">
        <button
          type="button"
          class="menu-toggle"
          aria-label="Меню"
          aria-haspopup="dialog"
          :aria-expanded="menuOpen"
          data-testid="menu-button"
          @click="uiStore.togglePanel('left')"
        >
          <SvgIcon name="menu" class="menu-sign" />
        </button>
      </Tooltip>
    </div>

    <div class="user-info">
      <router-link
        class="logo"
        :to="{ name: 'home' }"
        aria-label="Dungeon Master, на главную"
      />
      <div class="user-actions">
        <!-- One box for the whole greeting: the line is centred as a unit,
             and the words of a flex row would each be an item of their own
             and lose the spaces between them. -->
        <span
          v-if="user && user.username"
          class="greeting"
          data-testid="greeting"
        >
          Здравствуй,
          <router-link
            :to="{ name: 'profile', params: { username: user.username } }"
            class="username"
            :class="{ 'has-unread': hasUnread }"
            data-testid="user-menu"
            >{{ user.username }}</router-link
          >&nbsp;<span class="muted" aria-hidden="true">(</span
          ><Tooltip :text="`Непрочитанных сообщений: ${totalUnreadCount}`"
            ><router-link
              :to="{ name: 'messenger' }"
              class="count count--messages"
              :aria-label="`Непрочитанных сообщений: ${totalUnreadCount}`"
              data-testid="unread-messages"
              >{{ totalUnreadCount }}</router-link
            ></Tooltip
          ><span class="muted" aria-hidden="true">/</span
          ><Tooltip
            :text="`Непрочитанных уведомлений: ${unreadNotificationsCount}`"
            ><router-link
              :to="{ name: 'notifications' }"
              class="count count--notifications"
              :class="{ 'has-unread': hasUnreadNotifications }"
              :aria-label="`Непрочитанных уведомлений: ${unreadNotificationsCount}`"
              data-testid="notifications"
              >{{ unreadNotificationsCount }}</router-link
            ></Tooltip
          ><span class="muted" aria-hidden="true">)</span>
          <!-- No "Настройки" here: the account settings are reached from the
               reader's own profile page, the one place they belong. -->
          |
          <span class="logout-menu" @keydown="handleMenuKeydown">
            <button
              ref="logoutTrigger"
              type="button"
              class="action-link"
              :aria-expanded="logoutMenuOpen"
              aria-haspopup="true"
              @click="toggleLogoutMenu"
              data-testid="logout-button"
            >
              Выйти
            </button>
            <template v-if="logoutMenuOpen">
              <!-- Invisible backdrop closes the menu on any outside click -->
              <button
                type="button"
                class="logout-backdrop"
                aria-hidden="true"
                tabindex="-1"
                @click="closeLogoutMenu"
              />
              <ul ref="logoutMenu" class="logout-dropdown" role="menu">
                <li role="none">
                  <button
                    type="button"
                    class="logout-item"
                    role="menuitem"
                    @click="handleSignOut"
                    data-testid="logout-current"
                  >
                    Выйти
                  </button>
                </li>
                <li role="none">
                  <button
                    type="button"
                    class="logout-item"
                    role="menuitem"
                    @click="handleSignOutAll"
                    data-testid="logout-all"
                  >
                    Выйти со всех устройств
                  </button>
                </li>
              </ul>
            </template>
          </span>
        </span>
        <GuestActions v-else />
      </div>
    </div>

    <MainMenu v-if="!narrow" class="top-menu" />

    <div v-if="!narrow" class="stats-col">
      <SiteStatistics />
    </div>
  </header>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *
@use "@/assets/styles/Inputs" as *

// Bottom padding that aligns the three header columns on a shared baseline:
// the torn edge of the paper strip takes the bottom of the header.
$baseline-pad: 9px


// The logo's box, and the width of the drawing in it. The drawing is
// contained in the box, so it takes the full height and a width by its own
// proportions (the viewBox of logo.svg, 4079.6 by 827.08), which is a few
// pixels short of the box and starts at its left edge.
$logo-width: 275px
$logo-offset: -5px
$logo-drawn-width: calc($header-row-height * 4079.6 / 827.08)

// The button of the sliding column, 44 square, and how far it stands from its
// edge: the corner the column's own close button takes (MobileDrawer). Its
// sign is 22 across, the width the sign of three bars always had.
$menu-button: 44px
$menu-button-inset: $small
$menu-sign: 22px
// The side of a narrow header the button takes, its inset and one gutter
// after it. The other side keeps as much free, so the logo between them
// stands in the middle of the screen.
$menu-side: $menu-button-inset + $menu-button + $small

// What decides where the greeting stands, kept on the header so one rule
// serves every width: where the logo starts in its column, and how wide its
// drawing is.
.header
  --logo-start: #{$big + $logo-offset}
  --logo-drawn: #{$logo-drawn-width}

.header
  position: relative
  display: grid
  grid-template-columns: $sidebar-width minmax(0, 1fr) $sidebar-width
  grid-template-areas: "user menu stats"
  box-sizing: border-box
  // One row high, and higher only when the greeting wraps.
  min-height: $header-height

  // The paper strip under the whole header, however high a greeting that
  // wraps makes it. The picture is drawn at the height of a header of one
  // row, as on 1366, twice: at the bottom with its torn edge, and at the top
  // for the rest of a taller header, where only its flat paper shows (the
  // picture is opaque, and the bottom one covers the edge of the top one in a
  // header up to 175 high). Stretched to the header as one layer, the edge
  // came out larger than on 1366; stretched only at the top, the paper there
  // came out in a coarser grain, and the line where the two met showed.
  &::before
    content: ''
    position: absolute
    inset: 0
    background-image: url('@/assets/images/decorations/header-decoration.png'), url('@/assets/images/decorations/header-decoration.png')
    background-position: left bottom, left top
    background-size: auto $header-height
    background-repeat: repeat-x
    filter: $filter-invert

// Over the paper strip: positioned, and after it in the order of the page.
.user-info,
.top-menu,
.stats-col
  position: relative

.user-info
  grid-area: user
  min-width: 0
  padding-bottom: $baseline-pad
  box-sizing: border-box
  white-space: nowrap
  cursor: default
  display: flex
  flex-direction: column
  justify-content: center

.logo
  display: block
  flex: none
  margin-bottom: $tiny
  margin-left: var(--logo-start)
  height: $header-row-height
  width: $logo-width
  background: transparent url('@/assets/images/logos/logo.svg') no-repeat
  background-size: contain

// The line under the logo, the guest's and the member's alike, centred on the
// drawing of the logo rather than on the column (owner's call, 28 September
// 2026), and never closer to the edge of the screen than $medium.
//
// The box is the room the line has, from $medium off the left edge of the
// screen to the end of the column (the menu keeps its own space from there).
// The line is centred in the box together with an empty spacer after
// it, the spacer as wide as the middle of the box is right of the logo's: the
// line then stands centred on the logo. A line too long for that presses the
// spacer to nothing and stands from the left end of the box instead, so it
// moves right rather than out of the screen.
.user-actions
  display: flex
  justify-content: center
  margin-left: $medium
  min-width: 0
  font-size: $secondary-font-size
  color: $text

  > *
    flex: none

  &::after
    content: ''
    flex: 0 1 max(0px, calc(100% + 2 * #{$medium} - 2 * var(--logo-start) - var(--logo-drawn)))

  .muted
    color: $text-muted

  .has-unread
    font-weight: bold
    color: $accent-green
    &:hover
      color: $accent-green-hover

// The greeting is the whole name, never cut (owner, 12 October 2026: nothing
// shortened). While it fits the box it keeps the width of its words, one line,
// where it always stood. A longer one, a login of 20 wide letters, is held to
// the box and wraps there between its words, and the header grows by the
// line; a name wider than the box breaks at its edge (the word-wrap of the
// page). The name and its counts are held together by the space between them.
// The guest's line and the logo keep the one line of .user-info.
.greeting
  white-space: normal
  max-width: 100%
  text-align: center

// The two counts are a few pixels of figure each. A finger finds them by a
// field 32 high and at least 24 wide around each, out of the flow so the line
// does not move: the first reaches left over the bracket, the second right
// over the bracket and the bar, and the two meet in the middle of the slash
// between them. Pressing the field is pressing the link.
$count-target-height: 32px
$count-target-width: 24px
$count-target-meet: 2px

.count
  position: relative

  &::after
    content: ''
    position: absolute
    top: 50%
    height: $count-target-height
    width: calc(100% + #{$count-target-meet})
    min-width: $count-target-width
    transform: translateY(-50%)

.count--messages::after
  right: -$count-target-meet

.count--notifications::after
  left: -$count-target-meet

// Keyboard-accessible control that looks exactly like a link
.action-link
  vertical-align: baseline
  +inline-link-button

// "Выйти" dropdown: the trigger stays inline; the menu is absolutely
// positioned below it. A full-viewport transparent backdrop catches any
// outside click to dismiss it.
.logout-menu
  position: relative
  display: inline-block

.logout-backdrop
  position: fixed
  inset: 0
  // The scale, not a pair of numbers below it: at 40/41 the menu went under
  // every dropdown on the page (100) the moment the two came near each other.
  z-index: $z-dropdown
  border: none
  background: transparent
  padding: 0
  cursor: default

.logout-dropdown
  position: absolute
  top: 100%
  right: 0
  z-index: $z-sticky
  margin-top: $tiny
  min-width: 200px
  list-style: none
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element
  box-shadow: 0 4px 12px $shadow-color
  overflow: hidden

.logout-item
  display: block
  width: 100%
  padding: $small $medium
  border: none
  background: none
  font: inherit
  font-size: $secondary-font-size
  text-align: left
  white-space: nowrap
  color: $text
  cursor: pointer

  &:hover
    background-color: $bg-element-accent
    color: $link-hover

// The menu stands from the left of the room between the logo and the
// statistics, on the line the text of the page starts on, as the menu of DM2
// starts right after its logo. In the smallest step of its type it gives half
// of its two sides to the links (--menu-side), so that two lines still hold
// it. Down, its box spans the height of the header and keeps the same
// baseline padding as the two columns beside it, which centre their contents
// in that box too: one line or two, the menu stands in the middle of the
// header. The look of the links is the menu's own (MainMenu.vue); the header
// steps their size.
.top-menu
  grid-area: menu
  min-width: 0
  align-content: center
  padding: 0 var(--menu-side) $baseline-pad
  box-sizing: border-box

.stats-col
  grid-area: stats
  min-width: 0
  display: flex
  align-items: center
  justify-content: flex-start
  padding-bottom: $baseline-pad
  padding-right: $big
  box-sizing: border-box

// Font steps of the top menu, kept as custom properties of the header and
// read by the links (MainMenu.vue). The side columns are fixed at
// $sidebar-width each (they align with the page sidebars), so only the menu
// can give up space. It takes two lines where one does not hold it, both of
// one size, and a step down comes on only where two lines of the step above
// no longer hold it, scrollbar drawn: two lines of 25 are 65 of the 91 the
// header has above its baseline. Where not even the smallest step holds two
// lines, nine links in the last 47px over the shell breakpoint, it takes a
// third, still inside the header, rather than a size smaller still. Nine
// links (moderators and admins see "Модерация") take every step sooner than
// eight.
// widgets/header/menuSteps.spec.ts measures the links against these widths.
.header
  --menu-font: #{$menu-font-size}
  --menu-gap: #{$big}
  --menu-side: #{$big}

=menu-steps($header, $to-20, $to-17, $to-15)
  @media (max-width: $to-20)
    #{$header}
      --menu-font: 20px
      --menu-gap: #{$medium}
  @media (max-width: $to-17)
    #{$header}
      --menu-font: 17px
      --menu-gap: 10px
  @media (max-width: $to-15)
    #{$header}
      --menu-font: 15px
      --menu-gap: #{$small}
      --menu-side: #{$medium}

+menu-steps(".header", 1259px, 1133px, 1067px)
+menu-steps(".header:has(.top-menu > .link:nth-child(9))", 1357px, 1202px, 1124px)

// Up to the shell breakpoint the header holds the button of the sliding column
// at its left edge, and the logo with the line under it in the middle of the
// screen; the menu and the statistics are in the sliding column. A grid of
// three columns, the button's side at either edge, so the middle one is the
// middle of the screen. The button stands in the middle of the block of the
// logo and the line under it, down as well: its box spans the header less the
// baseline padding, the box that block is centred in.
.header--narrow
  grid-template-columns: $menu-side minmax(0, 1fr) $menu-side
  grid-template-areas: "button user ."

  .user-info
    align-items: center

  // The drawing of the logo, so its middle is the middle of the screen.
  .logo
    width: $logo-drawn-width
    margin-left: 0

  // The line is centred by itself under a logo already in the middle, so it
  // needs no spacer to stand on the drawing by.
  .user-actions
    align-self: stretch
    margin: 0 $medium

  .user-actions::after
    display: none

// The button: in the ink of the text as the logo beside it is, the ink of a
// link under the pointer.
.menu-button
  grid-area: button
  position: relative
  display: flex
  align-items: center
  padding: 0 0 $baseline-pad $menu-button-inset
  box-sizing: border-box

.menu-toggle
  display: flex
  align-items: center
  justify-content: center
  width: $menu-button
  height: $menu-button
  padding: 0
  border: none
  background: none
  color: $text
  cursor: pointer

  &:hover
    color: $link-hover

// The sign of three bars from the site settings' sign of the compact layout
// (icons.ts), 22 across at the proportions of its 16 by 12.
.menu-toggle .menu-sign
  width: $menu-sign
  height: $menu-sign * 0.75
</style>
