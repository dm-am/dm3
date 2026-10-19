<script setup lang="ts">
/**
 * Warnings across the site, newest first, in the server's order: the card is
 * the open log's, plus what only a moderator gets, the address of every text
 * and the change of points where the server allows it (`canChangePoints`). A
 * warning is never removed. "Удалить картинку" takes a profile's picture out of
 * a record, not offered on the moderator's own record: the server reads the
 * owner's mark as his own doing and the picture would leave his profile
 * instead. The name filter is looked up on "Найти", by the exact name, and
 * lives in the address.
 */
import { computed, reactive, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useModal } from "vue-final-modal";
import {
  warningApi,
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
import {
  parsePageNumber,
  parseStringFromUrl,
  toQueryValue,
} from "@/shared/lib/filters";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { useAuthStore } from "@/entities/user";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const route = useRoute();
const router = useRouter();
const { user } = storeToRefs(useAuthStore());
// A page as long as the reader's lists (account settings).
const { pageSize } = usePaging();

const warnings = ref<Warning[]>([]);
const paging = ref<PagingModel | null>(null);

// The number and the name both come from the address; the guard drops the answer the reader has left.
const {
  loading,
  error: loadError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить предупреждения" });

const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

/** The one filter the list takes; an unknown name is an empty page, not an error. */
const username = computed(
  () => parseStringFromUrl(toQueryValue(route.query.username)) || undefined,
);

// The field shows what the address says, so the back button puts the previous name back.
const usernameInput = ref(username.value ?? "");
watch(username, (name) => (usernameInput.value = name ?? ""));

function loadWarnings() {
  return run(
    () =>
      warningApi.getAllWarnings({
        username: username.value,
        skip: (pageNumber.value - 1) * pageSize.value,
        take: pageSize.value,
      }),
    (data) => {
      warnings.value = data?.resources ?? [];
      paging.value = data?.paging ?? null;
    },
  );
}

watch([pageNumber, username], loadWarnings, { immediate: true });

/**
 * The name goes to the address without the page number; an emptied field drops
 * the filter. The field takes the trimmed name: the same name padded is the
 * address already open, and a navigation there runs nothing.
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

/** An emptied field drops the filter at once. */
function onInput(value: string) {
  usernameInput.value = value;
  if (!value.trim() && username.value) applyFilter();
}

const changeTarget = ref<Warning | null>(null);

const { open: openChangeDialog, close: closeChangeDialog } = useModal({
  component: ChangeWarningPointsDialog,
  attrs: reactive({
    warningId: computed(() => changeTarget.value?.id ?? ""),
    currentPoints: computed(() => changeTarget.value?.points ?? 0),
    onSuccess: async () => {
      closeChangeDialog();
      changeTarget.value = null;
      // The page the moderator is on: the row stays whatever its points become.
      await loadWarnings();
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
 * The window runs the request and prints a refusal in itself; the page is
 * read again after the server agrees.
 */
async function removePicture() {
  const target = pictureTarget.value;
  if (!target) return;
  const result = await warningApi.removeWarningProfilePicture(
    target.id,
    OWNS_REFUSAL,
  );
  if (!result.error) void loadWarnings();
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

    <form class="search-line" @submit.prevent="applyFilter">
      <FilterSearchInput
        :model-value="usernameInput"
        placeholder="Точное имя пользователя"
        @update:model-value="onInput"
      />
      <Button type="submit" variant="primary">Найти</Button>
    </form>

    <ErrorState v-if="loadError" :message="loadError" :retry="loadWarnings" />

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

    <ConfirmDialog
      :show="pictureTarget !== null"
      title="Удалить картинку?"
      message="Картинка пропадет из обоих логов, вернуть ее будет нельзя"
      confirm-label="Удалить"
      failure="Не удалось удалить картинку"
      :action="removePicture"
      @update:show="(shown) => !shown && (pictureTarget = null)"
    />
  </div>
</template>

<style scoped lang="sass">
// The field takes the width the button leaves, one line at every width.
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
