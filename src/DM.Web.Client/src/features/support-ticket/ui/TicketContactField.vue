<script lang="ts">
import { TICKET_CONTACT_MAX_LENGTH } from "@/entities/ticket";
import { validators } from "@/shared/lib/composables/useValidatedField";
import {
  EMAIL_INVALID,
  readFieldError,
} from "@/shared/lib/errors/validationErrors";

// The shape of an address as the field of type email reads it; the server has the last word.
const emailProblem = validators.email();

export const contactProblem = (contact: string): string => {
  const address = contact.trim();
  if (!address) return "Empty";
  const shape = emailProblem(address);
  if (shape) return shape;
  if (address.length > TICKET_CONTACT_MAX_LENGTH) return "Long";
  return "";
};

// In the wording of the form's own check, so one mistake is not named in two ways.
export const contactRefusal = (fields: Record<string, string[]>): string =>
  fields.contact?.[0] === "Invalid"
    ? EMAIL_INVALID
    : (readFieldError(fields, "contact") ?? "");
</script>

<script setup lang="ts">
const contact = defineModel<string>({ required: true });

defineProps<{
  isGuest: boolean;
  username: string;
  error: string;
  disabled: boolean;
}>();
</script>

<template>
  <form-field
    v-if="isGuest"
    label="Контактные данные"
    name="contact"
    :errors="error ? [error] : []"
  >
    <template #hint>Укажите почту</template>
    <input
      id="contact"
      v-model="contact"
      type="email"
      :maxlength="TICKET_CONTACT_MAX_LENGTH"
      autocomplete="email"
      :disabled="disabled"
    />
  </form-field>

  <!-- Read-only, not disabled: the name is read out and copied, only typing is refused. -->
  <form-field v-else label="Контактные данные" name="sender">
    <span class="sender-plate">
      <span class="sender-ground" aria-hidden="true" />
      <input id="sender" type="text" :value="username" readonly />
    </span>
  </form-field>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Buttons" as *

// The disabled ground is drawn on a layer, so the name on top is not dimmed with it.
.sender-plate
  position: relative
  z-index: 0
  display: block
  border-radius: $border-radius

.sender-ground
  +disabled-hatch("")

  &
    position: absolute
    inset: 0
    z-index: -1
    pointer-events: none
    box-sizing: border-box
    border-radius: inherit
    background-color: $control-fill-overlay
    border: 1px solid $control-border-overlay

.sender-plate input
  display: block
  background-color: transparent
  border-color: transparent
  cursor: default
  caret-color: transparent

  // The keyboard still sees where it stands: the field's own ring.
  &:focus
    border-color: $link
</style>
