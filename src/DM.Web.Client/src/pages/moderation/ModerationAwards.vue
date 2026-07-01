<script setup lang="ts">
/**
 * ModerationAwards — список серий конкурсов + create-form + ссылка
 * на каталог типов наград. Главная admin-страница для блока «Награды».
 *
 * Реюзится `useContestSeries()` (singleton-кеш) — после create/edit
 * вызываем reload() чтобы все потребители (профиль, другие admin-вью)
 * увидели свежий список.
 */
import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { ContestType } from "@/shared/api/models/achievements";
import { achievementApi } from "@/shared/api";
import { useContestSeries } from "@/shared/lib/achievements/useContestSeries";
import { formatContestSeriesTitle } from "@/shared/lib/achievements/formatThreshold";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";

const router = useRouter();
const { series, loading, load, reload } = useContestSeries();

onMounted(() => load());

const sortedSeries = computed(() =>
  [...(series.value ?? [])].sort((a, b) => {
    if (a.year !== b.year) return b.year - a.year;
    if (a.contestType !== b.contestType) {
      return a.contestType.localeCompare(b.contestType);
    }
    return b.number - a.number;
  }),
);

// --- Create form ---
const showCreate = ref(false);
const form = ref({
  contestType: ContestType.Literary,
  number: 0,
  year: new Date().getFullYear(),
  topicUrl: "",
});
const saving = ref(false);
const formError = ref<string | null>(null);

function openCreate() {
  // Авто-предложение Number: max + 1 в рамках типа.
  const maxN = (series.value ?? [])
    .filter((s) => s.contestType === form.value.contestType)
    .reduce((m, s) => Math.max(m, s.number), 0);
  form.value = {
    contestType: ContestType.Literary,
    number: maxN + 1,
    year: new Date().getFullYear(),
    topicUrl: "",
  };
  formError.value = null;
  showCreate.value = true;
}

async function createSeries() {
  if (saving.value) return;
  saving.value = true;
  formError.value = null;
  try {
    const { error } = await achievementApi.createContestSeries({
      contestType: form.value.contestType,
      number: form.value.number,
      year: form.value.year,
      topicUrl: form.value.topicUrl.trim() || null,
    });
    if (error) {
      formError.value = error.title ?? "Не удалось создать серию";
      return;
    }
    showCreate.value = false;
    await reload();
  } finally {
    saving.value = false;
  }
}

async function toggleActive(id: string, isActive: boolean) {
  await achievementApi.updateContestSeries(id, { isActive: !isActive });
  await reload();
}

function openSeries(id: string) {
  router.push({
    name: "moderation-awards-series",
    params: { id },
  });
}
</script>

<template>
  <section class="awards-admin">
    <header class="awards-admin__header">
      <BlockTitle>Серии конкурсов</BlockTitle>
      <div class="awards-admin__actions">
        <router-link
          :to="{ name: 'moderation-award-types' }"
          class="link-button"
        >
          Каталог типов наград
        </router-link>
        <button type="button" class="primary-button" @click="openCreate">
          + Новая серия
        </button>
      </div>
    </header>

    <SecondaryText v-if="loading && !series">Загрузка…</SecondaryText>
    <SecondaryText v-else-if="sortedSeries.length === 0">
      Нет серий конкурсов
    </SecondaryText>

    <table v-else class="series-table">
      <thead>
        <tr>
          <th>Конкурс</th>
          <th>Год</th>
          <th>Топик</th>
          <th>Статус</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="s in sortedSeries"
          :key="s.id"
          :class="{ 'is-inactive': !s.isActive }"
        >
          <td>
            <a href="#" @click.prevent="openSeries(s.id)">
              {{ formatContestSeriesTitle(s.contestType, s.number) }}
            </a>
          </td>
          <td>{{ s.year }}</td>
          <td>
            <a
              v-if="s.topicUrl"
              :href="s.topicUrl"
              target="_blank"
              rel="noopener"
            >
              открыть →
            </a>
            <span v-else class="muted">—</span>
          </td>
          <td>{{ s.isActive ? "Активна" : "Скрыта" }}</td>
          <td>
            <button
              type="button"
              class="text-button"
              @click="toggleActive(s.id, s.isActive)"
            >
              {{ s.isActive ? "Скрыть" : "Вернуть" }}
            </button>
          </td>
        </tr>
      </tbody>
    </table>

    <!-- Create modal -->
    <div
      v-if="showCreate"
      class="modal-overlay"
      @click.self="showCreate = false"
    >
      <form class="modal" @submit.prevent="createSeries">
        <h3>Новая серия конкурса</h3>

        <div class="form-row">
          <label>Тип</label>
          <select v-model="form.contestType">
            <option :value="ContestType.Literary">Литературный</option>
            <option :value="ContestType.Art">Художественный</option>
          </select>
        </div>

        <div class="form-row">
          <label>Номер</label>
          <input v-model.number="form.number" type="number" min="1" required />
        </div>

        <div class="form-row">
          <label>Год</label>
          <input
            v-model.number="form.year"
            type="number"
            min="2000"
            max="2100"
            required
          />
        </div>

        <div class="form-row">
          <label>Топик с итогами (опц.)</label>
          <input v-model="form.topicUrl" type="url" placeholder="https://..." />
        </div>

        <p v-if="formError" class="form-error">{{ formError }}</p>

        <div class="modal-actions">
          <button type="button" class="text-button" @click="showCreate = false">
            Отмена
          </button>
          <button type="submit" class="primary-button" :disabled="saving">
            {{ saving ? "Создаем…" : "Создать" }}
          </button>
        </div>
      </form>
    </div>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"
@import "src/assets/styles/ZIndex"

.awards-admin
  display: flex
  flex-direction: column
  gap: $medium

  &__header
    display: flex
    align-items: center
    justify-content: space-between
    gap: $medium

  &__actions
    display: flex
    align-items: center
    gap: $small

.series-table
  width: 100%
  border-collapse: collapse
  font-size: $secondary-font-size

  th, td
    padding: $small $minor
    text-align: left
    border-bottom: 1px solid $border

  th
    font-weight: 500
    color: $text-meta

  .is-inactive
    opacity: 0.5

  a
    color: $link
    text-decoration: none

    &:hover
      text-decoration: underline

  .muted
    color: $text-muted

.primary-button
  +button

.link-button
  +button
  text-decoration: none

.text-button
  background: none
  border: none
  color: $link
  cursor: pointer
  font: inherit
  padding: 0

  &:hover
    text-decoration: underline

.modal-overlay
  position: fixed
  inset: 0
  background: rgba(0, 0, 0, 0.5)
  display: flex
  align-items: center
  justify-content: center
  z-index: $z-modal

.modal
  background: $bg-element
  border: 1px solid $border
  border-radius: $border-radius
  padding: $large
  width: 100%
  max-width: 420px
  box-shadow: 0 4px 20px $shadow-color

  h3
    margin: 0 0 $medium

.form-row
  display: flex
  flex-direction: column
  gap: $tiny
  margin-bottom: $small

  label
    font-size: $secondary-font-size
    color: $text-muted

  input, select
    padding: $small
    border: 1px solid $border
    border-radius: $border-radius
    background: $bg-element
    color: $text
    font: inherit

    &:focus
      outline: none
      border-color: $link

.form-error
  color: $accent-red
  margin: $small 0
  font-size: $secondary-font-size

.modal-actions
  display: flex
  justify-content: flex-end
  gap: $small
  margin-top: $medium
</style>
