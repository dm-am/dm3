<script setup lang="ts">
/**
 * ModerationSchemaTemplate - one starting character sheet, opened to be
 * written. One page answers both addresses of the section, because creating
 * and editing differ in a single fact: whether the address carries an
 * identifier. Two pages would be one form kept in two places.
 *
 * The form is the editor its other two hosts use, asked for the name field a
 * template has and given no list of sources: there is nothing to copy into a
 * template, and what a game copies is a template. Persistence is this page's,
 * as everywhere else the editor is embedded.
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
import {
  describeFailure,
  describeRateLimit,
  refusalHere,
} from "@/shared/lib/errors";
import { useRoleGate } from "./lib/useRoleGate";
import { sectionRole } from "./lib/sections";

const { hasAccess, deniedText } = useRoleGate(
  sectionRole("moderation-schema-templates"),
);
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
 * The read answers for itself, because this page does.
 *
 * A failed read stands where the editor would be, and the interceptor's plate
 * over it repeated that sentence word for word on a 403 and a 5xx. The plate
 * alone would not do either: a sheet that never arrived leaves an empty page,
 * and the line in its place is what says which page is empty.
 *
 * The saves below are not in here: a refused save leaves the form standing, and
 * the line under the editor says it, the plate the interceptor held for it
 * taken down.
 */
const OWNS_REFUSAL = { ownsRefusal: true, ownsRateLimit: true } as const;

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
      // A wait is said in the shared wording, the one the interceptor would
      // have raised: it is the only one that names the seconds the server asked
      // for, and the title sent with a 429 carries no number.
      error.value =
        answer.error.status === 429
          ? describeRateLimit(answer.error.retryAfterSeconds)
          : describeFailure(answer.error, "Не удалось загрузить данные");
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
    title: next.title.trim(),
    specifications: next.specifications,
  };
  const { error: failure } = templateId.value
    ? await attributeSchemaTemplateApi.updateTemplate(templateId.value, body)
    : await attributeSchemaTemplateApi.createTemplate(body);
  saving.value = false;

  if (failure) {
    // Answered in the form and not over it: a name already taken has its
    // field right there, and the server is what names the reason; every other
    // refusal stands in the same line.
    refusal.value = refusalHere(failure, "Не удалось сохранить шаблон") ?? "";
    return;
  }

  await toCatalogue();
}

function toCatalogue() {
  return router.push({ name: "moderation-schema-templates" });
}

// The read follows the address rather than the mount: one route record serves
// every template, so a step from one template to another reuses this component
// instance and nothing remounts. A sheet read once would then be the sheet of the
// template the reader came from, and saving would write it over the one the
// address names.
watch(
  templateId,
  () => {
    if (hasAccess.value) load();
  },
  { immediate: true },
);
</script>

<template>
  <page-title>Шаблоны схем атрибутов</page-title>

  <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

  <section v-else class="schema-template">
    <div v-if="loading" class="loading">Загрузка...</div>
    <div v-else-if="error" class="error">{{ error }}</div>
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

<style scoped lang="sass">
.error
  color: $accent-red
</style>
