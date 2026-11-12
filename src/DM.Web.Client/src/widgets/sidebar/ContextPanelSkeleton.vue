<script setup lang="ts">
/**
 * "Меню игры" or "Меню блога" while its chunk loads: the block with the
 * panel's own loading skeleton, so the column below stands from the first frame.
 */
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";

// The token keeps the reader's collapsed state of the panel it stands for.
defineProps<{ token: string; title: string }>();
</script>

<template>
  <SidebarBlock :token="token" :title="title">
    <!-- The rows of the panel's skeleton while its game or blog loads (LeftSidebar.contextPanel.spec). -->
    <SidebarSkeleton :lines="8" />
  </SidebarBlock>
</template>
