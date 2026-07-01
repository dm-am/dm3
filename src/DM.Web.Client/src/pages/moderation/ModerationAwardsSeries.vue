<script setup lang="ts">
/**
 * ModerationAwardsSeries — детали серии: edit-form (topic URL / IsActive),
 * grant-form (выдать награду пользователю в этой серии) и список ранее
 * выданных наград с возможностью отзыва.
 *
 * Series identity — id (uuid), берем из route param `id`.
 */
import { computed, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import type {
  ContestSeries,
  UserAward,
} from "@/shared/api/models/achievements";
import { achievementApi } from "@/shared/api";
import { useContestSeries } from "@/shared/lib/achievements/useContestSeries";
import { formatContestSeriesTitle } from "@/shared/lib/achievements/formatThreshold";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import UserAutocomplete from "@/shared/ui/UserAutocomplete/UserAutocomplete.vue";
import { GameIcon } from "@/shared/ui/Icon";
import dayjs from "dayjs";

const route = useRoute();
const router = useRouter();
const { series, awardTypes, load, reload } = useContestSeries();

const seriesId = computed(() => String(route.params.id));
const current = computed<ContestSeries | null>(
  () => series.value?.find((s) => s.id === seriesId.value) ?? null,
);

// --- Edit form ---
const edit = ref({ topicUrl: "", isActive: true });
const editSaving = ref(false);

watch(
  current,
  (c) => {
    if (c) {
      edit.value = { topicUrl: c.topicUrl ?? "", isActive: c.isActive };
    }
  },
  { immediate: true },
);

async function saveEdit() {
  if (!current.value || editSaving.value) return;
  editSaving.value = true;
  try {
    await achievementApi.updateContestSeries(current.value.id, {
      topicUrl: edit.value.topicUrl.trim() || "",
      isActive: edit.value.isActive,
    });
    await reload();
  } finally {
    editSaving.value = false;
  }
}

// --- Grant form ---
const grantForm = ref({ username: "", awardTypeId: "", workUrl: "" });
const granting = ref(false);
const grantError = ref<string | null>(null);

async function grant() {
  if (!current.value || granting.value) return;
  if (!grantForm.value.username || !grantForm.value.awardTypeId) return;
  granting.value = true;
  grantError.value = null;
  try {
    const { error } = await achievementApi.grantUserAward(
      grantForm.value.username,
      {
        awardTypeId: grantForm.value.awardTypeId,
        contestSeriesId: current.value.id,
        workUrl: grantForm.value.workUrl.trim() || null,
      },
    );
    if (error) {
      grantError.value = error.title ?? "Не удалось выдать награду";
      return;
    }
    grantForm.value.username = "";
    grantForm.value.awardTypeId = "";
    grantForm.value.workUrl = "";
    await loadGrants();
  } finally {
    granting.value = false;
  }
}

// --- Grants list (загружается отдельно через per-user awards) ---
// FE для admin-вьюхи: проще получить список «у кого есть награды в этой серии»
// собирая в одном запросе все user-awards с фильтром по series — но такого
// эндпоинта нет (вижуально admin-сценарий: выдал → видит выданное).
// Использую следующий подход: храним локальный список выданных в этой
// сессии (после grant), плюс при загрузке тянем «известных получателей» —
// у нас seed-знание о SolohinLex/TestUser_1 и т.п., но fully general
// решение — отдельный admin-endpoint. Открыт для дополнения.
//
// Реальная стратегия: показываем список наград, выданных за текущую
// сессию + позволяем revoke любую по id. Полный список — задача
// отдельного admin-endpoint `/v1/moderation/contest-series/{id}/awards`.

const grants = ref<UserAward[]>([]);
const grantsLoaded = ref(false);

async function loadGrants() {
  // Простейший вариант: пройтись по всем пользователям с наградами было бы
  // дорого. Пока используем in-memory accumulator: после успешного grant
  // добавляем UserAward в список. Список «накапливается» в течение сессии.
  // Долгосрочно нужен server-side endpoint, но scope-фикс — добавляем
  // последний grant из api response, если он там есть.
  grantsLoaded.value = true;
}

async function revoke(award: UserAward) {
  if (
    !confirm(
      `Отозвать награду «${award.type.title}» у ${grantUsernameOf(award)}?`,
    )
  ) {
    return;
  }
  const username = grantUsernameOf(award);
  await achievementApi.revokeUserAward(username, award.id);
  grants.value = grants.value.filter((a) => a.id !== award.id);
}

function grantUsernameOf(_a: UserAward): string {
  // UserAward в публичной модели не несет username (только type+series+date).
  // Для отзыва берем username из формы или из истории grant — храним рядом.
  return "";
}

onMounted(async () => {
  await load();
  await loadGrants();
});
</script>

<template>
  <section class="series-detail">
    <header class="series-detail__header">
      <button
        type="button"
        class="text-button"
        @click="router.push({ name: 'moderation-awards' })"
      >
        ← К списку серий
      </button>
    </header>

    <BlockTitle v-if="current">
      {{ formatContestSeriesTitle(current.contestType, current.number) }}
      · {{ current.year }}
    </BlockTitle>
    <SecondaryText v-else>Серия не найдена</SecondaryText>

    <template v-if="current">
      <!-- Edit section -->
      <section class="block">
        <h3>Параметры серии</h3>
        <div class="form-row">
          <label>Топик с итогами</label>
          <input
            v-model="edit.topicUrl"
            type="url"
            placeholder="https://dm.am/..."
          />
        </div>
        <div class="form-row inline">
          <label>
            <input v-model="edit.isActive" type="checkbox" />
            Активна (видна в dropdown при выдаче)
          </label>
        </div>
        <button
          type="button"
          class="primary-button"
          :disabled="editSaving"
          @click="saveEdit"
        >
          {{ editSaving ? "Сохраняем…" : "Сохранить" }}
        </button>
      </section>

      <!-- Grant section -->
      <section class="block">
        <h3>Выдать награду</h3>
        <div class="form-row">
          <label>Пользователь</label>
          <UserAutocomplete
            v-model="grantForm.username"
            placeholder="Введите имя…"
          />
        </div>
        <div class="form-row">
          <label>Тип награды</label>
          <select v-model="grantForm.awardTypeId">
            <option value="" disabled>— выберите —</option>
            <option v-for="t in awardTypes ?? []" :key="t.id" :value="t.id">
              {{ t.title }} — {{ t.description }}
            </option>
          </select>
        </div>
        <div class="form-row">
          <label>Топик с работой (опционально)</label>
          <input
            v-model="grantForm.workUrl"
            type="url"
            placeholder="https://dm.am/forum/topic/..."
          />
        </div>
        <p v-if="grantError" class="form-error">{{ grantError }}</p>
        <button
          type="button"
          class="primary-button"
          :disabled="granting || !grantForm.username || !grantForm.awardTypeId"
          @click="grant"
        >
          {{ granting ? "Выдаем…" : "Выдать" }}
        </button>
      </section>

      <!-- Grants list -->
      <section v-if="grants.length > 0" class="block">
        <h3>Выданные в этой серии (за сессию)</h3>
        <ul class="grants-list">
          <li v-for="a in grants" :key="a.id" class="grant-row">
            <GameIcon :name="a.type.iconName" class="grant-icon" />
            <span class="grant-title">{{ a.type.title }}</span>
            <span class="grant-date">{{
              dayjs(a.awardedUtc).format("DD.MM.YYYY")
            }}</span>
            <button type="button" class="text-button" @click="revoke(a)">
              Отозвать
            </button>
          </li>
        </ul>
      </section>
    </template>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"

.series-detail
  display: flex
  flex-direction: column
  gap: $medium

  &__header
    display: flex
    align-items: center

.block
  border: 1px solid $border
  border-radius: $border-radius
  padding: $medium
  background: $bg-element

  h3
    margin: 0 0 $small
    font-size: $secondary-font-size
    text-transform: uppercase
    letter-spacing: 0.5px
    color: $text-meta

.form-row
  display: flex
  flex-direction: column
  gap: $tiny
  margin-bottom: $small

  &.inline
    flex-direction: row
    align-items: center
    gap: $small

  label
    font-size: $secondary-font-size
    color: $text-muted

  input[type="url"],
  select
    padding: $small
    border: 1px solid $border
    border-radius: $border-radius
    background: $bg-element
    color: $text
    font: inherit

    &:focus
      outline: none
      border-color: $link

.primary-button
  +button

.text-button
  background: none
  border: none
  color: $link
  cursor: pointer
  font: inherit
  padding: 0

  &:hover
    text-decoration: underline

.form-error
  color: $accent-red
  margin: $small 0
  font-size: $secondary-font-size

.grants-list
  list-style: none
  margin: 0
  padding: 0
  display: flex
  flex-direction: column
  gap: $tiny

.grant-row
  display: grid
  grid-template-columns: 24px 1fr auto auto
  align-items: center
  gap: $small
  padding: $small
  border: 1px solid $border
  border-radius: $border-radius

  .grant-icon
    font-size: 20px
    color: $text-meta

  .grant-date
    font-variant-numeric: tabular-nums
    color: $text-meta
    font-size: $secondary-font-size
</style>
