let prepared;
export function prepareGoogleClient(config) {
  if (!prepared)
    prepared = Promise.all([import("firebase/app"), import("firebase/auth")])
      .then(([appSdk, authSdk]) => {
        const name = `planora-google-${config.projectId}`;
        const app =
          appSdk.getApps().find((item) => item.name === name) ||
          appSdk.initializeApp(config, name);
        const auth = authSdk.initializeAuth(app, {
          persistence: authSdk.inMemoryPersistence,
          popupRedirectResolver: authSdk.browserPopupRedirectResolver,
        });
        const provider = new authSdk.GoogleAuthProvider();
        provider.setCustomParameters({ prompt: "select_account" });
        return {
          async signIn() {
            const result = await authSdk.signInWithPopup(auth, provider);
            return result.user.getIdToken(true);
          },
          signOut: () => authSdk.signOut(auth),
        };
      })
      .catch((error) => {
        prepared = undefined;
        throw error;
      });
  return prepared;
}

export function googleErrorMessage(error) {
  switch (error.code) {
    case "auth/popup-closed-by-user":
      return "Bạn đã đóng cửa sổ Google. Có thể thử lại khi sẵn sàng.";
    case "auth/popup-blocked":
      return "Trình duyệt chặn cửa sổ Google. Hãy cho phép popup của trang này rồi thử lại.";
    case "auth/cancelled-popup-request":
      return "Một cửa sổ đăng nhập Google đã được mở.";
    case "auth/network-request-failed":
      return "Không kết nối được với Google. Kiểm tra mạng rồi thử lại.";
    case "auth/unauthorized-domain":
      return "Đăng nhập Google chưa khả dụng trên địa chỉ này.";
    case "auth/operation-not-allowed":
      return "Đăng nhập Google hiện chưa khả dụng.";
    default:
      return error.status
        ? error.message
        : "Không thể đăng nhập Google. Hãy thử lại.";
  }
}
