<script setup lang="ts">
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import { PulseFeed } from "@/widgets/pulse-feed";
</script>

<template>
  <div class="pulse-page">
    <PageTitle v-once>Пульс</PageTitle>
    <LeadText v-once>
      Посты из открытых комнат, получившие оценки за текущую неделю
    </LeadText>

    <PulseFeed />
  </div>
</template>

<style scoped lang="sass">
.pulse-page
  width: 100%
</style>
