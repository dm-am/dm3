// Shared file size formatting (SSOT for byte counts shown in the UI)

/**
 * Format a byte count as a human-readable size: "512 Б", "12.3 КБ", "1.5 МБ".
 * Mirrors the format used across upload tables site-wide.
 */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} Б`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} КБ`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} МБ`;
}
