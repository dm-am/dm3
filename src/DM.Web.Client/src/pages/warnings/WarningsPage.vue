<script setup lang="ts">
/**
 * The open log of warnings, newest first. A warning whose points were later
 * taken off stays, with the correction under it: a verdict corrected to
 * nothing shown with the first signature only reads as one that still stands.
 * The row shows the text the warning was issued for, as DM2's log did.
 */
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import {
  warningApi,
  WarningCard,
  RecordSkeleton,
  type WarningLogEntry,
} from "@/entities/moderation";
import type { PagingInfo as PagingModel } from "@/shared/api/models/common";
import { PageTitle, LeadText, SecondaryText } from "@/shared/ui/Layout";
import { Paging } from "@/shared/ui/Paging";
import { ErrorState } from "@/shared/ui/ErrorState";
import {
  parsePageNumber,
  parseStringFromUrl,
  toQueryValue,
} from "@/shared/lib/filters";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";

const route = useRoute();
const { pageSize } = usePaging();

const warnings = ref<WarningLogEntry[]>([]);
const paging = ref<PagingModel | null>(null);

// The number and the name both come from the address, so two answers can be
// on the wire; the guard drops the one the reader has already left.
const {
  loading,
  error: loadError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить предупреждения" });

const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

/** The one filter the log takes; an unknown name is an empty page, not an error. */
const username = computed(
  () => parseStringFromUrl(toQueryValue(route.query.username)) || undefined,
);

function loadWarnings() {
  return run(
    () =>
      warningApi.getWarningLog({
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

const isEmpty = computed(() => !loading.value && warnings.value.length === 0);

/** A page number past the end of a log that is not empty; the reader is offered the way back. */
const isPastEnd = computed(
  () => isEmpty.value && (paging.value?.total ?? 0) > 0,
);

/** The address of the first page: this one without the page number, filter kept. */
const firstPageQuery = computed(() => {
  const query = { ...route.query };
  delete query.number;
  return query;
});
</script>

<template>
  <div class="warnings-page">
    <PageTitle v-if="username">Лог предупреждений: {{ username }}</PageTitle>
    <PageTitle v-else>Лог предупреждений</PageTitle>

    <!-- Narrowed to one person, the lead is the way back to everybody's. -->
    <LeadText v-if="username">
      <router-link :to="{ name: 'warnings' }">Лог предупреждений</router-link>
    </LeadText>

    <ErrorState v-if="loadError" :message="loadError" :retry="loadWarnings" />

    <RecordSkeleton v-else-if="loading && !warnings.length" :count="5" />

    <div v-else-if="isPastEnd" class="warnings-empty">
      <SecondaryText>
        На этой странице предупреждений нет.
        <router-link :to="{ name: 'warnings', query: firstPageQuery }"
          >Вернуться на первую страницу</router-link
        >
      </SecondaryText>
    </div>

    <div v-else-if="isEmpty" class="warnings-empty">
      <SecondaryText>{{
        username
          ? "Предупреждений по заданным фильтрам не найдено"
          : "Предупреждений пока нет"
      }}</SecondaryText>
    </div>

    <template v-else>
      <div class="warning-list">
        <WarningCard
          v-for="warning in warnings"
          :key="warning.id"
          :warning="warning"
        />
      </div>

      <Paging
        v-if="paging"
        :paging="paging"
        :to="{ name: 'warnings' }"
        use-query
      />
    </template>
  </div>
</template>

<style scoped lang="sass">
.warnings-page
  width: 100%

.warning-list
  display: flex
  flex-direction: column
  gap: $medium
  margin-bottom: $medium
</style>
