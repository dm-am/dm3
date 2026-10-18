<script setup lang="ts">
/**
 * "Подписки": everything the reader follows, in the sections the site names
 * things by, as lists the way the site draws its lists ("- " and the name, the
 * action a grey word after " | ", as in the left column and in the cabinet of
 * DM2). A filter over one mixed column used to stand here, with a badge of the
 * type on every row and a button on every row: the sections show it all at
 * once and count it, so neither the filter nor the badges are needed.
 *
 * "Отписаться" does not take the row away. The word turns into its opposite and
 * the count in the heading goes down, so a press on the wrong row is put right
 * in the same place, with the settings the subscription had (the same way "В
 * черный список" turns into its way back on a profile). A row the reader left
 * goes on the next visit. So the page keeps its own list of what it opened on,
 * and asks the store only whether each of those is still followed.
 *
 * A person's row says what the subscription sends and opens the panel the
 * profile opens under "Подписан" (UserSubscriptionPanel), so the kinds are
 * changed here as well as there.
 *
 * Game and blog names are coloured by status, as everywhere on the site
 * (UI_STANDARDS, "Цвет ссылок игр и блогов по статусу"): green while new,
 * grey once closed. The row of a subscription carries the status of its game
 * or blog and the day it opened (targetStatus, targetActivatedUtc), and the
 * rule of "new" is the one of GameLink and BlogLink (useGameDisplay and
 * useBlogDisplay).
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

/** A section of the page and the words its rows say. */
interface Section {
  type: SubscriptionTargetType;
  title: string;
  /**
   * The name of a target that no longer exists. None for a person: a removed
   * account is named the way every surface names it, by RemovedAccountLink, a
   * link of the ordinary colour to the profile that gathers what removed
   * accounts left (owner, 12 October 2026: not grey, like any other name).
   */
  gone: string | null;
  unsubscribe: string;
  subscribe: string;
  /** The refusal of either press, when the server named nothing. */
  unsubscribeFailed: string;
  subscribeFailed: string;
}

// A blog is followed by reading it, and its panel calls the subscription so:
// "Добавить в читаемое" / "Убрать из читаемого".
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

// The list the page opened on, and anything subscribed since. A subscription
// that goes leaves its row where it was; one that comes back refreshes the
// settings the row will restore the next time.
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
  // A target that is gone has no name to sort by and goes to the end.
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

/**
 * The colour a game or a blog is named in: grey once closed, which outranks
 * green, and green while it opened less than a week ago. Topics and people
 * keep the colour of a link.
 */
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
 * Where a row's name leads. Named routes: a game lives at /game/:id, a topic
 * addressed by id goes through the resolver route, and a profile is addressed
 * by name, which is why the row carries targetUsername. A target that is gone
 * has no link: every one of these would open an error page. A removed person
 * is the exception, named by RemovedAccountLink in the template (Section.gone).
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

async function unsubscribe(row: Row, section: Section) {
  const subscription = live(row);
  if (!subscription || pending.has(row.key)) return;
  pending.add(row.key);
  refusal.value = null;
  const error = await store.unsubscribe(subscription.id);
  pending.delete(row.key);
  if (!error) return;
  const text = refusalHere(error, section.unsubscribeFailed);
  if (text) refusal.value = { key: row.key, text };
}

async function resubscribe(row: Row, section: Section) {
  if (pending.has(row.key)) return;
  pending.add(row.key);
  refusal.value = null;
  const error = await store.subscribe(row.type, row.targetId, row.settings);
  pending.delete(row.key);
  if (!error) return;
  const text = refusalHere(error, section.subscribeFailed);
  if (text) refusal.value = { key: row.key, text };
}

// --- The panel of a person's subscription ---

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

// --- Loading ---

const loadFailed = computed(
  () => !!store.subscriptionsError && !store.subscriptions,
);

// The page says a failed load where the list would stand, so the plate the
// interceptor raised for it comes down. Sync: the plate is held only until
// the end of the task the answer arrived in.
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

    <SecondaryText v-if="store.subscriptionsLoading && !rows.length"
      >Загрузка...</SecondaryText
    >

    <ErrorState
      v-else-if="loadFailed"
      message="Не удалось загрузить подписки"
      :retry="() => store.fetchSubscriptions(true)"
    />

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
            <!-- The dash and the line, the dash hanging: what wraps goes on
                 under the name and not under the dash. -->
            <div class="subscription-head">
              <span class="subscription-dash" aria-hidden="true">- </span
              ><CaptionLine size="secondary" class="subscription-line">
                <!-- The name runs on from the dash as words of the line and
                     wraps at its spaces; the rest of the line moves whole. -->
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
                <!-- A target that is gone is left, not followed again. -->
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
@use "@/assets/styles/Inputs" as *

// The page stands where every page of the column stands: no padding of its
// own, which pushed the heading 16 to the right of its neighbours.

.subscriptions-intro
  margin-bottom: $small

.subscriptions-list
  list-style: none
  padding: 0
  margin: 0

// A line of the list: the name in the size of the text, the rest of the line
// grey and a step smaller, as the caption line of a comment reads.
.subscription-row
  font-size: $font-size
  line-height: 1.5

.subscription-dash
  color: $text-muted

// "- " hangs: a line that wraps goes on under the name, not under the dash.
// The block is indented by the dash and its first line takes the indent back.
// 0.548em is the advance of "- " in PT Sans (8.77 at 16), measured on the
// page as CaptionLine measures its bar.
$dash: 0.548em

.subscription-head
  padding-left: $dash
  text-indent: -$dash

.subscription-line
  // The parts of the line are boxes of their own, and each would take the
  // indent back on its own first line.
  text-indent: 0

  // The name in the size of the text. The line breaks at its gaps and inside
  // the name, which flows as words of the line (CaptionPart flow).
  :deep(.subscription-title)
    font-size: $font-size

  :deep(.subscription-title--gone)
    color: $text-muted

  // A game or a blog in the colour of its status, as the site names them.
  :deep(.subscription-title--new)
    +link-tone($accent-green, $accent-green-hover)

  :deep(.subscription-title--closed)
    +muted-link

// A line of its own under the row it answers.
.subscription-refusal
  --action-result-margin: 0
</style>
