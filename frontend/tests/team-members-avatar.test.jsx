import { expect, it } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { TeamMembers } from "../src/team-components.jsx";

it("shows account avatars for all members and updates to a new profile image after a previous image fails", () => {
  const detail = {
    team: { role: "Leader" },
    members: [
      {
        userId: "me",
        name: "SPWPOS System",
        role: "Leader",
        avatarUrl: "/assets/admin.png",
      },
      {
        userId: "member",
        name: "Quân Phan Lê Nhật",
        role: "Member",
        avatarUrl: "/assets/member.png",
      },
    ],
  };
  const view = render(<TeamMembers detail={detail} uid="me" />);
  expect(
    screen
      .getByRole("img", { name: "Ảnh đại diện của SPWPOS System" })
      .getAttribute("src"),
  ).toBe("/assets/admin.png");
  fireEvent.error(
    screen.getByRole("img", { name: "Ảnh đại diện của Quân Phan Lê Nhật" }),
  );
  expect(
    screen.getByLabelText("Ảnh đại diện của Quân Phan Lê Nhật").textContent,
  ).toBe("LN");
  view.rerender(
    <TeamMembers
      detail={{
        ...detail,
        members: detail.members.map((m) =>
          m.userId === "member"
            ? { ...m, avatarUrl: "/assets/member-updated.png" }
            : m,
        ),
      }}
      uid="me"
    />,
  );
  expect(
    screen
      .getByRole("img", { name: "Ảnh đại diện của Quân Phan Lê Nhật" })
      .getAttribute("src"),
  ).toBe("/assets/member-updated.png");
});
