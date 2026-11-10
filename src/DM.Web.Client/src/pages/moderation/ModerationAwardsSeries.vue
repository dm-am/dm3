<script setup lang="ts">
import { formatDate } from "@/shared/lib/utils/datetime";
import { computed, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import type {
  ContestSeries,
  ContestSeriesAward,
} from "@/shared/api/models/achievements";
import {
  achievementApi,
  awardMetal,
  metalStyle,
  useContestSeries,
  formatContestSeriesTitle,
} from "@/entities/achievement";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import { UserAutocomplete } from "@/entities/user";
import GameIcon from "@/shared/ui/Icon/GameIcon.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { UserLink } from "@/entities/user";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { refusalHere } from "@/shared/lib/errors";
import { ActionResult } from "@/shared/ui/ActionResult";
import { ErrorState } from "@/shared/ui/ErrorState";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const route = useRoute();
const router = useRouter();
const {
  adminSeries: series,
  awardTypes,
  failed,
  adminFailed,
  load: loadPublic,
  loadAdmin,
  reload,
} = useContestSeries();

// The series from the admin catalog (a hidden one must still open to be
// restored), the award types from the public one (a hidden one is not offered).
const load = (fresh = false) =>
  Promise.all([loadPublic(fresh), loadAdmin(fresh)]);

/** A refused read of either catalog: the series shown, if any, is not the answer. */
const readFailed = computed(() => failed.value || adminFailed.value);

const seriesId = computed(() => String(route.params.id));
const current = computed<ContestSeries | null>(
  () => series.value?.find((s) => s.id === seriesId.value) ?? null,
);

// The public slice, so a hidden type is not offered.
const awardTypeOptions = computed<SelectOption[]>(() =>
  (awardTypes.value ?? []).map((t) => ({
    value: t.id,
    label: `${t.title} (${t.description})`,
  })),
);

const edit = ref({ topicUrl: "", isActive: true });
const editSaving = ref(false);

/** A refused "Сохранить", on the line under the fields; cleared by the next press. */
const editRefusal = ref<string | null>(null);

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
  editRefusal.value = null;
  try {
    const { error } = await achievementApi.updateContestSeries(
      current.value.id,
      {
        topicUrl: edit.value.topicUrl.trim(),
        isActive: edit.value.isActive,
      },
    );
    if (error) {
      editRefusal.value = refusalHere(error, "Не удалось сохранить серию");
      return;
    }
    await reload();
  } finally {
    editSaving.value = false;
  }
}

/** The fields of the two forms, for their skeleton while the series is read. */
const SKELETON_FORMS = [
  { fields: 1, checks: 1 },
  { fields: 3, checks: 0 },
];

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
      // The strip under the form is the one place a rejected grant is answered;
      // an expired session and a dead connection stay with the plate.
      grantError.value = refusalHere(error, "Не удалось выдать награду");
      return;
    }
    grantForm.value.username = "";
    grantForm.value.awardTypeId = "";
    grantForm.value.workUrl = "";
    await loadGrants();
  } finally {
    granting.value = false;
  }
}

const grants = ref<ContestSeriesAward[]>([]);
const grantsLoading = ref(false);

const GRANTS_FAILED = "Не удалось загрузить выданные награды";

/**
 * Why the list did not arrive, printed where the list would stand. The api
 * method has no seam for `ownsRefusal`, so the statuses the interceptor
 * announces are left to it. Cleared by the next read.
 */
const grantsRefusal = ref("");

/** A refused read makes no claim that nobody was awarded in the series. */
const grantsFailed = ref(false);

async function loadGrants() {
  if (!current.value) return;
  grantsLoading.value = true;
  grantsRefusal.value = "";
  grantsFailed.value = false;
  const { data, error } = await achievementApi.getContestSeriesAwards(
    current.value.id,
  );
  grantsLoading.value = false;
  if (error) {
    // The rows of an earlier read are not this answer.
    grants.value = [];
    grantsFailed.value = true;
    grantsRefusal.value = refusalHere(error, GRANTS_FAILED) ?? "";
    return;
  }
  grants.value = data?.resources ?? [];
}

const pendingRevoke = ref<ContestSeriesAward | null>(null);

// The window waits for it, prints a refusal in itself and closes on success alone.
async function confirmRevoke() {
  const award = pendingRevoke.value;
  if (!award) return;
  const result = await achievementApi.revokeUserAward(
    award.user.username,
    award.id,
    OWNS_REFUSAL,
  );
  if (!result.error) {
    grants.value = grants.value.filter((a) => a.id !== award.id);
  }
  return result;
}

// Follows the resolved series, so a switch reloads without depending on mount order.
watch(() => current.value?.id, loadGrants);

onMounted(async () => {
  await load();
  await loadGrants();
});
</script>

<template>
  <page-title>Серия конкурсов</page-title>

  <section class="series-detail">
    <header class="series-detail__header">
      <button
        type="button"
        class="text-button"
        @click="router.push({ name: 'moderation-awards' })"
      >
        ← К списку серий
      </button>
    </header>

    <!-- Unread, the title and the two forms stand as their skeleton: not "not found" yet. -->
    <ErrorState
      v-if="readFailed"
      message="Не удалось загрузить серию"
      :retry="() => load(true)"
    />
    <template v-else-if="series === null">
      <div class="skeleton-line skeleton-title" aria-hidden="true" />
      <section
        v-for="(rows, index) in SKELETON_FORMS"
        :key="index"
        class="block"
        aria-hidden="true"
      >
        <div class="skeleton-line skeleton-heading" />
        <div v-for="row in rows.fields" :key="row" class="form-row">
          <div class="skeleton-line skeleton-caption" />
          <div class="skeleton-line skeleton-field" />
        </div>
        <div v-for="row in rows.checks" :key="row" class="form-row">
          <div class="skeleton-line skeleton-caption" />
        </div>
        <div class="skeleton-line skeleton-button" />
      </section>
    </template>
    <BlockTitle v-else-if="current">
      {{ formatContestSeriesTitle(current.contestType, current.number) }},
      {{ current.year }}
    </BlockTitle>
    <SecondaryText v-else>Серия не найдена</SecondaryText>

    <template v-if="current && !readFailed">
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
            Активна (видна в списке при выдаче)
          </label>
        </div>
        <ActionResult :text="editRefusal" class="edit-refusal" />
        <Button
          type="button"
          variant="primary"
          :loading="editSaving"
          @click="saveEdit"
        >
          Сохранить
        </Button>
      </section>

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
          <Select
            id="grant-award-type"
            v-model="grantForm.awardTypeId"
            :options="awardTypeOptions"
            placeholder="Не выбрано"
          />
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
          variant="primary"
          :loading="granting"
          :disabled="!grantForm.username || !grantForm.awardTypeId"
          @click="grant"
        >
          Выдать
        </Button>
      </section>

      <section class="block">
        <h3>Выданные в этой серии</h3>
        <!-- Text: how many were awarded is anyone's guess. A read again keeps the rows. -->
        <SecondaryText v-if="grantsLoading && !grants.length">
          Загрузка...
        </SecondaryText>
        <!-- Why the list did not arrive, in its place. -->
        <p v-else-if="grantsRefusal" class="form-error" role="alert">
          {{ grantsRefusal }}
        </p>
        <SecondaryText v-else-if="grants.length === 0 && !grantsFailed">
          Награды в этой серии еще не выдавались
        </SecondaryText>
        <ul
          v-else-if="grants.length > 0"
          class="grants-list"
          :aria-busy="grantsLoading || undefined"
        >
          <li v-for="a in grants" :key="a.id" class="grant-row">
            <GameIcon
              :name="a.type.iconName"
              class="grant-icon"
              :style="metalStyle(awardMetal(a.type.tier))"
            />
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
      title="Отозвать награду?"
      :message="`Награда &quot;${pendingRevoke?.type.title ?? ''}&quot; пропадет из профиля &quot;${pendingRevoke?.user.username ?? ''}&quot;`"
      confirm-label="Отозвать"
      failure="Не удалось отозвать награду"
      :action="confirmRevoke"
      @update:show="(v) => !v && (pendingRevoke = null)"
    />
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/Links" as *
@use "@/assets/styles/Skeleton" as *

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
  gap: $minor
  margin-bottom: $small

  &.inline
    flex-direction: row
    align-items: center
    gap: $small

  label
    +field-caption

  // The words of a checkbox are not a caption: regular weight.
  &.inline label
    color: $text-muted
    font-weight: 400

.text-button
  +inline-link-button

// Above the button, where the grant form says its refusal.
.edit-refusal
  --action-result-margin: #{$small} 0

// Twins of what they wait for: a line box of the text, a field and a button of a control's height.
.skeleton-line
  +skeleton-shimmer

// BlockTitle: its margins, a line of 16px type.
.skeleton-title
  width: 40%
  height: 1.3em
  font-size: $font-size
  margin: $medium 0 $small

// The h3 of a block, a line of its type, as far from the first row.
.skeleton-heading
  width: 30%
  height: 1.3em
  font-size: $secondary-font-size
  margin-bottom: $small

.skeleton-caption
  width: 25%
  height: 1.3em
  font-size: $secondary-font-size

.skeleton-field
  height: $control-height

.skeleton-button
  width: 25%
  height: $control-height

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

  // The metal of the award, as the profile draws it (tierMetal).
  .grant-icon
    font-size: 20px
    color: var(--tier-metal, $text-muted)

  .grant-date
    font-variant-numeric: tabular-nums
    color: $text-meta
    font-size: $secondary-font-size
</style>
