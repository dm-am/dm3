<script setup lang="ts">
/**
 * WarningsPage - Public log of warnings issued by moderators
 * TODO: Implement warnings list with filters
 */
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
</script>

<template>
  <div class="warnings-page">
    <PageTitle>Лог предупреждений</PageTitle>
    <p class="description">В разработке</p>
  </div>
</template>

<style scoped lang="sass">
.warnings-page
  width: 100%

.description
  margin: 0 0 $medium 0
  color: $text-muted
  font-style: italic
</style>
