<script setup lang="ts">
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import { BlogsDataTable } from "@/widgets/blogs-table";
</script>

<template>
  <page-title v-once>Блоги</page-title>
  <LeadText v-once>
    Читайте блоги сообщества: авторские истории, заметки и мысли об играх
  </LeadText>
  <BlogsDataTable />
</template>
