export const PASSWORD_REQUIREMENTS =
  "Mật khẩu phải có 8–32 ký tự, gồm chữ thường, chữ hoa, số và ký tự đặc biệt (không tính khoảng trắng). Không chứa tên đăng nhập, email, tên trước @, password, websitename hoặc PLANORA. Mật khẩu mới phải khác mật khẩu hiện tại.";

export function getPasswordRequirements(
  password,
  username = "",
  currentPassword,
  additionalUsername = "",
) {
  const normalized = password.toLowerCase();
  const accountName = username.trim().toLowerCase();
  const hasPassword = password.length > 0;
  const requirements = [
    {
      id: "length",
      label: "Từ 8 đến 32 ký tự",
      met: password.length >= 8 && password.length <= 32,
    },
    {
      id: "lowercase",
      label: "Ít nhất một chữ thường (a–z)",
      met: /[a-z]/.test(password),
    },
    {
      id: "uppercase",
      label: "Ít nhất một chữ hoa (A–Z)",
      met: /[A-Z]/.test(password),
    },
    {
      id: "number",
      label: "Ít nhất một chữ số (0–9)",
      met: /[0-9]/.test(password),
    },
    {
      id: "special",
      label: "Ít nhất một ký tự đặc biệt (không tính khoảng trắng)",
      met: /[^A-Za-z0-9\s]/.test(password),
    },
    {
      id: "username",
      label: "Không chứa tên đăng nhập, email hoặc tên trước @",
      met:
        hasPassword &&
        ![
          accountName,
          accountName.split("@")[0],
          additionalUsername.trim().toLowerCase(),
        ].some((value) => value && normalized.includes(value)),
    },
    {
      id: "forbidden",
      label: "Không chứa password, websitename hoặc PLANORA",
      met:
        hasPassword &&
        !["password", "websitename", "planora"].some((value) =>
          normalized.includes(value),
        ),
    },
  ];
  if (currentPassword !== undefined) {
    requirements.push({
      id: "different",
      label: "Khác mật khẩu hiện tại",
      met: hasPassword && password !== currentPassword,
    });
  }
  return requirements;
}

export function validateNewPassword(
  password,
  username = "",
  currentPassword,
  additionalUsername = "",
) {
  const failed = getPasswordRequirements(
    password,
    username,
    undefined,
    additionalUsername,
  ).find((requirement) => !requirement.met);
  if (failed) {
    const messages = {
      username:
        "Mật khẩu không được chứa tên đăng nhập, email hoặc tên tài khoản (phần trước @).",
      forbidden: "Mật khẩu không được chứa password, websitename hoặc PLANORA.",
    };
    throw new Error(messages[failed.id] || PASSWORD_REQUIREMENTS);
  }
  if (currentPassword !== undefined && password === currentPassword) {
    throw new Error("Mật khẩu mới phải khác mật khẩu hiện tại.");
  }
}
