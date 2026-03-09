<template>
  <form @submit.prevent="submit" autocomplete="off">
    <slot />
    <div v-if="slots.controls" class="controls">
      <slot name="controls" />
    </div>
    <div v-else-if="action" class="controls">
      <the-button :disabled="valid === false" :loading="loading">{{
        action
      }}</the-button>
      <the-button v-if="cancel" type="button" secondary @click="handleCancel">
        {{ cancel }}
      </the-button>
    </div>
  </form>
</template>

<script setup lang="ts">
import TheButton from "@/shared/ui/Button/TheButton.vue";

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
@import "src/assets/styles/Inputs"

.controls
  display: flex
  gap: $small
  margin: $medium (-$medium) (-$medium)
  padding: $medium
  background-color: $bg-element-accent
  border-radius: 0 0 $border-radius $border-radius
</style>
