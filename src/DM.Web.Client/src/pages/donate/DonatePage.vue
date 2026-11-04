<script setup lang="ts">
import { computed } from "vue";
import ProgressBar from "@/shared/ui/ProgressBar/ProgressBar.vue";
import { ErrorState } from "@/shared/ui/ErrorState";
import { fundraisingApi, type Fundraising } from "@/entities/fundraising";
import { useApiResource } from "@/shared/lib/composables/useApiResource";
import type { Envelope } from "@/shared/api/models/common";

// The sidebar panel's instance may serve a cached sum; this page refetches on every visit.
const { data, loading, error, fetch } = useApiResource<Envelope<Fundraising>>(
  () => fundraisingApi.getFundraising(),
);

const fundraising = computed(() => data.value?.resource ?? null);

// During setup, so `loading` is already true on the first render.
void fetch();
</script>

<template>
  <page-title v-once>Помочь проекту</page-title>

  <div class="donate-content">
    <p v-once>
      Dungeon Master &ndash; некоммерческий проект: сайт делают и содержат сами
      участники. Поддержать его можно деньгами, а можно временем и вниманием, и
      второе ценится ничуть не меньше.
    </p>

    <block-title>Сбор средств</block-title>
    <!-- What the money is for: the numbers alone do not say it. -->
    <p v-if="fundraising" class="goal-title">{{ fundraising.title }}</p>
    <div
      v-if="loading && !fundraising"
      class="progress-skeleton"
      aria-hidden="true"
    >
      <div class="skeleton-line" />
    </div>
    <ProgressBar
      v-else-if="fundraising"
      :current="fundraising.collectedAmount"
      :goal="fundraising.goalAmount"
    >
      {{ fundraising.collectedAmount.toLocaleString("ru-RU") }} /
      {{ fundraising.goalAmount.toLocaleString("ru-RU") }} р.
    </ProgressBar>
    <ErrorState
      v-else-if="error"
      message="Не удалось загрузить сбор средств"
      :retry="() => fetch(true)"
    />
    <p v-once>
      Материальная поддержка идет через объявленный сбор: цель и собранную сумму
      устанавливает администрация, а ход сбора виден каждому на этой странице и
      в блоке "Поддержка проекта".
    </p>

    <block-title>Чем еще помочь</block-title>
    <ul class="help-list" v-once>
      <li>
        Играйте и водите игры: живая площадка держится на тех, кто пишет посты и
        набирает игроков
      </li>
      <!-- A found fault is a forum topic; the administration's form is for security and accounts. -->
      <li>
        Тестируйте сайт и
        <router-link :to="{ name: 'forum', params: { alias: 'bugs' } }"
          ><strong>сообщайте о находках</strong></router-link
        >: каждая описанная ошибка приближает исправление
      </li>
      <li>Участвуйте в обсуждениях на форуме и в блогах</li>
      <li>Рассказывайте о сайте: новый участник ценнее любого пожертвования</li>
    </ul>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

.donate-content
  display: flex
  flex-direction: column
  gap: $tiny
  color: $text
  line-height: 1.6

  p
    margin: 0 0 $small

  .goal-title
    margin-bottom: 0
    font-weight: bold

// The box of ProgressBar, so nothing shifts when the bar replaces it.
.progress-skeleton
  display: flex
  align-items: center
  height: 1.6em
  box-sizing: content-box
  margin: $small 0
  padding: $minor
  border-radius: $border-radius

.skeleton-line
  height: 12px
  width: 40%
  +skeleton-shimmer

.help-list
  margin: $small 0 $medium
  padding-left: $big

  li
    margin: $tiny 0
</style>
