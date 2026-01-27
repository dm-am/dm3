<script setup lang="ts">
import { ref, watch } from "vue";
import UserAutocomplete from "@/components/inputs/UserAutocomplete.vue";
import SecondaryText from "@/components/layout/SecondaryText.vue";

const model = defineModel<string | null>();

const selectedLogin = ref(model.value || "");

watch(selectedLogin, (newValue) => {
  model.value = newValue || null;
});

watch(model, (newValue) => {
  if (newValue !== selectedLogin.value) {
    selectedLogin.value = newValue || "";
  }
});
</script>

<template>
  <div class="assistant-selector">
    <user-autocomplete
      v-model="selectedLogin"
      placeholder="Введите логин помощника..."
    />
    <secondary-text class="helper-text">
      Помощник сможет управлять персонажами и постами в игре
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
