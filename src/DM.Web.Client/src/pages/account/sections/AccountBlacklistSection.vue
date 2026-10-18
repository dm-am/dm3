<template>
  <section class="section">
    <h2 class="section-title">Черный список</h2>

    <div class="blacklist-content">
      <div class="settings-group">
        <p :id="captionId" class="settings-caption">Что делать с внесенными</p>
        <div v-if="settingsLoading" class="loading-state">Загрузка...</div>
        <div
          v-else
          class="settings-list"
          role="group"
          :aria-labelledby="captionId"
        >
          <label
            v-for="option in OPTIONS"
            :key="option.key"
            class="setting-item"
          >
            <input
              v-model="settings[option.key]"
              type="checkbox"
              :aria-busy="settingsSaving || undefined"
              @click="holdWhileSaving"
              @change="saveSettings"
            />
            <span>{{ option.label }}</span>
          </label>
        </div>
        <ActionResult :result="settingsResult" class="blacklist-refusal" />
      </div>

      <!-- The list: its caption, the table of the site (BlacklistEditor) with
           the field a name is put on with as its last line, and the rest of a
           long list under it. -->
      <div class="list-group">
        <p class="settings-caption">В черном списке</p>
        <BlacklistEditor
          :rows="blockedUsers"
          :loading="listLoading"
          dated
          :error="listError"
          :add="add"
          :remove="unblock"
        />

        <!-- The list comes a page at a time; the rest is asked for here,
             under the rows it continues. -->
        <Button
          v-if="hasMore"
          type="button"
          class="more-btn"
          :loading="loadingMore"
          @click="loadMore"
        >
          Показать еще
        </Button>
        <ActionResult :text="moreRefusal" class="blacklist-refusal" />
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * The account's own blacklist: the settings of what is done with the people on
 * it, and under them the list. Two groups, each under a caption of its own,
 * "Что делать с внесенными" and "В черном списке", as the section read before
 * the wave of 5 October.
 *
 * The list is the table the game and the blog draw (BlacklistEditor), with
 * the day each name went on in a column of its own ("Внесен"): the bin at the
 * end of a row, the field and "Добавить" as the last line of the table. What
 * stays here is what only this list has: the settings, the day, and the
 * paging of a list that can be long.
 *
 * The server hands the list back newest-first (UserBlacklistRepository orders
 * by CreatedUtc descending), so a new entry is the first row. The bin asks
 * nothing (ACC-241): the row leaves on the spot, and the field puts it back.
 *
 * A list that did not come is not an empty one: the line says so where the
 * rows would stand, and the field stays; a name put on then reads the list
 * again.
 */
import { ref, reactive, computed, onMounted } from "vue";
import { blacklistApi } from "@/entities/user";
import { BlacklistEditor } from "@/features/roster";
import type { GeneralError } from "@/shared/api/models/common";
import type {
  BlacklistEntry,
  BlacklistSettings,
} from "@/shared/api/models/personal";
import { refusalHere } from "@/shared/lib/errors";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import Button from "@/shared/ui/Button/Button.vue";

/**
 * The settings, each said as what is done with the people on the list, so a
 * box reads whole without a heading over it.
 */
const OPTIONS: { key: keyof BlacklistSettings; label: string }[] = [
  { key: "hideComments", label: "Скрывать их комментарии" },
  { key: "hideMessages", label: "Скрывать их сообщения в общем чате" },
  { key: "hideGames", label: "Скрывать их игры" },
  { key: "hideBlogs", label: "Скрывать их блоги" },
  { key: "blockDirectMessages", label: "Не принимать от них личные сообщения" },
  {
    key: "autoPopulateContentBlacklist",
    label: "Переносить список в мои новые игры и блоги",
  },
];

const captionId = `blacklist-settings-${Math.random().toString(36).slice(2, 9)}`;

// Settings state
const settingsLoading = ref(true);
const settingsSaving = ref(false);
const settings = reactive<BlacklistSettings>({
  hideComments: false,
  hideMessages: false,
  hideGames: false,
  hideBlogs: false,
  blockDirectMessages: false,
  autoPopulateContentBlacklist: false,
});

// Blocked users state
const listLoading = ref(true);
const blockedUsers = ref<BlacklistEntry[]>([]);
/** Why the list is not on screen: its first page did not come. */
const listError = ref<string | null>(null);

/**
 * One page of the list. The server answers in pages and says how many there
 * are in all; the first page used to be the whole of what this section
 * showed, so a list longer than it simply ended.
 */
const PAGE_SIZE = 50;

/**
 * How many names the list holds on the server, kept in step with the presses
 * made here. Without a paging answer (an older server) it is the page itself,
 * and nothing more is offered.
 */
const total = ref(0);
const hasMore = computed(() => blockedUsers.value.length < total.value);

onMounted(async () => {
  await Promise.all([loadSettings(), loadBlockedUsers()]);
});

async function loadSettings() {
  settingsLoading.value = true;
  const { data, error } = await blacklistApi.getSettings();
  settingsLoading.value = false;

  if (!error && data) {
    Object.assign(settings, data);
  }
}

// A setting stays drawn as it is while its save is in flight. Disabled, a
// ticked box would show a tick on a control that cannot be ticked, and the
// site's disabled state says "not on offer", which a setting being saved is
// not. What the disabled attribute also did is kept: a press during the save
// is refused before it toggles anything, so it never becomes a second request
// racing the first.
function holdWhileSaving(event: Event) {
  if (settingsSaving.value) event.preventDefault();
}

/**
 * A refused setting, under the list of settings. The boxes are read back from
 * the server, so the line says why the one just ticked went back.
 */
const settingsResult = useActionResult();

async function saveSettings() {
  settingsSaving.value = true;
  settingsResult.clear();
  const { error } = await blacklistApi.updateSettings({ ...settings });
  settingsSaving.value = false;

  if (error) {
    settingsResult.refuse(
      error,
      "Не удалось сохранить настройки черного списка",
    );
    // Reload to restore correct state
    await loadSettings();
  }
}

async function loadBlockedUsers() {
  listLoading.value = true;
  const { data, error } = await blacklistApi.getBlacklist(0, PAGE_SIZE);
  listLoading.value = false;

  if (error) {
    listError.value = "Не удалось загрузить черный список";
    return;
  }
  listError.value = null;
  if (data) {
    blockedUsers.value = data.resources;
    total.value = data.paging?.total ?? data.resources.length;
  }
}

// --- Putting a name on ---
// Sent from the field, which waits for the answer and says a refusal under
// itself. The new entry is the table's first row, where the server puts it;
// the server now holds one more name, which the next page is counted from.
// A name already on the list comes back as its old entry, which is not put on
// screen twice. Where the list did not come, it is read again instead: there
// is no list on screen to put the entry at the head of.
async function add(username: string): Promise<GeneralError | null> {
  const { data, error } = await blacklistApi.blockUser({ username });
  if (error) return error;
  if (listError.value) {
    await loadBlockedUsers();
    return null;
  }
  if (!data) return null;
  const known = blockedUsers.value.some((entry) => entry.id === data.id);
  blockedUsers.value = [
    data,
    ...blockedUsers.value.filter((entry) => entry.id !== data.id),
  ];
  if (!known) total.value += 1;
  return null;
}

// --- The next page ---
// Asked from where the rows on screen end. A name added here went to the head
// of the list on the server as it did on screen, and a name taken off left
// both, so the count of rows on screen is the offset the server means; a row
// that turns up twice all the same (a change made in another tab) is dropped.
const loadingMore = ref(false);
const moreRefusal = ref<string | null>(null);

async function loadMore() {
  if (loadingMore.value) return;
  loadingMore.value = true;
  moreRefusal.value = null;
  const { data, error } = await blacklistApi.getBlacklist(
    blockedUsers.value.length,
    PAGE_SIZE,
  );
  loadingMore.value = false;

  if (error) {
    moreRefusal.value = refusalHere(
      error,
      "Не удалось загрузить черный список",
    );
    return;
  }
  if (!data) return;
  const onScreen = new Set(blockedUsers.value.map((entry) => entry.id));
  blockedUsers.value.push(
    ...data.resources.filter((entry) => !onScreen.has(entry.id)),
  );
  total.value = data.paging?.total ?? blockedUsers.value.length;
}

// --- Taking a name off ---
// The bin asks nothing: the row leaves the list on the spot. The table shuts
// the bin while its answer is on the wire and says a refusal under the row,
// which then stays where it was.
async function unblock(username: string): Promise<GeneralError | null> {
  const { error } = await blacklistApi.unblockUser(username);
  if (error) return error;
  blockedUsers.value = blockedUsers.value.filter(
    (entry) => entry.username !== username,
  );
  total.value = Math.max(0, total.value - 1);
  return null;
}
</script>

<style scoped lang="sass">
@use "../AccountPage.styles" as *
@use "@/assets/styles/Inputs" as *

// The card of the section: the group of settings and the group of the list,
// 16 apart.
.blacklist-content
  padding: $medium
  background-color: $bg-element
  display: flex
  flex-direction: column
  gap: $medium

// Inside a group its parts stand 8 apart: the caption and the boxes; the
// caption, the table and "Показать еще".
.settings-group,
.list-group
  display: flex
  flex-direction: column
  gap: $small

// The caption of a group of fields: 14, bold, as the captions of the fields of
// the site are.
.settings-caption
  margin: 0
  font-size: $secondary-font-size
  font-weight: 700
  color: $text

.loading-state
  padding: $small 0

.settings-list
  display: flex
  flex-direction: column
  gap: $small

// The line of the words: the body's 1.3 at the size of the text.
$setting-line: 1.3 * $font-size

// A box and its words. The box stands by the first line, centred on it, and
// words that wrap (three of the six on a phone of 360) go on under themselves
// and not under the box, as the check lines of the guest forms stand
// (AuthSurface.sass, .auth-checkline). Centred on the whole label, the box
// hung between the two lines. On one line the place is the one centring gave.
.setting-item
  display: flex
  align-items: flex-start
  gap: $small
  cursor: pointer
  color: $text
  line-height: $setting-line

  input
    margin-top: calc((#{$setting-line} - #{$checkbox-size}) / 2)

// From the left edge, under the rows it continues.
.more-btn
  align-self: flex-start

// Spaced by the gap of the column it stands in.
.blacklist-refusal
  --action-result-margin: 0
</style>
