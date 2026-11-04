<script setup lang="ts">
/**
 * The catalogue of starting character sheets. The default mark is never
 * missing: the server refuses to hide its template, so that row offers no
 * removal. A game keeps its own copy of the sheet; nothing here reaches it.
 */
import { onMounted, ref } from "vue";
import {
  attributeSchemaTemplateApi,
  type AttributeSchemaTemplate,
} from "@/entities/attribute-schema-template";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { describeFailureOrWait, refusalHere } from "@/shared/lib/errors";
import { ActionResult } from "@/shared/ui/ActionResult";
import { ErrorState } from "@/shared/ui/ErrorState";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const templates = ref<AttributeSchemaTemplate[]>([]);
const loading = ref(false);
const error = ref<string | null>(null);

/**
 * The read owns its refusal: it stands where the list would be. The removal,
 * asked in a window, prints its refusal there; the handover of the mark leaves
 * the catalogue standing, so its refusal goes on a plate.
 */

async function load() {
  loading.value = true;
  error.value = null;
  try {
    const answer = await attributeSchemaTemplateApi.getTemplates(OWNS_REFUSAL);
    if (answer.error) {
      // The shared wording names the seconds; the 429 title carries no number.
      error.value = describeFailureOrWait(
        answer.error,
        "Не удалось загрузить данные",
      );
      return;
    }
    templates.value = answer.data?.resources ?? [];
  } finally {
    loading.value = false;
  }
}

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
    // The mark moves between two rows, so the whole list is read again.
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

const deleteTarget = ref<AttributeSchemaTemplate | null>(null);

// The window waits for it, prints a refusal in itself and closes on success alone.
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

onMounted(load);
</script>

<template>
  <page-title>Шаблоны схем атрибутов</page-title>

  <section class="schema-templates">
    <!-- Text, not a skeleton: how many templates there are is anyone's guess. A read again keeps the rows. -->
    <div v-if="loading && !templates.length" class="loading">Загрузка...</div>
    <ErrorState v-else-if="error" :message="error" :retry="load" />
    <template v-else>
      <div class="templates-header">
        <router-link
          class="text-button"
          :to="{ name: 'moderation-schema-template-new' }"
          >+ Шаблон</router-link
        >
      </div>

      <ul class="templates-list" :aria-busy="loading || undefined">
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
            <!-- The one action here without a dialog, so it announces its own wait. -->
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
@use "@/assets/styles/Links" as *

.schema-templates
  display: flex
  flex-direction: column
  gap: $medium

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
