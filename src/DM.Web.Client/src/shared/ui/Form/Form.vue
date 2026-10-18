<template>
  <!-- No form-level autocomplete="off": part of the browsers read it as a veto
       over the fields' own autocomplete, and every value that matters here is
       declared on the field (email, current-password, new-password, the
       support contact). Anything that must not be filled - the bot traps, the
       username and date inputs - carries its own "off". -->
  <form :class="{ 'form--dialog': inDialog }" @submit.prevent="submit">
    <slot />
    <p v-if="dialogRefusal" class="form-refusal" role="alert">
      {{ dialogRefusal }}
    </p>
    <!-- On a page the refusal of the press stands over the footer strip, at
         the place the reader pressed (UI_STANDARDS, "Всплывающие сообщения"). -->
    <ActionResult v-else-if="!routed" :text="pageRefusal" />
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
  /**
   * On a page: why the last press was refused, from the page's
   * useActionResult. A window says its own and ignores this.
   */
  refusal?: string | null;
}>();
const emit = defineEmits(["submit", "cancel"]);
const slots = defineSlots();

/** A form in a window takes the window's row of buttons (see the styles). */
const inDialog = useInDialog();

/**
 * A form in a window that runs an action hands its buttons to the window: the
 * main one runs the action, "Отмена" leaves the way the cross does, and the
 * wait, the refusal and the closing are the window's. The form then emits
 * neither of its events; everywhere else it emits both, as it always has.
 */
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
const pageRefusal = computed(() => props.refusal ?? null);

const submit = () => (routed.value ? dialog?.submit() : emit("submit"));
const handleCancel = () => (routed.value ? dialog?.cancel() : emit("cancel"));
</script>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/DialogSurface" as *

// The buttons of a form on a page: a row under the last field, one step of 16
// below it, the main action first. No strip (owner's standard, 27 September
// 2026): the grey footer that ran across the foot of the card was worn by the
// six forms built on this component, and every other form of the site ended
// with its buttons right under the fields. The buttons keep the page's height.
// The step collapses with the last field's own margin of 8.
.controls
  display: flex
  gap: $small
  margin-top: $medium

// In a window the row is the window's: two equal halves across the card at
// the window's control height, one field step under the last field. The step
// collapses with the field's own, so it is 16 and not 32.
.form--dialog .controls
  +dialog-actions
  margin: $dialog-field-step 0 0

// The refusal of the window's request, between the last field and the row. It
// takes no margin of its own: the last field's step stands over it and the
// row's step under it, both collapsing into one step of 16.
.form--dialog .form-refusal
  +dialog-refusal
</style>
