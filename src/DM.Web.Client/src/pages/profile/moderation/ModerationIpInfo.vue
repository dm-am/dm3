<script setup lang="ts">
/**
 * "Входы", a section of the profile's "Модерация" tab: the address the account
 * is registered to, the addresses it signs in from, and the history of its
 * sign-ins, the last behind a reveal.
 *
 * The two lists are the site's tables (DataTable), as the games and blogs of
 * the profile are: they used to be tables of a look of their own, with grey
 * headers of another size and rules of another colour.
 */
import { computed, ref } from "vue";
import { useExpandableSection } from "@/shared/lib/composables/useExpandableSection";
import { symbols } from "@/shared/lib/utils/icons";
import type { UserIpInfo, LoginRecord } from "@/shared/api/models/moderation";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { BlockTitle } from "@/shared/ui/Layout";
import { StatLine } from "@/shared/ui/StatLine";
import { formatDate, formatDateFull } from "@/shared/lib/utils/datetime";

const props = defineProps<{
  email?: string;
  ipAddresses?: UserIpInfo[];
  loginHistory?: LoginRecord[];
}>();

const ipColumns: Column[] = [
  { key: "ipAddress", label: "IP" },
  { key: "firstSeen", label: "Первый вход", width: "25%" },
  { key: "lastSeen", label: "Последний вход", width: "25%" },
  { key: "loginsCount", label: "Входов", width: "15%", align: "center" },
];

const historyColumns: Column[] = [
  { key: "when", label: "Дата", width: "40%" },
  { key: "ipAddress", label: "IP" },
  { key: "result", label: "Результат", width: "20%", align: "center" },
];

// DataTable keys its rows by `id`.
const ipRows = computed(() =>
  (props.ipAddresses ?? []).map((ip) => ({ ...ip, id: ip.ipAddress })),
);
const historyRows = computed(() =>
  (props.loginHistory ?? []).map((record, index) => ({
    ...record,
    id: `${record.loginUtc}-${record.ipAddress}-${index}`,
  })),
);

// Login-history table — a content expandable section (unified reveal
// animation + the page-wide "Развернуть/Свернуть все" toggle).
const showLoginHistory = ref(false);
const historyZoneRef = ref<HTMLElement | null>(null);
const { toggle: toggleLoginHistory, zoneBindings: historyZoneBindings } =
  useExpandableSection({
    el: historyZoneRef,
    model: showLoginHistory,
    label: "ModerationLoginHistory",
  });
</script>

<template>
  <section class="mod-section">
    <BlockTitle>Входы</BlockTitle>

    <StatLine v-if="email" label="Почта" :value="email" class="mod-ip_email" />

    <p class="mod-ip_caption">IP-адреса:</p>
    <DataTable :columns="ipColumns" :data="ipRows" aria-label="IP-адреса">
      <template #cell-firstSeen="{ row }">{{
        formatDate(row.firstSeenUtc)
      }}</template>
      <template #cell-lastSeen="{ row }">{{
        formatDate(row.lastSeenUtc)
      }}</template>
    </DataTable>

    <button
      v-if="loginHistory?.length"
      type="button"
      class="mod-ip_history"
      :aria-expanded="showLoginHistory"
      @click="toggleLoginHistory()"
    >
      История входов ({{ loginHistory.length }})<span
        class="mod-expand-icon expand-marker"
        :class="{ expanded: showLoginHistory }"
        aria-hidden="true"
      />
    </button>
    <div ref="historyZoneRef" class="expand-zone" v-bind="historyZoneBindings">
      <div v-if="showLoginHistory" class="mod-ip_history-table">
        <DataTable
          :columns="historyColumns"
          :data="historyRows"
          aria-label="История входов"
        >
          <template #cell-when="{ row }">{{
            formatDateFull(row.loginUtc)
          }}</template>
          <template #cell-result="{ row }">
            <span v-if="row.isSuccessful" class="mod-success">{{
              symbols.checkmark
            }}</span>
            <span v-else class="mod-fail">{{ symbols.cross }}</span>
          </template>
        </DataTable>
      </div>
    </div>
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.mod-ip_email
  margin-bottom: $small

.mod-ip_caption
  margin: 0 0 $small
  color: $text

// A reveal is pressed like a link of the text and answers the pointer as one.
.mod-ip_history
  display: block
  margin-top: $small
  +inline-link-button($text)

.mod-expand-icon
  margin-left: $minor
  font-size: $tertiary-font-size

.mod-ip_history-table
  padding-top: $small

.mod-success
  color: $accent-green

.mod-fail
  color: $accent-red
</style>
