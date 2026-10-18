<script setup lang="ts">
/**
 * AttributeSchemaSection: the character sheet of the game, in its settings.
 *
 * The sheet belongs to the game, not to whoever first wrote it, so the leads
 * and senior moderation edit it: the same audience the rooms and the blacklist
 * answer to (GameIntention.Edit). The page decides that and draws this section
 * only for them.
 *
 * A save replaces the whole set and drops the values of every specification it
 * leaves out, which is why two things guard it: the version the sheet was read
 * at, refused by the server when somebody else has saved in the meantime, and
 * the data-loss confirmation the editor raises while the game has characters.
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
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { useSavedState } from "@/shared/lib/composables/useSavedState";
import type { DialogActionResult } from "@/shared/lib/composables/useDialogAction";
import { refusalHere } from "@/shared/lib/errors";

const store = useGameDetailsStore();
const { game, characters, charactersLoading, charactersError } =
  storeToRefs(store);

const {
  sources,
  refusal: sourcesRefusal,
  load: loadSchemaSources,
} = useSchemaSources();

const draft = ref<AttributeSchemaDraft | null>(null);

/**
 * Whether the draft under the editor is the schema the server has.
 *
 * The section says it on the editor's own button rather than in a plate: the
 * list of attributes after a save is the list that was there before it, the
 * section does not move, and a message in the corner was the only thing that
 * ever answered.
 */
const { saved, remember } = useSavedState(() => draft.value);
/**
 * The version the draft under it was built on, and the one the save carries.
 *
 * Read here rather than off the game at save time: the details are reloaded by
 * whatever else on the page saves, and a version picked up from such a reload
 * would call somebody else's sheet the one this draft was written against.
 * That is the whole of the guard, so it moves only where the sheet itself is
 * read again.
 */
const readVersion = ref<number | null>(null);
const saving = ref(false);
const refusal = ref("");
/** Whether a read of the roster has finished under this section. */
const rosterRead = ref(false);

/**
 * Whether the section knows what the roster holds. A read still on the wire,
 * and one that was refused, tell it nothing: the store empties the list for a
 * refusal, which reads exactly like a game nobody has applied to.
 */
const rosterKnown = computed(
  () => rosterRead.value && !charactersLoading.value && !charactersError.value,
);

/**
 * The sheet as the server last served it, identifiers kept: a specification
 * keeps its stored values only while the save sends its id back.
 */
function resetDraft() {
  const schema = game.value?.schema;
  draft.value = {
    title: "",
    specifications: cloneSpecs(schema?.specifications ?? []),
  };
  readVersion.value = schema?.version ?? null;
}

/**
 * A declined application is not a filled sheet anybody loses: it never entered
 * the game. Everyone else counts, so the warning is raised.
 *
 * A roster that has not answered counts too. The save it guards cannot be taken
 * back, so not knowing whether there are sheets to lose is a reason to ask, not
 * a reason to go ahead.
 */
const hasCharacters = computed(
  () =>
    !rosterKnown.value ||
    characters.value.some((character) => character.status !== "Declined"),
);

onMounted(async () => {
  resetDraft();
  loadSchemaSources();
  const current = game.value;
  if (!current) return;
  // The rooms section reads the same roster, and both are drawn for the same
  // audience; whichever mounts first is enough.
  if (characters.value.length) {
    rosterRead.value = true;
    return;
  }
  await store.loadCharacters(current.publicId ?? current.id);
  rosterRead.value = true;
});

function cancel() {
  refusal.value = "";
  resetDraft();
}

/**
 * Sends the draft, and answers with what the server said.
 *
 * The editor's data-loss confirmation calls it with the refusal flags: the
 * window prints a refusal in itself and closes on success alone, so the flags
 * ride on the request and the section says nothing of its own. The action bar
 * calls it without them, and there the line under the editor carries every
 * refusal, the plate the interceptor held for it taken down.
 */
async function save(
  next: AttributeSchemaDraft,
  options?: { ownsRefusal?: boolean; ownsRateLimit?: boolean },
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
    if (error.status === 409) {
      // The refusal is a line and not a toast, because what the reader has to
      // do next is in the editor under it. The sheet is read again so the next
      // attempt carries the version it now has; the draft is deliberately left
      // as typed. In the window the line is the window's, in the server's
      // words, which are these (GameAttributeSchemaService).
      if (!inWindow)
        refusal.value =
          "Схему только что изменили. Проверьте и сохраните снова";
      await reread(current.publicId ?? current.id);
      return { error };
    }
    if (!inWindow)
      refusal.value =
        refusalHere(error, "Не удалось сохранить схему атрибутов") ?? "";
    return { error };
  }

  // Remembered after the reread has rebuilt the draft from the answer: taken
  // before it, the snapshot would be of what was sent, and any normalisation
  // the server did would read as a change the author had made.
  if (await reread(current.publicId ?? current.id)) resetDraft();
  await nextTick();
  remember();
}

/**
 * Reads the game again behind the section, and reports whether it answered.
 *
 * The version travels with the sheet, so the two move together or not at all:
 * a refused read leaves the draft, the line under it and the version it was
 * written against exactly as the reader left them, and the reader saves again
 * when the network lets them.
 */
async function reread(id: string): Promise<boolean> {
  const { ok } = await store.loadGame(id, { keepCurrentOnError: true });
  if (ok) readVersion.value = game.value?.schema?.version ?? null;
  return ok;
}
</script>

<template>
  <section class="settings-section">
    <BlockTitle>Система атрибутов</BlockTitle>
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
  </section>
</template>

<style scoped lang="sass">
.settings-section
  margin-bottom: $big
  padding: $medium
  background-color: $bg-element
</style>
