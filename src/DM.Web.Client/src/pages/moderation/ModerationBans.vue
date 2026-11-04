<script setup lang="ts">
/**
 * "Последние баны", as DM2's bot wrote its ban log: every issue and every lift
 * an entry of its own, newest first, in the server's order. The lift is
 * offered where the server says this reader may (`canLift`), not by a role
 * read here. The number of an entry links to it by "entry" in the address,
 * because a page number would lose it as newer entries push it down; the
 * server answers with the page that holds it.
 */
import { computed, nextTick, reactive, ref, watch } from "vue";
import { useRoute, useRouter, type LocationQuery } from "vue-router";
import { useModal } from "vue-final-modal";
import {
  banApi,
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
import {
  parsePageNumber,
  parseStringFromUrl,
  toQueryValue,
} from "@/shared/lib/filters";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { markArrival } from "@/shared/lib/composables/useScrollToElement";

const route = useRoute();
const router = useRouter();
// A page as long as the reader's lists (account settings).
const { pageSize } = usePaging();

const entries = ref<BanLogEntry[]>([]);
const paging = ref<PagingModel | null>(null);

// The number and the name both come from the address; the guard keeps the answer asked for last.
const {
  loading,
  error: loadError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить баны" });

const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

/** The entry a link to it names, whose page is asked for. */
const entryNumber = computed(() => parsePageNumber(route.query.entry));

/** The one filter the list takes; an unknown name is an empty page, not an error. */
const username = computed(
  () => parseStringFromUrl(toQueryValue(route.query.username)) || undefined,
);

// The field shows what the address says, so the back button puts the previous name back.
const usernameInput = ref(username.value ?? "");
watch(username, (name) => (usernameInput.value = name ?? ""));

/** The list on screen, once drawn; none while a read is on the way or after one failed. */
let shown: string | null = null;
const viewOf = (page: number, name: string | undefined) =>
  `${page}|${name ?? ""}`;

function loadBans() {
  const entry = entryNumber.value;
  // The page of the answer is counted in the size it was asked for.
  const size = pageSize.value;
  shown = null;
  return run(
    () =>
      banApi.getBanHistory({
        username: username.value,
        entry,
        skip: (pageNumber.value - 1) * size,
        take: size,
      }),
    (data) => {
      entries.value = data?.resources ?? [];
      paging.value = data?.paging ?? null;
      const page = data?.paging
        ? Math.floor(data.paging.skip / size) + 1
        : pageNumber.value;
      shown = viewOf(page, username.value);
      if (entry) showPageInAddress(page);
    },
  );
}

/** The change of address that names the entry's page, while on its way. */
let addressSettled: Promise<unknown> = Promise.resolve();

/** The entry's page in the address, the anchor kept; the list on screen is that page already. */
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
    void loadBans();
  },
  { immediate: true },
);

/** The anchor of an entry, by its number: the same on every page. */
const ANCHOR_PREFIX = "ban-";
const anchorOf = (entry: BanLogEntry) => `${ANCHOR_PREFIX}${entry.number}`;

/** The entry, in the list of the name this one is narrowed to, on whatever page. */
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

// The entry is brought to the middle and marked once its page is drawn, as a
// comment is (useCommentHashScroll). It waits for the change of address and the
// pause: the router pulls an anchor to the top a frame after every change. The
// sources are watched each by itself: a tuple built anew on every change of
// address fired again when the page itself put its number in the address.
watch(
  [entries, () => route.hash],
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
 * The name goes to the address without the page number; an emptied field drops
 * the filter. The field takes the trimmed name: the same name padded is the
 * address already open, and a navigation there hands nothing back.
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

const liftTarget = ref<Ban | null>(null);

const { open: openLiftDialog, close: closeLiftDialog } = useModal({
  component: LiftBanDialog,
  attrs: reactive({
    banId: computed(() => liftTarget.value?.id ?? ""),
    username: computed(() => liftTarget.value?.user?.username ?? ""),
    onSuccess: () => {
      closeLiftDialog();
      liftTarget.value = null;
      void loadBans();
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

    <form class="search-line" @submit.prevent="applyFilter">
      <FilterSearchInput
        :model-value="usernameInput"
        placeholder="Точное имя пользователя"
        @update:model-value="onInput"
      />
      <Button type="submit" variant="primary">Найти</Button>
    </form>

    <ErrorState v-if="loadError" :message="loadError" :retry="loadBans" />

    <!-- The twin of the record every moderation log uses. -->
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
  </div>
</template>

<style scoped lang="sass">
// The field takes the width the button leaves, one line at every width.
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
