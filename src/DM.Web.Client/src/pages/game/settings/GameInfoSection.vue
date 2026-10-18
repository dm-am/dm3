<script setup lang="ts">
/**
 * GameInfoSection — "Общие настройки" of a game: title, the number of players
 * and the recruitment, the link to the game, system, setting, the discussion,
 * the boxes of what is shown, the tags. One form of captioned rows in the order
 * of DM2's ModuleSettings (_FormRows.sass). Persists via gameApi.updateGame
 * (PATCH games/{id}/details) then reloads the game.
 *
 * DM2's "Состояние" is not a field here: a game in the making is started by the
 * transition the game's menu offers ("Начать игру"), which the details patch
 * does not carry, and a started game does not go back. The assistant is invited
 * in "Управление ролями" and the sheet is edited in its own section.
 *
 * A save leaves the screen looking exactly as it did before it, so the form
 * answers with its own submit button: it reads "Сохранено" and stops
 * responding while the fields still make the patch that was sent, and comes
 * back the moment anything changes (useSavedState).
 *
 * Tags were settable only while the game was being created, so a master who
 * mislabelled a game was stuck with the labels. They are the same control and
 * the same catalog as on the creation form: moderation owns the vocabulary at
 * /moderation/tags and nobody invents a tag here.
 *
 * The public description (info) is intentionally NOT edited here: the details
 * endpoint returns it as server-rendered HTML (InfoBbText), so there is no raw
 * BBCode source to round-trip through an editor without corrupting it. Editing
 * the description needs a raw-source endpoint that does not exist yet.
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
import { Form, FormField } from "@/shared/ui/Form";
import { Select } from "@/shared/ui/Select";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { useSavedState } from "@/shared/lib/composables/useSavedState";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { permalinkOrigin } from "@/shared/config/site";

const store = useGameDetailsStore();
const { game } = storeToRefs(store);
const router = useRouter();

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

/**
 * The page of the game, the address a master hands out. Written out whole as
 * the text of the link, the way DM2 shows "Ссылка на модуль", so it is copied
 * as any link is, from the text or from the browser's menu.
 */
const gameRoute = computed(() => ({
  name: "game",
  params: { id: game.value?.publicId ?? game.value?.id ?? "" },
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

// Seed the local draft from the loaded game.
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

/**
 * The patch this form sends, and the only thing the saved state compares.
 *
 * Built apart from the save so the comparison reads exactly what goes on the
 * wire and nothing else. A snapshot of the fields themselves would call the
 * form edited over a trailing space that is trimmed away before sending, and
 * over a tag set that the untouched-means-untouched rule below leaves out of
 * the patch entirely.
 */
function buildPatch(): UpdateGameInput {
  const parsedLimit = pcLimit.value.trim()
    ? parseInt(pcLimit.value, 10)
    : undefined;
  return {
    title: title.value.trim(),
    system: system.value.trim(),
    setting: setting.value.trim(),
    // The whole set, or nothing at all. The server replaces the game's tags
    // with what arrives here, so an empty list is how the last tag comes off -
    // but an untouched set is not sent, because the per-group limits are
    // checked only on what is submitted. A game imported over its limits would
    // otherwise refuse every settings save, including one that only fixes a
    // typo in the title, and the master would have to make tag decisions before
    // being allowed to fix anything. Untouched means untouched.
    tags: tagsChanged.value ? [...tags.value] : undefined,
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

// Form draws its submit disabled while `valid` is false, and a form that
// matches the copy the server has holds nothing to submit. So the button reads
// "Сохранено" and stays inert until the reader changes a field.
const canSubmit = computed(() => title.value.trim().length > 0 && !saved.value);

/** A refused save, said over the footer of the form. */
const saveResult = useActionResult();

async function save() {
  const current = game.value;
  // Enter in a text field submits the form even while the button under it is
  // disabled, so the guard is here and not only on the control.
  if (!current || saving.value || saved.value) return;
  saving.value = true;
  saveResult.clear();

  const { error } = await gameApi.updateGame(current.id, buildPatch());
  saving.value = false;
  if (error) {
    // Keyed by the server's names for the fields, which are the command's and
    // not the body's: "system" is refused as SystemName. The labels are the
    // captions of the fields below, less the colon.
    saveResult.refuse(error, "Не удалось сохранить информацию игры", {
      title: "Название",
      systemName: "Система",
      narrativeSetting: "Сеттинг",
    });
    return;
  }
  await store.loadGame(current.publicId ?? current.id);
  // The reload re-seeds every field through the watch above, and that watch
  // runs before nextTick resolves. Remembering ahead of it would snapshot the
  // form as typed and then let the server's own answer overwrite it, which
  // reads exactly like a form the reader has just edited.
  await nextTick();
  remember();
}
</script>

<template>
  <section class="settings-section">
    <BlockTitle>Общие настройки</BlockTitle>
    <Form
      class="info-form"
      :valid="canSubmit"
      :loading="saving"
      :action="label"
      :refusal="saveResult.refusal.value"
      @submit="save"
    >
      <FormField class="row" label="Название:" name="game-title">
        <input id="game-title" v-model="title" type="text" maxlength="200" />
      </FormField>

      <!-- DM2 says the number and "по приглашениям" in one list; here they
           are the limit (empty for none) and the recruitment switch, side by
           side in the one row. -->
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
        <template #hint>Используйте эту ссылку, чтобы позвать в игру</template>
      </FormField>

      <FormField class="row" label="Система:" name="game-system">
        <input id="game-system" v-model="system" type="text" maxlength="100" />
      </FormField>
      <FormField class="row" label="Сеттинг:" name="game-setting">
        <input
          id="game-setting"
          v-model="setting"
          type="text"
          maxlength="100"
        />
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
          Показывать всем приватные сообщения
        </label>
      </div>

      <FormField label="Теги:" name="game-tags">
        <TagSelector v-model="tags" />
      </FormField>
    </Form>
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/FormRows" as *

.settings-section
  margin-bottom: $big
  padding: $medium
  background-color: $bg-element

.info-form .row
  +form-row

// The limit is a number of two or three digits and its field is as wide as
// the word it shows while empty, the switch beside it. FormField stretches
// the controls of its row, so the width is set from the row's own block.
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
