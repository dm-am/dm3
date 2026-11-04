<script setup lang="ts">
// The personal notepad: NotepadBoard, bound to `/v1/users/me/notepad`.
import { storeToRefs } from "pinia";
import { notepadEndpoints } from "@/shared/api";
import { useAuthStore } from "@/entities/user";
import { NotepadBoard, type NotepadAdapter } from "@/widgets/notepad";

const { user } = storeToRefs(useAuthStore());

const adapter: NotepadAdapter = notepadEndpoints("users/me/notepad");
</script>

<template>
  <NotepadBoard
    standalone
    :adapter="adapter"
    :viewer-id="user?.id ?? null"
    title="Блокнот"
    empty-text="Нет записей в блокноте"
    empty-hint="Создайте первую запись, чтобы хранить личные заметки"
    load-error-text="Не удалось загрузить записи блокнота"
  />
</template>
