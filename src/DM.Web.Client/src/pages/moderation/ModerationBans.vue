<script setup lang="ts">
/**
 * ModerationBans, "Последние баны" (doc 4.2.1.5 / 4.2.2.22), the ban list
 * written as DM2's bot wrote its ban log: every issue and every lift of a ban
 * an entry of its own, newest first, a page at a time (GET v1/bans/history),
 * so a reader of that log reads this one the same way. An entry is the card
 * of the moderation entity (BanLogCard), and it says everything there is to
 * say about the ban: who, to whom, what scope, until when, why. There is no
 * state of the ban apart from it, no "Активен" and no "Истек": DM2 had none,
 * the end is in the sentence and a lift stands as an entry of its own.
 *
 * The order is the server's: a lift made today stands on top, not at the date
 * of the ban it ended.
 *
 * Lifting a ban early is offered on the entry of the issue where the server
 * says this reader may lift it (`canLift`), not by a role read here: a senior
 * moderator may not lift his own ban nor a permanent one, and the page used to
 * offer him both. The lift is asked in a window that takes the reason
 * (LiftBanDialog), and the page read again shows the lift on top.
 *
 * One filter narrows the list, the name of the banned user, in the search line
 * of "Последние предупреждения" next door: the site's search field and
 * "Найти" beside it, the exact name, in the address. So the bans of one person
 * are a link, the one the profile's "Баны: N" is.
 *
 * The number of an entry is a link to it, as the number of a comment is. The
 * list is newest first, so every new entry moves the older ones down a page in
 * time, and a link by the page number would lose its entry: the link names the
 * entry by its number instead ("entry" in the address, beside the name of the
 * list it was taken from), the server answers with the page of this list that
 * holds it, and the address then says that page as the paging does, so the
 * paging goes on from it. The page comes to the entry once it is drawn.
 */
import { computed, nextTick, reactive, ref, watch } from "vue";
import { useRoute, useRouter, type LocationQuery } from "vue-router";
import { useModal } from "vue-final-modal";
import {
  moderationApi,
  BanLogCard,
  RecordSkeleton,
  type Ban,
  type BanLogEntry,
} from "@/entities/moderation";
import { LiftBanDialog } from "@/features/moderation-actions";
import type { PagingInfo as PagingModel } from "@/shared/api/models/common";
import { Paging } from "@/shared/ui/Paging";
import { ErrorState } from "@/shared/ui/ErrorState";
import { FilterSearchInput } from "@/shared/ui/Filters";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { parsePageNumber } from "@/shared/lib/filters";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { markArrival } from "@/shared/lib/composables/useScrollToElement";
import { useRoleGate } from "./lib/useRoleGate";

const PAGE_SIZE = 20;

const route = useRoute();
const router = useRouter();
const { hasAccess, deniedText } = useRoleGate("Moderator");

const entries = ref<BanLogEntry[]>([]);
const paging = ref<PagingModel | null>(null);

// A page number and a name both come from the address and both change under
// the reader, so two answers can be on the wire at once; the guard keeps the
// one the reader asked for last.
const {
  loading,
  error: loadError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить баны" });

const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

/** The entry a link to it names, whose page is asked for. */
const entryNumber = computed(() => parsePageNumber(route.query.entry));

/** The one filter the list takes; an unknown name is an empty page, not an error. */
const username = computed(() => {
  const value = route.query.username;
  const name = Array.isArray(value) ? value[0] : value;
  return name ? String(name) : undefined;
});

// The field shows what the address says, so a link handed over opens with the
// name already in it and the back button puts the previous name back.
const usernameInput = ref(username.value ?? "");
watch(username, (name) => (usernameInput.value = name ?? ""));

/**
 * The page and the name of the list on screen, once it is drawn; none while a
 * read is on the way or after one failed, so the address read again is read.
 */
let shown: string | null = null;
const viewOf = (page: number, name: string | undefined) =>
  `${page}|${name ?? ""}`;

function fetch() {
  const entry = entryNumber.value;
  shown = null;
  return run(
    () =>
      moderationApi.getBanHistory({
        username: username.value,
        entry,
        skip: (pageNumber.value - 1) * PAGE_SIZE,
        take: PAGE_SIZE,
      }),
    (data) => {
      entries.value = data?.resources ?? [];
      paging.value = data?.paging ?? null;
      const page = data?.paging
        ? Math.floor(data.paging.skip / PAGE_SIZE) + 1
        : pageNumber.value;
      shown = viewOf(page, username.value);
      if (entry) showPageInAddress(page);
    },
  );
}

/**
 * The change of address that names the page an entry was found on, while it is
 * on its way: the entry comes into view once it is done (see below).
 */
let addressSettled: Promise<unknown> = Promise.resolve();

/**
 * The page an entry was found on, in the address the paging reads: the page
 * number in place of the entry, the anchor kept. The list on screen is that
 * page already, so the change of address reads nothing again.
 */
function showPageInAddress(page: number) {
  const query = { ...route.query };
  delete query.entry;
  if (page > 1) query.number = String(page);
  else delete query.number;
  addressSettled = router.replace({
    name: "moderation-bans",
    query,
    hash: route.hash,
  });
}

watch(
  [pageNumber, username, entryNumber],
  () => {
    if (
      !entryNumber.value &&
      viewOf(pageNumber.value, username.value) === shown
    )
      return;
    void fetch();
  },
  { immediate: true },
);

/** The anchor of an entry, by its number: the same on every page. */
const ANCHOR_PREFIX = "ban-";
const anchorOf = (entry: BanLogEntry) => `${ANCHOR_PREFIX}${entry.number}`;

/**
 * Where the number of an entry leads: the entry, in the list of the name this
 * one is narrowed to, whatever page it then stands on.
 */
function numberTo(entry: BanLogEntry) {
  const query: LocationQuery = {
    ...route.query,
    entry: String(entry.number),
  };
  delete query.number;
  return { name: "moderation-bans", query, hash: `#${anchorOf(entry)}` };
}

/** The pause a comment its number leads to waits for its page to settle. */
const SETTLE_MS = 100;

// The entry the address names, brought into the middle of the window and
// marked once its page is drawn, as a comment its number leads to is
// (useCommentHashScroll). The router brings an anchor to the top of the window
// a frame after every change of address (router.afterEach), the change that
// names the page of the entry among them, so the entry waits for that change
// and then for the pause a comment waits: brought into view earlier, it would
// be pulled back to the top edge. A reader who went elsewhere in the meantime
// is not taken back.
watch(
  () => [entries.value, route.hash] as const,
  async () => {
    const anchor = route.hash?.slice(1);
    if (!anchor?.startsWith(ANCHOR_PREFIX) || !entries.value.length) return;
    await addressSettled;
    await nextTick();
    await new Promise((resolve) => setTimeout(resolve, SETTLE_MS));
    if (route.hash?.slice(1) !== anchor) return;
    const element = document.getElementById(anchor);
    if (!element) return;
    element.scrollIntoView({ behavior: "smooth", block: "center" });
    markArrival(element);
  },
  { flush: "post" },
);

/**
 * The name goes to the address without the page number, which counted pages of
 * the list being left; an emptied field drops the filter. The field takes the
 * trimmed name at once: the same name again, padded, is the address already
 * open, and a navigation there hands nothing back.
 */
function applyFilter() {
  const query = { ...route.query };
  delete query.number;
  const name = usernameInput.value.trim();
  if (name) query.username = name;
  else delete query.username;
  usernameInput.value = name;
  router.push({ name: "moderation-bans", query });
}

/** Emptied by its cross or by hand, the field drops the filter at once. */
function onInput(value: string) {
  usernameInput.value = value;
  if (!value.trim() && username.value) applyFilter();
}

// --- Lift a ban (the server says on which entries) ---
const liftTarget = ref<Ban | null>(null);

const { open: openLiftDialog, close: closeLiftDialog } = useModal({
  component: LiftBanDialog,
  attrs: reactive({
    banId: computed(() => liftTarget.value?.id ?? ""),
    username: computed(() => liftTarget.value?.user?.username ?? ""),
    onSuccess: () => {
      closeLiftDialog();
      liftTarget.value = null;
      // The page the moderator is on: the lift comes back as an entry on top.
      void fetch();
    },
    onCancel: () => {
      closeLiftDialog();
      liftTarget.value = null;
    },
  }),
});

function liftBan(ban: Ban) {
  liftTarget.value = ban;
  openLiftDialog();
}

const isEmpty = computed(() => !loading.value && entries.value.length === 0);

/** A page past the end of a list that is not empty: a stale link, or a filter. */
const isPastEnd = computed(
  () => isEmpty.value && (paging.value?.total ?? 0) > 0,
);

/** The address of the first page: this one without the page number. */
const firstPageQuery = computed(() => {
  const query = { ...route.query };
  delete query.number;
  return query;
});
</script>

<template>
  <div class="moderation-bans">
    <page-title>Последние баны</page-title>

    <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

    <template v-else>
      <form class="search-line" @submit.prevent="applyFilter">
        <FilterSearchInput
          :model-value="usernameInput"
          placeholder="Точное имя пользователя"
          @update:model-value="onInput"
        />
        <Button type="submit" variant="primary">Найти</Button>
      </form>

      <ErrorState v-if="loadError" :message="loadError" :retry="fetch" />

      <!-- Skeleton: an entry is drawn on the record every moderation log
           uses, so it waits on the record's twin. -->
      <RecordSkeleton v-else-if="loading && !entries.length" :count="5" />

      <div v-else-if="isPastEnd" class="bans-empty">
        <SecondaryText>
          На этой странице банов нет.
          <router-link :to="{ name: 'moderation-bans', query: firstPageQuery }"
            >Вернуться на первую страницу</router-link
          >
        </SecondaryText>
      </div>

      <SecondaryText v-else-if="isEmpty">{{
        username ? "Банов по заданным фильтрам не найдено" : "Банов пока нет"
      }}</SecondaryText>

      <template v-else>
        <div class="ban-list">
          <BanLogCard
            v-for="entry in entries"
            :id="anchorOf(entry)"
            :key="`${entry.kind}-${entry.ban.id}`"
            :entry="entry"
            :number-to="numberTo(entry)"
            @lift="liftBan(entry.ban)"
          />
        </div>

        <Paging
          v-if="paging"
          :paging="paging"
          :to="{ name: 'moderation-bans' }"
          use-query
        />
      </template>
    </template>
  </div>
</template>

<style scoped lang="sass">
// The search line of the moderation lists: the field takes the width the
// button leaves, both $control-height, one line at every width.
.search-line
  display: flex
  align-items: center
  gap: $small
  margin-bottom: $medium

// The step between two entries of the warning log (WarningsPage.vue).
.ban-list
  display: flex
  flex-direction: column
  gap: $medium
  margin-bottom: $medium
</style>
