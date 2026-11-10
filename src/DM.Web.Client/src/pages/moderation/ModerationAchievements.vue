<script setup lang="ts">
/** The achievement catalog. A category's metric is fixed: the server resolves it. */
import { computed, onMounted, reactive, ref, type Ref } from "vue";
import { useModal } from "vue-final-modal";
import {
  achievementApi,
  useAchievementCatalog,
  formatThreshold,
  nextChainTier,
  tierWord,
} from "@/entities/achievement";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import type {
  AchievementCategory,
  AchievementType,
} from "@/shared/api/models/achievements";
import { SecondaryText } from "@/shared/ui/Layout";
import GameIcon from "@/shared/ui/Icon/GameIcon.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { ErrorState } from "@/shared/ui/ErrorState";
import { CaptionAction, CaptionLine } from "@/shared/ui/CaptionLine";
import Button from "@/shared/ui/Button/Button.vue";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { RowIconButton } from "@/shared/ui/RowIconButton";
import AchievementCategoryDialog from "./dialogs/AchievementCategoryDialog.vue";
import AchievementTierDialog from "./dialogs/AchievementTierDialog.vue";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const {
  adminCategories: categories,
  adminTypes: types,
  adminFailed: failed,
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

// The pencil and the bin at the end of a row, "+ Тир" under the table, as in AttributeSchemaEditor.
const tierColumns: Column[] = [
  { key: "tier", label: "Тир", align: "left" },
  { key: "code", label: "Код", align: "left" },
  { key: "title", label: "Название", align: "left" },
  { key: "threshold", label: "Порог", align: "left" },
  { key: "actions", label: "", width: "1px", align: "center" },
];

// Set before the dialog first mounts, so the non-null cast below is safe.
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

const editingTier = ref<AchievementType | null>(null);
const tierCategoryId = ref("");
const tierDefault = ref<number | null>(null);

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
  tierDefault.value = nextChainTier(tiersOf(c).map((t) => t.tier));
  openTierDialog();
}

function openEditTier(t: AchievementType) {
  editingTier.value = t;
  tierCategoryId.value = t.category.id;
  tierDefault.value = t.tier ?? 1;
  openTierDialog();
}

const deleteTierTarget = ref<AchievementType | null>(null);

// The window waits for it, prints a refusal in itself and closes on success alone.
async function confirmDeleteTier() {
  const target = deleteTierTarget.value;
  if (!target) return;
  const result = await achievementApi.deleteAchievementType(
    target.id,
    OWNS_REFUSAL,
  );
  if (!result.error) void reload();
  return result;
}
</script>

<template>
  <page-title>Достижения</page-title>

  <section class="achievements-admin">
    <SecondaryText>
      Категория считает одно: игровые посты, дни на сайте, комментарии и так
      далее. Тир категории выдается, когда счет доходит до его порога.
    </SecondaryText>

    <ErrorState
      v-if="failed"
      message="Не удалось загрузить достижения"
      :retry="load"
    />

    <ul v-else class="cat-list">
      <li
        v-for="c in sortedCategories"
        :key="c.id"
        class="cat-row"
        :class="{ 'is-inactive': !c.isActive }"
      >
        <!-- The actions are the caption line under the description, at every width. -->
        <div class="cat-header">
          <GameIcon :name="c.iconName" class="cat-icon" />
          <div class="cat-info">
            <div class="cat-title">{{ c.title }}</div>
            <div class="cat-meta">
              <code>{{ c.code }}</code
              >, <code>{{ c.metric }}</code
              >, <span class="cat-order">порядок {{ c.sortOrder }}</span>
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
          <template #cell-tier="{ row }">
            {{ tierWord(row.tier) ?? VALUE_UNAVAILABLE }}
          </template>
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
@use "@/pages/moderation/lib/catalogCode" as *

.achievements-admin
  display: flex
  flex-direction: column
  gap: $medium

  // The codes of a category and of its tiers.
  code
    +catalog-code

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
    opacity: $muted-opacity

// The icon and the words: the words take what the icon leaves.
.cat-header
  display: flex
  align-items: flex-start
  gap: $medium
  margin-bottom: $small

// Codes break at the comma, and inside a code only where a column is narrower.
.cat-info
  flex: 1 1 0
  min-width: 0
  overflow-wrap: anywhere

// A category has no metal of its own: the metals are a tier's (tierMetal).
.cat-icon
  flex: none
  width: $catalog-icon-box
  font-size: $catalog-icon-size
  color: $text-muted

.cat-title
  font-weight: 400
  margin-bottom: $tiny

.cat-meta
  font-size: $tertiary-font-size
  color: $text-muted

// A number stays with its word: "порядок 3".
.cat-order
  white-space: nowrap

.cat-desc
  margin: $tiny 0 0
  font-size: $secondary-font-size
  color: $text

.cat-actions
  display: block
  margin-top: $tiny

// Headers on the left, as the cells are (Column.align).
.tier-table
  font-size: $secondary-font-size

  :deep(.col-actions)
    white-space: nowrap

// Under the table it adds a row to, at its left edge.
.add-tier
  margin-top: $small
</style>
