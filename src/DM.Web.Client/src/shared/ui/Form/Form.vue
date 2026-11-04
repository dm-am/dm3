<script setup lang="ts">
import { computed } from "vue";
import Button from "@/shared/ui/Button/Button.vue";
import { ActionResult } from "@/shared/ui/ActionResult";
import { useDialogForm, useInDialog } from "@/shared/ui/Layout/dialogContext";

const props = defineProps<{
  valid?: boolean;
  /** The caller's own request is in flight. In a window with an action the window says it. */
  loading?: boolean;
  action?: string;
  cancel?: string;
  /** On a page: why the last press was refused. A window says its own. */
  refusal?: string | null;
}>();
const emit = defineEmits<{ submit: []; cancel: [] }>();
const slots = defineSlots<{
  default?: () => unknown;
  controls?: () => unknown;
}>();

const inDialog = useInDialog();

/** In a window with an action the window runs it, and the form emits neither event. */
const dialog = useDialogForm();
const routed = computed(() => dialog?.routed.value ?? false);

const busy = computed(() =>
  routed.value ? (dialog?.busy.value ?? false) : props.loading,
);
const shut = computed(
  () => props.valid === false || (routed.value && !!dialog?.waiting.value),
);
const dialogRefusal = computed(() =>
  routed.value ? dialog?.refusal.value : "",
);

const submit = () => (routed.value ? dialog?.submit() : emit("submit"));
const handleCancel = () => (routed.value ? dialog?.cancel() : emit("cancel"));
</script>

<template>
  <!-- No form-level autocomplete="off": some browsers read it as a veto over the fields' own. -->
  <form :class="{ 'form--dialog': inDialog }" @submit.prevent="submit">
    <slot />
    <p v-if="dialogRefusal" class="form-refusal" role="alert">
      {{ dialogRefusal }}
    </p>
    <ActionResult v-else-if="!routed" :text="refusal" />
    <div v-if="slots.controls" class="controls">
      <slot name="controls" />
    </div>
    <div v-else-if="action" class="controls">
      <Button variant="primary" :disabled="shut" :loading="busy">{{
        action
      }}</Button>
      <Button
        v-if="cancel"
        type="button"
        :disabled="routed && busy"
        @click="handleCancel"
      >
        {{ cancel }}
      </Button>
    </div>
  </form>
</template>

<style scoped lang="sass">
@use "@/assets/styles/DialogSurface" as *

// The step collapses with the last field's own margin of 8.
.controls
  display: flex
  gap: $small
  margin-top: $medium

// The window's row; its step collapses with the field's own, so it is 16 and not 32.
.form--dialog .controls
  +dialog-actions
  margin: $dialog-field-step 0 0

// No margin of its own: the steps over and under it collapse into one.
.form--dialog .form-refusal
  +dialog-refusal
</style>
