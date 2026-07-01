<script setup lang="ts">
/**
 * ModerationAchievements — каталог достижений: 13 категорий + 52 тира.
 * Категории — PATCH (Title/Description/IconName/SortOrder/IsActive),
 * тиры — POST/PATCH/DELETE. Metric у категории immutable (привязка
 * к серверному AchievementMetricResolver).
 */
import { computed, onMounted, ref } from "vue";
import { achievementApi } from "@/shared/api";
import type {
  AchievementCategory,
  AchievementType,
} from "@/shared/api/models/achievements";
import { useAchievementCatalog } from "@/shared/lib/achievements/useAchievementCatalog";
import { formatThreshold } from "@/shared/lib/achievements/formatThreshold";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import { GameIcon } from "@/shared/ui/Icon";

const { categories, types, load, reload } = useAchievementCatalog();

onMounted(() => load());

const sortedCategories = computed(() =>
  [...(categories.value ?? [])].sort((a, b) => a.sortOrder - b.sortOrder),
);

function tiersOf(c: AchievementCategory): AchievementType[] {
  return (types.value ?? [])
    .filter((t) => t.category.id === c.id)
    .sort((a, b) => a.threshold - b.threshold);
}

// --- Category edit ---
const editingCategory = ref<AchievementCategory | null>(null);
const catForm = ref({
  title: "",
  description: "",
  iconName: "",
  sortOrder: 0,
  isActive: true,
});
const catSaving = ref(false);

function openEditCategory(c: AchievementCategory) {
  editingCategory.value = c;
  catForm.value = {
    title: c.title,
    description: c.description,
    iconName: c.iconName,
    sortOrder: c.sortOrder,
    isActive: c.isActive,
  };
}

async function saveCategory() {
  if (!editingCategory.value || catSaving.value) return;
  catSaving.value = true;
  try {
    await achievementApi.updateAchievementCategory(editingCategory.value.id, {
      title: catForm.value.title,
      description: catForm.value.description,
      iconName: catForm.value.iconName,
      sortOrder: catForm.value.sortOrder,
      isActive: catForm.value.isActive,
    });
    editingCategory.value = null;
    await reload();
  } finally {
    catSaving.value = false;
  }
}

// --- Tier edit / create / delete ---
const editingTier = ref<AchievementType | null>(null);
const tierFormCategoryId = ref<string | null>(null);
const tierForm = ref({ code: "", title: "", threshold: 0, tier: 1 });
const tierSaving = ref(false);

function openCreateTier(c: AchievementCategory) {
  editingTier.value = null;
  tierFormCategoryId.value = c.id;
  const maxTier = tiersOf(c).reduce((m, t) => Math.max(m, t.tier ?? 0), 0);
  tierForm.value = { code: "", title: "", threshold: 0, tier: maxTier + 1 };
}

function openEditTier(t: AchievementType) {
  editingTier.value = t;
  tierFormCategoryId.value = t.category.id;
  tierForm.value = {
    code: t.code,
    title: t.title,
    threshold: t.threshold,
    tier: t.tier ?? 1,
  };
}

async function saveTier() {
  if (tierSaving.value || tierFormCategoryId.value === null) return;
  tierSaving.value = true;
  try {
    if (editingTier.value) {
      await achievementApi.updateAchievementType(editingTier.value.id, {
        title: tierForm.value.title,
        threshold: tierForm.value.threshold,
        tier: tierForm.value.tier,
      });
    } else {
      await achievementApi.createAchievementType({
        code: tierForm.value.code,
        title: tierForm.value.title,
        threshold: tierForm.value.threshold,
        tier: tierForm.value.tier,
        achievementCategoryId: tierFormCategoryId.value,
      });
    }
    editingTier.value = null;
    tierFormCategoryId.value = null;
    await reload();
  } finally {
    tierSaving.value = false;
  }
}

async function deleteTier(t: AchievementType) {
  if (
    !confirm(
      `Удалить тир «${t.title}»? Уже выданные UserAchievement останутся.`,
    )
  ) {
    return;
  }
  await achievementApi.deleteAchievementType(t.id);
  await reload();
}
</script>

<template>
  <section class="achievements-admin">
    <BlockTitle>Каталог достижений</BlockTitle>
    <SecondaryText>
      Категории привязаны к серверной метрике — название и описание можно
      править, метрику нельзя. Тиры в категории добавляются/удаляются без
      ограничений.
    </SecondaryText>

    <ul class="cat-list">
      <li
        v-for="c in sortedCategories"
        :key="c.id"
        class="cat-row"
        :class="{ 'is-inactive': !c.isActive }"
      >
        <div class="cat-header">
          <GameIcon :name="c.iconName" class="cat-icon" />
          <div class="cat-info">
            <div class="cat-title">{{ c.title }}</div>
            <div class="cat-meta">
              <code>{{ c.code }}</code> · <code>{{ c.metric }}</code> · sort
              {{ c.sortOrder }}
            </div>
            <p class="cat-desc">{{ c.description }}</p>
          </div>
          <div class="cat-actions">
            <button
              type="button"
              class="text-button"
              @click="openEditCategory(c)"
            >
              Редактировать
            </button>
            <button
              type="button"
              class="text-button"
              @click="openCreateTier(c)"
            >
              + Тир
            </button>
          </div>
        </div>

        <table class="tier-table">
          <thead>
            <tr>
              <th>Tier</th>
              <th>Code</th>
              <th>Название</th>
              <th>Порог</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="t in tiersOf(c)" :key="t.id">
              <td>{{ t.tier }}</td>
              <td>
                <code>{{ t.code }}</code>
              </td>
              <td>{{ t.title }}</td>
              <td>{{ formatThreshold(c.metric, t.threshold) }}</td>
              <td>
                <button
                  type="button"
                  class="text-button"
                  @click="openEditTier(t)"
                >
                  Edit
                </button>
                <button
                  type="button"
                  class="text-button danger"
                  @click="deleteTier(t)"
                >
                  Del
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </li>
    </ul>

    <!-- Category modal -->
    <div
      v-if="editingCategory"
      class="modal-overlay"
      @click.self="editingCategory = null"
    >
      <form class="modal" @submit.prevent="saveCategory">
        <h3>Категория: {{ editingCategory.code }}</h3>
        <div class="form-row">
          <label>Название</label>
          <input v-model="catForm.title" type="text" required maxlength="200" />
        </div>
        <div class="form-row">
          <label>Описание (показывается в popover)</label>
          <textarea
            v-model="catForm.description"
            rows="3"
            maxlength="1000"
            required
          />
        </div>
        <div class="form-row">
          <label>Имя иконки</label>
          <input
            v-model="catForm.iconName"
            type="text"
            required
            maxlength="80"
          />
        </div>
        <div class="form-row">
          <label>Порядок</label>
          <input v-model.number="catForm.sortOrder" type="number" min="0" />
        </div>
        <div class="form-row inline">
          <label>
            <input v-model="catForm.isActive" type="checkbox" />
            Активна
          </label>
        </div>
        <div class="modal-actions">
          <button
            type="button"
            class="text-button"
            @click="editingCategory = null"
          >
            Отмена
          </button>
          <button type="submit" class="primary-button" :disabled="catSaving">
            {{ catSaving ? "Сохраняем…" : "Сохранить" }}
          </button>
        </div>
      </form>
    </div>

    <!-- Tier modal (create or edit) -->
    <div
      v-if="tierFormCategoryId"
      class="modal-overlay"
      @click.self="tierFormCategoryId = null"
    >
      <form class="modal" @submit.prevent="saveTier">
        <h3>{{ editingTier ? `Тир: ${editingTier.code}` : "Новый тир" }}</h3>
        <div v-if="!editingTier" class="form-row">
          <label>Code (стабильный, например POSTS_100)</label>
          <input v-model="tierForm.code" type="text" required maxlength="80" />
        </div>
        <div class="form-row">
          <label>Название</label>
          <input
            v-model="tierForm.title"
            type="text"
            required
            maxlength="200"
          />
        </div>
        <div class="form-row">
          <label>Порог</label>
          <input
            v-model.number="tierForm.threshold"
            type="number"
            min="1"
            required
          />
        </div>
        <div class="form-row">
          <label>Tier (1-4)</label>
          <input v-model.number="tierForm.tier" type="number" min="1" max="9" />
        </div>
        <div class="modal-actions">
          <button
            type="button"
            class="text-button"
            @click="tierFormCategoryId = null"
          >
            Отмена
          </button>
          <button type="submit" class="primary-button" :disabled="tierSaving">
            {{ tierSaving ? "Сохраняем…" : "Сохранить" }}
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

.achievements-admin
  display: flex
  flex-direction: column
  gap: $medium

.cat-list
  list-style: none
  margin: 0
  padding: 0
  display: flex
  flex-direction: column
  gap: $medium

.cat-row
  border: 1px solid $border
  border-radius: $border-radius
  padding: $medium
  background: $bg-element

  &.is-inactive
    opacity: 0.55

.cat-header
  display: grid
  grid-template-columns: 48px 1fr auto
  gap: $medium
  align-items: start
  margin-bottom: $small

.cat-icon
  font-size: 36px
  color: $heading

.cat-title
  font-weight: 500
  margin-bottom: $tiny

.cat-meta
  font-size: $tertiary-font-size
  color: $text-muted

  code
    background: $bg-element-accent
    padding: 0 4px
    border-radius: 3px

.cat-desc
  margin: $tiny 0 0
  font-size: $secondary-font-size
  color: $text

.cat-actions
  display: flex
  flex-direction: column
  gap: $tiny

.tier-table
  width: 100%
  border-collapse: collapse
  font-size: $secondary-font-size

  th, td
    padding: $tiny $small
    text-align: left
    border-bottom: 1px solid $border

  th
    color: $text-meta
    font-weight: 500

  code
    background: $bg-element-accent
    padding: 0 4px
    border-radius: 3px

.text-button
  background: none
  border: none
  color: $link
  cursor: pointer
  font: inherit
  padding: 0
  margin-right: $small

  &:hover
    text-decoration: underline

  &.danger
    color: $accent-red

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

  &.inline
    flex-direction: row
    align-items: center
    gap: $small

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

.modal-actions
  display: flex
  justify-content: flex-end
  gap: $small
  margin-top: $medium
</style>
