<script setup lang="ts">
/**
 * ModerationAwardTypes — the award-type catalog (timeless).
 * Edit title / description / icon / tier / sortOrder. Deactivate (soft).
 * Admin only — the route is gated in the router meta.
 */
import { onMounted, ref, reactive, type Ref } from "vue";
import { useModal } from "vue-final-modal";
import { achievementApi, useContestSeries } from "@/entities/achievement";
import type { AwardType } from "@/shared/api/models/achievements";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import { GameIcon } from "@/shared/ui/Icon";
import AwardTypeEditDialog from "./dialogs/AwardTypeEditDialog.vue";
import { useRoleGate } from "./lib/useRoleGate";

const { hasAccess, deniedText } = useRoleGate("SeniorModerator");
const { awardTypes, load, reload } = useContestSeries();

onMounted(() => load());

// --- Edit dialog (shared Dialog idiom) ---
// Null until the first openEdit sets it; the dialog only mounts after that,
// so the non-null cast below is safe.
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

async function toggleActive(t: AwardType) {
  await achievementApi.updateAwardType(t.id, { isActive: !t.isActive });
  await reload();
}
</script>

<template>
  <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

  <section v-else class="award-types-admin">
    <BlockTitle>Каталог типов наград</BlockTitle>
    <SecondaryText>
      Timeless каталог: 6 типов, переиспользуются между всеми сериями конкурсов.
      Деактивация скрывает тип из dropdown при выдаче, исторические награды
      сохраняются.
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
            <code>{{ t.code }}</code
            >, <code>{{ t.iconName }}</code
            >, sort {{ t.sortOrder }}
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
  </section>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

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
    color: var(--award-gold)
  &.tier-2
    color: var(--award-silver)
  &.tier-3
    color: var(--award-bronze)
  &.tier-5
    color: var(--award-diamond)

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
  text-align: left
  +inline-link-button
</style>
