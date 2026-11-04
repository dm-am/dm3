<script setup lang="ts">
/** The content of a block's item when its list did not come: the words and the retry. */
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";

const emit = defineEmits<{ retry: [] }>();
</script>

<template>
  <SecondaryText
    >Не удалось загрузить
    <button type="button" class="retry" @click="emit('retry')">
      Повторить
    </button></SecondaryText
  >
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

.retry
  margin-left: $small
  +inline-link-button
</style>
