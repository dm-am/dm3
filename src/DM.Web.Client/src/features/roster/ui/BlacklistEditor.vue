<script setup lang="ts">
// Draws the list; what it closes and the requests stay with the section that holds it.
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import { UserLink, useAuthStore } from "@/entities/user";
import type { GeneralError } from "@/shared/api/models/common";
import { refusalHere } from "@/shared/lib/errors";
import { formatDate } from "@/shared/lib/utils/datetime";
import { ActionResult } from "@/shared/ui/ActionResult";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { RowIconButton } from "@/shared/ui/RowIconButton";
import type { BlacklistRow } from "../model/types";
import BlacklistAddRow from "./BlacklistAddRow.vue";

const props = defineProps<{
  hint?: string;
  rows: BlacklistRow[];
  loading: boolean;
  /** Draw the day each name went on ("Внесен"): the account's list has it. */
  dated?: boolean;
  error?: string | null;
  add: (username: string) => Promise<GeneralError | null>;
  remove: (username: string) => Promise<GeneralError | null>;
  exclude?: readonly string[];
}>();

const NAME_COLUMN: Column = { key: "user", label: "Пользователь" };
const DATE_COLUMN: Column = {
  key: "created",
  label: "Внесен",
  width: "18%",
  align: "center",
};
const ACTION_COLUMN: Column = {
  key: "action",
  label: "",
  width: "1px",
  align: "center",
};

const columns = computed(() =>
  props.dated
    ? [NAME_COLUMN, DATE_COLUMN, ACTION_COLUMN]
    : [NAME_COLUMN, ACTION_COLUMN],
);

const { user: viewer } = storeToRefs(useAuthStore());

const suggestNot = computed(() => [
  ...(viewer.value ? [viewer.value.username] : []),
  ...props.rows.map((row) => row.username),
  ...(props.exclude ?? []),
]);

// Removals on the wire: their bins are shut until the answer.
const removing = ref(new Set<string>());

const removeRefusal = ref<{ id: string; text: string } | null>(null);

// The bin asks nothing: the name goes back on with the field.
async function submitRemove(row: BlacklistRow) {
  if (removing.value.has(row.username)) return;
  removing.value.add(row.username);
  removeRefusal.value = null;
  const error = await props.remove(row.username);
  removing.value.delete(row.username);
  if (!error) return;
  const text = refusalHere(error, "Пользователь не удален из черного списка");
  if (text) removeRefusal.value = { id: row.id, text };
}
</script>

<template>
  <div class="blacklist">
    <SecondaryText v-if="hint" class="bl-hint">{{ hint }}</SecondaryText>

    <div class="bl-table">
      <DataTable
        :columns="columns"
        :data="rows"
        :loading="loading"
        :details-for="removeRefusal?.id ?? null"
        empty-text="В черном списке пока никого нет"
        aria-label="Черный список"
      >
        <template #cell-user="{ row }">
          <UserLink v-if="row.user" :user="row.user" hide-badge /><RouterLink
            v-else
            :to="{ name: 'profile', params: { username: row.username } }"
            class="user-link"
            >{{ row.username }}</RouterLink
          >
        </template>
        <template #cell-created="{ row }">
          {{ row.createdUtc ? formatDate(row.createdUtc) : "" }}
        </template>
        <template #cell-action="{ row }">
          <RowIconButton
            icon="trash"
            :label="`Удалить ${row.username} из черного списка`"
            :disabled="removing.has(row.username)"
            :aria-busy="removing.has(row.username) || undefined"
            @click="submitRemove(row)"
          />
        </template>
        <template v-if="error && !loading" #empty>
          <ActionResult :text="error" class="bl-load-refusal" />
        </template>
        <template #details>
          <ActionResult :text="removeRefusal?.text" class="bl-refusal" />
        </template>
      </DataTable>
      <div class="bl-foot">
        <BlacklistAddRow :add="add" :exclude="suggestNot" />
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Tables" as *

.blacklist
  display: flex
  flex-direction: column
  gap: $medium

// Under the scrolling box, not inside it: a box scrolling one way clips the other too.
.bl-foot
  border: $table-gap solid $border
  border-top: none
  padding: $table-cell-padding-v $table-cell-padding-h
  background-color: $bg-element-accent

.bl-refusal,
.bl-load-refusal
  --action-result-margin: 0
</style>
