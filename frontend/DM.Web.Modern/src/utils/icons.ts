/**
 * SVG Icons Library
 * Все иконки с выверенными viewBox для единообразного отображения
 */

export const icons = {
  // ===== Основные иконки (для хэдера и тулбара) =====

  /** Карандаш (редактирование) */
  pencil: {
    viewBox: "-0.7 -1.2 25.4 25.4",
    path: '<path d="M12 20h9M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>',
    fill: "none",
  },

  /** Глаз открытый (видимость) */
  eyeOpen: {
    viewBox: "0.3 0.57 23.35 23.35",
    path: '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/><circle cx="12" cy="12" r="3" stroke="currentColor" stroke-width="2"/>',
    fill: "none",
  },

  /** Глаз закрытый (скрыто) */
  eyeClosed: {
    viewBox: "0.3 0.57 23.35 23.35",
    path: '<path d="M3 12c0 0 4 5 9 5s9-5 9-5" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>',
    fill: "none",
  },

  /** Якорь/ссылка */
  anchor: {
    viewBox: "-2 -2 28 28",
    path: '<path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>',
    fill: "none",
  },

  /** Корзина (удаление) */
  trash: {
    viewBox: "-2.27 -3.0 28.54 28.54",
    path: '<path d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>',
    fill: "none",
  },

  /** Сердце пустое (лайк) */
  heartEmpty: {
    viewBox: "-1.2 -0.75 26.4 26.4",
    path: '<path d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z" stroke="currentColor" stroke-width="2"/>',
    fill: "none",
  },

  /** Сердце заполненное (лайкнуто) */
  heartFilled: {
    viewBox: "-1.2 -0.75 26.4 26.4",
    path: '<path d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z" fill="currentColor" stroke="currentColor" stroke-width="2"/>',
    fill: "currentColor",
  },

  /** Крестик (закрыть/отмена) */
  close: {
    viewBox: "3.1 3.1 17.8 17.8",
    path: '<path d="M18 6L6 18M6 6l12 12" stroke="currentColor" stroke-width="1.48" stroke-linecap="round"/>',
    fill: "none",
  },

  // ===== Стрелки навигации =====

  /** Стрелка вверх (свернуть) */
  chevronUp: {
    viewBox: "0 0 12 12",
    path: '<path d="M2 8L6 4L10 8" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>',
    fill: "none",
  },

  /** Стрелка вниз (развернуть) */
  chevronDown: {
    viewBox: "0 0 12 12",
    path: '<path d="M2 4L6 8L10 4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>',
    fill: "none",
  },

  /** Двойная стрелка вниз (прокрутка вниз) */
  scrollDown: {
    viewBox: "0 0 24 24",
    path: '<path d="M7 13l5 5 5-5M7 6l5 5 5-5" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>',
    fill: "none",
  },

  // ===== View toggle (переключение вида) =====

  /** Вид списком (с буллитами) */
  viewList: {
    viewBox: "0 0 16 12",
    path: '<circle cx="1.5" cy="1.5" r="1.5"/><rect x="5" y="0" width="11" height="2.5"/><circle cx="1.5" cy="6" r="1.5"/><rect x="5" y="4.75" width="11" height="2.5"/><circle cx="1.5" cy="10.5" r="1.5"/><rect x="5" y="9.25" width="11" height="2.5"/>',
    fill: "currentColor",
  },

  /** Компактный вид (линии) */
  viewCompact: {
    viewBox: "0 0 16 12",
    path: '<rect x="0" y="0" width="16" height="2"/><rect x="0" y="3.33" width="16" height="2"/><rect x="0" y="6.67" width="16" height="2"/><rect x="0" y="10" width="16" height="2"/>',
    fill: "currentColor",
  },

  // ===== Отправка сообщения =====

  /** Отправить */
  send: {
    viewBox: "-2 -3 28 30",
    path: '<path d="M22 12L4 2v20L22 12z"/><path d="M22 12H4M4 2l8 10-8 10"/>',
    fill: "none",
    stroke: "currentColor",
    strokeWidth: 2,
    strokeLinejoin: "round",
  },

  // ===== Соцсети =====

  /** VK */
  vk: {
    viewBox: "-3 -3 30 30",
    path: '<path d="M15.684 0H8.316C1.592 0 0 1.592 0 8.316v7.368C0 22.408 1.592 24 8.316 24h7.368C22.408 24 24 22.408 24 15.684V8.316C24 1.592 22.391 0 15.684 0zm3.692 17.123h-1.744c-.66 0-.864-.525-2.05-1.727-1.033-1-1.49-1.135-1.744-1.135-.356 0-.458.102-.458.593v1.575c0 .424-.135.678-1.253.678-1.846 0-3.896-1.118-5.335-3.202C4.624 10.857 4 8.418 4 7.928c0-.254.102-.491.593-.491h1.744c.44 0 .61.203.78.678.847 2.489 2.27 4.674 2.853 4.674.22 0 .322-.102.322-.66V9.623c-.068-1.186-.695-1.287-.695-1.71 0-.203.17-.407.44-.407h2.744c.373 0 .508.203.508.643v3.473c0 .372.17.508.271.508.22 0 .407-.136.813-.542 1.253-1.406 2.143-3.574 2.143-3.574.119-.254.322-.491.763-.491h1.744c.525 0 .644.27.525.643-.22 1.017-2.354 4.031-2.354 4.031-.186.305-.254.44 0 .78.186.254.796.779 1.203 1.253.745.847 1.32 1.558 1.473 2.05.17.49-.085.744-.576.744z"/>',
    fill: "currentColor",
  },

  /** Discord */
  discord: {
    viewBox: "0 0 24 24",
    path: '<path d="M20.317 4.37a19.791 19.791 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057a.082.082 0 0 0 .031.057 19.9 19.9 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028 14.09 14.09 0 0 0 1.226-1.994.076.076 0 0 0-.041-.106 13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128 10.2 10.2 0 0 0 .372-.292.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127 12.299 12.299 0 0 1-1.873.892.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.839 19.839 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z"/>',
    fill: "currentColor",
  },

  /** YouTube */
  youtube: {
    viewBox: "0 0 24 24",
    path: '<path d="M23.498 6.186a3.016 3.016 0 0 0-2.122-2.136C19.505 3.545 12 3.545 12 3.545s-7.505 0-9.377.505A3.017 3.017 0 0 0 .502 6.186C0 8.07 0 12 0 12s0 3.93.502 5.814a3.016 3.016 0 0 0 2.122 2.136c1.871.505 9.376.505 9.376.505s7.505 0 9.377-.505a3.015 3.015 0 0 0 2.122-2.136C24 15.93 24 12 24 12s0-3.93-.502-5.814zM9.545 15.568V8.432L15.818 12l-6.273 3.568z"/>',
    fill: "currentColor",
  },

  // ===== Поиск =====

  /** Лупа (поиск) */
  search: {
    viewBox: "0 0 24 24",
    path: '<circle cx="11" cy="11" r="8" stroke="currentColor" stroke-width="2"/><path d="M21 21l-4.35-4.35" stroke="currentColor" stroke-width="2"/>',
    fill: "none",
  },

  // ===== Специальные =====

  /** Пустой конверт (нет переписок) */
  emptyEnvelope: {
    viewBox: "0 0 64 64",
    path: '<rect x="8" y="12" width="48" height="36" rx="4" stroke="currentColor" stroke-width="1.5"/><path d="M8 20l24 16 24-16" stroke="currentColor" stroke-width="1.5"/>',
    fill: "none",
  },

  /** Удалённый аватар */
  deletedAvatar: {
    viewBox: "0 0 56 56",
    path: '<circle cx="28" cy="28" r="26" fill="none" stroke="currentColor" stroke-width="1" stroke-dasharray="4 2"/><path d="M18 18l20 20M38 18l-20 20" stroke="currentColor" stroke-width="1.5"/>',
    fill: "none",
  },
} as const;

export type IconName = keyof typeof icons;
