<script setup lang="ts">
/**
 * The game's blacklist in the shared editor. The field does not suggest the
 * people of the game, whom the server sends to the game's own tools
 * (GameBlacklistService.Add).
 */
import { computed, onMounted } from "vue";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameApi } from "@/entities/game";
import { BlacklistEditor, type BlacklistRow } from "@/features/roster";
import type { GeneralError } from "@/shared/api/models/common";
import { SettingsSection } from "@/shared/ui/SettingsSection";

const store = useGameDetailsStore();
const { game, blacklist, blacklistLoading, blacklistError } =
  storeToRefs(store);

const rows = computed<BlacklistRow[]>(() =>
  blacklist.value.map((user) => ({
    id: user.id,
    username: user.username,
    user,
  })),
);

const inTheGame = computed(() => {
  const g = game.value;
  if (!g) return [];
  return [
    g.master?.username,
    g.mentor?.username,
    ...(g.assistants ?? []).map((a) => a.username),
    ...(g.players ?? []).map((p) => p.username),
  ].filter((name): name is NonNullable<typeof name> => !!name);
});

function reload(): Promise<void> {
  return game.value
    ? store.loadBlacklist(game.value.publicId)
    : Promise.resolve();
}

onMounted(reload);

async function add(username: string): Promise<GeneralError | null> {
  if (!game.value) return null;
  const { error } = await gameApi.addToBlacklist(game.value.id, username);
  if (error) return error;
  // Awaited, so the field empties onto a list that already holds the name.
  await reload();
  return null;
}

async function remove(username: string): Promise<GeneralError | null> {
  if (!game.value) return null;
  const { error } = await gameApi.removeFromBlacklist(game.value.id, username);
  if (error) return error;
  reload();
  return null;
}
</script>

<template>
  <SettingsSection title="Черный список">
    <BlacklistEditor
      hint="Внесенные не пишут в обсуждении игры и не подают заявки."
      :rows="rows"
      :loading="blacklistLoading"
      :error="blacklistError"
      :add="add"
      :remove="remove"
      :exclude="inTheGame"
    />
  </SettingsSection>
</template>
