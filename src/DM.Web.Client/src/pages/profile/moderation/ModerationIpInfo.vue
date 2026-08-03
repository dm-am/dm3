<script setup lang="ts">
import { ref } from "vue";
import { useExpandableSection } from "@/shared/lib/composables";
import { symbols } from "@/shared/lib/utils/icons";
import type { UserIpInfo, LoginRecord } from "@/shared/api/models/moderation";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { formatDate, formatDateFull } from "@/shared/lib/utils/datetime";

defineProps<{
  email?: string;
  ipAddresses?: UserIpInfo[];
  loginHistory?: LoginRecord[];
}>();

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
  <div class="mod-section">
    <h4 class="mod-section_title">Информация</h4>

    <div v-if="email" class="mod-field">
      <span class="mod-field_label">Почта:</span>
      <span class="mod-field_value">{{ email }}</span>
    </div>

    <div class="mod-subsection">
      <h5 class="mod-subsection_title">
        IP-адреса ({{ ipAddresses?.length ?? 0 }})
      </h5>
      <table v-if="ipAddresses?.length" class="mod-table">
        <thead>
          <tr>
            <th>IP</th>
            <th>Первый вход</th>
            <th>Последний вход</th>
            <th>Входов</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="ip in ipAddresses" :key="ip.ipAddress">
            <td class="mod-ip">{{ ip.ipAddress }}</td>
            <td>{{ formatDate(ip.firstSeenUtc) }}</td>
            <td>{{ formatDate(ip.lastSeenUtc) }}</td>
            <td>{{ ip.loginsCount }}</td>
          </tr>
        </tbody>
      </table>
      <secondary-text v-else>Нет данных</secondary-text>
    </div>

    <div class="mod-subsection">
      <h5 class="mod-subsection_title">
        <button
          type="button"
          class="mod-collapsible"
          :aria-expanded="showLoginHistory"
          @click="toggleLoginHistory()"
        >
          История входов ({{ loginHistory?.length ?? 0 }})
          <span
            class="mod-expand-icon expand-marker"
            :class="{ expanded: showLoginHistory }"
            aria-hidden="true"
          />
        </button>
      </h5>
      <div
        ref="historyZoneRef"
        class="expand-zone"
        v-bind="historyZoneBindings"
      >
        <table
          v-if="showLoginHistory && loginHistory?.length"
          class="mod-table"
        >
          <thead>
            <tr>
              <th>Дата</th>
              <th>IP</th>
              <th>Результат</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="record in loginHistory"
              :key="`${record.loginUtc}-${record.ipAddress}`"
              :class="{ 'mod-row-failed': !record.isSuccessful }"
            >
              <td>{{ formatDateFull(record.loginUtc) }}</td>
              <td class="mod-ip">{{ record.ipAddress }}</td>
              <td>
                <span v-if="record.isSuccessful" class="mod-success">{{
                  symbols.checkmark
                }}</span>
                <span v-else class="mod-fail">{{ symbols.cross }}</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
.mod-section
  margin-bottom: $medium

.mod-section_title
  margin: 0 0 $small 0
  color: $heading

.mod-field
  margin-bottom: $small

.mod-field_label
  color: $text-muted
  margin-right: $small

.mod-field_value
  color: $text

.mod-subsection
  margin-top: $small

.mod-subsection_title
  margin: 0 0 $small 0
  font-size: $secondary-font-size
  color: $heading-alt

// Button reset so the toggle inside the <h5> looks exactly like the plain
// heading text it replaced (full-width clickable row, inherited heading
// font/color, no button chrome).
.mod-collapsible
  display: block
  width: 100%
  box-sizing: border-box
  border: none
  background: none
  padding: 0
  font: inherit
  color: inherit
  text-align: left
  cursor: pointer
  &:hover
    color: $link-hover

.mod-expand-icon
  font-size: $tertiary-font-size
  margin-left: $minor

.mod-table
  width: 100%
  border-collapse: collapse
  font-size: $secondary-font-size

  th
    text-align: left
    padding: $minor $small
    border-bottom: 1px solid $border
    color: $text-muted
    font-weight: bold

  td
    padding: $minor $small
    border-bottom: 1px solid $bg-element-accent

.mod-ip
  font-family: $code-font
  font-size: $tertiary-font-size

.mod-row-failed
  background: $bg-highlight-red

.mod-success
  color: $accent-green

.mod-fail
  color: $accent-red
</style>
