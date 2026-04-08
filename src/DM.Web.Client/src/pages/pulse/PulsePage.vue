<script setup lang="ts">
import { computed } from "vue";
import dayjs from "dayjs";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import { PulseDataTable } from "@/widgets/pulse-table";
import { getWeekStartUtc } from "@/entities/game";

const weekStart = computed(() => dayjs(getWeekStartUtc()).format("DD.MM.YYYY"));
</script>

<template>
  <div class="pulse-page">
    <PageTitle>Пульс</PageTitle>
    <p class="description">Оцененные посты за неделю с {{ weekStart }}</p>

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
