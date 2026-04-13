<script setup lang="ts">
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import { GamesDataTable } from "@/widgets/games-table";
</script>

<template>
  <page-title v-once>Игры</page-title>
  <GamesDataTable />
</template>
