/** A variable visual.mjs passes to the run: a missing one means it was started by hand. */
export const env = (name: string): string => {
  const value = process.env[name];
  if (!value)
    throw new Error(`${name} is not set: run scripts/visual/visual.mjs`);
  return value;
};
