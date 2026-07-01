<script setup lang="ts">
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import { PulseDataTable } from "@/widgets/pulse-table";
</script>

<template>
  <div class="pulse-page">
    <PageTitle v-once>Пульс</PageTitle>
    <LeadText v-once>
      Посты из открытых комнат, получившие оценки за текущую неделю
    </LeadText>

    <PulseDataTable />
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.pulse-page
  width: 100%
</style>
