<script setup lang="ts">
/**
 * BlacklistSection — the blog blacklist: users barred from commenting in the
 * blog. Thin wrapper around the shared BlacklistEditor; owns the blog
 * store/api wiring and copy, delegating the UI to the shared feature.
 */
import { onMounted } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import { BlacklistEditor } from "@/features/roster";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "@/shared/lib/errors";

const store = useBlogDetailsStore();
const { blog, blacklist, blacklistLoading } = storeToRefs(store);
const toast = useToast();

function reload() {
  if (blog.value) store.loadBlacklist(blog.value.id);
}

onMounted(reload);

async function add(username: string): Promise<boolean> {
  if (!blog.value) return false;
  const { error } = await blogApi.addToBlacklist(blog.value.id, username);
  if (error) {
    notifyFailure(error, "Не удалось добавить в черный список");
    return false;
  }
  toast.success("Пользователь добавлен в черный список");
  reload();
  return true;
}

async function remove(username: string) {
  if (!blog.value) return;
  const { error } = await blogApi.removeFromBlacklist(blog.value.id, username);
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
