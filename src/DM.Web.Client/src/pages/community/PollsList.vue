<script setup lang="ts">
import { computed } from "vue";
import { storeToRefs } from "pinia";
import Paging from "@/shared/ui/Paging/Paging.vue";
import Poll from "@/widgets/sidebar/Poll.vue";
import { EmptyState } from "@/shared/ui/EmptyState";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { usePollsStore } from "@/entities/poll";
import { PollsFilter, usePollsFilter } from "@/features/poll-filter";
import { CreatePollForm } from "@/features/create-poll";

const pollsStore = usePollsStore();
const { polls, pollsLoading, pollsError } = storeToRefs(pollsStore);

// Two-state empty text
const { hasActiveFilters } = usePollsFilter();
const emptyTitle = computed(() =>
  hasActiveFilters.value ? "Опросов по заданным фильтрам не найдено" : "Опросов пока нет"
);
const emptyHint = computed(() =>
  hasActiveFilters.value ? "Попробуйте изменить параметры поиска" : undefined
);
</script>

<template>
  <page-title>Опросы</page-title>

  <!-- Create poll form (moderators only) -->
  <CreatePollForm />

  <!-- Filter -->
  <PollsFilter />

  <!-- Loading state -->
  <div v-if="pollsLoading" class="polls-loading">
    <SecondaryText>Загрузка опросов...</SecondaryText>
  </div>

  <!-- Error state -->
  <div v-else-if="pollsError" class="error-message">
    {{ pollsError }}
  </div>

  <!-- Empty state -->
  <EmptyState
    v-else-if="polls && polls.resources.length === 0"
    :title="emptyTitle"
    :hint="emptyHint"
  />

  <!-- Polls list -->
  <div v-else-if="polls" class="polls-list">
    <!-- Top paging -->
    <template v-if="polls.paging && polls.paging.pages > 1">
      <div class="separator separator--paging">
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - -
      </div>
      <Paging
        :paging="polls.paging"
        :to="{ name: 'polls' }"
        :use-query="true"
      />
      <div class="separator separator--paging">
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - -
      </div>
    </template>

    <div class="polls-grid">
      <div
        v-for="poll in polls.resources"
        :key="poll.id"
        class="poll-card"
      >
        <Poll :poll="poll" :controls="true" />
      </div>
    </div>

    <!-- Bottom paging -->
    <template v-if="polls.paging && polls.paging.pages > 1">
      <div class="separator separator--paging">
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - -
      </div>
      <Paging
        :paging="polls.paging"
        :to="{ name: 'polls' }"
        :use-query="true"
      />
      <div class="separator separator--paging">
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
        - - - - - - - - - - - - - -
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.polls-list
  display: flex
  flex-direction: column
  gap: $tiny
  margin-top: $medium

.polls-grid
  display: grid
  grid-template-columns: repeat(3, 1fr)
  gap: $medium

  @media (max-width: 1000px)
    grid-template-columns: repeat(2, 1fr)

  @media (max-width: 600px)
    grid-template-columns: 1fr

.poll-card
  padding: $medium
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element

  :deep(.poll)
    margin: 0

.polls-loading
  padding: $large
  text-align: center

.error-message
  padding: $medium
  color: $text-on-red
  background-color: $bg-highlight-red
  border-radius: $border-radius
  margin-bottom: $medium

.separator
  margin: $tiny 0
  color: $text-muted
  white-space: nowrap
  overflow: hidden
  max-width: 100%
  width: 0
  min-width: 100%
  user-select: none

  &--paging
    margin: 0
</style>
