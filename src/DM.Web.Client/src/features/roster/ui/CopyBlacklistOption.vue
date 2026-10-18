<script setup lang="ts">
/**
 * "Перенести мой черный список": the box that carries the author's own
 * blacklist into a blog being created, with what it does and the people it
 * carries said under it - "Внесет в черный список блога: Player-Two,
 * RatingNegative" - every one of them, however long the list: the line used to stop at three
 * and say "и еще N", which is the "Еще" the owner forbade (12 October 2026:
 * nothing shortened). The list comes in pages, so it is read page by page
 * until it is whole.
 *
 * Where the author's list is empty there is nothing to carry, and the box is
 * not drawn at all: it used to stand on every new blog, and to promise to
 * close what the list no longer closes. It is not drawn either while the list
 * has not come, or when it did not come, a page of it included: what to
 * carry is then the server's to decide, by the account setting, and the
 * model stays undefined.
 *
 * Each name is a piece of its own that does not break, its comma with it, and
 * the line breaks between the pieces: a login is never split over two lines on
 * the site, and "Player-Two" used to break at its hyphen here. The space
 * between them is the line's.
 *
 * The box starts where the account setting "Переносить список в мои новые
 * игры и блоги" stands, and it is drawn with it, so there is no choice of the
 * author's for the setting to overwrite; without the setting (a failed read)
 * it starts unticked. What the box shows is what the form sends.
 */
import { onMounted, ref } from "vue";
import { blacklistApi } from "@/entities/user";

defineProps<{
  /**
   * What ticking the box does, said before the names it carries: "Внесет в
   * черный список блога". Worded by what the box does, not by what the list
   * forbids: what a blacklist forbids is said where the list is kept.
   */
  does: string;
}>();

/** Whether to carry the list; undefined while the box is not drawn. */
const copy = defineModel<boolean | undefined>();

/** The largest page the server serves (PagingQuery.Take). */
const PAGE = 200;

const names = ref<string[]>([]);

/**
 * Every name on the author's list, a page at a time, or null when a page did
 * not come. It stops at the count the server gives, or at a page that brings
 * no name it has not brought already (a list that shrank meanwhile).
 */
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
// An option of the form it stands in: the box and its words, the line under
// them grey and indented to the words, as the other options of the blog's
// access settings are.
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
  padding-left: 16px + $small
  color: $text-muted
  font-size: $secondary-font-size
  line-height: 1.4

// A login whole on its line (D2, "Имена": the name a piece that does not break,
// the break between pieces, the space the line's).
.copy-blacklist-name
  white-space: nowrap
</style>
