<template>
  <section class="section">
    <h2 class="section-title">Личные настройки</h2>

    <div class="settings-content">
      <!-- The fields wait for the saved values: a default drawn meanwhile reads as what is saved. -->
      <AccountSectionSkeleton v-if="loading" shape="fields" :count="3" />

      <ErrorState v-else-if="loadError" :message="loadError" :retry="load" />

      <template v-else>
        <FormField label="Сколько показывать на странице">
          <Select
            id="account-page-size"
            :model-value="String(settingsForm.pageSize)"
            :options="pageSizeOptions"
            @update:model-value="(v) => (settingsForm.pageSize = Number(v))"
          />
          <template #hint>
            Посты в комнате, комментарии, топики, сообщения в переписке и строки
            списков: игры, блоги, пользователи, пульс, уведомления
          </template>
        </FormField>

        <FormField label="Часовой пояс">
          <Select
            id="account-time-zone"
            :model-value="settingsForm.timeZoneId"
            :options="timeZoneOptions"
            @update:model-value="(v) => (settingsForm.timeZoneId = v)"
          />
          <template #hint>
            По нему показываются даты и время на сайте и в письмах
          </template>
        </FormField>

        <FormField label="Тема на новом устройстве">
          <Select
            id="account-theme"
            :model-value="settingsForm.theme"
            :options="themeOptions"
            @update:model-value="(v) => (settingsForm.theme = v as Theme)"
          />
          <template #hint>
            Тема этого устройства, верстка и перенос слов меняются в настройках
            сайта у шестеренки
          </template>
        </FormField>

        <Button
          variant="primary"
          :loading="saving"
          :disabled="saved || saveResult.waiting.value"
          @click="saveSettings"
          class="save-button"
        >
          {{ label }}
        </Button>
        <ActionResult :result="saveResult" />
      </template>
    </div>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { personalApi, fetchUser } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { ActionResult } from "@/shared/ui/ActionResult";
import { ErrorState } from "@/shared/ui/ErrorState";
import { FormField } from "@/shared/ui/Form";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { useSavedState } from "@/shared/lib/composables/useSavedState";
import { DEFAULT_PAGE_SIZE } from "@/shared/lib/composables/usePaging";
import { Theme, type Preferences } from "@/shared/api/models/personal";
import { unwrapResource } from "@/shared/api/envelope";
import {
  deviceTimeZone,
  knownTimeZones,
  labelOf,
  setAccountTimeZone,
} from "@/shared/lib/utils/timeZone";
import AccountSectionSkeleton from "./AccountSectionSkeleton.vue";
import { describeLoadFailure, OWNED_BY_CARD } from "./loadFailure";

const LOAD_FAILED = "Не удалось загрузить настройки";

const themeOptions: SelectOption[] = [
  { value: Theme.Light, label: "Светлая" },
  { value: Theme.Dark, label: "Темная" },
];

/** The zones, this device's first and not repeated below: it is the answer for nearly every reader. */
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

// The page sizes the server takes (PagingPolicy.AllowedPageSizes, held by PagingContractShould).
const pagingOptions = [5, 10, 20, 30, 40, 50, 100, 200];
const pageSizeOptions: SelectOption[] = pagingOptions.map((size) => ({
  value: String(size),
  label: String(size),
}));

/** The form as the server holds it; until the read answers it is not drawn. */
const settingsForm = ref({
  pageSize: DEFAULT_PAGE_SIZE,
  // A blank asks for UTC, told apart by the server from an omitted field.
  timeZoneId: "",
  theme: Theme.Light,
});

const loading = ref(true);
const loadError = ref<string | null>(null);

/** The three values of the account, read together: the viewer carries two of them, not the zone. */
async function load() {
  loading.value = true;
  const { data, error } = await personalApi.getMyPreferences(OWNED_BY_CARD);
  // A single resource may come in an envelope.
  const preferences = unwrapResource<Preferences>(data);
  loadError.value = error
    ? describeLoadFailure(error, LOAD_FAILED)
    : preferences
      ? null
      : LOAD_FAILED;
  if (preferences) {
    settingsForm.value = {
      pageSize: preferences.pageSize,
      timeZoneId: preferences.timeZoneId ?? "",
      theme: preferences.theme,
    };
  }
  loading.value = false;
}

onMounted(load);

/**
 * What a save sends, and what the button compares to call the form saved: the
 * fields written out in one order, so no extra value reopens it.
 */
function currentSettings(): Preferences {
  return {
    theme: settingsForm.value.theme,
    pageSize: settingsForm.value.pageSize,
    timeZoneId: settingsForm.value.timeZoneId,
  };
}

/** The button says the outcome: nothing else on the screen changes. */
const { saved, label, remember } = useSavedState(currentSettings);

const saveResult = useActionResult();
const saving = ref(false);

async function saveSettings() {
  saveResult.clear();
  // Held until the button can say the outcome: a free button in between would read as unsaved.
  saving.value = true;
  const { error } = await personalApi.updateMyPreferences(currentSettings());
  if (error) {
    saving.value = false;
    // Nothing is applied: the line says why, the plate taken down, and the button offers the save again.
    saveResult.refuse(error, "Не удалось сохранить настройки аккаунта");
    return;
  }

  // The theme saved here is the start of a new device and leaves this one's
  // alone: the switch at the gear changes it. The zone draws every date for a
  // signed-in reader, and the page holds it as it was read; a blank is UTC.
  setAccountTimeZone(settingsForm.value.timeZoneId);
  remember();
  // The lists read the page size off the viewer, so the next one opens with it.
  await fetchUser();
  saving.value = false;
}
</script>

<style scoped lang="sass">
@use "../AccountPage.styles" as *

.settings-content
  padding: $medium
  background-color: $bg-element
  +account-fields

// The save stands under the last hint at the step of the fields above it.
.save-button
  margin-top: $small
</style>
