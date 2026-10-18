<template>
  <section class="section">
    <h2 class="section-title">Настройки</h2>

    <div class="settings-content">
      <FormField label="Цветовая схема">
        <Select
          id="account-theme"
          :model-value="settingsForm.theme"
          :options="themeOptions"
          @update:model-value="(v) => (settingsForm.theme = v as Theme)"
        />
      </FormField>

      <FormField label="Часовой пояс">
        <Select
          id="account-time-zone"
          :model-value="settingsForm.timeZoneId"
          :options="timeZoneOptions"
          @update:model-value="(v) => (settingsForm.timeZoneId = v)"
        />
      </FormField>

      <div class="pagination-group">
        <div class="pagination-group-label">Размер страниц</div>
        <div class="pagination-grid">
          <FormField label="Постов на странице">
            <Select
              id="account-posts-per-page"
              :model-value="String(settingsForm.paging.postsPerPage)"
              :options="pagingSelectOptions"
              @update:model-value="
                (v) => (settingsForm.paging.postsPerPage = Number(v))
              "
            />
          </FormField>

          <FormField label="Комментариев на странице">
            <Select
              id="account-comments-per-page"
              :model-value="String(settingsForm.paging.commentsPerPage)"
              :options="pagingSelectOptions"
              @update:model-value="
                (v) => (settingsForm.paging.commentsPerPage = Number(v))
              "
            />
          </FormField>

          <FormField label="Топиков на странице">
            <Select
              id="account-topics-per-page"
              :model-value="String(settingsForm.paging.topicsPerPage)"
              :options="pagingSelectOptions"
              @update:model-value="
                (v) => (settingsForm.paging.topicsPerPage = Number(v))
              "
            />
          </FormField>

          <FormField label="Сообщений на странице">
            <Select
              id="account-messages-per-page"
              :model-value="String(settingsForm.paging.messagesPerPage)"
              :options="pagingSelectOptions"
              @update:model-value="
                (v) => (settingsForm.paging.messagesPerPage = Number(v))
              "
            />
          </FormField>

          <FormField label="Сущностей на странице">
            <Select
              id="account-entities-per-page"
              :model-value="String(settingsForm.paging.entitiesPerPage)"
              :options="pagingSelectOptions"
              @update:model-value="
                (v) => (settingsForm.paging.entitiesPerPage = Number(v))
              "
            />
          </FormField>
        </div>
      </div>

      <Button
        variant="primary"
        :loading="saveSettingsAction.loading.value"
        :disabled="saved"
        @click="saveSettings"
        class="save-button"
      >
        {{ label }}
      </Button>
      <ActionResult :text="saveSettingsAction.error.value" class="error-text" />
    </div>
  </section>
</template>

<script setup lang="ts">
import { nextTick, onMounted, ref, watch } from "vue";
import { personalApi, fetchUser } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { ActionResult } from "@/shared/ui/ActionResult";
import { FormField } from "@/shared/ui/Form";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { useAsyncAction } from "@/shared/lib/composables/useAsyncAction";
import { useSavedState } from "@/shared/lib/composables/useSavedState";
import { DEFAULT_PAGE_SIZES } from "@/shared/lib/composables/usePaging";
import {
  Theme,
  type Paging,
  type Preferences,
} from "@/shared/api/models/personal";
import type { User } from "@/shared/api/models/community/users";
import { useUiStore } from "@/shared/stores/ui";
import { unwrapResource } from "@/shared/api/envelope";
import {
  deviceTimeZone,
  knownTimeZones,
  labelOf,
  setAccountTimeZone,
} from "@/shared/lib/utils/timeZone";
import { refusalHere } from "@/shared/lib/errors";

const props = defineProps<{
  user: User;
}>();

const uiStore = useUiStore();

const themeOptions: SelectOption[] = [
  { value: Theme.Light, label: "Светлая" },
  { value: Theme.Dark, label: "Темная" },
];

/**
 * The zones, with the one this device is in at the head.
 *
 * Standing first and named for what it is, it is the answer for nearly every
 * reader who opens this field: they are here because their letters carry the
 * hours of a city they left. The rest of the list is for the reader who lives
 * between two of them. The device zone is not repeated below — one value, one
 * row, or the field shows the same zone selected in two places.
 */
const device = deviceTimeZone();
const timeZoneOptions: SelectOption[] = [
  ...(device
    ? [{ value: device, label: `Как на этом устройстве (${labelOf(device)})` }]
    : []),
  { value: "", label: "UTC" },
  ...knownTimeZones()
    .filter((zone) => zone !== device)
    .map((zone) => ({ value: zone, label: labelOf(zone) })),
];

// Backend-allowed paging values
const pagingOptions = [5, 10, 20, 30, 40, 50, 100, 200];
const pagingSelectOptions: SelectOption[] = pagingOptions.map((opt) => ({
  value: String(opt),
  label: String(opt),
}));

/**
 * The page sizes a viewer with no saved preferences is actually served.
 * usePaging owns the numbers; this form only has to agree with them, or it
 * offers the reader a size no list on the site would use.
 */
function defaultPaging(): Paging {
  return {
    postsPerPage: DEFAULT_PAGE_SIZES.postsPerPage,
    commentsPerPage: DEFAULT_PAGE_SIZES.commentsPerPage,
    topicsPerPage: DEFAULT_PAGE_SIZES.topicsPerPage,
    messagesPerPage: DEFAULT_PAGE_SIZES.messagesPerPage,
    entitiesPerPage: DEFAULT_PAGE_SIZES.entitiesPerPage,
  };
}

// Settings form.
//
// Its theme is the one this device is in, not the one stored on the account:
// the theme belongs to the device and the switch in the settings panel writes
// it there. A select filled from the account would hand the account value back
// over that choice the next time any field of this form is saved — pagination
// included, since the form is saved by one button.
const settingsForm = ref({
  theme: uiStore.theme,
  paging: defaultPaging(),
  // Blank until the server answers: the zone lives on the account and not on
  // the user this page carries, so it is read by a request of its own.
  timeZoneId: "",
});

// Initialize form from user data
watch(
  () => props.user,
  (currentUser) => {
    if (currentUser) {
      settingsForm.value = {
        theme: uiStore.theme,
        paging: currentUser.settings?.paging || defaultPaging(),
        timeZoneId: settingsForm.value.timeZoneId,
      };
    }
  },
  { immediate: true },
);

// The zone comes from the account preferences rather than with the user: the
// user document does not carry it, and one form is the only thing that asks.
onMounted(async () => {
  const { data } = await personalApi.getMyPreferences();
  // A single resource arrives in an envelope, and the answer is inside it.
  const preferences = unwrapResource<Preferences>(data);
  if (preferences) settingsForm.value.timeZoneId = preferences.timeZoneId ?? "";
});

/**
 * What a save sends - and, because the button compares the form through this
 * same function, what the button calls a saved form.
 *
 * The fields are written out instead of spread from the form for two reasons.
 * A value the request does not carry would otherwise turn the button back to
 * "Сохранить" over a form the server already agrees with. And the saved copy
 * is compared as JSON against a form the server refills through `fetchUser`,
 * where the five paging numbers arrive in whatever order the answer spells
 * them: written out here, they are always in one.
 */
function currentSettings(): Preferences {
  return {
    theme: settingsForm.value.theme,
    paging: {
      postsPerPage: settingsForm.value.paging.postsPerPage,
      commentsPerPage: settingsForm.value.paging.commentsPerPage,
      topicsPerPage: settingsForm.value.paging.topicsPerPage,
      messagesPerPage: settingsForm.value.paging.messagesPerPage,
      entitiesPerPage: settingsForm.value.paging.entitiesPerPage,
    },
    // A blank is a request for UTC, and the server tells it from an omitted
    // field: sharing a spelling, a zone once set could never be taken off.
    timeZoneId: settingsForm.value.timeZoneId,
  };
}

/**
 * The form's own answer about the save. Nothing else on this screen changes
 * when the settings are stored - the fields hold the values the reader typed
 * before and after - so the button is where the reader reads the outcome.
 */
const { saved, label, remember } = useSavedState(currentSettings);

// Save settings
const saveSettingsAction = useAsyncAction();

const saveSettings = () => {
  saveSettingsAction.execute(async () => {
    const { error } = await personalApi.updateMyPreferences(currentSettings());
    if (error) {
      // Nothing is applied either way - the theme, the account refresh and the
      // saved copy are all below this line, so the button goes back to offering
      // the save. The line above the button says why, the refusals the
      // interceptor speaks for included, and its plate is taken down: the form
      // is still on screen, and the line is where the reader looks.
      const text = refusalHere(
        error,
        "Не удалось сохранить настройки аккаунта",
      );
      if (text) throw new Error(text);
      return;
    }

    // Apply theme immediately
    uiStore.updateTheme(settingsForm.value.theme);
    // And the zone: every date on the site is drawn by it for a signed-in
    // reader (siteTime), and it is held for the page only as the account
    // stood when the page read it. A blank is UTC there as well.
    setAccountTimeZone(settingsForm.value.timeZoneId);

    await fetchUser();
    // The copy is taken after the refreshed user has reached the form: that
    // call moves the store the watcher above is on, and the watcher refills
    // the fields on the next tick. Taken before it, the copy would be of the
    // form as it stood a moment before the server's own answer landed in it.
    await nextTick();
    remember();
  });
};
</script>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "../AccountPage.styles" as *

.settings-content
  padding: $medium
  background-color: $bg-element

.pagination-group
  margin: $small 0

.pagination-group-label
  margin-bottom: $tiny
  color: $text-muted
  font-size: $secondary-font-size

// The fields of the grid a step of 8 apart, the step of the fields over them
// ("Цветовая схема" to "Часовой пояс") and of every page form of the site
// (FormField: 8 above and 8 below, folded into one 8 in a block flow). A grid
// does not fold margins, it adds them: its rows stood 24 apart. So its fields
// put their margins off, and the grid keeps 8 at its own top and bottom, where
// the caption over it and the button under it stand as they did.
.pagination-grid
  display: grid
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr))
  gap: $small
  padding: $small 0

  :deep(.form-field__labeled)
    margin: 0
</style>
