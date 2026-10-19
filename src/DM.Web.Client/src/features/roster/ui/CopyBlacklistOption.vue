<script setup lang="ts">
// Drawn only over a list that came whole and is not empty; otherwise the server decides by the setting.
import { onMounted, ref } from "vue";
import { blacklistApi } from "@/entities/user";

defineProps<{
  /** Worded by what the box does, not by what the list forbids. */
  does: string;
}>();

/** Undefined while the box is not drawn. */
const copy = defineModel<boolean | undefined>();

const PAGE = 200;

const names = ref<string[]>([]);

// Stops at the server's count, or at a page that brings no new name (a list that shrank).
async function readWholeList(): Promise<string[] | null> {
  const seen = new Set<string>();
  let read = 0;
  for (;;) {
    const { data, error } = await blacklistApi.getBlacklist(read, PAGE);
    if (error || !data) return null;
    const before = seen.size;
    read += data.resources.length;
    for (const entry of data.resources) seen.add(entry.username);
    const total = data.paging?.total ?? read;
    if (read >= total || seen.size === before) return [...seen];
  }
}

onMounted(async () => {
  const [list, settings] = await Promise.all([
    readWholeList(),
    blacklistApi.getSettings(),
  ]);
  if (!list?.length) return;
  copy.value = settings.data?.autoPopulateContentBlacklist ?? false;
  names.value = list;
});
</script>

<template>
  <div v-if="names.length" class="copy-blacklist">
    <label class="copy-blacklist-label">
      <input v-model="copy" type="checkbox" />
      Перенести мой черный список
    </label>
    <p class="copy-blacklist-hint">
      {{ does }}:<template v-for="(name, i) in names" :key="name"
        >{{ " "
        }}<span class="copy-blacklist-name"
          >{{ name }}{{ i < names.length - 1 ? "," : "" }}</span
        ></template
      >
    </p>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.copy-blacklist
  display: flex
  flex-direction: column

.copy-blacklist-label
  display: flex
  align-items: center
  gap: $small
  cursor: pointer

.copy-blacklist-hint
  margin: $minor 0 0
  padding-left: $checkbox-size + $small
  color: $text-muted
  font-size: $secondary-font-size
  line-height: 1.4

// A login is never split over two lines.
.copy-blacklist-name
  white-space: nowrap
</style>
