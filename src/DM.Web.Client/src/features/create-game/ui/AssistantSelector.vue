<script setup lang="ts">
import { ref, watch } from "vue";
import { UserAutocomplete } from "@/shared/ui/UserAutocomplete";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";

const model = defineModel<string | null>();

const selectedUsername = ref(model.value || "");

watch(selectedUsername, (newValue) => {
  model.value = newValue || null;
});

watch(model, (newValue) => {
  if (newValue !== selectedUsername.value) {
    selectedUsername.value = newValue || "";
  }
});
</script>

<template>
  <div class="assistant-selector">
    <user-autocomplete
      v-model="selectedUsername"
      placeholder=""
    />
    <secondary-text class="helper-text">
      Ассистент сможет управлять персонажами и постами в игре
    </secondary-text>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.assistant-selector
  max-width: $grid-step * 80

.helper-text
  margin-top: $small
</style>
