<script setup lang="ts">
/**
 * ModerationAchievements — the achievement catalog: 13 categories + 52
 * tiers. Categories support PATCH (Title/Description/IconName/SortOrder/
 * IsActive), tiers support POST/PATCH/DELETE. A category's Metric is
 * immutable (bound to the server-side AchievementMetricResolver).
 */
import { computed, onMounted, reactive, ref, type Ref } from "vue";
import { useModal } from "vue-final-modal";
import {
  achievementApi,
  useAchievementCatalog,
  formatThreshold,
} from "@/entities/achievement";
import type {
  AchievementCategory,
  AchievementType,
} from "@/shared/api/models/achievements";
import { SecondaryText } from "@/shared/ui/Layout";
import { GameIcon } from "@/shared/ui/Icon";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { CaptionAction, CaptionLine } from "@/shared/ui/CaptionLine";
import Button from "@/shared/ui/Button/Button.vue";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { RowIconButton } from "@/shared/ui/RowIconButton";
import AchievementCategoryDialog from "./dialogs/AchievementCategoryDialog.vue";
import AchievementTierDialog from "./dialogs/AchievementTierDialog.vue";
import { useRoleGate } from "./lib/useRoleGate";
import { sectionRole } from "./lib/sections";

const { hasAccess, deniedText } = useRoleGate(
  sectionRole("moderation-achievements"),
);
const {
  adminCategories: categories,
  adminTypes: types,
  loadAdmin: load,
  reload,
} = useAchievementCatalog();

onMounted(() => load());

const sortedCategories = computed(() =>
  [...(categories.value ?? [])].sort((a, b) => a.sortOrder - b.sortOrder),
);

function tiersOf(c: AchievementCategory): AchievementType[] {
  return (types.value ?? [])
    .filter((t) => t.category.id === c.id)
    .sort((a, b) => a.threshold - b.threshold);
}

// The tiers of a category, in the site's table, as a table whose rows are
// added and removed in place is drawn (AttributeSchemaEditor): the pencil and
// the bin at the end of a row, "+ Тир" right under the table.
const tierColumns: Column[] = [
  { key: "tier", label: "Тир", align: "left" },
  { key: "code", label: "Код", align: "left" },
  { key: "title", label: "Название", align: "left" },
  { key: "threshold", label: "Порог", align: "left" },
  { key: "actions", label: "", width: "1px", align: "center" },
];

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

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone, so the interceptor keeps quiet about it.
async function confirmDeleteTier() {
  const target = deleteTierTarget.value;
  if (!target) return;
  const result = await achievementApi.deleteAchievementType(target.id, {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  if (!result.error) void reload();
  return result;
}
</script>

<template>
  <page-title>Достижения</page-title>

  <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

  <section v-else class="achievements-admin">
    <SecondaryText>
      Категория считает одно: игровые посты, дни на сайте, комментарии и так
      далее. Тир категории выдается, когда счет доходит до его порога.
    </SecondaryText>

    <ul class="cat-list">
      <li
        v-for="c in sortedCategories"
        :key="c.id"
        class="cat-row"
        :class="{ 'is-inactive': !c.isActive }"
      >
        <!-- The icon and the words of the category; its actions are the
             caption line under the description, at every width. They used to
             stand in a column of their own at the right, and on a phone the
             whole line scrolled sideways inside the card. -->
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
            <CaptionLine class="cat-actions">
              <CaptionAction @click="openEditCategory(c)"
                >Редактировать</CaptionAction
              >
            </CaptionLine>
          </div>
        </div>

        <DataTable
          class="tier-table"
          :columns="tierColumns"
          :data="tiersOf(c)"
          empty-text="Тиров нет"
          :aria-label="`Тиры: ${c.title}`"
        >
          <template #cell-code="{ row }">
            <code>{{ row.code }}</code>
          </template>
          <template #cell-threshold="{ row }">
            {{ formatThreshold(c.metric, row.threshold) }}
          </template>
          <template #cell-actions="{ row }">
            <RowIconButton
              icon="pencil"
              :label="`Редактировать тир ${row.title}`"
              @click="openEditTier(row)"
            />
            <RowIconButton
              icon="trash"
              :label="`Удалить тир ${row.title}`"
              @click="deleteTierTarget = row"
            />
          </template>
        </DataTable>
        <Button type="button" class="add-tier" @click="openCreateTier(c)">
          + Тир
        </Button>
      </li>
    </ul>

    <!-- Tier delete confirmation -->
    <ConfirmDialog
      :show="deleteTierTarget !== null"
      title="Удалить тир?"
      :message="`Уже полученные достижения тира &quot;${deleteTierTarget?.title ?? ''}&quot; останутся у пользователей`"
      confirm-label="Удалить"
      failure="Не удалось удалить тир"
      :action="confirmDeleteTier"
      @update:show="(v) => !v && (deleteTierTarget = null)"
    />
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

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

// The icon and the words: the words take what the icon leaves.
.cat-header
  display: flex
  align-items: flex-start
  gap: $medium
  margin-bottom: $small

// The codes break where the words do, at the comma, and inside a code only
// where a column is narrower than one of them.
.cat-info
  flex: 1 1 0
  min-width: 0
  overflow-wrap: anywhere

// A category spans all four tiers and has no metal of its own, so its icon
// is neutral: the metals are for a tier (see entities/achievement tierMetal).
.cat-icon
  flex: none
  width: 48px
  font-size: 36px
  color: $text-muted

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

// A caption line of its own under the description.
.cat-actions
  display: block
  margin-top: $tiny

// The site's table in the size this list has always had, its headers on the
// left as its cells are (Column.align).
.tier-table
  font-size: $secondary-font-size

  code
    background: $bg-element-accent
    padding: 0 4px
    border-radius: 3px

  // The two signs of a row on one line.
  :deep(.col-actions)
    white-space: nowrap

// The shared secondary button, under the table it adds a row to, at its left
// edge.
.add-tier
  margin-top: $small
</style>
