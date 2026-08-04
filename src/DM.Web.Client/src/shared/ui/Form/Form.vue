<template>
  <!-- No form-level autocomplete="off": part of the browsers read it as a veto
       over the fields' own autocomplete, and every value that matters here is
       declared on the field (email, current-password, new-password, the
       support contact). Anything that must not be filled - the bot traps, the
       username and date inputs - carries its own "off". -->
  <form @submit.prevent="submit">
    <slot />
    <div v-if="slots.controls" class="controls">
      <slot name="controls" />
    </div>
    <div v-else-if="action" class="controls">
      <Button
        variant="primary"
        :disabled="valid === false"
        :loading="loading"
        >{{ action }}</Button
      >
      <Button v-if="cancel" type="button" @click="handleCancel">
        {{ cancel }}
      </Button>
    </div>
  </form>
</template>

<script setup lang="ts">
import Button from "@/shared/ui/Button/Button.vue";

defineProps<{
  valid?: boolean;
  loading?: boolean;
  action?: string;
  cancel?: string;
}>();
const emit = defineEmits(["submit", "cancel"]);
const submit = () => emit("submit");
const handleCancel = () => emit("cancel");
const slots = defineSlots();
</script>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.controls
  display: flex
  gap: $small
  margin: $medium (-$medium) (-$medium)
  padding: $medium
  background-color: $bg-element-accent
  border-radius: 0 0 $border-radius $border-radius
</style>
