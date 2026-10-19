<script setup lang="ts">
import { moderationApi, type PremoderatedGame } from "@/entities/moderation";
import type { Column } from "@/shared/ui/DataTable";
import PremoderatedQueue from "./PremoderatedQueue.vue";

const columns: Column[] = [
  { key: "title", label: "Игра" },
  { key: "master", label: "Мастер", width: "22%" },
  { key: "created", label: "Создана", width: "22%" },
  { key: "status", label: "Статус премодерации", width: "22%" },
];
</script>

<template>
  <PremoderatedQueue
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
