<script setup lang="ts">
import { computed } from "vue";
import { UserAutocomplete } from "@/entities/user";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";

const model = defineModel<string | null>();

const selectedUsername = computed({
  get: () => model.value || "",
  set: (value: string) => {
    model.value = value || null;
  },
});
</script>

<template>
  <div class="assistant-selector">
    <user-autocomplete v-model="selectedUsername" placeholder="" />
    <secondary-text class="helper-text">
      Ассистент сможет управлять персонажами и постами в игре
    </secondary-text>
  </div>
</template>

<style scoped lang="sass">
.assistant-selector
  max-width: $grid-step * 80

.helper-text
  margin-top: $small
</style>
