<script setup lang="ts">
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import { PulseDataTable } from "@/widgets/pulse-table";
</script>

<template>
  <div class="pulse-page">
    <PageTitle v-once>Пульс</PageTitle>
    <p class="description">Посты из открытых комнат получившие оценки за текущую неделю</p>

    <PulseDataTable />
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.pulse-page
  width: 100%

.description
  margin: 0 0 $medium 0
  color: $text-muted
  font-size: $secondary-font-size
</style>
