<script setup lang="ts">
/**
 * The character sheet of the game. A save replaces the whole set and drops the
 * values of every specification it leaves out, so the version it was read at
 * and the editor's data-loss confirmation guard it.
 */
import { computed, nextTick, onMounted, ref } from "vue";
import { storeToRefs } from "pinia";
import {
  useGameDetailsStore,
  gameApi,
  cloneSpecs,
  type AttributeSchemaDraft,
} from "@/entities/game";
import {
  AttributeSchemaEditor,
  useSchemaSources,
} from "@/features/attribute-schema-editor";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { useSavedState } from "@/shared/lib/composables/useSavedState";
import type { DialogActionResult } from "@/shared/lib/composables/useDialogAction";
import { refusalHere } from "@/shared/lib/errors";
import type { RequestOptions } from "@/shared/api/interceptor";

const store = useGameDetailsStore();
const { game, characters, charactersLoading, charactersError } =
  storeToRefs(store);

const {
  sources,
  refusal: sourcesRefusal,
  load: loadSchemaSources,
} = useSchemaSources();

const draft = ref<AttributeSchemaDraft | null>(null);

// Said on the editor's own button, not in a plate.
const { saved, remember } = useSavedState(() => draft.value);
// Not off the game at save time: another section's save reloads the game.
const readVersion = ref<number | null>(null);
const saving = ref(false);
const refusal = ref("");
const rosterRead = ref(false);

// The store empties the list for a refusal, which reads like an empty roster.
const rosterKnown = computed(
  () => rosterRead.value && !charactersLoading.value && !charactersError.value,
);

// Ids kept: a specification keeps its values only while the save sends its id.
function resetDraft() {
  const schema = game.value?.schema;
  draft.value = {
    title: "",
    specifications: cloneSpecs(schema?.specifications ?? []),
  };
  readVersion.value = schema?.version ?? null;
}

// A roster that has not answered counts: the save cannot be taken back.
const hasCharacters = computed(
  () => !rosterKnown.value || characters.value.length > 0,
);

onMounted(async () => {
  resetDraft();
  loadSchemaSources();
  const current = game.value;
  if (!current) return;
  if (characters.value.length) {
    rosterRead.value = true;
    return;
  }
  await store.loadCharacters(current.publicId);
  rosterRead.value = true;
});

function cancel() {
  refusal.value = "";
  resetDraft();
}

// With the refusal flags from the confirmation window, which prints its own.
async function save(
  next: AttributeSchemaDraft,
  options?: RequestOptions,
): Promise<DialogActionResult> {
  const current = game.value;
  const version = readVersion.value;
  if (!current || version === null || saving.value) return;
  const inWindow = Boolean(options?.ownsRefusal);

  saving.value = true;
  refusal.value = "";
  const { error } = await gameApi.replaceAttributeSchema(
    current.id,
    { specifications: next.specifications, version },
    options,
  );
  saving.value = false;

  if (error) {
    if (!inWindow)
      refusal.value =
        refusalHere(error, "Не удалось сохранить схему атрибутов") ?? "";
    // Somebody saved first: the draft stays as typed, the next save carries the new version.
    if (error.status === 409) await reread(current.publicId);
    return { error };
  }

  // After the reread, or the server's normalisation would read as an edit.
  if (await reread(current.publicId)) resetDraft();
  await nextTick();
  remember();
}

// The version moves with the sheet or not at all.
async function reread(id: string): Promise<boolean> {
  const { ok } = await store.loadGame(id, { keepCurrentOnError: true });
  if (ok) readVersion.value = game.value?.schema?.version ?? null;
  return ok;
}
</script>

<template>
  <SettingsSection title="Схема атрибутов">
    <AttributeSchemaEditor
      v-model="draft"
      :sources="sources"
      :sources-refusal="sourcesRefusal"
      :has-characters="hasCharacters"
      :saving="saving"
      :saved="saved"
      :refusal="refusal"
      :save="save"
      @cancel="cancel"
    />
  </SettingsSection>
</template>
