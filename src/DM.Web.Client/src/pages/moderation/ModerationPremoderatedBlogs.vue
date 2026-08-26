<script setup lang="ts">
/**
 * ModerationPremoderatedBlogs — "Премодерируемые блоги" (doc 4.2.1.5).
 * Blogs with premoderation status != Approved, oldest first. The queue itself
 * (filter, merge, sort, table) is PremoderatedQueue; this page is what a blog
 * queue calls its columns, where a row leads and how it is loaded.
 */
import { moderationApi, type PremoderatedBlog } from "@/entities/moderation";
import type { Column } from "@/shared/ui/DataTable";
import PremoderatedQueue from "./PremoderatedQueue.vue";

const columns: Column[] = [
  { key: "title", label: "Блог" },
  { key: "author", label: "Владелец", width: "20%" },
  { key: "created", label: "Создан", width: "20%", hideOnMobile: true },
  { key: "status", label: "Статус премодерации", width: "20%" },
];
</script>

<template>
  <PremoderatedQueue
    root-class="premoderated-blogs"
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
