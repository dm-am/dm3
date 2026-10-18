<script setup lang="ts">
/**
 * BlacklistSection — the blog blacklist: users who may not comment on the
 * blog or under its publications, and do everything else as anybody does
 * (BlogRoleExtensions.IsBlacklisted). The list is the shared BlacklistEditor;
 * this owns the blog store and api wiring, the line that says what the list
 * does, and who the field does not suggest: the people who run the blog,
 * whom the server refuses (BlogBlacklistService.Add). The server hands the
 * list oldest first, so a name put on is the last row, over the field.
 */
import { computed, onMounted } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import { BlacklistEditor, type BlacklistRow } from "@/features/roster";
import type { GeneralError } from "@/shared/api/models/common";
import { SettingsSection } from "@/shared/ui/SettingsSection";

const store = useBlogDetailsStore();
const { blog, blacklist, blacklistLoading, blacklistError } =
  storeToRefs(store);

const rows = computed<BlacklistRow[]>(() =>
  blacklist.value.map((user) => ({
    id: user.id,
    username: user.username,
    user,
  })),
);

const runningTheBlog = computed(() => {
  const b = blog.value;
  if (!b) return [];
  return [
    b.author?.username,
    b.mentor?.username,
    ...(b.assistants ?? []).map((a) => a.username),
  ].filter((name): name is NonNullable<typeof name> => !!name);
});

function reload(): Promise<void> {
  return blog.value ? store.loadBlacklist(blog.value.id) : Promise.resolve();
}

onMounted(reload);

async function add(username: string): Promise<GeneralError | null> {
  if (!blog.value) return null;
  const { error } = await blogApi.addToBlacklist(blog.value.id, username);
  // The field says a refusal under itself, with the name kept in it.
  if (error) return error;
  // Awaited, so the field empties onto the list that already holds the new
  // row: "Добавить" waits shut until `add` settles. Left unawaited, the name
  // left the field before it was in the list, a round trip early.
  await reload();
  return null;
}

async function remove(username: string): Promise<GeneralError | null> {
  if (!blog.value) return null;
  const { error } = await blogApi.removeFromBlacklist(blog.value.id, username);
  // The editor says a refusal under the row of the name.
  if (error) return error;
  reload();
  return null;
}
</script>

<template>
  <SettingsSection title="Черный список">
    <BlacklistEditor
      hint="Внесенные не пишут комментарии в блоге."
      :rows="rows"
      :loading="blacklistLoading"
      :error="blacklistError"
      :add="add"
      :remove="remove"
      :exclude="runningTheBlog"
    />
  </SettingsSection>
</template>
