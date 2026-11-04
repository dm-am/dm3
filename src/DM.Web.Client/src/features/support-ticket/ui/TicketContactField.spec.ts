/**
 * @vitest-environment jsdom
 */

// The guest's address is read as the field of type email reads it, the site's
// one check (validators.email); what that reading lets through the server decides.
import { describe, expect, it } from "vitest";
import { TICKET_CONTACT_MAX_LENGTH } from "@/entities/ticket";
import { EMAIL_INVALID } from "@/shared/lib/errors/validationErrors";
import { contactProblem } from "./TicketContactField.vue";

describe("contactProblem", () => {
  it("names an address the field cannot read", () => {
    expect(contactProblem("reader@")).toBe(EMAIL_INVALID);
  });

  it("leaves an address without a dot in its domain to the server", () => {
    expect(contactProblem("reader@localhost")).toBe("");
  });

  it("asks for an address when there is none", () => {
    expect(contactProblem("   ")).toBe("Empty");
  });

  it("holds the address to the length of the field", () => {
    const local = "a".repeat(TICKET_CONTACT_MAX_LENGTH);
    expect(contactProblem(`${local}@example.com`)).toBe("Long");
  });
});
