<script setup lang="ts">
/**
 * ModerationAwardTypes — the award-type catalog (timeless).
 * Edit title / description / icon / tier / sortOrder. Deactivate (soft).
 * Admin only — the route is gated in the router meta.
 */
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
import { GameIcon } from "@/shared/ui/Icon";
import AwardTypeEditDialog from "./dialogs/AwardTypeEditDialog.vue";
import { useRoleGate } from "./lib/useRoleGate";
import { sectionRole } from "./lib/sections";

// A sub-page of the awards tab, so it wears that tab's row.
const { hasAccess, deniedText } = useRoleGate(sectionRole("moderation-awards"));
const {
  adminAwardTypes: awardTypes,
  loadAdmin: load,
  reload,
} = useContestSeries();

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
  <page-title>Каталог типов наград</page-title>

  <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

  <section v-else class="award-types-admin">
    <SecondaryText>
      Типы наград общие для всех серий конкурсов. Скрытый тип не предлагается
      при выдаче награды, а уже выданные награды этого типа остаются.
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
      </li>
    </ul>
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

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
    opacity: 0.55

// The codes break where the words do, at the comma, and inside a code only
// where a column is narrower than one of them.
.type-info
  overflow-wrap: anywhere

// The metal the profile draws this award in (see entities/achievement
// tierMetal); a tier without one stays neutral, as it does there.
.type-icon
  font-size: 40px
  color: var(--tier-metal, $text-muted)

.type-title
  font-weight: 500
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
