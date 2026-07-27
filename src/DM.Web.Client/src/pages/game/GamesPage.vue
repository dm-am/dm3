<script setup lang="ts">
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import { GamesDataTable } from "@/widgets/games-table";
</script>

<template>
  <page-title v-once>Игры</page-title>
  <LeadText v-once>
    Найдите партию с открытым набором или
    <router-link to="/games/create">создайте собственную игру</router-link>
  </LeadText>
  <GamesDataTable />
</template>
