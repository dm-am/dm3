<script setup lang="ts">
/**
 * ModerationPremoderatedGames — "Премодерируемые игры" (doc 4.2.3.8.2).
 * Games with premoderation status != Approved, oldest first. The queue itself
 * (filter, merge, sort, table) is PremoderatedQueue; this page is what a game
 * queue calls its columns, where a row leads and how it is loaded.
 */
import { moderationApi, type PremoderatedGame } from "@/entities/moderation";
import type { Column } from "@/shared/ui/DataTable";
import PremoderatedQueue from "./PremoderatedQueue.vue";

const columns: Column[] = [
  { key: "title", label: "Игра" },
  { key: "master", label: "Мастер", width: "20%" },
  { key: "created", label: "Создана", width: "20%", hideOnMobile: true },
  { key: "status", label: "Статус премодерации", width: "20%" },
];
</script>

<template>
  <PremoderatedQueue
    root-class="premoderated-games"
    title="Премодерируемые игры"
    :columns="columns"
    owner-key="master"
    empty-text="Премодерируемых игр пока нет"
    error-text="Не удалось загрузить премодерируемые игры"
    :load="moderationApi.getPremoderatedGames"
    :title-route="
      (row: PremoderatedGame) => ({
        name: 'game',
        params: { id: row.publicId || row.id },
      })
    "
    :owner="(row: PremoderatedGame) => row.master"
  />
</template>
