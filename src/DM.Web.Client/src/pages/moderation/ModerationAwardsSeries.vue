<script setup lang="ts">
import { formatDate } from "@/shared/lib/utils/datetime";
/**
 * ModerationAwardsSeries — contest series details: an edit form (topic
 * URL / IsActive), a grant form (award a user within this series) and the
 * list of previously granted awards with a revoke action.
 *
 * Series identity — id (uuid), taken from the `id` route param.
 */
import { computed, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import type {
  ContestSeries,
  ContestSeriesAward,
} from "@/shared/api/models/achievements";
import {
  achievementApi,
  useContestSeries,
  formatContestSeriesTitle,
} from "@/entities/achievement";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import { UserAutocomplete } from "@/entities/user";
import { GameIcon } from "@/shared/ui/Icon";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { UserLink } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import { describeFailure, notifyFailure } from "@/shared/lib/errors";
import { useRoleGate } from "./lib/useRoleGate";

const { hasAccess, deniedText } = useRoleGate("SeniorModerator");
const route = useRoute();
const router = useRouter();
const toast = useToast();
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
      grantError.value = describeFailure(error, "Не удалось выдать награду");
      return;
    }
    grantForm.value.username = "";
    grantForm.value.awardTypeId = "";
    grantForm.value.workUrl = "";
    await loadGrants();
    toast.success("Награда выдана");
  } finally {
    granting.value = false;
  }
}

// --- Grants list ---
// Everyone awarded in this series, straight from the server. It used to be an
// in-memory accumulator that never accumulated, over a UserAward model with no
// recipient — so the list was always empty and its revoke button would have
// sent an empty username.

const grants = ref<ContestSeriesAward[]>([]);
const grantsLoading = ref(false);

async function loadGrants() {
  if (!current.value) return;
  grantsLoading.value = true;
  const { data, error } = await achievementApi.getContestSeriesAwards(
    current.value.id,
  );
  grantsLoading.value = false;
  if (error) {
    notifyFailure(error, "Не удалось загрузить выданные награды");
    return;
  }
  grants.value = data?.resources ?? [];
}

const pendingRevoke = ref<ContestSeriesAward | null>(null);
const revoking = ref(false);

async function confirmRevoke() {
  const award = pendingRevoke.value;
  if (!award || revoking.value) return;

  revoking.value = true;
  const { error } = await achievementApi.revokeUserAward(
    award.user.username,
    award.id,
  );
  revoking.value = false;
  if (error) {
    notifyFailure(error, "Не удалось отозвать награду");
    return;
  }

  pendingRevoke.value = null;
  grants.value = grants.value.filter((a) => a.id !== award.id);
  toast.success("Награда отозвана");
}

// The grants follow the resolved series, so switching series from the list
// reloads them without depending on mount order.
watch(() => current.value?.id, loadGrants);

onMounted(async () => {
  await load();
  await loadGrants();
});
</script>

<template>
  <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

  <section v-else class="series-detail">
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
      {{ formatContestSeriesTitle(current.contestType, current.number) }},
      {{ current.year }}
    </BlockTitle>
    <SecondaryText v-else>Серия не найдена</SecondaryText>

    <template v-if="current">
      <!-- Edit section -->
      <section class="block">
        <h3>Параметры серии</h3>
        <div class="form-row">
          <label for="series-topic-url">Топик с итогами</label>
          <input
            id="series-topic-url"
            v-model="edit.topicUrl"
            type="url"
            placeholder="/forum/topic/..."
          />
        </div>
        <div class="form-row inline">
          <label>
            <input v-model="edit.isActive" type="checkbox" />
            Активна (видна в dropdown при выдаче)
          </label>
        </div>
        <Button type="button" :loading="editSaving" @click="saveEdit">
          Сохранить
        </Button>
      </section>

      <!-- Grant section -->
      <section class="block">
        <h3>Выдать награду</h3>
        <div class="form-row">
          <label for="grant-username">Пользователь</label>
          <UserAutocomplete
            id="grant-username"
            v-model="grantForm.username"
            placeholder="Введите имя..."
          />
        </div>
        <div class="form-row">
          <label for="grant-award-type">Тип награды</label>
          <select id="grant-award-type" v-model="grantForm.awardTypeId">
            <option value="" disabled>Не выбрано</option>
            <option v-for="t in awardTypes ?? []" :key="t.id" :value="t.id">
              {{ t.title }} ({{ t.description }})
            </option>
          </select>
        </div>
        <div class="form-row">
          <label for="grant-work-url">Топик с работой (опционально)</label>
          <input
            id="grant-work-url"
            v-model="grantForm.workUrl"
            type="url"
            placeholder="/forum/topic/..."
          />
        </div>
        <p v-if="grantError" class="form-error">{{ grantError }}</p>
        <Button
          type="button"
          :loading="granting"
          :disabled="!grantForm.username || !grantForm.awardTypeId"
          @click="grant"
        >
          Выдать
        </Button>
      </section>

      <!-- Grants list -->
      <section class="block">
        <h3>Выданные в этой серии</h3>
        <SecondaryText v-if="grantsLoading">Загрузка...</SecondaryText>
        <SecondaryText v-else-if="grants.length === 0">
          Награды в этой серии еще не выдавались
        </SecondaryText>
        <ul v-else class="grants-list">
          <li v-for="a in grants" :key="a.id" class="grant-row">
            <GameIcon :name="a.type.iconName" class="grant-icon" />
            <span class="grant-title">{{ a.type.title }}</span>
            <UserLink :user="a.user" />
            <span class="grant-date">{{ formatDate(a.awardedUtc) }}</span>
            <button
              type="button"
              class="text-button"
              @click="pendingRevoke = a"
            >
              Отозвать
            </button>
          </li>
        </ul>
      </section>
    </template>

    <ConfirmDialog
      :show="pendingRevoke !== null"
      title="Отзыв награды"
      :message="`Отозвать награду &quot;${pendingRevoke?.type.title ?? ''}&quot; у ${pendingRevoke?.user.username ?? ''}?`"
      confirm-label="Отозвать"
      danger
      :loading="revoking"
      @update:show="(v) => !v && (pendingRevoke = null)"
      @confirm="confirmRevoke"
    />
  </section>
</template>

<style scoped lang="sass">
.series-detail
  display: flex
  flex-direction: column
  gap: $medium

  &__header
    display: flex
    align-items: center

.block
  +card()

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

    &:focus:not(:focus-visible)
      outline: none
      border-color: $link

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
  grid-template-columns: 24px 1fr auto auto auto
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
