<script setup lang="ts">
/**
 * ModerationWarnings, "Последние предупреждения" (doc 4.2.1.5 / 4.2.2.21).
 * Warnings across the website, newest first, a page at a time
 * (GET v1/moderation/warnings). The card itself lives in the moderation
 * entity: the open log draws the same block, and one warning must not read
 * two ways depending on which screen it is on.
 *
 * What this page adds to the shared card is what only a moderator gets: the
 * address of every text, where the open log keeps it from a guest who could
 * not follow it, and the control that changes the points. There is no control
 * that removes a warning, because there is no way to remove one: the record of
 * a decision stays, and a correction moves its points and signs itself. Who
 * may correct which row is the server's answer — `canChangePoints` on the row —
 * and not a role read here.
 *
 * The other control is "Удалить картинку" on a record of a profile. The avatar
 * the profile had when the warning was issued stays in both logs after the
 * person changes it, as the text of a comment does; a picture that may not be
 * shown even as evidence comes out here, after the site's question, for good.
 * It is offered where the record has a picture, and not on the moderator's own
 * record (one issued to him before his promotion or by a higher rank): the
 * server reads a mark set by the owner of a picture as the owner's own doing,
 * so the picture would stay in the log and leave his profile instead. The
 * server checks the role and refuses a moderator's own record.
 *
 * The order is the server's. It used to be re-sorted here after the fetch,
 * which was harmless while the answer was the whole history and wrong the
 * moment it became one page: sorting a page reorders that page against the
 * others, not the log.
 *
 * One filter narrows the list, the name of the warned user, in the search line
 * the sitewide uploads list carries: the site's search field as wide as the
 * column and "Найти" beside it, no caption over it. It looks the name up on
 * "Найти" and not as the reader types, because the server finds the user by
 * his exact name, and the placeholder says so. It lives in the address rather
 * than in a ref here, so a narrowed list is a link a moderator can hand to
 * another one.
 */
import { computed, reactive, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useModal } from "vue-final-modal";
import {
  moderationApi,
  WarningCard,
  RecordSkeleton,
  type Warning,
} from "@/entities/moderation";
import { ChangeWarningPointsDialog } from "@/features/moderation-actions";
import type { PagingInfo as PagingModel } from "@/shared/api/models/common";
import { Paging } from "@/shared/ui/Paging";
import { ErrorState } from "@/shared/ui/ErrorState";
import { CaptionAction } from "@/shared/ui/CaptionLine";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { FilterSearchInput } from "@/shared/ui/Filters";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { parsePageNumber } from "@/shared/lib/filters";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { useRoleGate } from "./lib/useRoleGate";

// A moderation list draws a page of its own size, the way the neighbouring
// ban and newcomer lists do: the reader's page-size preference belongs to the
// open lists of the site, and the open warning log is one of them.
const PAGE_SIZE = 20;

const route = useRoute();
const router = useRouter();
const { hasAccess, deniedText, user } = useRoleGate("Moderator");

const warnings = ref<Warning[]>([]);
const paging = ref<PagingModel | null>(null);

// A page number and a name both come from the address and both change under the
// reader, so two answers can be on the wire at once; the guard drops the one the
// reader has already left instead of letting whichever lands last decide what
// the list shows.
const {
  loading,
  error: loadError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить предупреждения" });

const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

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

function fetch() {
  return run(
    () =>
      moderationApi.getAllWarnings({
        username: username.value,
        skip: (pageNumber.value - 1) * PAGE_SIZE,
        take: PAGE_SIZE,
      }),
    (data) => {
      warnings.value = data?.resources ?? [];
      paging.value = data?.paging ?? null;
    },
  );
}

watch([pageNumber, username], fetch, { immediate: true });

/**
 * The name goes to the address, and the page number stays behind: it counted
 * pages of the list the moderator is leaving, and carrying it over would open
 * the new list at a page it may not have. An emptied field drops the filter.
 *
 * The field takes the trimmed name here rather than waiting for the address to
 * hand it back: the same name again, padded, leaves the address exactly where
 * it already is, and a navigation to the address already open is no navigation
 * at all, so nothing downstream of it runs.
 */
function applyFilter() {
  const query = { ...route.query };
  delete query.number;
  const name = usernameInput.value.trim();
  if (name) query.username = name;
  else delete query.username;
  usernameInput.value = name;
  router.push({ name: "moderation-warnings", query });
}

/**
 * Emptied by the cross of the field or by hand, the field drops the filter at
 * once: a list narrowed to a name under a field that names nobody would say
 * two different things.
 */
function onInput(value: string) {
  usernameInput.value = value;
  if (!value.trim() && username.value) applyFilter();
}

// --- Change the points of a warning (the server says on which rows) ---
const changeTarget = ref<Warning | null>(null);

const { open: openChangeDialog, close: closeChangeDialog } = useModal({
  component: ChangeWarningPointsDialog,
  attrs: reactive({
    warningId: computed(() => changeTarget.value?.id ?? ""),
    currentPoints: computed(() => changeTarget.value?.points ?? 0),
    onSuccess: async () => {
      closeChangeDialog();
      changeTarget.value = null;
      // The page the moderator is on, not the first one: the row stays in the
      // log whatever its points become, so the page it is on is still the page
      // to re-read.
      await fetch();
    },
    onCancel: () => {
      closeChangeDialog();
      changeTarget.value = null;
    },
  }),
});

function changePoints(warning: Warning) {
  changeTarget.value = warning;
  openChangeDialog();
}

// --- Take the picture out of a record of a profile ---
/** The record the question is about; null while nothing is asked. */
const pictureTarget = ref<Warning | null>(null);

/** A record with a picture to take out, and not the moderator's own. */
function offersPictureRemoval(warning: Warning): boolean {
  return (
    !!warning.profile?.picture &&
    !!warning.user &&
    warning.user.id !== user.value?.id
  );
}

/**
 * The window runs the request and prints a refusal in itself, so the request
 * carries the flags that keep the corner quiet. The page the moderator is on is
 * read again after the server agrees: the record comes back without the
 * picture, its status and "О себе" in place.
 */
async function removePicture() {
  const target = pictureTarget.value;
  if (!target) return;
  const result = await moderationApi.removeWarningProfilePicture(target.id, {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  if (!result.error) void fetch();
  return result;
}

const isEmpty = computed(() => !loading.value && warnings.value.length === 0);

/** A page past the end of a log that is not empty: a stale link, or a filter. */
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
  <div class="moderation-warnings">
    <page-title>Последние предупреждения</page-title>

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

      <RecordSkeleton v-else-if="loading && !warnings.length" :count="5" />

      <div v-else-if="isPastEnd" class="warnings-empty">
        <SecondaryText>
          На этой странице предупреждений нет.
          <router-link
            :to="{ name: 'moderation-warnings', query: firstPageQuery }"
            >Вернуться на первую страницу</router-link
          >
        </SecondaryText>
      </div>

      <SecondaryText v-else-if="isEmpty">{{
        username
          ? "Предупреждений по заданным фильтрам не найдено"
          : "Предупреждений пока нет"
      }}</SecondaryText>

      <template v-else>
        <div class="warning-list">
          <WarningCard
            v-for="warning in warnings"
            :key="warning.id"
            :warning="warning"
          >
            <template #record-controls>
              <CaptionAction
                v-if="offersPictureRemoval(warning)"
                @click="pictureTarget = warning"
                >Удалить картинку</CaptionAction
              >
            </template>
            <template #controls>
              <CaptionAction
                v-if="warning.canChangePoints"
                @click="changePoints(warning)"
                >Изменить баллы</CaptionAction
              >
            </template>
          </WarningCard>
        </div>

        <Paging
          v-if="paging"
          :paging="paging"
          :to="{ name: 'moderation-warnings' }"
          use-query
        />
      </template>
    </template>

    <ConfirmDialog
      :show="pictureTarget !== null"
      title="Удалить картинку?"
      message="Картинка пропадет из записи в обоих логах, вернуть ее будет нельзя"
      confirm-label="Удалить"
      failure="Не удалось удалить картинку"
      :action="removePicture"
      @update:show="(shown) => !shown && (pictureTarget = null)"
    />
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

.warning-list
  display: flex
  flex-direction: column
  gap: $medium
  margin-bottom: $medium
</style>
