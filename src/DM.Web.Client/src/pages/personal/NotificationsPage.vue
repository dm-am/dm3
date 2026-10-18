<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import type { RouteLocationRaw } from "vue-router";
import {
  notificationApi,
  notificationParts,
  useNotificationStore,
  type NotificationParts,
} from "@/entities/notification";
import type {
  NotificationDetail,
  UserNotification,
} from "@/shared/api/models/notifications";
import type { GeneralError } from "@/shared/api/models/common";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { refusalHere } from "@/shared/lib/errors";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { ActionResult } from "@/shared/ui/ActionResult";
import Button from "@/shared/ui/Button/Button.vue";
import { CaptionLine } from "@/shared/ui/CaptionLine";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { UnreadLine } from "@/shared/ui/UnreadLine";

const notificationStore = useNotificationStore();
const { unreadCount } = storeToRefs(notificationStore);
const notifications = ref<UserNotification[]>([]);
const loading = ref(false);
const hasMore = ref(true);
const skip = ref(0);
const take = 20;

const LOAD_FAILED = "Не удалось загрузить уведомления";

/**
 * Where Telegram and Discord are switched on: the section of the account page
 * that holds them. That section is the last of a long page, and a reader for
 * whom the bell is not enough looks for the bots here first.
 */
const CHANNELS: RouteLocationRaw = { name: "account", hash: "#notifications" };

/**
 * Why the page the reader asked for did not arrive: in place of the list for
 * the first page, under the rows already on screen for a later one.
 *
 * A failed load is the state of the page and not a plate over it, and a plate
 * in the corner over an empty list explains nothing (UI_STANDARDS,
 * "Всплывающие сообщения"). The api method has no seam for `ownsRefusal`, so
 * the page keeps quiet on the statuses the interceptor announces and prints
 * the rest in the server's words. Cleared by the next read that lands rows
 * on the screen, the reader's own or the unattended one.
 */
const loadRefusal = ref("");

/**
 * Whether the rows on screen are the server's answer. "Нет уведомлений" is a
 * claim about the list, and a refused read makes none: without this a dead
 * server read as an empty list.
 */
const loadFailed = ref(false);

/**
 * Notifications read on this page since it was opened, by id: opened from
 * their title or marked with the rest. A row says whether it was read when the
 * server answered with it, and the page does not rewrite what the server said;
 * it remembers what it has done since.
 */
const readHere = reactive(new Set<string>());

/** Whether a row is still unread for the reader looking at it. */
const isUnread = (notification: UserNotification): boolean =>
  !notification.isRead && !readHere.has(notification.id);

/** The look of a title: bold while unread, grey once read. */
const titleClass = (notification: UserNotification) =>
  isUnread(notification)
    ? "notification-title--unread"
    : "notification-title--read";

/** Rows of a fetched page this page does not show yet. */
const unseen = (page: UserNotification[]): UserNotification[] => {
  const held = new Set(notifications.value.map((n) => n.id));
  return page.filter((n) => !held.has(n.id));
};

/**
 * One detail as the letter about the same event writes it: the label, a
 * no-break space, the value. The space keeps the label from ending a line
 * alone; a moment is written in the reader's clock, like every date of the
 * site.
 */
const detailText = (detail: NotificationDetail): string =>
  `${detail.label}:\u00A0${detail.isMoment ? formatDateFull(detail.value) : detail.value}`;

/**
 * The columns of the list: what happened, where, who, when. A notification
 * is one row, as a letter is one row of the mail list of DM2 and a topic one
 * row of a board: the eye runs down the column it needs, and the title of
 * every row starts at the same edge. A column with a width of its own takes
 * it before the others, so only the date, whose words are the same in every
 * row, has one: the place and the people are as wide as their words, and what
 * happened takes the rest.
 */
const COLUMNS: Column[] = [
  { key: "event", label: "Событие", align: "left" },
  { key: "place", label: "Где", align: "left" },
  { key: "people", label: "Кто", align: "left" },
  { key: "when", label: "Когда", width: "86px", align: "center" },
];

/** A row of the table: the notification, and its details sorted by column. */
interface Row {
  id: string;
  notification: UserNotification;
  parts: NotificationParts;
}

const rows = computed<Row[]>(() =>
  notifications.value.map((notification) => ({
    id: notification.id,
    notification,
    parts: notificationParts(notification),
  })),
);

/** The profile a name in the column "Кто" leads to. */
const profileOf = (username: string): RouteLocationRaw => ({
  name: "profile",
  params: { username },
});

/** Guards the unattended re-read; `loading` belongs to the reader's own requests. */
let refreshing = false;

/**
 * A count that grew while a request was already in flight.
 *
 * The re-read such an arrival asks for is held back instead of being dropped:
 * whichever request stood in the way runs it once it settles. Without this, an
 * arrival during the reader's own page, or during a re-read already going,
 * left the screen a row short until the next arrival happened to come.
 */
let pendingRefresh = false;

/**
 * Read the newest page and merge it into the list.
 *
 * A push is only a hint and this page never sees one: the socket feeds the
 * notification store, the store owns the unread count, and the truth is read
 * back over REST (API_DESIGN.md, "Realtime"). Deduplication is by id and the
 * rows already drawn are left in place, so the reader keeps their scroll
 * position: a reset would empty the list and throw them back to the top.
 *
 * `skip` counts the rows the server has answered with, not the rows on screen,
 * so prepending leaves it alone. An arrival does shift the window, but the
 * shifted row comes back inside the next page and deduplication drops it
 * there. Advancing `skip` as well would count one shift twice whenever a page
 * and an arrival interleave, and the row at the offset stepped over would
 * never be asked for again.
 */
const readNewestPage = async () => {
  const { data, error } = await notificationApi.getNotifications(0, take);
  // Silent on failure: nobody asked for this request, and the next arrival or
  // a page reload tries again. A toast here would report a fault the reader
  // did nothing to cause.
  if (error) return;
  // Whatever this read puts on the screen is the server's answer, and a line
  // under it about a page that did not arrive would be about another list.
  loadFailed.value = false;
  loadRefusal.value = "";
  const page = data?.resources || [];
  const fresh = unseen(page);
  // A whole page of strangers means more arrived than one page holds, and
  // splicing it on top would leave a hole between the two windows that no
  // offset reaches any more. So the list becomes this page, which is the whole
  // re-read the contract asks for: the newest window is already in hand, and
  // asking the server for it a second time would only add a request whose
  // refusal would leave the reader an empty list.
  if (fresh.length === take) {
    notifications.value = page;
    skip.value = page.length;
    hasMore.value = page.length === take;
    return;
  }
  if (fresh.length > 0) {
    notifications.value = [...fresh, ...notifications.value];
  }
};

const refreshFirstPage = async () => {
  if (refreshing || loading.value) {
    pendingRefresh = true;
    return;
  }
  refreshing = true;
  try {
    // An arrival during the request below raises the flag again, and the loop
    // reads once more rather than losing it.
    do {
      pendingRefresh = false;
      await readNewestPage();
    } while (pendingRefresh);
  } finally {
    refreshing = false;
  }
};

const fetchNotifications = async (reset = false) => {
  if (loading.value) return;

  if (reset) {
    skip.value = 0;
    notifications.value = [];
    hasMore.value = true;
  }

  loading.value = true;
  loadRefusal.value = "";
  loadFailed.value = false;
  try {
    const { data, error } = await notificationApi.getNotifications(
      skip.value,
      take,
    );
    if (error) {
      // hasMore is left alone: a failed page is not the end of the list, and
      // clearing it would turn a transient error into "больше ничего нет".
      loadFailed.value = true;
      loadRefusal.value = refusalHere(error, LOAD_FAILED) ?? "";
      return;
    }
    const newItems = data?.resources || [];
    // Only rows not on screen yet are drawn, while skip and hasMore count the
    // raw answer: they address a window on the server, not what is on screen.
    notifications.value = [...notifications.value, ...unseen(newItems)];
    hasMore.value = newItems.length === take;
    skip.value += newItems.length;
  } finally {
    loading.value = false;
    if (pendingRefresh) void refreshFirstPage();
  }
};

// Growth only. The count falls as well, since marking read is what this very
// page does, and a fall says nothing arrived.
watch(unreadCount, (current, previous) => {
  if (current > previous) void refreshFirstPage();
});

/**
 * "Отметить прочитанными" of the line under the heading: everything marked
 * read, and the refusal handed back to the line to say, or null once done.
 *
 * The rows stay where they are and lose their bold: the list is the history
 * of what happened, read or not, and "Показать еще" still leads to the older
 * part of it.
 */
const markAllAsRead = async (): Promise<GeneralError | null> => {
  const { error } = await notificationApi.markAsRead();
  if (error) return error;
  for (const notification of notifications.value) {
    readHere.add(notification.id);
  }
  // The bell badge counts the very notifications this page just marked. The
  // store owns that number, so it is told to read it back instead of being
  // guessed at from here; immediate, because the reader is watching the badge
  // they just acted on. The line under the heading goes with that number.
  await notificationStore.fetchUnreadCount(true);
  return null;
};

/**
 * The title or the place pressed: the reader is on the way to what the
 * notification is about, and opening it is reading it. The way there does not wait for the
 * request, and the row is read on the page at once.
 *
 * The bell is re-read only once the server has answered the mark: read beside
 * it, the count can overtake the mark and show the old number, and read
 * without `immediate` it waits for the debounce of pushes. A refused mark is
 * not said anywhere, since the reader has left for the page the title led to;
 * the count read after it shows the truth, and the row is bold again on the
 * next visit.
 */
const open = async (notification: UserNotification) => {
  if (!isUnread(notification)) return;
  readHere.add(notification.id);
  try {
    await notificationApi.markAsRead(notification.id);
  } finally {
    await notificationStore.fetchUnreadCount(true);
  }
};

onMounted(() => fetchNotifications());
</script>

<template>
  <div class="notifications-page">
    <page-title>Уведомления</page-title>
    <secondary-text class="channels-hint"
      >Уведомления в Telegram и Discord подключаются в
      <router-link :to="CHANNELS"
        >настройках аккаунта</router-link
      ></secondary-text
    >
    <!-- The count is the bell's, which the store owns: the list holds read and
         unread alike, and one page of it is not the number. -->
    <UnreadLine
      :count="unreadCount"
      :mark="markAllAsRead"
      failure="Не удалось отметить уведомления"
      done="Все уведомления прочитаны"
    />

    <secondary-text v-if="loading && notifications.length === 0"
      >Загрузка...</secondary-text
    >

    <template v-else-if="notifications.length === 0 && !loadFailed">
      <secondary-text>Нет уведомлений</secondary-text>
    </template>

    <!-- Newest first, read and unread together. An unread title is bold in
         the colour of a link and a read one grey, as the mail list of DM2
         tells its letters apart; a screen reader, which hears neither, is
         told in words inside the title. The title and the place lead to what
         the notification is about, the names to the profiles. -->
    <DataTable
      v-else-if="notifications.length > 0"
      :columns="COLUMNS"
      :data="rows"
      aria-label="Уведомления"
      class="notification-table"
    >
      <template #cell-event="{ row }">
        <router-link
          v-if="row.notification.url"
          :to="row.notification.url"
          class="notification-title"
          :class="titleClass(row.notification)"
          @click="open(row.notification)"
          >{{ row.notification.title
          }}<span v-if="isUnread(row.notification)" class="visually-hidden"
            >, не прочитано</span
          ></router-link
        ><span
          v-else
          class="notification-title notification-title--plain"
          :class="titleClass(row.notification)"
          >{{ row.notification.title
          }}<span v-if="isUnread(row.notification)" class="visually-hidden"
            >, не прочитано</span
          ></span
        >
        <!-- What else the server wrote about it, under the title: every
             detail a part of the caption line, whole. -->
        <CaptionLine
          v-if="row.parts.rest.length > 0"
          size="secondary"
          class="notification-details"
        >
          <span
            v-for="(detail, index) in row.parts.rest"
            :key="index"
            class="notification-detail"
            >{{ detailText(detail) }}</span
          >
        </CaptionLine>
      </template>

      <!-- The place the notification leads to, and under it the larger
           place it stands in ("Комната: ...", "Блог: ..."). -->
      <template #cell-place="{ row }">
        <template v-if="row.parts.places.length > 0">
          <router-link
            v-if="row.notification.url"
            :to="row.notification.url"
            class="notification-place"
            @click="open(row.notification)"
            >{{ row.parts.places[0].value }}</router-link
          ><span v-else class="notification-place">{{
            row.parts.places[0].value
          }}</span>
          <CaptionLine
            v-if="row.parts.places.length > 1"
            size="secondary"
            class="notification-details"
          >
            <span
              v-for="(detail, index) in row.parts.places.slice(1)"
              :key="index"
              class="notification-detail"
              >{{ detailText(detail) }}</span
            >
          </CaptionLine>
        </template>
      </template>

      <template #cell-people="{ row }">
        <template v-for="(person, index) in row.parts.people" :key="index"
          ><template v-if="index > 0">, </template
          ><router-link
            :to="profileOf(person.value)"
            class="notification-person"
            >{{ person.value }}</router-link
          ></template
        >
      </template>

      <template #cell-when="{ row }">
        <span class="notification-date">{{
          formatDateFull(row.notification.createdUtc)
        }}</span>
      </template>
    </DataTable>

    <!-- Why the page asked for did not arrive: where the list would stand, or
         under the rows already drawn. A plate in the corner used to say this. -->
    <ActionResult :text="loadRefusal" />

    <div v-if="hasMore && notifications.length > 0" class="load-more">
      <Button
        type="button"
        class="load-more-btn"
        :loading="loading"
        @click="fetchNotifications()"
      >
        <template v-if="loading">Загрузка...</template>
        <template v-else>Показать еще</template>
      </Button>
    </div>
  </div>
</template>

<style scoped lang="sass">
// One step down from the line about the channels to what follows it.
.channels-hint
  margin: 0 0 $medium

// The title of what happened. A link keeps the colour and the hover of every
// link of the site; a title that leads nowhere is text.
.notification-title--plain
  color: $text

.notification-title--unread
  font-weight: bold

// Read: grey, as a read letter of the mail list of DM2 is. Pointed at, it
// answers as every link does.
.notification-title--read
  color: $text-muted

a.notification-title--read:hover
  color: $link-hover

// The details under a title or a place, a hair under it.
.notification-details
  display: block
  margin-top: $tiny

// Left, as every control of the column: the button stands under the list.
.load-more
  margin-top: $medium
</style>
