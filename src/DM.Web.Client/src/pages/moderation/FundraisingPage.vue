<script setup lang="ts">
import { ref, onMounted, watch } from "vue";
import { fundraisingApi } from "@/entities/fundraising";
import { refusalHere } from "@/shared/lib/errors";
import { useSavedState } from "@/shared/lib/composables/useSavedState";
import { FUNDRAISING_TITLE_MAX_LENGTH } from "@/shared/lib/constants/community";
import { ErrorState } from "@/shared/ui/ErrorState";

const loading = ref(true);
const loadError = ref<string | null>(null);
const saving = ref(false);

const title = ref("");
const goalAmount = ref<number>(0);
const collectedAmount = ref<number>(0);

/** What the save was refused with, printed in the form under the fields. */
const saveError = ref<string | null>(null);

const SAVE_FAILED = "Не удалось сохранить сбор средств";

/** What a save sends: the title as typed. */
function currentGoal() {
  return {
    title: title.value,
    goalAmount: goalAmount.value,
    collectedAmount: collectedAmount.value,
  };
}

/** The button is the only place the outcome of a save can be read. */
const { saved, label, remember } = useSavedState(currentGoal);

async function load() {
  loading.value = true;
  loadError.value = null;
  const { data, error } = await fundraisingApi.getFundraising();
  if (error || !data) {
    loadError.value = "Не удалось загрузить данные сбора средств";
  } else {
    title.value = data.resource.title;
    goalAmount.value = data.resource.goalAmount;
    collectedAmount.value = data.resource.collectedAmount;
  }
  loading.value = false;
}

async function save() {
  saving.value = true;
  saveError.value = null;
  const { data, error } = await fundraisingApi.updateFundraising(currentGoal());
  if (error) {
    // In the form, beside the fields, not in `loadError`: that one replaces the
    // form and would take the filled fields away.
    saveError.value = refusalHere(error, SAVE_FAILED);
  } else if (!data) {
    saveError.value = SAVE_FAILED;
  } else {
    title.value = data.resource.title;
    goalAmount.value = data.resource.goalAmount;
    collectedAmount.value = data.resource.collectedAmount;
    // From the refilled fields, so the button speaks about what the server keeps.
    remember();
  }
  saving.value = false;
}

// A refusal left standing over changed fields reads as a verdict on them.
watch([title, goalAmount, collectedAmount], () => {
  saveError.value = null;
});

onMounted(load);
</script>

<template>
  <page-title>Сбор средств</page-title>

  <div class="moderation-fundraising">
    <!-- The form's twin in its own frame: the note, three fields and the button. -->
    <div v-if="loading" class="fundraising-form" aria-hidden="true">
      <div class="skeleton-line skeleton-note" />
      <div v-for="field in 3" :key="field" class="form-field">
        <div class="skeleton-line skeleton-caption" />
        <div class="skeleton-line skeleton-input" />
      </div>
      <div class="form-actions">
        <div class="skeleton-line skeleton-button" />
      </div>
    </div>
    <ErrorState v-else-if="loadError" :message="loadError" :retry="load" />
    <form v-else class="fundraising-form" @submit.prevent="save">
      <SecondaryText>
        Значения отображаются в блоке "Поддержка проекта" и на странице "Помочь
        проекту"
      </SecondaryText>
      <div class="form-field">
        <label for="fundraising-title">Название сбора</label>
        <input
          id="fundraising-title"
          v-model="title"
          type="text"
          :maxlength="FUNDRAISING_TITLE_MAX_LENGTH"
          required
        />
      </div>
      <div class="form-field">
        <label for="fundraising-goal">Цель (р.)</label>
        <input
          id="fundraising-goal"
          v-model.number="goalAmount"
          type="number"
          min="1"
          step="1"
          required
        />
      </div>
      <div class="form-field">
        <label for="fundraising-collected">Собрано (р.)</label>
        <input
          id="fundraising-collected"
          v-model.number="collectedAmount"
          type="number"
          min="0"
          step="1"
          required
        />
      </div>
      <p v-if="saveError" class="form-error" role="alert">{{ saveError }}</p>
      <div class="form-actions">
        <Button variant="primary" :loading="saving" :disabled="saved">{{
          label
        }}</Button>
      </div>
    </form>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/Skeleton" as *

// The goal's form: as wide as its fields need, not the page.
$fundraising-form-width: 450px

.fundraising-form
  max-width: $fundraising-form-width
  display: flex
  flex-direction: column
  gap: $medium
  padding: $medium
  background: $bg-element
  border: 1px solid $border

// The page's field (+input), stretched to the form.
.form-field
  label
    display: block
    margin-bottom: $minor
    +field-caption

  input
    width: 100%
    box-sizing: border-box

// The form's refusal line. No margin: the form is a flex column with a gap.
.form-error
  margin: 0
  color: $accent-red
  font-size: $secondary-font-size

.form-actions
  display: flex

// Line boxes of the type they wait for, and controls of a control's height.
.skeleton-line
  +skeleton-shimmer

// The note above the fields takes two lines at the form's width.
.skeleton-note
  height: 2.6em
  font-size: $secondary-font-size

.skeleton-caption
  width: 30%
  height: 1.3em
  font-size: $secondary-font-size
  margin-bottom: $minor

.skeleton-input
  height: $control-height

.skeleton-button
  width: 30%
  height: $control-height
</style>
