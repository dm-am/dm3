<script setup lang="ts">
/**
 * BlacklistSection — the game blacklist: users barred from applying to or
 * reading the game. Thin wrapper around the shared BlacklistEditor; owns the
 * game store/api wiring and copy, delegating the UI to the shared feature.
 */
import { onMounted } from "vue";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameApi } from "@/entities/game";
import { BlacklistEditor } from "@/features/roster";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "@/shared/lib/errors";

const store = useGameDetailsStore();
const { game, blacklist, blacklistLoading } = storeToRefs(store);
const toast = useToast();

function reload() {
  if (game.value) store.loadBlacklist(game.value.publicId ?? game.value.id);
}

onMounted(reload);

async function add(username: string): Promise<boolean> {
  if (!game.value) return false;
  const { error } = await gameApi.addToBlacklist(game.value.id, username);
  if (error) {
    notifyFailure(error, "Не удалось добавить в черный список");
    return false;
  }
  toast.success("Пользователь добавлен в черный список");
  reload();
  return true;
}

async function remove(username: string) {
  if (!game.value) return;
  const { error } = await gameApi.removeFromBlacklist(game.value.id, username);
  if (error) {
    notifyFailure(error, "Не удалось удалить из черного списка");
    return;
  }
  toast.success("Пользователь удален из черного списка");
  reload();
}
</script>

<template>
  <BlacklistEditor
    :items="blacklist"
    :loading="blacklistLoading"
    :add="add"
    :remove="remove"
  />
</template>
