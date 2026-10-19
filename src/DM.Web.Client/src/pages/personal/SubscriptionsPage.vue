<script setup lang="ts">
/**
 * Everything the reader follows, in the site's sections, as the site draws its
 * lists. "Отписаться" does not take the row away: the word turns into its
 * opposite and the count goes down, so a wrong press is put right in place
 * with the old settings; the row goes on the next visit. So the page keeps
 * its own list of what it opened on and asks the store whether each is still
 * followed. Game and blog names are coloured by status, by the rule of
 * GameLink and BlogLink.
 */
import { computed, onMounted, reactive, ref, watch } from "vue";
import type { RouteLocationRaw } from "vue-router";
import { useSubscriptionsStore } from "@/entities/subscription";
import { useGameDisplay } from "@/entities/game";
import { useBlogDisplay } from "@/entities/blog";
import { RemovedAccountLink } from "@/entities/user";
import {
  describeUserSubscription,
  UserSubscriptionPanel,
} from "@/features/user-subscribe";
import {
  SubscriptionTargetType,
  type Subscription,
} from "@/shared/api/models/subscriptions";
import { ModuleStatus } from "@/shared/api/models/common";
import { refusalHere, takeOverRefusal } from "@/shared/lib/errors";
import { ActionResult } from "@/shared/ui/ActionResult";
import {
  CaptionAction,
  CaptionLine,
  CaptionPart,
} from "@/shared/ui/CaptionLine";
import { ErrorState } from "@/shared/ui/ErrorState";
import { BlockTitle, PageTitle, SecondaryText } from "@/shared/ui/Layout";
import SubscriptionListSkeleton from "./SubscriptionListSkeleton.vue";

/** A section of the page and the words its rows say. */
interface Section {
  type: SubscriptionTargetType;
  title: string;
  /** The name of a target that no longer exists; none for a person, named by RemovedAccountLink. */
  gone: string | null;
  unsubscribe: string;
  subscribe: string;
  /** The refusal of either press, when the server named nothing. */
  unsubscribeFailed: string;
  subscribeFailed: string;
}

// A blog is followed by reading it, and its panel says so.
const SECTIONS: readonly Section[] = [
  {
    type: SubscriptionTargetType.Game,
    title: "Игры",
    gone: "Игра удалена",
    unsubscribe: "Отписаться",
    subscribe: "Подписаться",
    unsubscribeFailed: "Не удалось отписаться от игры",
    subscribeFailed: "Не удалось подписаться на игру",
  },
  {
    type: SubscriptionTargetType.Blog,
    title: "Блоги",
    gone: "Блог удален",
    unsubscribe: "Убрать из читаемого",
    subscribe: "Добавить в читаемое",
    unsubscribeFailed: "Не удалось убрать блог из читаемого",
    subscribeFailed: "Не удалось добавить блог в читаемое",
  },
  {
    type: SubscriptionTargetType.Topic,
    title: "Топики",
    gone: "Топик удален",
    unsubscribe: "Отписаться",
    subscribe: "Подписаться",
    unsubscribeFailed: "Не удалось отписаться от топика",
    subscribeFailed: "Не удалось подписаться на топик",
  },
  {
    type: SubscriptionTargetType.User,
    title: "Пользователи",
    gone: null,
    unsubscribe: "Отписаться",
    subscribe: "Подписаться",
    unsubscribeFailed: "Не удалось отписаться от пользователя",
    subscribeFailed: "Не удалось подписаться на пользователя",
  },
];

/** A target the page opened on, kept for the visit whatever becomes of it. */
interface Row {
  key: string;
  type: SubscriptionTargetType;
  targetId: string;
  /** Absent when the target is gone. */
  title?: string;
  username?: string;
  /** A game's or a blog's status and first activation, for its colour. */
  status?: ModuleStatus;
  activatedUtc?: string;
  /** The settings last seen on its subscription, which "Подписаться" restores. */
  settings: number;
}

const store = useSubscriptionsStore();

const keyOf = (type: SubscriptionTargetType, targetId: string) =>
  `${type}:${targetId}`;

/** Every target seen on this visit, in the order it was first seen. */
const rows = ref<Row[]>([]);

// A subscription that goes leaves its row; one that comes back refreshes the settings the row restores.
watch(
  () => store.subscriptions,
  (list) => {
    for (const s of list ?? []) {
      const key = keyOf(s.targetType, s.targetId);
      const row = rows.value.find((r) => r.key === key);
      if (row) {
        row.settings = s.settings;
        if (s.targetTitle) row.title = s.targetTitle;
        if (s.targetUsername) row.username = s.targetUsername;
        if (s.targetStatus) row.status = s.targetStatus;
        if (s.targetActivatedUtc) row.activatedUtc = s.targetActivatedUtc;
      } else {
        rows.value.push({
          key,
          type: s.targetType,
          targetId: s.targetId,
          title: s.targetTitle,
          username: s.targetUsername,
          settings: s.settings,
          status: s.targetStatus,
          activatedUtc: s.targetActivatedUtc,
        });
      }
    }
  },
  { immediate: true },
);

/** The subscription a row stands for, while it lasts. */
const live = (row: Row): Subscription | undefined =>
  store.getSubscription(row.type, row.targetId);

const byName = (a: Row, b: Row) => {
  if (!a.title || !b.title) return a.title ? -1 : b.title ? 1 : 0;
  return a.title.localeCompare(b.title, "ru", { sensitivity: "base" });
};

/** A row as it is drawn now. */
interface Line {
  row: Row;
  /** Its subscription, absent once the reader has left it on this visit. */
  subscription?: Subscription;
  /** Where its name leads; none for a target that is gone. */
  to: RouteLocationRaw | null;
  /** The colour of a game's or a blog's name: its status, else the link's. */
  tone: "new" | "closed" | null;
  /** What a person's subscription sends ("игры, блоги"), "" for the rest. */
  sends: string;
  /** A person's row opens the panel instead of unsubscribing outright. */
  opensPanel: boolean;
}

const { isNew: isNewGame } = useGameDisplay();
const { isNew: isNewBlog } = useBlogDisplay();

/** Grey once closed, which outranks green while it opened less than a week ago. */
function toneOf(row: Row): Line["tone"] {
  const isNew =
    row.type === SubscriptionTargetType.Game
      ? isNewGame
      : row.type === SubscriptionTargetType.Blog
        ? isNewBlog
        : null;
  if (!isNew || !row.status) return null;
  if (row.status === ModuleStatus.Closed) return "closed";
  return isNew({ activatedUtc: row.activatedUtc }) ? "new" : null;
}

function lineOf(row: Row): Line {
  const subscription = live(row);
  const person = row.type === SubscriptionTargetType.User && !!row.title;
  return {
    row,
    subscription,
    to: targetLocation(row),
    tone: toneOf(row),
    sends:
      person && subscription
        ? describeUserSubscription(subscription.settings)
        : "",
    opensPanel: person && !!subscription,
  };
}

const sections = computed(() =>
  SECTIONS.map((section) => {
    const lines = rows.value
      .filter((r) => r.type === section.type)
      .sort(byName)
      .map(lineOf);
    return {
      ...section,
      lines,
      count: lines.filter((line) => line.subscription).length,
    };
  }).filter((section) => section.lines.length > 0),
);

/**
 * Where a row's name leads, by named routes. A target that is gone has no
 * link, since each would open an error page; a removed person is named by
 * RemovedAccountLink in the template.
 */
function targetLocation(row: Row): RouteLocationRaw | null {
  if (!row.title) return null;
  switch (row.type) {
    case SubscriptionTargetType.Game:
      return { name: "game", params: { id: row.targetId } };
    case SubscriptionTargetType.Blog:
      return { name: "blog", params: { id: row.targetId } };
    case SubscriptionTargetType.Topic:
      return {
        name: "forum-topic-redirect",
        params: { topicId: row.targetId },
      };
    case SubscriptionTargetType.User:
      return row.username
        ? { name: "profile", params: { username: row.username } }
        : null;
    default:
      return null;
  }
}

/** Rows with a press out: their action is shut until the answer. */
const pending = reactive(new Set<string>());

/** The last refused press, under the row it was made in. */
const refusal = ref<{ key: string; text: string } | null>(null);

async function press(
  row: Row,
  call: () => ReturnType<typeof store.subscribe>,
  failure: string,
) {
  if (pending.has(row.key)) return;
  pending.add(row.key);
  refusal.value = null;
  const error = await call();
  pending.delete(row.key);
  if (!error) return;
  const text = refusalHere(error, failure);
  if (text) refusal.value = { key: row.key, text };
}

function unsubscribe(row: Row, section: Section) {
  const subscription = live(row);
  if (!subscription) return;
  return press(
    row,
    () => store.unsubscribe(subscription.id),
    section.unsubscribeFailed,
  );
}

function resubscribe(row: Row, section: Section) {
  return press(
    row,
    () => store.subscribe(row.type, row.targetId, row.settings),
    section.subscribeFailed,
  );
}

/** The row the panel is open under, and the rows it may hang from. */
const panelFor = ref<string | null>(null);
const rowElements = new Map<string, HTMLElement>();
const panelAnchor = ref<HTMLElement | null>(null);

function rememberRow(key: string, el: unknown) {
  if (el instanceof HTMLElement) rowElements.set(key, el);
  else rowElements.delete(key);
}

function togglePanel(row: Row) {
  if (panelFor.value === row.key) {
    panelFor.value = null;
    return;
  }
  panelAnchor.value = rowElements.get(row.key) ?? null;
  refusal.value = null;
  panelFor.value = row.key;
}

function closePanel(returnFocus: boolean) {
  const key = panelFor.value;
  panelFor.value = null;
  if (returnFocus && key) {
    rowElements
      .get(key)
      ?.querySelector<HTMLElement>("button.caption-action")
      ?.focus();
  }
}

const loadFailed = computed(
  () => !!store.subscriptionsError && !store.subscriptions,
);

// The page says a failed load in place, so the interceptor's plate comes down;
// sync, since the plate is held only until the end of the answer's task.
watch(
  () => store.subscriptionsError,
  (error) => {
    if (error && !store.subscriptions) takeOverRefusal(error);
  },
  { flush: "sync" },
);

onMounted(() => store.fetchSubscriptions());
</script>

<template>
  <div class="subscriptions-page">
    <PageTitle>Подписки</PageTitle>

    <ErrorState
      v-if="loadFailed"
      message="Не удалось загрузить подписки"
      :retry="() => store.fetchSubscriptions(true)"
    />

    <!-- Before the list answers: its skeleton, and no word of an empty list. -->
    <SubscriptionListSkeleton v-else-if="!store.subscriptions" />

    <SecondaryText v-else-if="!rows.length" class="subscriptions-intro"
      >Подписок нет. Подписаться на игру или блог можно на их странице, на
      пользователя в его профиле.</SecondaryText
    >

    <template v-else>
      <SecondaryText class="subscriptions-intro"
        >Уведомления приходят о новом в этих играх, блогах и у этих
        пользователей. Подписаться на игру или блог можно на их странице, на
        пользователя в его профиле.</SecondaryText
      >

      <section
        v-for="section in sections"
        :key="section.type"
        class="subscriptions-section"
      >
        <BlockTitle>{{ section.title }} ({{ section.count }})</BlockTitle>
        <ul class="subscriptions-list">
          <li
            v-for="line in section.lines"
            :key="line.row.key"
            :ref="(el) => rememberRow(line.row.key, el)"
            class="subscription-row"
          >
            <!-- The dash hangs: what wraps goes on under the name. -->
            <div class="subscription-head">
              <span class="subscription-dash" aria-hidden="true">- </span
              ><CaptionLine size="secondary" class="subscription-line">
                <!-- The name wraps at its spaces as words of the line; the rest moves whole. -->
                <CaptionPart flow>
                  <router-link
                    v-if="line.to"
                    :to="line.to"
                    class="subscription-title"
                    :class="line.tone && `subscription-title--${line.tone}`"
                    >{{ line.row.title }}</router-link
                  >
                  <span
                    v-else-if="section.gone"
                    class="subscription-title subscription-title--gone"
                    >{{ section.gone }}</span
                  >
                  <RemovedAccountLink v-else class="subscription-title" />
                </CaptionPart>
                <span v-if="line.sends">{{ line.sends }}</span>
                <CaptionAction
                  v-if="line.opensPanel"
                  aria-haspopup="dialog"
                  :aria-expanded="panelFor === line.row.key"
                  @click="togglePanel(line.row)"
                  >Настроить</CaptionAction
                >
                <CaptionAction
                  v-else-if="line.subscription"
                  :disabled="pending.has(line.row.key)"
                  @click="unsubscribe(line.row, section)"
                  >{{ section.unsubscribe }}</CaptionAction
                >
                <CaptionAction
                  v-else-if="line.row.title"
                  :disabled="pending.has(line.row.key)"
                  @click="resubscribe(line.row, section)"
                  >{{ section.subscribe }}</CaptionAction
                >
              </CaptionLine>
            </div>
            <ActionResult
              v-if="refusal?.key === line.row.key"
              :text="refusal.text"
              class="subscription-refusal"
            />
            <UserSubscriptionPanel
              v-if="panelFor === line.row.key"
              :user-id="line.row.targetId"
              :anchor="panelAnchor"
              @close="closePanel"
            />
          </li>
        </ul>
      </section>
    </template>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *
@use "@/shared/ui/CaptionLine/dashList" as *


.subscriptions-intro
  margin-bottom: $small

.subscriptions-list
  +dash-list

// The name in the size of the text, the rest grey and a step smaller, as a comment's caption line.
.subscription-row
  font-size: $font-size

.subscription-dash
  color: $text-muted

.subscription-head
  +dash-hang

.subscription-line
  // Each part is a box of its own and would take the indent back on its first line.
  text-indent: 0

  // The line breaks at its gaps and inside the name (CaptionPart flow).
  :deep(.subscription-title)
    font-size: $font-size

  :deep(.subscription-title--gone)
    color: $text-muted

  :deep(.subscription-title--new)
    +link-tone($accent-green, $accent-green-hover)

  :deep(.subscription-title--closed)
    +muted-link

// A line of its own under the row it answers.
.subscription-refusal
  --action-result-margin: 0
</style>
