<script setup lang="ts">
import { moderationApi, type PremoderatedBlog } from "@/entities/moderation";
import type { Column } from "@/shared/ui/DataTable";
import PremoderatedQueue from "./PremoderatedQueue.vue";

const columns: Column[] = [
  { key: "title", label: "Блог" },
  { key: "author", label: "Владелец", width: "22%" },
  { key: "created", label: "Создан", width: "22%" },
  { key: "status", label: "Статус премодерации", width: "22%" },
];
</script>

<template>
  <PremoderatedQueue
    title="Премодерируемые блоги"
    :columns="columns"
    owner-key="author"
    empty-text="Премодерируемых блогов пока нет"
    error-text="Не удалось загрузить премодерируемые блоги"
    :load="moderationApi.getPremoderatedBlogs"
    :title-route="
      (row: PremoderatedBlog) => ({ name: 'blog', params: { id: row.id } })
    "
    :owner="(row: PremoderatedBlog) => row.author"
  />
</template>
