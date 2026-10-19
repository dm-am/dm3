<script setup lang="ts">
/**
 * The general settings of a game, in the order of DM2's ModuleSettings. The
 * description is the master's alone: the server gives its BBCode as typed to
 * its author, and a lead or a moderator would be given it drawn.
 */
import { computed, nextTick, ref, watch } from "vue";
import { RouterLink, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import {
  useGameDetailsStore,
  gameApi,
  CommentariesAccessMode,
  TagSelector,
  type UpdateGameInput,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { Form, FormField } from "@/shared/ui/Form";
import { Select } from "@/shared/ui/Select";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { useSavedState } from "@/shared/lib/composables/useSavedState";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { permalinkOrigin } from "@/shared/config/site";
import {
  GAME_INFO_MIN_LENGTH,
  GAME_TITLE_MAX_LENGTH,
} from "@/shared/lib/constants/game";
import { pluralize } from "@/shared/lib/utils/pluralize";
import { parseApiErrors } from "@/shared/lib/utils/apiErrors";
import { readFieldError } from "@/shared/lib/errors/validationErrors";
import type { BadRequestError } from "@/shared/api/models/common";

const store = useGameDetailsStore();
const { game } = storeToRefs(store);
const router = useRouter();
const { user } = storeToRefs(useAuthStore());

// By id, as the server compares: a username can change.
const isMaster = computed(
  () =>
    !!user.value &&
    !!game.value?.master &&
    game.value.master.id === user.value.id,
);

const commentariesOptions = [
  {
    value: CommentariesAccessMode.Public,
    label: "Доступно всем без ограничений",
  },
  {
    value: CommentariesAccessMode.Readonly,
    label: "Доступно посторонним только для чтения",
  },
];

// Written out whole as the text of the link, as DM2 shows "Ссылка на модуль".
const gameRoute = computed(() => ({
  name: "game",
  params: { id: game.value?.publicId ?? "" },
}));
const gameUrl = computed(
  () => permalinkOrigin() + router.resolve(gameRoute.value).href,
);

const title = ref("");
const system = ref("");
const setting = ref("");
const isRecruitmentOpen = ref(false);
const pcLimit = ref("");
const viewPrivates = ref(false);
const viewDice = ref(false);
const commentariesAccess = ref(CommentariesAccessMode.Public);
const tags = ref<number[]>([]);
const storedTags = ref<number[]>([]);
/** The description as typed, and as the server last gave it; null until it is read. */
const info = ref("");
const storedInfo = ref<string | null>(null);
/** The server's refusal of the description, under the field it is about. */
const infoRefusal = ref("");

/** Same members, order aside: the selector is a set, the payload is a list. */
const tagsChanged = computed(() => {
  const before = [...storedTags.value].sort((a, b) => a - b);
  const after = [...tags.value].sort((a, b) => a - b);
  return (
    before.length !== after.length ||
    before.some((id, index) => id !== after[index])
  );
});

const saving = ref(false);

watch(
  game,
  (g) => {
    if (!g) return;
    title.value = g.title ?? "";
    system.value = g.system ?? "";
    setting.value = g.setting ?? "";
    tags.value = [...(g.tagIds ?? [])];
    storedTags.value = [...(g.tagIds ?? [])];
    isRecruitmentOpen.value = g.recruitment?.isOpen ?? false;
    pcLimit.value =
      g.recruitment?.pcLimit != null ? String(g.recruitment.pcLimit) : "";
    viewPrivates.value = g.privacySettings?.viewPrivates ?? false;
    viewDice.value = g.privacySettings?.viewDice ?? false;
    commentariesAccess.value =
      g.privacySettings?.commentariesAccess ?? CommentariesAccessMode.Public;
  },
  { immediate: true },
);

// The saved state compares what goes on the wire, not the fields as typed.
function buildPatch(): UpdateGameInput {
  const parsedLimit = pcLimit.value.trim()
    ? parseInt(pcLimit.value, 10)
    : undefined;
  return {
    title: title.value,
    system: system.value,
    setting: setting.value,
    // Untouched is not sent: the server checks the per-group limits on what
    // arrives, and a game imported over them would refuse every save.
    tags: tagsChanged.value ? [...tags.value] : undefined,
    // Untouched is not sent either: the text is not written again as it was.
    info:
      storedInfo.value !== null && info.value !== storedInfo.value
        ? info.value
        : undefined,
    recruitment: {
      isOpen: isRecruitmentOpen.value,
      pcLimit: Number.isFinite(parsedLimit) ? parsedLimit : undefined,
    },
    privacySettings: {
      viewPrivates: viewPrivates.value,
      viewDice: viewDice.value,
      commentariesAccess: commentariesAccess.value,
    },
  };
}

const { saved, label, remember } = useSavedState(buildPatch);

const canSubmit = computed(() => title.value.trim().length > 0 && !saved.value);

const saveResult = useActionResult();

async function loadInfo(publicId: string) {
  const { data, error } = await gameApi.getGameForEdit(publicId);
  if (game.value?.publicId !== publicId) return;
  if (error) {
    saveResult.refuse(error, "Не удалось загрузить описание игры");
    return;
  }
  storedInfo.value = data?.resource.info ?? "";
  info.value = storedInfo.value;
}

watch(
  () => (isMaster.value ? game.value?.publicId : undefined),
  (publicId) => {
    storedInfo.value = null;
    if (publicId) void loadInfo(publicId);
  },
  { immediate: true },
);

async function save() {
  const current = game.value;
  // Enter submits the form even under a disabled button.
  if (!current || saving.value || saved.value) return;
  saving.value = true;
  saveResult.clear();
  infoRefusal.value = "";

  const { error } = await gameApi.updateGame(current.id, buildPatch());
  saving.value = false;
  if (error) {
    const said = readFieldError(
      parseApiErrors(error as BadRequestError),
      "info",
    );
    if (said) {
      infoRefusal.value = said;
      return;
    }
    // By the command's names of the fields, not the body's.
    saveResult.refuse(error, "Не удалось сохранить информацию игры", {
      title: "Название",
      systemName: "Система",
      narrativeSetting: "Сеттинг",
    });
    return;
  }
  await store.loadGame(current.publicId);
  // The field shows what the server stored, not what was sent.
  if (isMaster.value) await loadInfo(current.publicId);
  // After the watch above re-seeds the fields from the server's answer.
  await nextTick();
  remember();
}
</script>

<template>
  <SettingsSection title="Общие настройки">
    <Form
      class="info-form"
      :valid="canSubmit"
      :loading="saving"
      :action="label"
      :refusal="saveResult.refusal.value"
      @submit="save"
    >
      <FormField class="row" label="Название:" name="game-title">
        <input
          id="game-title"
          v-model="title"
          type="text"
          :maxlength="GAME_TITLE_MAX_LENGTH"
        />
      </FormField>

      <FormField class="row" label="Количество игроков:" name="pc-limit">
        <div class="recruitment">
          <input
            id="pc-limit"
            v-model="pcLimit"
            class="pc-limit"
            type="text"
            inputmode="numeric"
            placeholder="без ограничения"
          />
          <label class="checkbox-label">
            <input v-model="isRecruitmentOpen" type="checkbox" />
            Набор открыт
          </label>
        </div>
      </FormField>

      <FormField class="row" label="Ссылка на игру:" name="game-link">
        <div class="game-link">
          <RouterLink :to="gameRoute">{{ gameUrl }}</RouterLink>
        </div>
      </FormField>

      <FormField class="row" label="Система:" name="game-system">
        <input id="game-system" v-model="system" type="text" />
      </FormField>
      <FormField class="row" label="Сеттинг:" name="game-setting">
        <input id="game-setting" v-model="setting" type="text" />
      </FormField>

      <FormField class="row" label="Обсуждение игры:" name="commentaries">
        <Select
          id="game-settings-commentaries-access"
          :model-value="commentariesAccess"
          :options="commentariesOptions"
          @update:model-value="
            (v) => (commentariesAccess = v as CommentariesAccessMode)
          "
        />
      </FormField>

      <div class="boxes">
        <label class="checkbox-label">
          <input v-model="viewDice" type="checkbox" />
          Показывать всем результаты бросков
        </label>
        <label class="checkbox-label">
          <input v-model="viewPrivates" type="checkbox" />
          Показывать всем скрытые блоки
        </label>
      </div>

      <FormField
        v-if="storedInfo !== null"
        label="Описание:"
        name="game-info"
        :errors="infoRefusal ? [infoRefusal] : []"
      >
        <BBCodeEditor
          v-model="info"
          context="common"
          :min-height="200"
          :max-height="500"
          :resizable="true"
          @update:model-value="infoRefusal = ''"
        />
        <template #hint
          >Не менее {{ GAME_INFO_MIN_LENGTH }}
          {{
            pluralize(GAME_INFO_MIN_LENGTH, "символа", "символов", "символов")
          }}</template
        >
      </FormField>

      <FormField label="Теги:" name="game-tags">
        <!-- The settings are wider than the game form: the catalogue wraps into fewer rows -->
        <TagSelector v-model="tags" :loading-rows="[4, 3, 1, 1, 1, 1, 1, 1]" />
      </FormField>
    </Form>
  </SettingsSection>
</template>

<style scoped lang="sass">
@use "@/assets/styles/FormRows" as *

.info-form .row
  +form-row

// FormField stretches its controls, so the width is set from the row's block.
.recruitment
  display: flex
  align-items: center
  gap: $medium

.recruitment .pc-limit
  width: $grid-step * 36

.game-link
  +form-row-text
  font-weight: 700
  overflow-wrap: anywhere

.boxes
  +form-row-boxes

.checkbox-label
  display: flex
  align-items: center
  gap: $small
  cursor: pointer
</style>
