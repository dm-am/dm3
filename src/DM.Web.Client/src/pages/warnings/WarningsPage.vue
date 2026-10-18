<script setup lang="ts">
/**
 * The open log of warnings the moderation team has issued.
 *
 * The rules page has always linked here as the proof that moderation is done
 * in the open, and the page answered "В разработке". It now shows what the
 * link promised: who was warned, for how many points, why, by whom and when,
 * newest first and a page at a time.
 *
 * Every warning ever issued is here, including the ones whose points were
 * later taken off: a warning is the record of a decision and is never
 * withdrawn. What a correction adds is the second decision the card draws
 * under the first — who changed the points, when and why — and it is public
 * along with the warning, because a verdict corrected to nothing that is shown
 * with only the first signature reads as a verdict that still stands.
 *
 * The row shows the text the warning was issued for, as DM2's log did, always
 * (the owner, 05.10: "Всегда показывай"), a hidden block erased from it for
 * everybody; the place links to the text where a guest could follow it, and
 * the server decides where that is.
 */
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import {
  moderationApi,
  WarningCard,
  RecordSkeleton,
  type WarningLogEntry,
} from "@/entities/moderation";
import type { PagingInfo as PagingModel } from "@/shared/api/models/common";
import { PageTitle, LeadText, SecondaryText } from "@/shared/ui/Layout";
import { Paging } from "@/shared/ui/Paging";
import { ErrorState } from "@/shared/ui/ErrorState";
import { parsePageNumber } from "@/shared/lib/filters";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";

const route = useRoute();
const { entitiesPerPage } = usePaging();

const warnings = ref<WarningLogEntry[]>([]);
const paging = ref<PagingModel | null>(null);

// A page number and a name both come from the address and both can change under
// the reader, so two answers can be on the wire at once; the guard drops the one
// the reader has already left instead of letting whichever lands last decide
// what the list shows.
const {
  loading,
  error: loadError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить предупреждения" });

const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

/** The one filter the log takes; an unknown name is an empty page, not an error. */
const username = computed(() => {
  const value = route.query.username;
  const name = Array.isArray(value) ? value[0] : value;
  return name ? String(name) : undefined;
});

function fetch() {
  return run(
    () =>
      moderationApi.getWarningLog({
        username: username.value,
        skip: (pageNumber.value - 1) * entitiesPerPage.value,
        take: entitiesPerPage.value,
      }),
    (data) => {
      warnings.value = data?.resources ?? [];
      paging.value = data?.paging ?? null;
    },
  );
}

watch([pageNumber, username], fetch, { immediate: true });

const isEmpty = computed(() => !loading.value && warnings.value.length === 0);

/**
 * A page number past the end of a log that is not empty: a stale link, or a
 * name the log has no rows for. The reader is told and offered the way back
 * rather than moved without asking.
 */
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

    <!-- Narrowed to one person, the heading says whose warnings these are and
         the lead is the way back to everybody's. It carries no sentence around
         the link: the name is already in the heading, and every row names its
         own user, so there is nothing left for a lead-in to explain. -->
    <LeadText v-if="username">
      <router-link :to="{ name: 'warnings' }">Лог предупреждений</router-link>
    </LeadText>

    <ErrorState v-if="loadError" :message="loadError" :retry="fetch" />

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
