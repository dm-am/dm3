/// <reference types="vite/client" />

/** navigator.connection is not in lib.dom: the fields read in shared/lib/utils/connection. */
interface NetworkInformationLike {
  readonly saveData?: boolean;
  readonly effectiveType?: string;
  addEventListener?(type: "change", listener: () => void): void;
  removeEventListener?(type: "change", listener: () => void): void;
}

interface Navigator {
  readonly connection?: NetworkInformationLike;
}
