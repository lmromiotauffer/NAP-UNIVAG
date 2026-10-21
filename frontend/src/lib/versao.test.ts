import { describe, expect, it } from "vitest";
import { comVersao } from "./api";

describe("comVersao", () => {
  it("envia_if_match_entre_aspas", () => {
    expect(comVersao("AAAAAAAAB9E=")).toEqual({ headers: { "If-Match": '"AAAAAAAAB9E="' } });
  });
});
