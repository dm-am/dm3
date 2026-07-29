<script setup lang="ts">
/**
 * ModerationAchievements — the achievement catalog: 13 categories + 52
 * tiers. Categories support PATCH (Title/Description/IconName/SortOrder/
 * IsActive), tiers support POST/PATCH/DELETE. A category's Metric is
 * immutable (bound to the server-side AchievementMetricResolver).
 */
import { computed, onMounted, reactive, ref, type Ref } from "vue";
import { useModal } from "vue-final-modal";
import { achievementApi } from "@/shared/api";
import type {
  AchievementCategory,
  AchievementType,
} from "@/shared/api/models/achievements";
import { useAchievementCatalog } from "@/shared/lib/achievements/useAchievementCatalog";
import { formatThreshold } from "@/shared/lib/achievements/formatThreshold";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import { GameIcon } from "@/shared/ui/Icon";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import AchievementCategoryDialog from "./dialogs/AchievementCategoryDialog.vue";
import AchievementTierDialog from "./dialogs/AchievementTierDialog.vue";

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

// --- Category edit dialog (shared Dialog idiom) ---
// Null until the first openEditCategory sets it; the dialog only mounts
// after that, so the non-null cast below is safe.
const editingCategory = ref<AchievementCategory | null>(null);

const { open: openCategoryDialog, close: closeCategoryDialog } = useModal({
  component: AchievementCategoryDialog,
  attrs: reactive({
    category: editingCategory as Ref<AchievementCategory>,
    onSuccess: async () => {
      closeCategoryDialog();
      await reload();
    },
    onCancel: () => closeCategoryDialog(),
  }),
});

function openEditCategory(c: AchievementCategory) {
  editingCategory.value = c;
  openCategoryDialog();
}

// --- Tier create/edit dialog (shared Dialog idiom) ---
const editingTier = ref<AchievementType | null>(null);
const tierCategoryId = ref("");
const tierDefault = ref(1);

const { open: openTierDialog, close: closeTierDialog } = useModal({
  component: AchievementTierDialog,
  attrs: reactive({
    tier: editingTier,
    categoryId: tierCategoryId,
    defaultTier: tierDefault,
    onSuccess: async () => {
      closeTierDialog();
      await reload();
    },
    onCancel: () => closeTierDialog(),
  }),
});

function openCreateTier(c: AchievementCategory) {
  editingTier.value = null;
  tierCategoryId.value = c.id;
  const maxTier = tiersOf(c).reduce((m, t) => Math.max(m, t.tier ?? 0), 0);
  tierDefault.value = maxTier + 1;
  openTierDialog();
}

function openEditTier(t: AchievementType) {
  editingTier.value = t;
  tierCategoryId.value = t.category.id;
  tierDefault.value = t.tier ?? 1;
  openTierDialog();
}

// --- Tier delete (ConfirmDialog) ---
const deleteTierTarget = ref<AchievementType | null>(null);
const deletingTier = ref(false);

async function confirmDeleteTier() {
  if (!deleteTierTarget.value || deletingTier.value) return;
  deletingTier.value = true;
  try {
    await achievementApi.deleteAchievementType(deleteTierTarget.value.id);
    deleteTierTarget.value = null;
    await reload();
  } finally {
    deletingTier.value = false;
  }
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
              <code>{{ c.code }}</code
              >, <code>{{ c.metric }}</code
              >, sort {{ c.sortOrder }}
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
                  @click="deleteTierTarget = t"
                >
                  Del
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </li>
    </ul>

    <!-- Tier delete confirmation -->
    <ConfirmDialog
      :show="deleteTierTarget !== null"
      title="Удаление тира"
      :message="`Удалить тир &quot;${deleteTierTarget?.title ?? ''}&quot;? Уже выданные UserAchievement останутся.`"
      confirm-label="Удалить"
      danger
      :loading="deletingTier"
      @update:show="(v) => !v && (deleteTierTarget = null)"
      @confirm="confirmDeleteTier"
    />
  </section>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"
@import "@/assets/styles/Tables"

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
  +card()

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

// Site table idiom: 1px gaps painted by the $border background
// (same visual language as DataTable / +table)
.tier-table
  width: 100%
  border-collapse: separate
  border-spacing: 1px
  background-color: $border
  font-size: $secondary-font-size

  th
    text-align: left
    +table-header

  td
    text-align: left
    vertical-align: middle
    +table-row

  tbody tr
    +table-row-hover

  code
    background: $bg-element-accent
    padding: 0 4px
    border-radius: 3px

.text-button
  margin-right: $small
  +inline-link-button

  &.danger
    &,
    &:hover:not(:disabled)
      color: $accent-red
</style>
