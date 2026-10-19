// Close the dialog first so the support page does not open behind its overlay.
import { nextTick } from "vue";
import { useRouter } from "vue-router";

export function useSupportExit(close: () => void) {
  const router = useRouter();

  return (address: string) => {
    close();
    nextTick(() => router.push(address));
  };
}
