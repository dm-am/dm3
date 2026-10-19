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
import { usePaging } from "@/shared/lib/composables/usePaging";
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
// A page of the list is as long as the reader's lists (account settings).
const { pageSize: take } = usePaging();

const LOAD_FAILED = "Не удалось загрузить уведомления";

/** Where Telegram and Discord are switched on: the account page's section. */
const CHANNELS: RouteLocationRaw = { name: "account", hash: "#notifications" };

/**
 * Why the page asked for did not arrive: in place of the list for the first
 * page, under the rows for a later one. The api method has no seam for
 * `ownsRefusal`, so the statuses the interceptor announces are left to it.
 * Cleared by the next read that lands rows on the screen.
 */
const loadRefusal = ref("");

/** A refused read makes no claim that the list is empty. */
const loadFailed = ref(false);

/** The list has answered: before it, the table's skeleton, and no word of an empty list. */
const loaded = ref(false);

/** Read on this page since it was opened, by id: the page does not rewrite what the server said. */
const readHere = reactive(new Set<string>());

const isUnread = (notification: UserNotification): boolean =>
  !notification.isRead && !readHere.has(notification.id);

const titleClass = (notification: UserNotification) =>
  isUnread(notification)
    ? "notification-title--unread"
    : "notification-title--read";

/** Rows of a fetched page this page does not show yet. */
const unseen = (page: UserNotification[]): UserNotification[] => {
  const held = new Set(notifications.value.map((n) => n.id));
  return page.filter((n) => !held.has(n.id));
};

/** One detail as the letter writes it: the label, a no-break space, the value. */
const detailText = (detail: NotificationDetail): string =>
  `${detail.label}:\u00A0${detail.isMoment ? formatDateFull(detail.value) : detail.value}`;

/**
 * One row per notification, as a letter is one row of DM2's mail list. Only
 * the date has a width: the place and the people are as wide as their words.
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

/** A count that grew during a request: its re-read is run once the request settles, not dropped. */
let pendingRefresh = false;

/**
 * Read the newest page and merge it by id, rows on screen left in place so
 * the scroll stays. A push is only a hint: the store owns the count, the truth
 * is read over REST. `skip` counts the server's rows, so prepending leaves it.
 */
const readNewestPage = async () => {
  const { data, error } = await notificationApi.getNotifications(0, take.value);
  // Silent on failure: nobody asked for this request, and the next arrival tries again.
  if (error) return;
  // What this read puts on the screen is the server's answer.
  loadFailed.value = false;
  loadRefusal.value = "";
  const page = data?.resources || [];
  const fresh = unseen(page);
  // A whole page of strangers means more arrived than a page holds: the list
  // becomes this page rather than keep a hole between the two windows.
  if (fresh.length === take.value) {
    notifications.value = page;
    skip.value = page.length;
    hasMore.value = page.length === take.value;
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
    // An arrival during the request raises the flag again, and the loop reads once more.
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
    loaded.value = false;
  }

  loading.value = true;
  loadRefusal.value = "";
  loadFailed.value = false;
  try {
    const { data, error } = await notificationApi.getNotifications(
      skip.value,
      take.value,
    );
    if (error) {
      // A failed page is not the end of the list.
      loadFailed.value = true;
      loadRefusal.value = refusalHere(error, LOAD_FAILED) ?? "";
      return;
    }
    const newItems = data?.resources || [];
    // skip and hasMore count the raw answer: they address a window on the server.
    notifications.value = [...notifications.value, ...unseen(newItems)];
    hasMore.value = newItems.length === take.value;
    skip.value += newItems.length;
  } finally {
    loading.value = false;
    loaded.value = true;
    if (pendingRefresh) void refreshFirstPage();
  }
};

// Growth only: marking read on this page is what makes the count fall.
watch(unreadCount, (current, previous) => {
  if (current > previous) void refreshFirstPage();
});

/** Marks everything read; the rows stay, losing their bold. Returns the refusal or null. */
const markAllAsRead = async (): Promise<GeneralError | null> => {
  const { error } = await notificationApi.markAsRead();
  if (error) return error;
  for (const notification of notifications.value) {
    readHere.add(notification.id);
  }
  // The store owns the bell's count, so it reads it back at once.
  await notificationStore.fetchUnreadCount(true);
  return null;
};

/**
 * Opening a notification is reading it: the row is read on the page at once,
 * and the bell is read again only after the server answered the mark, or the
 * count could overtake it.
 */
const markRead = async (notification: UserNotification) => {
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
    <!-- The bell's count, which the store owns: one page of the list is not the number. -->
    <UnreadLine
      :count="unreadCount"
      :mark="markAllAsRead"
      failure="Не удалось отметить уведомления"
      done="Все уведомления прочитаны"
    />

    <template v-if="loaded && notifications.length === 0 && !loadFailed">
      <secondary-text>Нет уведомлений</secondary-text>
    </template>

    <!-- Newest first; an unread title bold, a read one grey, as in DM2's mail list,
         and a screen reader is told in words inside the title. -->
    <!-- Until the list answers, the table stands as its skeleton. -->
    <DataTable
      v-else-if="!loaded || notifications.length > 0"
      :columns="COLUMNS"
      :data="rows"
      :loading="!loaded"
      aria-label="Уведомления"
      class="notification-table"
    >
      <template #cell-event="{ row }">
        <router-link
          v-if="row.notification.url"
          :to="row.notification.url"
          class="notification-title"
          :class="titleClass(row.notification)"
          @click="markRead(row.notification)"
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
        <!-- Every other detail, a part of the caption line, whole. -->
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

      <!-- The place, and under it the larger place it stands in. -->
      <template #cell-place="{ row }">
        <template v-if="row.parts.places.length > 0">
          <router-link
            v-if="row.notification.url"
            :to="row.notification.url"
            class="notification-place"
            @click="markRead(row.notification)"
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

    <!-- Why the page did not arrive: where the list would stand, or under the rows. -->
    <ActionResult :text="loadRefusal" />

    <div v-if="hasMore && notifications.length > 0" class="load-more">
      <Button
        type="button"
        class="load-more-btn"
        :loading="loading"
        @click="fetchNotifications()"
      >
        Показать еще
      </Button>
    </div>
  </div>
</template>

<style scoped lang="sass">
.channels-hint
  margin: 0 0 $medium

// A title that leads nowhere is text.
.notification-title--plain
  color: $text

.notification-title--unread
  font-weight: bold

// Read: grey, as a read letter of DM2's mail list.
.notification-title--read
  color: $text-muted

a.notification-title--read:hover
  color: $link-hover

.notification-details
  display: block
  margin-top: $tiny

.load-more
  margin-top: $medium
</style>
