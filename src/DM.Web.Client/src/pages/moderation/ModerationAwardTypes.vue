<script setup lang="ts">
/**
 * ModerationAwardTypes — каталог типов наград (timeless, 6 строк).
 * Edit title / description / icon / tier / sortOrder. Deactivate (soft).
 * Admin only — route gated в router meta.
 */
import { onMounted, ref } from "vue";
import { achievementApi } from "@/shared/api";
import type { AwardType } from "@/shared/api/models/achievements";
import { useContestSeries } from "@/shared/lib/achievements/useContestSeries";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import { GameIcon } from "@/shared/ui/Icon";

const { awardTypes, load, reload } = useContestSeries();

onMounted(() => load());

const editing = ref<AwardType | null>(null);
const form = ref({
  title: "",
  description: "",
  iconName: "",
  tier: null as number | null,
  sortOrder: 0,
});
const saving = ref(false);
const error = ref<string | null>(null);

function openEdit(t: AwardType) {
  editing.value = t;
  form.value = {
    title: t.title,
    description: t.description,
    iconName: t.iconName,
    tier: t.tier ?? null,
    sortOrder: t.sortOrder,
  };
  error.value = null;
}

async function save() {
  if (!editing.value || saving.value) return;
  saving.value = true;
  error.value = null;
  try {
    const { error: e } = await achievementApi.updateAwardType(
      editing.value.id,
      {
        title: form.value.title,
        description: form.value.description,
        iconName: form.value.iconName,
        tier: form.value.tier,
        sortOrder: form.value.sortOrder,
      },
    );
    if (e) {
      error.value = e.title ?? "Не удалось сохранить";
      return;
    }
    editing.value = null;
    await reload();
  } finally {
    saving.value = false;
  }
}

async function toggleActive(t: AwardType) {
  await achievementApi.updateAwardType(t.id, { isActive: !t.isActive });
  await reload();
}
</script>

<template>
  <section class="award-types-admin">
    <BlockTitle>Каталог типов наград</BlockTitle>
    <SecondaryText>
      Timeless каталог — 6 типов, переиспользуются между всеми сериями
      конкурсов. Деактивация скрывает тип из dropdown при выдаче, исторические
      награды сохраняются.
    </SecondaryText>

    <ul class="types-list">
      <li
        v-for="t in awardTypes ?? []"
        :key="t.id"
        class="type-row"
        :class="{ 'is-inactive': !t.isActive }"
      >
        <GameIcon
          :name="t.iconName"
          class="type-icon"
          :class="`tier-${t.tier ?? 0}`"
        />
        <div class="type-info">
          <div class="type-title">
            {{ t.title }}
            <span v-if="t.tier" class="type-tier">tier {{ t.tier }}</span>
          </div>
          <div class="type-meta">
            <code>{{ t.code }}</code>
            ·
            <code>{{ t.iconName }}</code>
            · sort {{ t.sortOrder }}
          </div>
          <p class="type-desc">{{ t.description }}</p>
        </div>
        <div class="type-actions">
          <button type="button" class="text-button" @click="openEdit(t)">
            Редактировать
          </button>
          <button type="button" class="text-button" @click="toggleActive(t)">
            {{ t.isActive ? "Скрыть" : "Вернуть" }}
          </button>
        </div>
      </li>
    </ul>

    <!-- Edit modal -->
    <div v-if="editing" class="modal-overlay" @click.self="editing = null">
      <form class="modal" @submit.prevent="save">
        <h3>Редактировать тип: {{ editing.code }}</h3>

        <div class="form-row">
          <label>Название</label>
          <input v-model="form.title" type="text" required maxlength="200" />
        </div>
        <div class="form-row">
          <label>Описание</label>
          <textarea
            v-model="form.description"
            rows="3"
            maxlength="1000"
            required
          />
        </div>
        <div class="form-row">
          <label>Имя иконки (из game-icons sprite)</label>
          <input v-model="form.iconName" type="text" required maxlength="80" />
        </div>
        <div class="form-row">
          <label>Tier (1 gold / 2 silver / 3 bronze / null)</label>
          <input v-model.number="form.tier" type="number" min="0" max="9" />
        </div>
        <div class="form-row">
          <label>Порядок сортировки</label>
          <input v-model.number="form.sortOrder" type="number" min="0" />
        </div>

        <p v-if="error" class="form-error">{{ error }}</p>

        <div class="modal-actions">
          <button type="button" class="text-button" @click="editing = null">
            Отмена
          </button>
          <button type="submit" class="primary-button" :disabled="saving">
            {{ saving ? "Сохраняем…" : "Сохранить" }}
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

.award-types-admin
  display: flex
  flex-direction: column
  gap: $medium

.types-list
  list-style: none
  margin: 0
  padding: 0
  display: flex
  flex-direction: column
  gap: $small

.type-row
  display: grid
  grid-template-columns: 56px 1fr auto
  align-items: center
  gap: $medium
  padding: $medium
  background: $bg-element
  border: 1px solid $border
  border-radius: $border-radius

  &.is-inactive
    opacity: 0.55

.type-icon
  font-size: 40px
  color: $heading

  &.tier-1
    color: #d4af37
  &.tier-2
    color: #c0c0c0
  &.tier-3
    color: #cd7f32

.type-title
  font-weight: 500
  margin-bottom: $tiny

.type-tier
  font-size: $tertiary-font-size
  color: $text-meta
  margin-left: $small

.type-meta
  font-size: $tertiary-font-size
  color: $text-muted

  code
    background: $bg-element-accent
    padding: 0 4px
    border-radius: 3px

.type-desc
  margin: $tiny 0 0
  font-size: $secondary-font-size
  color: $text

.type-actions
  display: flex
  flex-direction: column
  gap: $tiny

.text-button
  background: none
  border: none
  color: $link
  cursor: pointer
  font: inherit
  padding: 0
  text-align: left

  &:hover
    text-decoration: underline

.primary-button
  +button

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
  max-width: 480px

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

  input, textarea
    padding: $small
    border: 1px solid $border
    border-radius: $border-radius
    background: $bg-element
    color: $text
    font: inherit

    &:focus
      outline: none
      border-color: $link

  textarea
    resize: vertical

.form-error
  color: $accent-red
  margin: $small 0

.modal-actions
  display: flex
  justify-content: flex-end
  gap: $small
  margin-top: $medium
</style>
