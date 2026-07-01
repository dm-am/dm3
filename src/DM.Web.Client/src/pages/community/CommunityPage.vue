<script setup lang="ts">
import { UsersDataTable } from "@/widgets/users-table";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
</script>

<template>
  <page-title v-once>Сообщество</page-title>
  <LeadText v-once
    >Знакомьтесь с игроками и мастерами DM.AM: ищите по имени, рейтингу или
    активности</LeadText
  >
  <UsersDataTable />
</template>
