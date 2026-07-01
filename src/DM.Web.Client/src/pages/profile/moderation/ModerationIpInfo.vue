<script setup lang="ts">
import { ref } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import type { UserIpInfo, LoginRecord } from "@/shared/api/models/moderation";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import dayjs from "dayjs";

defineProps<{
  email?: string;
  ipAddresses?: UserIpInfo[];
  loginHistory?: LoginRecord[];
}>();

const showLoginHistory = ref(false);

function formatDate(dateStr: string): string {
  return dayjs(dateStr).format("DD.MM.YYYY");
}

function formatDateTime(dateStr: string): string {
  return dayjs(dateStr).format("DD.MM.YYYY [в] HH:mm");
}
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
      <h5
        class="mod-subsection_title mod-collapsible"
        @click="showLoginHistory = !showLoginHistory"
      >
        История входов ({{ loginHistory?.length ?? 0 }})
        <span class="mod-expand-icon">{{
          showLoginHistory ? symbols.triangleDown : symbols.triangleRight
        }}</span>
      </h5>
      <table v-if="showLoginHistory && loginHistory?.length" class="mod-table">
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
            <td>{{ formatDateTime(record.loginUtc) }}</td>
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
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

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

.mod-collapsible
  cursor: pointer
  user-select: none
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
