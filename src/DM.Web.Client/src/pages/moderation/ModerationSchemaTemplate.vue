<script setup lang="ts">
/**
 * One starting character sheet; one page answers both addresses, because
 * creating and editing differ only in whether the address carries an id.
 */
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { attributeSchemaTemplateApi } from "@/entities/attribute-schema-template";
import {
  cloneSpecs,
  createEmptySchema,
  type AttributeSchemaDraft,
} from "@/entities/game";
import { AttributeSchemaEditor } from "@/features/attribute-schema-editor";
import { describeFailureOrWait, refusalHere } from "@/shared/lib/errors";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import { ErrorState } from "@/shared/ui/ErrorState";

const route = useRoute();
const router = useRouter();

/** The template being replaced; empty on the address that creates one. */
const templateId = computed(() =>
  typeof route.params.id === "string" ? route.params.id : "",
);

const draft = ref<AttributeSchemaDraft>(createEmptySchema());
const loading = ref(false);
const error = ref<string | null>(null);
const saving = ref(false);
const refusal = ref("");

/**
 * The read owns its refusal: it stands where the editor would be. A refused
 * save leaves the form standing and says it in the line under the editor.
 */

async function load() {
  if (!templateId.value) return;
  loading.value = true;
  error.value = null;
  try {
    const answer = await attributeSchemaTemplateApi.getTemplate(
      templateId.value,
      OWNS_REFUSAL,
    );
    if (answer.error) {
      // The shared wording names the seconds; the 429 title carries no number.
      error.value = describeFailureOrWait(
        answer.error,
        "Не удалось загрузить данные",
      );
      return;
    }
    const template = answer.data?.resource;
    if (!template) return;
    draft.value = {
      title: template.title,
      specifications: cloneSpecs(template.specifications),
    };
  } finally {
    loading.value = false;
  }
}

async function save(next: AttributeSchemaDraft) {
  if (saving.value) return;
  saving.value = true;
  refusal.value = "";
  const body = {
    title: next.title,
    specifications: next.specifications,
  };
  const { error: failure } = templateId.value
    ? await attributeSchemaTemplateApi.updateTemplate(templateId.value, body)
    : await attributeSchemaTemplateApi.createTemplate(body);
  saving.value = false;

  if (failure) {
    // In the form: a name already taken has its field right there.
    refusal.value = refusalHere(failure, "Не удалось сохранить шаблон") ?? "";
    return;
  }

  await toCatalogue();
}

function toCatalogue() {
  return router.push({ name: "moderation-schema-templates" });
}

// The read follows the address: one route record serves every template, so a
// step to another template reuses this instance and a sheet read on mount
// would be saved over the one the address names.
watch(templateId, load, { immediate: true });
</script>

<template>
  <page-title>Шаблоны схем атрибутов</page-title>

  <section class="schema-template">
    <!-- Text, not a skeleton: a sheet has any number of attributes. -->
    <div v-if="loading" class="loading">Загрузка...</div>
    <ErrorState v-else-if="error" :message="error" :retry="load" />
    <AttributeSchemaEditor
      v-else
      v-model="draft"
      show-title
      :saving="saving"
      :refusal="refusal"
      :save="save"
      @cancel="toCatalogue"
    />
  </section>
</template>
