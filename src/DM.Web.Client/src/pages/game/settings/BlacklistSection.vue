<script setup lang="ts">
/**
 * BlacklistSection — the game blacklist: users who may not comment in the
 * game's discussion or apply to it, and do everything else as anybody does
 * (GameRoleExtensions.IsBlacklisted). The list is the shared BlacklistEditor;
 * this owns the game store and api wiring, the line that says what the list
 * does, and who the field does not suggest: the people who run the game and
 * the players in it, whom the server sends to the game's own tools
 * (GameBlacklistService.Add). The server hands the list oldest first, so a
 * name put on is the last row, over the field.
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
    ? store.loadBlacklist(game.value.publicId ?? game.value.id)
    : Promise.resolve();
}

onMounted(reload);

async function add(username: string): Promise<GeneralError | null> {
  if (!game.value) return null;
  const { error } = await gameApi.addToBlacklist(game.value.id, username);
  // The field says a refusal under itself, with the name kept in it.
  if (error) return error;
  // Awaited, so the field empties onto the list that already holds the new
  // row: "Добавить" waits shut until `add` settles. Left unawaited, the name
  // left the field before it was in the list, a round trip early.
  await reload();
  return null;
}

async function remove(username: string): Promise<GeneralError | null> {
  if (!game.value) return null;
  const { error } = await gameApi.removeFromBlacklist(game.value.id, username);
  // The editor says a refusal under the row of the name.
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
