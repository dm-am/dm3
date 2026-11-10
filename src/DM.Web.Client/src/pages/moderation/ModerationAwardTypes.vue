<script setup lang="ts">
import { onMounted, ref, reactive, type Ref } from "vue";
import { useModal } from "vue-final-modal";
import {
  achievementApi,
  awardMetal,
  metalStyle,
  useContestSeries,
} from "@/entities/achievement";
import type { AwardType } from "@/shared/api/models/achievements";
import { SecondaryText } from "@/shared/ui/Layout";
import GameIcon from "@/shared/ui/Icon/GameIcon.vue";
import { ActionResult } from "@/shared/ui/ActionResult";
import { ErrorState } from "@/shared/ui/ErrorState";
import { refusalHere } from "@/shared/lib/errors";
import AwardTypeEditDialog from "./dialogs/AwardTypeEditDialog.vue";

// A sub-page of the awards tab, so it wears that tab's row.
const {
  adminAwardTypes: awardTypes,
  adminFailed: failed,
  loadAdmin: load,
  reload,
} = useContestSeries();

onMounted(() => load());

// Set before the dialog first mounts, so the non-null cast below is safe.
const editing = ref<AwardType | null>(null);

const { open: openEditDialog, close: closeEditDialog } = useModal({
  component: AwardTypeEditDialog,
  attrs: reactive({
    awardType: editing as Ref<AwardType>,
    onSuccess: async () => {
      closeEditDialog();
      await reload();
    },
    onCancel: () => closeEditDialog(),
  }),
});

function openEdit(t: AwardType) {
  editing.value = t;
  openEditDialog();
}

/** A refused "Скрыть" or "Вернуть", on a line under the row it was pressed in. */
const toggleRefusal = ref<{ id: string; text: string } | null>(null);

async function toggleActive(t: AwardType) {
  toggleRefusal.value = null;
  const { error } = await achievementApi.updateAwardType(t.id, {
    isActive: !t.isActive,
  });
  if (error) {
    const text = refusalHere(
      error,
      t.isActive
        ? "Не удалось скрыть тип награды"
        : "Не удалось вернуть тип награды",
    );
    if (text) toggleRefusal.value = { id: t.id, text };
    return;
  }
  await reload();
}
</script>

<template>
  <page-title>Каталог типов наград</page-title>

  <section class="award-types-admin">
    <SecondaryText>
      Типы наград общие для всех серий конкурсов. Скрытый тип не предлагается
      при выдаче награды, а уже выданные награды этого типа остаются.
    </SecondaryText>

    <ErrorState
      v-if="failed"
      message="Не удалось загрузить типы наград"
      :retry="() => load(true)"
    />

    <ul v-else class="types-list">
      <li
        v-for="t in awardTypes ?? []"
        :key="t.id"
        class="type-row"
        :class="{ 'is-inactive': !t.isActive }"
      >
        <GameIcon
          :name="t.iconName"
          class="type-icon"
          :style="metalStyle(awardMetal(t.tier))"
        />
        <div class="type-info">
          <div class="type-title">
            {{ t.title }}
            <span v-if="t.tier" class="type-tier">тир {{ t.tier }}</span>
          </div>
          <div class="type-meta">
            <code>{{ t.code }}</code
            >, <code>{{ t.iconName }}</code
            >, <span class="type-order">порядок {{ t.sortOrder }}</span>
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
        <ActionResult
          v-if="toggleRefusal?.id === t.id"
          :text="toggleRefusal.text"
          class="type-refusal"
        />
      </li>
    </ul>
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *
@use "@/pages/moderation/lib/catalogCode" as *

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

  &.is-inactive
    opacity: $muted-opacity

// A line of its own under the row it answers, inside the row's frame.
.type-refusal
  grid-column: 1 / -1
  --action-result-margin: 0

// Codes break at the comma, and inside a code only where a column is narrower.
.type-info
  overflow-wrap: anywhere

// The metal the profile draws this award in (tierMetal); without one it stays neutral.
.type-icon
  font-size: 40px
  color: var(--tier-metal, $text-muted)

.type-title
  font-weight: 400
  margin-bottom: $tiny

// A number stays with its word: "тир 1", "порядок 3".
.type-tier,
.type-order
  white-space: nowrap

.type-tier
  font-size: $tertiary-font-size
  color: $text-meta
  margin-left: $small

.type-meta
  font-size: $tertiary-font-size
  color: $text-muted

  code
    +catalog-code

.type-desc
  margin: $tiny 0 0
  font-size: $secondary-font-size
  color: $text

.type-actions
  display: flex
  flex-direction: column
  gap: $tiny

.text-button
  text-align: left
  +inline-link-button
</style>
