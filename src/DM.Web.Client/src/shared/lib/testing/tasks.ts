/** Resolves once the tasks queued before it have run. */
export const endOfTask = (): Promise<void> =>
  new Promise((resolve) => setTimeout(resolve, 0));

/** A promise the test settles by hand. */
export function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((settle, fail) => {
    resolve = settle;
    reject = fail;
  });
  return { promise, resolve, reject };
}
