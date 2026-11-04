<script setup lang="ts">
/**
 * The blog blacklist on the shared BlacklistEditor: the field does not suggest
 * the people who run the blog, whom the server refuses. The server hands the
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
  if (error) return error;
  // Awaited, so the field empties onto a list that already holds the new row.
  await reload();
  return null;
}

async function remove(username: string): Promise<GeneralError | null> {
  if (!blog.value) return null;
  const { error } = await blogApi.removeFromBlacklist(blog.value.id, username);
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
