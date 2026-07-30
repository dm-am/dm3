<script setup lang="ts">
// Personal notepad — private notes, visible to their owner only. The screen
// itself is NotepadBoard, shared with the game-master and blog notepads; this
// page only binds it to `/v1/users/me/notepad`.
import { notepadApi } from "@/entities/notepad";
import { NotepadBoard, type NotepadAdapter } from "@/widgets/notepad";

const adapter: NotepadAdapter = {
  list: () => notepadApi.getUserEntries(),
  create: (input) => notepadApi.createUserEntry(input),
  update: (id, input) => notepadApi.updateUserEntry(id, input),
  remove: (id) => notepadApi.deleteUserEntry(id),
};
</script>

<template>
  <NotepadBoard
    standalone
    :adapter="adapter"
    title="Блокнот"
    empty-text="Нет записей в блокноте"
    empty-hint="Создайте первую запись, чтобы хранить личные заметки."
    load-error-text="Не удалось загрузить записи блокнота"
  />
</template>
