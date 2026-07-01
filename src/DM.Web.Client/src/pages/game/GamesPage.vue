<script setup lang="ts">
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import { GamesDataTable } from "@/widgets/games-table";
</script>

<template>
  <page-title v-once>Игры</page-title>
  <LeadText v-once>
    Подберите игру по жанру, системе или статусу набора, а если хотите водить
    сами — <router-link to="/games/create">создайте собственную</router-link>
  </LeadText>
  <GamesDataTable />
</template>
