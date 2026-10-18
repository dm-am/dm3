<script setup lang="ts">
/**
 * ModerationSchemaTemplates - the catalogue of starting character sheets the
 * game creation form fills itself from.
 *
 * Exactly one entry carries the default mark, and the site is never without
 * one: the server refuses to hide the template that carries it. So the row
 * with the mark offers no removal, and the way to retire it is to hand the
 * mark to another row first.
 *
 * A game made from a template keeps its own copy of the sheet, so nothing here
 * reaches a game that already exists - neither an edit nor a removal.
 */
import { onMounted, ref } from "vue";
import {
  attributeSchemaTemplateApi,
  type AttributeSchemaTemplate,
} from "@/entities/attribute-schema-template";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import {
  describeFailure,
  describeRateLimit,
  refusalHere,
} from "@/shared/lib/errors";
import { ActionResult } from "@/shared/ui/ActionResult";
import { useRoleGate } from "./lib/useRoleGate";
import { sectionRole } from "./lib/sections";

const { hasAccess, deniedText } = useRoleGate(
  sectionRole("moderation-schema-templates"),
);

const templates = ref<AttributeSchemaTemplate[]>([]);
const loading = ref(false);
const error = ref<string | null>(null);

/**
 * The read answers for itself, because this page does.
 *
 * The failure stands where the list would be, so the interceptor's plate over
 * it said the same sentence a second time — word for word on a 403 and a 5xx,
 * since both read it out of the same problem document.
 *
 * The handover of the mark is not in here: it leaves the catalogue standing,
 * so its refusal belongs on a plate over a page that still reads. The removal
 * carries the same flags for another reason: it is asked in a window, and the
 * window prints the refusal in itself.
 */
const OWNS_REFUSAL = { ownsRefusal: true, ownsRateLimit: true } as const;

async function load() {
  loading.value = true;
  error.value = null;
  try {
    const answer = await attributeSchemaTemplateApi.getTemplates(OWNS_REFUSAL);
    // A load that failed is the state of the page and not a toast over it:
    // there is no list left underneath to go back to.
    if (answer.error) {
      // A wait is said in the shared wording, the one the interceptor would
      // have raised: it is the only one that names the seconds the server asked
      // for, and the title sent with a 429 carries no number.
      error.value =
        answer.error.status === 429
          ? describeRateLimit(answer.error.retryAfterSeconds)
          : describeFailure(answer.error, "Не удалось загрузить данные");
      return;
    }
    templates.value = answer.data?.resources ?? [];
  } finally {
    loading.value = false;
  }
}

// --- The default mark -----------------------------------------------------

const promoting = ref(false);

/** A refused "Сделать по умолчанию", under the row it was pressed in. */
const promoteRefusal = ref<{ id: string; text: string } | null>(null);

async function makeDefault(template: AttributeSchemaTemplate) {
  if (promoting.value) return;
  promoting.value = true;
  promoteRefusal.value = null;
  try {
    const { error: failure } = await attributeSchemaTemplateApi.makeDefault(
      template.id,
    );
    // The mark moves between two rows, so the whole list is re-read rather
    // than the clicked row flipped: the row that lost the mark is the other
    // one, and the server is what decides which.
    if (failure) {
      const text = refusalHere(
        failure,
        "Не удалось назначить шаблон по умолчанию",
      );
      if (text) promoteRefusal.value = { id: String(template.id), text };
      return;
    }
    await load();
  } finally {
    promoting.value = false;
  }
}

// --- Removal --------------------------------------------------------------

const deleteTarget = ref<AttributeSchemaTemplate | null>(null);

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone.
async function confirmDelete() {
  const target = deleteTarget.value;
  if (!target) return;
  const result = await attributeSchemaTemplateApi.deleteTemplate(
    target.id,
    OWNS_REFUSAL,
  );
  if (!result.error) void load();
  return result;
}

onMounted(() => {
  if (hasAccess.value) load();
});
</script>

<template>
  <page-title>Шаблоны схем атрибутов</page-title>

  <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

  <section v-else class="schema-templates">
    <div v-if="loading" class="loading">Загрузка...</div>
    <div v-else-if="error" class="error">{{ error }}</div>
    <template v-else>
      <div class="templates-header">
        <router-link
          class="text-button"
          :to="{ name: 'moderation-schema-template-new' }"
          >+ Шаблон</router-link
        >
      </div>

      <!--
        Unreachable while the catalogue holds its default: the seeder writes one
        and deletion refuses to take the last default away. Kept as the plain
        list form the standard asks for, so a catalogue that somehow empties
        reads like every other empty list rather than like a broken table.
      -->
      <SecondaryText v-if="templates.length === 0">
        Шаблонов пока нет
      </SecondaryText>
      <ul v-else class="templates-list">
        <li
          v-for="template in templates"
          :key="template.id"
          class="template-row"
        >
          <span class="template-name"
            ><span class="template-title">{{ template.title }}</span
            ><template v-if="template.isDefault"
              >{{ " "
              }}<span class="template-default">по умолчанию</span></template
            ></span
          >
          <div class="template-actions">
            <router-link
              class="text-button"
              :to="{
                name: 'moderation-schema-template',
                params: { id: template.id },
              }"
              >Редактировать</router-link
            >
            <!-- The one action of the section that goes without a dialog, so
                 the wait is its own to announce: the control stops answering
                 while the handover is in flight, and says it is coming back.
                 The dialog does as much for the removal beside it. -->
            <button
              v-if="!template.isDefault"
              type="button"
              class="text-button"
              :disabled="promoting"
              :aria-busy="promoting || undefined"
              @click="makeDefault(template)"
            >
              Сделать по умолчанию
            </button>
            <button
              v-if="!template.isDefault"
              type="button"
              class="text-button"
              @click="deleteTarget = template"
            >
              Удалить
            </button>
          </div>
          <ActionResult
            v-if="promoteRefusal?.id === String(template.id)"
            :text="promoteRefusal.text"
            class="template-refusal"
          />
        </li>
      </ul>
    </template>

    <ConfirmDialog
      :show="deleteTarget !== null"
      title="Удалить шаблон?"
      :message="`Шаблон &quot;${deleteTarget?.title ?? ''}&quot; пропадет из формы создания игры`"
      confirm-label="Удалить"
      failure="Не удалось удалить шаблон"
      :action="confirmDelete"
      @update:show="(shown) => !shown && (deleteTarget = null)"
    />
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.schema-templates
  display: flex
  flex-direction: column
  gap: $medium

.error
  color: $accent-red

.templates-header
  display: flex

.templates-list
  list-style: none
  margin: 0
  padding: 0
  display: flex
  flex-direction: column
  gap: $small

.template-row
  display: flex
  flex-wrap: wrap
  align-items: baseline
  gap: $tiny $medium
  +card($small)

// A line of its own under the row it answers, inside the card.
.template-refusal
  flex-basis: 100%
  --action-result-margin: 0

// The mark of the default stays whole where the name breaks.
.template-default
  font-size: $secondary-font-size
  color: $text-muted
  white-space: nowrap

.template-actions
  display: flex
  gap: $medium
  margin-left: auto

.text-button
  text-align: left
  +inline-link-button
</style>
