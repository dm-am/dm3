<script setup lang="ts">
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import { BlogsDataTable } from "@/widgets/blogs-table";
</script>

<template>
  <page-title v-once>Блоги</page-title>
  <LeadText v-once>
    Читайте авторские блоги участников, от игровых заметок до личных историй,
    или <router-link to="/blogs/create">заведите собственный блог</router-link>
  </LeadText>
  <BlogsDataTable />
</template>
