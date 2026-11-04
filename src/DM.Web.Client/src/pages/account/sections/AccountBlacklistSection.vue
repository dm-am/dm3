<template>
  <section class="section">
    <h2 class="section-title">Черный список</h2>

    <div class="blacklist-content">
      <div class="settings-group">
        <p :id="captionId" class="settings-caption">Что делать с внесенными</p>
        <!-- The boxes wait unticked of nothing: a box drawn before the answer reads as saved. -->
        <AccountSectionSkeleton
          v-if="settingsLoading"
          shape="checks"
          :count="OPTIONS.length"
        />
        <ErrorState
          v-else-if="settingsError"
          :message="settingsError"
          :retry="loadSettings"
        />
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

      <!-- The caption, the table of the site with the field as its last line, and the rest of a long list. -->
      <div class="list-group">
        <p class="settings-caption">В черном списке</p>
        <BlacklistEditor
          :rows="blacklistEntries"
          :loading="listLoading"
          dated
          :error="listError"
          :add="addToBlacklist"
          :remove="removeFromBlacklist"
        />

        <!-- The rest of the list, a page at a time, under the rows it continues. -->
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
 * it, and the list, on the table the game and the blog draw (BlacklistEditor)
 * with the day of each name. The server serves it newest first, so a new entry
 * is the first row; the bin asks nothing. A list that did not come says so,
 * and a name put on then reads it again.
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
import { ErrorState } from "@/shared/ui/ErrorState";
import AccountSectionSkeleton from "./AccountSectionSkeleton.vue";
import { OWNED_BY_CARD } from "./loadFailure";

/** Each setting said as what is done with the people listed, so a box reads whole. */
const OPTIONS: { key: keyof BlacklistSettings; label: string }[] = [
  { key: "hideComments", label: "Скрывать их комментарии" },
  { key: "hideMessages", label: "Скрывать их сообщения в глобальном чате" },
  { key: "hideGames", label: "Скрывать их игры" },
  { key: "hideBlogs", label: "Скрывать их блоги" },
  { key: "blockDirectMessages", label: "Не принимать от них личные сообщения" },
  {
    key: "autoPopulateContentBlacklist",
    label: "Переносить список в мои новые игры и блоги",
  },
];

const captionId = `blacklist-settings-${Math.random().toString(36).slice(2, 9)}`;

const SETTINGS_LOAD_FAILED = "Не удалось загрузить настройки черного списка";

const settingsLoading = ref(true);
const settingsError = ref<string | null>(null);
const settingsSaving = ref(false);
const settings = reactive<BlacklistSettings>({
  hideComments: false,
  hideMessages: false,
  hideGames: false,
  hideBlogs: false,
  blockDirectMessages: false,
  autoPopulateContentBlacklist: false,
});

const listLoading = ref(true);
const blacklistEntries = ref<BlacklistEntry[]>([]);
/** Why the list is not on screen: its first page did not come. */
const listError = ref<string | null>(null);

/** One page of the list; the server says how many there are in all. */
const PAGE_SIZE = 50;

/** How many names the server holds, kept in step with the presses here; a null paging counts the page. */
const total = ref(0);
const hasMore = computed(() => blacklistEntries.value.length < total.value);

onMounted(async () => {
  await Promise.all([loadSettings(), loadBlacklist()]);
});

async function loadSettings() {
  settingsLoading.value = true;
  const { data, error } = await blacklistApi.getSettings(OWNED_BY_CARD);
  settingsLoading.value = false;

  settingsError.value = !error && data ? null : SETTINGS_LOAD_FAILED;
  if (!error && data) {
    Object.assign(settings, data);
  }
}

// A setting stays drawn as it is while it is saved, and a press meanwhile is
// refused before it toggles: disabled, a ticked box would contradict itself.
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
    // Read back, so the box goes back to the server's state.
    await loadSettings();
  }
}

async function loadBlacklist() {
  listLoading.value = true;
  const { data, error } = await blacklistApi.getBlacklist(0, PAGE_SIZE);
  listLoading.value = false;

  if (error) {
    listError.value = "Не удалось загрузить черный список";
    return;
  }
  listError.value = null;
  if (data) {
    blacklistEntries.value = data.resources;
    total.value = data.paging?.total ?? data.resources.length;
  }
}

// The new entry is the first row, as the server puts it, and counts one more;
// a name already listed is not put twice. With no list on screen, it is read again.
async function addToBlacklist(username: string): Promise<GeneralError | null> {
  const { data, error } = await blacklistApi.blockUser({ username });
  if (error) return error;
  if (listError.value) {
    await loadBlacklist();
    return null;
  }
  if (!data) return null;
  const known = blacklistEntries.value.some((entry) => entry.id === data.id);
  blacklistEntries.value = [
    data,
    ...blacklistEntries.value.filter((entry) => entry.id !== data.id),
  ];
  if (!known) total.value += 1;
  return null;
}

// The next page from where the rows end: additions and removals here moved the server's list
// the same way. A row seen twice (another tab) is dropped.
const loadingMore = ref(false);
const moreRefusal = ref<string | null>(null);

async function loadMore() {
  if (loadingMore.value) return;
  loadingMore.value = true;
  moreRefusal.value = null;
  const { data, error } = await blacklistApi.getBlacklist(
    blacklistEntries.value.length,
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
  const onScreen = new Set(blacklistEntries.value.map((entry) => entry.id));
  blacklistEntries.value.push(
    ...data.resources.filter((entry) => !onScreen.has(entry.id)),
  );
  total.value = data.paging?.total ?? blacklistEntries.value.length;
}

// The row leaves on the spot; the table shuts the bin while the answer is out and says a refusal under the row.
async function removeFromBlacklist(
  username: string,
): Promise<GeneralError | null> {
  const { error } = await blacklistApi.unblockUser(username);
  if (error) return error;
  blacklistEntries.value = blacklistEntries.value.filter(
    (entry) => entry.username !== username,
  );
  total.value = Math.max(0, total.value - 1);
  return null;
}
</script>

<style scoped lang="sass">
@use "../AccountPage.styles" as *
@use "@/assets/styles/Inputs" as *

// The settings and the list, 16 apart.
.blacklist-content
  padding: $medium
  background-color: $bg-element
  display: flex
  flex-direction: column
  gap: $medium

// Inside a group its parts stand 8 apart.
.settings-group,
.list-group
  display: flex
  flex-direction: column
  gap: $small

// The caption of a group of fields, as the captions of the site's fields.
.settings-caption
  margin: 0
  font-size: $secondary-font-size
  font-weight: 700
  color: $text

.settings-list
  display: flex
  flex-direction: column
  gap: $small

// The line of the words.
$setting-line: 1.3 * $font-size

// The box stands by the first line, and wrapped words go on under themselves,
// as the check lines of the guest forms.
.setting-item
  display: flex
  align-items: flex-start
  gap: $small
  cursor: pointer
  color: $text
  line-height: $setting-line

  input
    margin-top: calc((#{$setting-line} - #{$checkbox-size}) / 2)

// From the left edge, under the rows.
.more-btn
  align-self: flex-start

// Spaced by the gap of the column.
.blacklist-refusal
  --action-result-margin: 0
</style>
