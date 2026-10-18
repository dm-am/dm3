<script setup lang="ts">
import { ref, onMounted, watch } from "vue";
import { fundraisingApi } from "@/entities/fundraising";
import { refusalHere } from "@/shared/lib/errors";
import { useSavedState } from "@/shared/lib/composables/useSavedState";
import { useRoleGate } from "./lib/useRoleGate";
import { sectionRole } from "./lib/sections";

const { hasAccess, deniedText } = useRoleGate(
  sectionRole("moderation-fundraising"),
);
const loading = ref(true);
const loadError = ref<string | null>(null);
const saving = ref(false);

const title = ref("");
const goalAmount = ref<number>(0);
const collectedAmount = ref<number>(0);

/** What the save was refused with, printed in the form under the fields. */
const saveError = ref<string | null>(null);

/** The fallback, for a refusal the server named nothing about. */
const SAVE_FAILED = "Не удалось сохранить сбор средств";

/**
 * What a save sends, and so the only thing the button compares the form
 * against. The title is trimmed here rather than at the request: a space typed
 * on the end of a saved title changes nothing the server would store, and the
 * button must not offer a save that would do nothing.
 */
function currentGoal() {
  return {
    title: title.value.trim(),
    goalAmount: goalAmount.value,
    collectedAmount: collectedAmount.value,
  };
}

/**
 * The form's own answer about the save. The page shows the three values and
 * nothing else, and a save leaves them exactly as they were on screen, so the
 * button is the only place the outcome can be read.
 */
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
    // The refusal goes into the form, beside the fields it is about and the
    // button that raised it, the way the guest forms put theirs
    // (features/auth/ui/LoginForm.vue, RegistrationForm.vue). Not into
    // `loadError` above: that one stands instead of the form, and it would
    // take away the three fields the administrator had just filled in.
    //
    // Every refusal stands in the form's line: a rejected form, where the
    // server named the field and the rule, and a refused action, a rate limit
    // or a dead server, whose plate the interceptor held is taken down. An
    // expired session and a dead connection stay with the plate.
    saveError.value = refusalHere(error, SAVE_FAILED);
  } else if (!data) {
    // An answer with neither a failure nor a body: nobody named anything.
    saveError.value = SAVE_FAILED;
  } else {
    title.value = data.resource.title;
    goalAmount.value = data.resource.goalAmount;
    collectedAmount.value = data.resource.collectedAmount;
    // The stored copy is taken from the fields the answer has just refilled,
    // so the button speaks about what the server keeps and not about what was
    // sent to it.
    remember();
  }
  saving.value = false;
}

// The refusal is about the values that were sent, so the first correction to
// any of them takes it off the screen - the same moment the button goes back
// to offering the save. A sentence left standing over changed fields is read
// as a verdict on what is in them now.
watch([title, goalAmount, collectedAmount], () => {
  saveError.value = null;
});

onMounted(load);
</script>

<template>
  <page-title>Сбор средств</page-title>

  <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

  <div v-else class="moderation-fundraising">
    <div v-if="loading" class="loading">Загрузка...</div>
    <div v-else-if="loadError" class="error">{{ loadError }}</div>
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
          maxlength="200"
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

.loading,
.error
  padding: $large

.error
  color: $accent-red

.fundraising-form
  max-width: 450px
  display: flex
  flex-direction: column
  gap: $medium
  padding: $medium
  background: $bg-element
  border: 1px solid $border

// The field of the site as it stands on a page (+input through InputsGlobal),
// only stretched to the form.
.form-field
  // A caption of a field of the page: +field-caption, as FormField draws it.
  label
    display: block
    margin-bottom: $minor
    +field-caption

  input
    width: 100%
    box-sizing: border-box

// The refusal line the rest of the site prints inside a form, in the same
// three declarations: features/edit-character/ui/CharacterForm.vue,
// features/attribute-schema-editor/ui/AttributeSchemaEditor.vue,
// ModerationAwardsSeries.vue. No margin of its own - the form is a flex column
// with a $medium gap, which spaces this like every other row in it. `.error`
// above is not reused: it belongs to the block that stands instead of the
// form, and carries that block's page padding.
.form-error
  margin: 0
  color: $accent-red
  font-size: $secondary-font-size

.form-actions
  display: flex
</style>
