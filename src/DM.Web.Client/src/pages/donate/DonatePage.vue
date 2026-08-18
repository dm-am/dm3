<script setup lang="ts">
import { computed } from "vue";
import ProgressBar from "@/shared/ui/ProgressBar/ProgressBar.vue";
import { ErrorState } from "@/shared/ui/ErrorState";
import { fundraisingApi, type Fundraising } from "@/entities/fundraising";
import { useApiResource } from "@/shared/lib/composables/useApiResource";
import type { Envelope } from "@/shared/api/models/common";

// Same endpoint as the sidebar SupportPanel, but useApiResource state is per
// component instance: this page remounts on every visit and always refetches,
// while the panel's long-lived instance may serve a sum cached for up to its
// window, so the two bars can briefly disagree after an admin edit.
const { data, loading, error, fetch } = useApiResource<Envelope<Fundraising>>(
  () => fundraisingApi.getFundraising(),
);

const fundraising = computed(() => data.value?.resource ?? null);

// Fetch synchronously during setup so `loading` is already true on the
// first render (avoids a flash of the no-progress-bar layout).
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
      на панели "Поддержка проекта" в правой колонке.
    </p>

    <block-title>Чем еще помочь</block-title>
    <ul class="help-list" v-once>
      <li>
        Играйте и водите игры: живая площадка держится на тех, кто пишет посты и
        набирает игроков.
      </li>
      <li>
        Тестируйте сайт и
        <router-link :to="{ name: 'complaint' }"
          ><strong>сообщайте о находках</strong></router-link
        >: каждая описанная ошибка приближает исправление.
      </li>
      <li>Участвуйте в обсуждениях на форуме и в блогах.</li>
      <li>
        Рассказывайте о сайте: новый участник ценнее любого пожертвования.
      </li>
    </ul>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Skeleton"

.donate-content
  display: flex
  flex-direction: column
  gap: $tiny
  color: $text
  line-height: 1.6

  p
    margin: 0 0 $small

  a
    color: $link
    &:hover
      color: $link-hover
      text-decoration: underline

// Same box as ProgressBar (margin, padding, one text row) so nothing
// shifts vertically when the real bar replaces it. The bar's text row
// inherits the 1.6 line-height of .donate-content, hence the height.
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
